using System.Collections.Generic;
using System.Linq;
using AITycoon.Features.AI_Agents;
using AITycoon.Features.SystemLoad;
using AITycoon.Features.SystemLoad.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AITycoon.Features.GridBuilder
{
    /// <summary>
    /// Baut den M1-Grundriss deterministisch aus OfficeGrid auf.
    ///
    /// Warum ein Generator statt Handplatzierung: der Grundriss ist die Variable, die im Playtest
    /// am haeufigsten geaendert wird. Als ASCII-Layout in OfficeGrid.Rows ist er versioniert,
    /// diffbar und in 30 Sekunden umgebaut — von Hand geklickte Szenen sind das nicht.
    ///
    /// Alles Generierte haengt unter einem einzigen Root "[Office]". Ein zweiter Lauf loescht
    /// diesen Root und baut neu (idempotent). Objekte, die NICHT dem Builder gehoeren
    /// (Desk1/PC des LLM-Spikes, die Puppets, Kamera, CityGround) werden nur umpositioniert.
    /// </summary>
    public static class OfficeLayoutBuilder
    {
        private const string RootName = "[Office]";

        private const string KitTiles = "Assets/ThirdParty/Mini Toon Office/Prefabs/tiles prefabs/";
        private const string KitProps = "Assets/ThirdParty/Mini Toon Office/Prefabs/props prefabs/";
        private const string Nappin = "Assets/ThirdParty/nappin/OfficeEssentialsPack/Prefabs/";
        private const string Plants = "Assets/ThirdParty/Level13/Low Poly Interior Flower Pots/URP/Prefab/";


        /// <summary>Ein Moebel/Deko-Stueck auf einer Grid-Zelle. Yaw in Grad, Offset in Weltmetern.</summary>
        private struct PropSpec
        {
            public string Path;
            public int X;
            public int Z;
            public float Yaw;
            public Vector3 Offset;
            public float Scale;
            public string Name;

            public PropSpec(string path, int x, int z, float yaw, string name,
                            Vector3 offset = default, float scale = 1f)
            {
                Path = path;
                X = x;
                Z = z;
                Yaw = yaw;
                Offset = offset;
                Scale = scale <= 0f ? 1f : scale;
                Name = name;
            }
        }

        [MenuItem("AI Tycoon/Build Office (M1)")]
        public static void Build()
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Office (M1)");

            Transform floors = NewGroup(root.transform, "Floor");
            Transform walls = NewGroup(root.transform, "Walls");
            Transform props = NewGroup(root.transform, "Props");
            Transform anchors = NewGroup(root.transform, "Anchors");

            int floorCount = BuildFloor(floors);
            int wallCount = BuildWalls(walls);
            BuildAnchors(anchors);
            int propCount = BuildProps(props);
            BuildFuseBox(props);

            PlaceExistingSpikeObjects(anchors);
            SinkCityGround();
            TuneLighting();
            FitCamera();

            int navPolys = BakeNavMesh(root);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                $"[OfficeLayoutBuilder] Grundriss gebaut: {floorCount} Boden-Tiles, {wallCount} Wandstuecke, " +
                $"{propCount} Props. Zelle = {OfficeGrid.Tile} m, Wandhoehe = {OfficeGrid.WallHeight} m, " +
                $"Durchgang = {OfficeGrid.Passage.Length} Zellen. " +
                $"NavMesh: {(navPolys > 0 ? "gebaut" : "FEHLGESCHLAGEN")}.");
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ Boden

        private static int BuildFloor(Transform parent)
        {
            GameObject tile = Load(KitTiles + "toon_office_tile_ground_low.prefab");
            if (tile == null)
                return 0;

            int count = 0;
            for (int z = 0; z < OfficeGrid.Depth; z++)
            {
                for (int x = 0; x < OfficeGrid.Width; x++)
                {
                    if (!OfficeGrid.IsWalkable(x, z))
                        continue;

                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(tile, parent);
                    // Tile-Pivot liegt unten: um die Dicke absenken, damit die Oberkante genau y=0 ist.
                    inst.transform.position = OfficeGrid.CellCenter(x, z) + Vector3.down * OfficeGrid.FloorThickness;
                    inst.transform.rotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one * OfficeGrid.KitScale;
                    inst.name = $"Floor_{x:00}_{z:00}";
                    count++;
                }
            }
            return count;
        }

        // ------------------------------------------------------------------ Waende

        private static int BuildWalls(Transform parent)
        {
            GameObject plain = Load(KitTiles + "toon_office_tile_wall.prefab");
            GameObject door = Load(KitTiles + "toon_office_tile_wall_door.prefab");
            GameObject window = Load(KitTiles + "toon_office_tile_wall_window.prefab");
            if (plain == null)
                return 0;

            int count = 0;

            // Tuer- und Fensterstuecke sind 2 Zellen lang und duerfen nicht skaliert werden,
            // sonst verzerrt die Oeffnung. Deshalb zuerst zusammenhaengende Laeufe bilden.
            for (int z = 0; z < OfficeGrid.Depth; z++)
            {
                int x = 0;
                while (x < OfficeGrid.Width)
                {
                    OfficeCell cell = OfficeGrid.Get(x, z);
                    if (cell != OfficeCell.WallDoor && cell != OfficeCell.WallWindow)
                    {
                        x++;
                        continue;
                    }

                    int runStart = x;
                    while (x < OfficeGrid.Width && OfficeGrid.Get(x, z) == cell)
                        x++;

                    GameObject prefab = cell == OfficeCell.WallDoor ? door : window;
                    string label = cell == OfficeCell.WallDoor ? "Door" : "Window";
                    int runLength = x - runStart;

                    for (int seg = 0; seg + 1 < runLength; seg += 2)
                    {
                        PlaceWall(parent, prefab ?? plain, runStart + seg + 1, z, 2, false, label);
                        count++;
                    }
                    // Ungerader Rest: mit einem normalen 1-Zellen-Stueck schliessen.
                    if (runLength % 2 == 1)
                    {
                        PlaceWall(parent, plain, x - 1, z, 1, false, "Wall");
                        count++;
                    }
                }
            }

            // Normale Waende zellenweise. Eine Ecke bekommt beide Ausrichtungen — die beiden
            // Stuecke ueberlappen im Zellzentrum, was von aussen genau wie ein Eckpfosten aussieht.
            for (int z = 0; z < OfficeGrid.Depth; z++)
            {
                for (int x = 0; x < OfficeGrid.Width; x++)
                {
                    if (OfficeGrid.Get(x, z) != OfficeCell.Wall)
                        continue;

                    bool horizontal = OfficeGrid.IsWallLike(x - 1, z) || OfficeGrid.IsWallLike(x + 1, z);
                    bool vertical = OfficeGrid.IsWallLike(x, z - 1) || OfficeGrid.IsWallLike(x, z + 1);

                    if (!horizontal && !vertical)
                        horizontal = true;

                    if (horizontal)
                    {
                        PlaceWall(parent, plain, x, z, 1, false, "Wall");
                        count++;
                    }
                    if (vertical)
                    {
                        PlaceWall(parent, plain, x, z, 1, true, "Wall");
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Setzt ein Wandstueck. Der Pivot des Kit-Prefabs sitzt am +X-Ende, die Wand laeuft nach -X
        /// (gemessen: bounds.center.x = -1 bei 2.0 Laenge). Deshalb wird am +X-Rand der rechten
        /// Zelle angesetzt; bei Rotation 90 Grad wird local -X zu world +Z, also am -Z-Rand.
        /// </summary>
        private static void PlaceWall(Transform parent, GameObject prefab, int xRight, int z,
                                      int lengthCells, bool vertical, string label)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            float half = OfficeGrid.Tile * 0.5f;
            Vector3 center = OfficeGrid.CellCenter(xRight, z);

            // Native Laenge des Prefabs ist 2.0 -> Skalierung auf lengthCells * Tile.
            float lengthScale = lengthCells * OfficeGrid.Tile / 2f;

            if (vertical)
            {
                inst.transform.position = new Vector3(center.x, 0f, center.z - half);
                inst.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                inst.name = $"{label}_V_{xRight:00}_{z:00}";
            }
            else
            {
                inst.transform.position = new Vector3(center.x + half, 0f, center.z);
                inst.transform.rotation = Quaternion.identity;
                inst.name = $"{label}_H_{xRight:00}_{z:00}";
            }

            inst.transform.localScale = new Vector3(lengthScale, OfficeGrid.KitScale, OfficeGrid.KitScale);
        }

        // ------------------------------------------------------------------ Anker

        private static void BuildAnchors(Transform parent)
        {
            NewAnchor(parent, "Station_Anchor", OfficeGrid.Station);
            NewAnchor(parent, "Shelf_Anchor", OfficeGrid.Shelf);
            NewAnchor(parent, "Inbox_Anchor", OfficeGrid.Inbox);
            NewAnchor(parent, "Outbox_Anchor", OfficeGrid.Outbox);

            for (int i = 0; i < OfficeGrid.QueueSlots.Length; i++)
                NewAnchor(parent, $"Queue_Slot_{i}", OfficeGrid.QueueSlots[i]);
        }

        private static Transform NewAnchor(Transform parent, string name, Vector2Int cell)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = OfficeGrid.CellCenter(cell);
            return go.transform;
        }

        // ------------------------------------------------------------------ Props

        private static int BuildProps(Transform parent)
        {
            // Nordwand-Innenkante und Westwand-Innenkante — fuer wandmontierte Objekte.
            float northInnerZ = OfficeGrid.CellCenter(0, OfficeGrid.Depth - 1).z - OfficeGrid.WallThickness * 0.5f;
            float westInnerX = OfficeGrid.CellCenter(0, 0).x + OfficeGrid.WallThickness * 0.5f;

            List<PropSpec> specs = new List<PropSpec>
            {
                // --- Denk-Station: Yaw 180, damit die Bedienseite (bei diesem Desk +X) nach -X
                //     zeigt und der Agent auf Queue_Slot_0 steht. Der Schreibtisch selbst ist das
                //     bestehende (Prb)Desk1 aus dem Spike, siehe PlaceExistingSpikeObjects().
                new PropSpec(Nappin + "(Prb)PCCase.prefab",
                             OfficeGrid.StationTower.x, OfficeGrid.StationTower.y, 180f, "Denkkiste_Tower"),

                // --- Wissens-Regal im unteren Schenkel, Ruecken zur Westwand: nah an der Inbox,
                //     maximal weit von der Station — genau die Nachbarschaft, die man nicht
                //     gleichzeitig mit "kurzer Weg zur Denk-Station" haben kann.
                new PropSpec(Nappin + "(Prb)Shelves1.prefab",
                             OfficeGrid.Shelf.x, OfficeGrid.Shelf.y, 0f, "Wissensregal"),
                new PropSpec(Nappin + "(Prb)BookPile3.prefab", 1, 3, 0f, "Wissensstapel"),

                // --- Triage / Inbox direkt an der Tuer.
                new PropSpec(Nappin + "(Prb)Desk2.prefab",
                             OfficeGrid.Inbox.x, OfficeGrid.Inbox.y, 0f, "Inbox_Desk"),
                new PropSpec(Nappin + "(Prb)OfficeChair.prefab", 4, 2, 270f, "Inbox_Chair"),
                new PropSpec(Nappin + "(Prb)DocumentHolder.prefab",
                             OfficeGrid.Inbox.x, OfficeGrid.Inbox.y, 0f, "Inbox_Ablage",
                             new Vector3(0f, 0.95f, 0.25f)),

                // --- Postausgang plus das obligatorische Kaffeekannen-Klischee.
                new PropSpec(Nappin + "(Prb)Drawer2.prefab",
                             OfficeGrid.Outbox.x, OfficeGrid.Outbox.y, 0f, "Postausgang"),
                new PropSpec(Nappin + "(Prb)CoffePot.prefab",
                             OfficeGrid.Outbox.x, OfficeGrid.Outbox.y, 0f, "Kaffeekanne",
                             new Vector3(0.15f, 0.58f, 0f)),

                // --- Das Drucker-Klischee. Auf z=5, weil diese Reihe von der Suedwand des oberen
                //     Schenkels verdeckt wird — dort gehoert Hintergrund-Deko hin, keine Mechanik.
                new PropSpec(Nappin + "(Prb)Printer.prefab", 8, 5, 180f, "Drucker"),
                new PropSpec(Nappin + "(Prb)WaterDispenser.prefab", 5, 3, 0f, "Wasserspender"),

                // --- Startup-Satire: der Nachlass der Vorbesitzer.
                new PropSpec(Nappin + "(Prb)Sofa1.prefab", 3, 7, 180f, "Satire_Sofa"),
                // Der Fatboy ist im Original 1.85 m breit — mehr als eine Zelle. Runterskaliert,
                // sonst frisst die Deko den Laufweg, um den es hier eigentlich geht.
                new PropSpec(Nappin + "(Prb)Fatboy.prefab", 4, 7, 0f, "Satire_Fatboy",
                             default, 0.7f),
                new PropSpec(Nappin + "(Prb)YogaBall.prefab", 5, 6, 0f, "Satire_Yogaball"),
                new PropSpec(Plants + "Plants/Plant3.prefab", 2, 7, 0f, "Vertrocknete_Monstera"),

                // --- Aussen ist zweitrangig: zwei Kuebel neben dem Eingang, sonst nichts.
                //     Die Level13-Kuebel sind im Original ueberlebensgross (2.6 m breit).
                new PropSpec(Plants + "Plant & pots/Plant & Pot.prefab", 1, -1, 0f, "Aussen_Kuebel_A",
                             default, 0.5f),
                new PropSpec(Plants + "Plant & pots/Pot1 & Cactus.prefab", 5, -1, 0f, "Aussen_Kuebel_B",
                             default, 0.7f),
            };

            int count = 0;
            foreach (PropSpec spec in specs)
            {
                GameObject prefab = Load(spec.Path);
                if (prefab == null)
                    continue;

                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.position = OfficeGrid.CellCenter(spec.X, spec.Z) + spec.Offset;
                inst.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
                inst.transform.localScale = Vector3.one * spec.Scale;
                inst.name = spec.Name;
                count++;
            }

            // Wandmontiert: Whiteboard an der Westwand, Uhr an der Nordwand.
            // Das Whiteboard des Toon-Kits ist in Z duenn, Yaw 90 dreht seine Front nach +X.
            count += PlaceOnWall(parent, KitProps + "toon_office_board.prefab", "Whiteboard_DISRUPT",
                                 new Vector3(westInnerX + 0.15f, 1.75f, OfficeGrid.CellCenter(0, 6).z),
                                 90f, OfficeGrid.KitScale);

            // Die Uhr ist in X duenn (0.05), ihr Zifferblatt zeigt also nach +-X. Yaw 90 dreht es
            // nach -Z, damit sie von der Nordwand in den Raum blickt.
            count += PlaceOnWall(parent, Nappin + "(Prb)Clock.prefab", "Wanduhr",
                                 new Vector3(OfficeGrid.CellCenter(3, 8).x, 2.35f, northInnerZ - 0.08f),
                                 90f, 1f);

            return count;
        }

        private static int PlaceOnWall(Transform parent, string path, string name,
                                       Vector3 position, float yaw, float scale)
        {
            GameObject prefab = Load(path);
            if (prefab == null)
                return 0;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.position = position;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale = Vector3.one * scale;
            inst.name = name;
            return 1;
        }

        // ------------------------------------------------------------------ Sicherung + Lastsaeule

        /// <summary>
        /// Weltobjekt-Haelfte der Denklast (Konzept §2.3). Instanziiert das spielfertige
        /// FuseBox-Prefab — ein Variant des Blender-Modells, den die FuseBoxPrefabFactory
        /// frisch aus dem FBX ableitet (Achskompensation, LoadPillar-Verdrahtung,
        /// Segment-Material). Position: Nordwand direkt hinter der Denk-Station, damit
        /// Engpass und Anzeige im selben Kamerabild liegen — Lesbarkeit, nicht Deko.
        /// </summary>
        private static void BuildFuseBox(Transform parent)
        {
            // Immer frisch ableiten: so kann das Prefab nie hinter dem FBX-Stand herhinken.
            GameObject prefab = FuseBoxPrefabFactory.CreateOrUpdate();
            if (prefab == null)
            {
                Debug.LogWarning("[OfficeLayoutBuilder] FuseBox-Prefab konnte nicht erzeugt " +
                                 "werden — Sicherungskasten wird uebersprungen.");
                return;
            }

            Vector3 cell = OfficeGrid.CellCenter(OfficeGrid.FuseBox);

            GameObject holder = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            holder.name = "Sicherungskasten";
            holder.transform.position = new Vector3(
                cell.x,
                OfficeGrid.FuseBoxMountHeight,
                cell.z - OfficeGrid.WallThickness * 0.5f);
            // Rotation und Scale kommen aus dem Prefab — die Achskompensation (-90/180) liegt
            // in der FuseBoxPrefabFactory. Hier wird nur platziert, nichts korrigiert.

            // Kein BoxCollider hier: BakeNavMesh() sammelt ueber CollectObjects.Children +
            // NavMeshCollectGeometry.RenderMeshes ausschliesslich Render-Meshes fuer den Bake
            // (siehe Kommentar dort: "RenderMeshes und nicht PhysicsColliders"). Ein Collider
            // wuerde also weder das NavMesh beeinflussen noch gebraucht — die FBX-Meshes selbst
            // sind als Kind von [Office] ohnehin schon Teil der eingesammelten Geometrie. Ohne
            // eine belastbare Notwendigkeit fuer Physik/Klick-Interaktion bleibt er bewusst weg,
            // statt eines zu raten.
        }


        // ------------------------------------------------------------------ Bestand einpassen

        /// <summary>
        /// Objekte, die dem Builder NICHT gehoeren, aber in den Raum muessen: der Schreibtisch samt
        /// PC aus dem LLM-Spike (bleibt ComputerTerminal), die Puppets, und die Verdrahtung von
        /// NPCGoToTerminal auf den vordersten Warte-Slot.
        /// </summary>
        private static void PlaceExistingSpikeObjects(Transform anchors)
        {
            Transform slot0 = anchors.Find("Queue_Slot_0");

            GameObject desk = GameObject.Find("(Prb)Desk1");
            GameObject pc = GameObject.Find("(Prb)PC");

            if (desk != null)
            {
                Vector3 target = OfficeGrid.CellCenter(OfficeGrid.Station);
                Quaternion rot = Quaternion.Euler(0f, 180f, 0f);

                // Der PC behaelt seine relative Lage auf der Tischplatte.
                Vector3 pcLocal = Vector3.zero;
                if (pc != null)
                    pcLocal = Quaternion.Inverse(desk.transform.rotation) * (pc.transform.position - desk.transform.position);

                Undo.RecordObject(desk.transform, "Build Office (M1)");
                desk.transform.SetPositionAndRotation(target, rot);
                desk.name = "(Prb)Desk1";

                if (pc != null)
                {
                    Undo.RecordObject(pc.transform, "Build Office (M1)");
                    pc.transform.SetPositionAndRotation(target + rot * pcLocal, rot);
                }
            }

            // Puppets in den Raum setzen und den Wander-Radius auf Raumgroesse begrenzen.
            // Nicht auf z=5: diese Reihe liegt im Verdeckungsschatten der Trennwand, dort stehende
            // Agenten waeren nur als Kopf ueber der Wand zu sehen.
            Vector2Int[] spawn =
            {
                new Vector2Int(3, 6),
                new Vector2Int(5, 6),
                new Vector2Int(4, 3),
                new Vector2Int(7, 7),
                new Vector2Int(2, 6)
            };

            NPCWander[] wanderers = Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Exclude);
            for (int i = 0; i < wanderers.Length; i++)
            {
                Undo.RecordObject(wanderers[i], "Build Office (M1)");
                Undo.RecordObject(wanderers[i].transform, "Build Office (M1)");
                wanderers[i].transform.position = OfficeGrid.CellCenter(spawn[i % spawn.Length]);
                wanderers[i].wanderRadius = 5f;
            }

            if (slot0 != null)
            {
                NPCGoToTerminal[] goers = Object.FindObjectsByType<NPCGoToTerminal>(FindObjectsInactive.Exclude);
                foreach (NPCGoToTerminal goer in goers)
                {
                    Undo.RecordObject(goer, "Build Office (M1)");
                    goer.interactPoint = slot0;
                }
            }
        }

        /// <summary>
        /// CityGround ist eine auf 200 skalierte Plane (2000 x 2000 m) bei y=0. Sie wuerde mit der
        /// Bodenoberkante z-fighten und fuellt als endloses Kopfsteinpflaster den ganzen Bildrahmen.
        /// Da alles ausserhalb des Bueros zweitrangig ist, bleibt nur eine schmale Vorflaeche stehen.
        /// </summary>
        private static void SinkCityGround()
        {
            GameObject ground = GameObject.Find("CityGround");
            if (ground == null)
                return;

            Bounds b = OfficeGrid.BuildingBounds();
            const float apron = 8f;

            Undo.RecordObject(ground.transform, "Build Office (M1)");
            ground.transform.position = new Vector3(b.center.x, -0.1f, b.center.z);
            // Unity-Plane ist 10 x 10 m bei Scale 1.
            ground.transform.localScale = new Vector3(
                (b.size.x + apron * 2f) / 10f, 1f,
                (b.size.z + apron * 2f) / 10f);
        }

        /// <summary>
        /// Minimaler Lesbarkeits-Fix, keine Art Direction: mit dem Szenen-Default (Ambient Sky 0.21,
        /// Equator 0.11) fallen die Waende so hart in den Raum, dass die halbe Grundflaeche schwarz
        /// ist. Aus der Iso-Perspektive muessen Schlange und Lastsaeule aber ablesbar sein.
        /// </summary>
        private static void TuneLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.58f);

            Light dir = null;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.type == LightType.Directional)
                {
                    dir = l;
                    break;
                }
            }

            if (dir == null)
                return;

            Undo.RecordObject(dir, "Build Office (M1)");
            Undo.RecordObject(dir.transform, "Build Office (M1)");
            // Steiler Einfall: flache Winkel legen den halben Raum in den Schatten der Nordwand.
            dir.transform.rotation = Quaternion.Euler(62f, 315f, 0f);
            dir.intensity = 1.15f;
            dir.shadowStrength = 0.55f;
        }

        // ------------------------------------------------------------------ Kamera

        /// <summary>
        /// Stellt das Kamera-Rig so ein, dass der ganze Grundriss bei Default-Zoom im Bild ist.
        /// Abnahmekriterium fuer M1: Denk-Station, Warte-Slots und Lastsaeule muessen gleichzeitig
        /// sichtbar sein — sonst laesst sich "Schlange = Engpass" nicht ohne Erklaerung lesen.
        /// </summary>
        private static void FitCamera()
        {
            RTSCameraController cam = Object.FindAnyObjectByType<RTSCameraController>();
            if (cam == null)
                return;

            Bounds b = OfficeGrid.BuildingBounds();
            const float margin = 6f;

            SerializedObject so = new SerializedObject(cam);
            so.FindProperty("boundsX").vector2Value =
                new Vector2(b.min.x - margin, b.max.x + margin);
            so.FindProperty("boundsZ").vector2Value =
                new Vector2(b.min.z - margin, b.max.z + margin);
            so.ApplyModifiedProperties();

            Undo.RecordObject(cam.transform, "Build Office (M1)");
            cam.transform.position = new Vector3(b.center.x, 0f, b.center.z);

            Transform holder = cam.transform.childCount > 0 ? cam.transform.GetChild(0) : null;
            if (holder == null)
                return;

            // 45 Grad Aufsicht. Der Zoom des Controllers laeuft entlang holder.localPosition,
            // der Blickwinkel bleibt beim Zoomen also erhalten.
            // Distanz aus der Kamera-Brennweite gerechnet, nicht geschaetzt: die Kamera faehrt
            // 30 Grad vertikale FOV, daraus ergibt sich die horizontale FOV ueber das Seitenverhaeltnis.
            Camera lens = cam.GetComponentInChildren<Camera>();
            float fovV = lens != null ? lens.fieldOfView : 30f;
            float aspect = lens != null && lens.pixelHeight > 0
                ? (float)lens.pixelWidth / lens.pixelHeight
                : 16f / 9f;
            float fovH = 2f * Mathf.Atan(Mathf.Tan(fovV * 0.5f * Mathf.Deg2Rad) * aspect);
            float needWidth = b.size.x + margin;
            float distance = (needWidth * 0.5f) / Mathf.Tan(fovH * 0.5f);

            // Kameraneigung folgt der Wandhoehe, nicht dem Geschmack: eine Wand der Hoehe h
            // verdeckt h / tan(pitch) Meter dahinter. Bei 3 m Wandhoehe sind das
            //   45 Grad -> 3.00 m = 2.00 Zellen (Warte-Zone unsichtbar)
            //   50 Grad -> 2.52 m = 1.68 Zellen (Agenten stehen halb hinter der Wand)
            //   60 Grad -> 1.73 m = 1.15 Zellen (nur die erste Reihe hinter der Wand faellt weg)
            // Steiler als 60 Grad kippt die Ansicht in Draufsicht und nimmt den Agenten das Profil.
            const float pitch = 60f;
            float pitchRad = pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(0f, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad));

            Undo.RecordObject(holder, "Build Office (M1)");
            holder.localPosition = dir * distance;
            holder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (lens != null && lens.transform != holder)
            {
                Undo.RecordObject(lens.transform, "Build Office (M1)");
                lens.transform.localPosition = Vector3.zero;
                lens.transform.localRotation = Quaternion.identity;
            }

            // Der Controller klemmt den Zoom — die berechnete Distanz muss im erlaubten Bereich liegen.
            SerializedProperty minZoom = so.FindProperty("minZoomDistance");
            if (minZoom != null && minZoom.floatValue > distance)
            {
                minZoom.floatValue = Mathf.Floor(distance * 0.5f);
                so.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------------------ NavMesh

        /// <summary>
        /// Baut das NavMesh ausschliesslich aus der Office-Geometrie (CollectObjects.Children).
        /// Damit ist die 2000x2000 grosse CityGround-Plane aussen vor: Agenten bleiben im Gebaeude,
        /// und die Waende wirken sofort als Hindernis.
        /// </summary>
        private static int BakeNavMesh(GameObject root)
        {
            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = root.AddComponent<NavMeshSurface>();

            // Fremde NavMeshSurfaces stilllegen. Konkret haengt eine an der CityGround mit
            // collectObjects = All und einem Bake ueber die vollen 2000 x 2000 m. Sie liefert eine
            // flache, hindernisfreie Ebene, die das Office-NavMesh vollstaendig ueberdeckt: Agenten
            // laufen dann quer durch die Waende und die Pfadlaengen entsprechen der Luftlinie.
            // Bewusst nur RemoveData + deaktivieren statt loeschen — die Komponente gehoert nicht
            // dem Builder, die Entscheidung ueber ihr Schicksal auch nicht.
            foreach (NavMeshSurface other in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
            {
                if (other == surface)
                    continue;

                Undo.RecordObject(other, "Build Office (M1)");
                other.RemoveData();
                other.enabled = false;
                Debug.Log(
                    $"[OfficeLayoutBuilder] NavMeshSurface auf '{other.name}' stillgelegt — sie hat " +
                    "das Office-NavMesh ueberdeckt. Falls dauerhaft nicht gebraucht, Komponente entfernen " +
                    "und 'Assets/_AITycoon/Scenes/MainScene/NavMesh-CityGround 1.asset' loeschen.");
            }

            // collectObjects = Children entspricht "Current Object Hierarchy" in der Doku:
            // gesammelt wird nur die Geometrie unter [Office]. Die CityGround-Plane bleibt damit
            // aussen vor, Agenten bleiben im Gebaeude.
            surface.collectObjects = CollectObjects.Children;

            // RenderMeshes und nicht PhysicsColliders: die Wandstuecke des Mini-Toon-Office-Kits
            // tragen keine Collider. Mit PhysicsColliders waeren sie kein Hindernis, und der ganze
            // Grundriss haette fuer die Navigation keine Bedeutung.
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = ~0;

            // Ohne das entstehen begehbare Inseln auf Tischplatten und Schrankdeckeln, weil
            // RenderMeshes auch Moebeloberflaechen einsammelt. Erreichbar sind die nie.
            surface.minRegionArea = 2f;

            surface.BuildNavMesh();
            PersistNavMeshData(surface);

            return surface.navMeshData != null ? 1 : 0;
        }

        /// <summary>
        /// BuildNavMesh() aus einem Script erzeugt die NavMeshData nur im Speicher — anders als der
        /// Bake-Button im Inspector, der sie als Asset ablegt. Ohne dieses Speichern referenziert die
        /// Szene nach dem Commit ein Objekt, das es auf anderen Rechnern nicht gibt: das Projekt
        /// startet dort ohne NavMesh, und die Agenten bewegen sich nicht.
        /// </summary>
        private static void PersistNavMeshData(NavMeshSurface surface)
        {
            NavMeshData data = surface.navMeshData;
            if (data == null || AssetDatabase.Contains(data))
                return;

            string scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning("[OfficeLayoutBuilder] Szene ist noch nie gespeichert worden — " +
                                 "NavMesh kann nicht als Asset abgelegt werden.");
                return;
            }

            // Unity legt Szenen-Nebendaten in einem Ordner neben der Szene ab, gleicher Name.
            string dir = System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            string folder = $"{dir}/{sceneName}";

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(dir, sceneName);

            // Der Root heisst "[Office]" — Klammern im Dateinamen sind plattformabhaengig heikel
            // und stoeren AssetDatabase-Suchen, deshalb raus damit.
            string safeName = surface.gameObject.name.Replace("[", string.Empty).Replace("]", string.Empty);
            string assetPath = $"{folder}/NavMesh-{safeName}.asset";

            // Vorhandenes Asset ueberschreiben statt Dubletten anzulegen.
            NavMeshData old = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (old != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();

            // Referenz explizit neu setzen, damit sie in die Szene serialisiert wird.
            Undo.RecordObject(surface, "Build Office (M1)");
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            EditorUtility.SetDirty(surface);

            Debug.Log($"[OfficeLayoutBuilder] NavMesh als Asset gespeichert: {assetPath}");
        }

        // ------------------------------------------------------------------ Helfer

        private static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[OfficeLayoutBuilder] Prefab nicht gefunden: {path}");
            return prefab;
        }
    }
}
