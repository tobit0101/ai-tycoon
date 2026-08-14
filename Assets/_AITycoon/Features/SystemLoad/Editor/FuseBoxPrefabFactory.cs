using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AITycoon.Features.SystemLoad.EditorTools
{
    /// <summary>
    /// Erzeugt bzw. aktualisiert das spielfertige FuseBox-Prefab:
    /// Menue "AI Tycoon → FuseBox-Prefab erzeugen/aktualisieren".
    ///
    /// Warum ein Prefab-VARIANT des FBX und keine Kopie: der Variant erbt jeden
    /// Blender-Re-Export automatisch (neue Kinder, geaenderte Meshes), traegt aber die
    /// Spiel-Schicht als Overrides — Achskompensation auf dem Root, LoadPillar mit fertig
    /// verdrahteten Referenzen, geteiltes Segment-Material. Niemand muss im Inspector
    /// Einzelteile setzen: die Verdrahtung folgt dem Namensvertrag aus Art_Source/README.md
    /// und ist per Menueklick reproduzierbar. Ein Inspector-Reset der Komponente heilt
    /// LoadPillar zur Laufzeit ohnehin selbst (ResolveContractBindings).
    /// </summary>
    public static class FuseBoxPrefabFactory
    {
        public const string PrefabPath = "Assets/_AITycoon/Prefabs/FuseBox.prefab";
        private const string ModelPath = "Assets/_AITycoon/Art/Models/FuseBox.fbx";
        private const string SegmentMaterialPath = "Assets/_AITycoon/Art/Materials/M_LoadSegment.mat";

        [MenuItem("AI Tycoon/FuseBox-Prefab erzeugen/aktualisieren")]
        public static void CreateOrUpdateMenu()
        {
            GameObject prefab = CreateOrUpdate();
            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[FuseBoxPrefabFactory] Prefab aktualisiert: {PrefabPath}");
            }
        }

        /// <summary>
        /// Baut den Variant frisch aus dem FBX und speichert ihn nach <see cref="PrefabPath"/>.
        /// Idempotent — ein zweiter Lauf ueberschreibt mit identischem Ergebnis.
        /// </summary>
        public static GameObject CreateOrUpdate()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[FuseBoxPrefabFactory] Modell nicht gefunden: {ModelPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = "FuseBox";

                // Achskompensation gehoert INS Prefab, nicht an jede Platzierstelle.
                // Reihenfolge beachten — Quaternion.Euler(x, y, z) wendet Z, dann X, dann Y an:
                // 1) -90 um X: FuseBox.fbx meldet im Header bereits Unitys Achsen (UpAxis = Y),
                //    die Geometrie liegt aber in rohen Blender-Achsen (Z = oben) — ohne die -90
                //    laege der Kasten flach. Bewusst auf dem Root statt im Import: so behalten
                //    alle Kinder ihre lokale Identitaets-Rotation, worauf sich LoadPillar beim
                //    Zuruecksetzen von Hebel/Bank/Tuer verlaesst.
                // 2) 180 um Y: das Modell schaut nach +Z; so schaut es "aus der Wand heraus",
                //    wenn das Prefab unrotiert an eine Nordwand gestellt wird.
                instance.transform.rotation = Quaternion.Euler(-90f, 180f, 0f);
                instance.transform.localScale = Vector3.one;

                Configure(instance);

                if (!AssetDatabase.IsValidFolder("Assets/_AITycoon/Prefabs"))
                    AssetDatabase.CreateFolder("Assets/_AITycoon", "Prefabs");

                return PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Verdrahtet LoadPillar auf einer FuseBox-Instanz ueber die Vertragsnamen und weist
        /// den Segmenten das geteilte Laufzeit-Material zu. Oeffentlich, damit auch der
        /// OfficeLayoutBuilder dieselbe Logik nutzt statt einer zweiten Implementierung.
        /// </summary>
        public static void Configure(GameObject holder)
        {
            LoadPillar pillar = holder.GetComponent<LoadPillar>();
            if (pillar == null)
                pillar = holder.AddComponent<LoadPillar>();

            // Segmente dynamisch einsammeln statt hart zu codieren: Segment_00 … Segment_11
            // sind per Zero-Padding alphabetisch = numerisch sortiert (unten nach oben).
            Transform segmentsRoot = holder.transform.Find("Segments");
            if (segmentsRoot == null || segmentsRoot.childCount == 0)
            {
                Debug.LogWarning("[FuseBoxPrefabFactory] Kind 'Segments' fehlt oder ist leer — " +
                                 "Lastsaeule bleibt ohne Segmente.");
            }
            else
            {
                List<Renderer> segments = segmentsRoot.Cast<Transform>()
                    .OrderBy(t => t.name, System.StringComparer.Ordinal)
                    .Select(t => t.GetComponent<Renderer>())
                    .Where(r => r != null)
                    .ToList();

                if (segments.Count == 0)
                {
                    Debug.LogWarning("[FuseBoxPrefabFactory] Keine gueltigen Segment-Renderer " +
                                     "unter 'Segments' gefunden.");
                }
                else
                {
                    // Gemeinsames Material statt der FBX-Materialien: LoadPillar faerbt zur
                    // Laufzeit per MaterialPropertyBlock um, ein geteiltes Material haelt dabei
                    // alle Segmente in einem Batch — und nur am geteilten Material kann das
                    // _EMISSION-Keyword aktiv sein (per PropertyBlock nicht setzbar).
                    Material segMat = GetOrCreateSegmentMaterial();
                    foreach (Renderer r in segments)
                        r.sharedMaterial = segMat;

                    pillar.AssignSegments(segments.ToArray());
                }
            }

            Transform lever = holder.transform.Find("Breaker_Lever");
            if (lever == null)
                Debug.LogWarning("[FuseBoxPrefabFactory] 'Breaker_Lever' nicht gefunden — " +
                                 "Hebel bleibt unanimiert.");
            pillar.AssignLever(lever);

            Transform door = holder.transform.Find("Box_Door");
            if (door == null)
                Debug.LogWarning("[FuseBoxPrefabFactory] 'Box_Door' nicht gefunden — " +
                                 "Tuer bleibt beim Blowout zu.");
            pillar.AssignDoor(door);

            // Kipphebel-Bank (seit ASSET_VERSION 3): das Empty kippt beim Blowout, die
            // Kipphebel-Renderer werden dabei rot getintet. Aeltere FBX-Staende ohne Bank
            // loggen nur eine Warnung.
            Transform bank = holder.transform.Find("Breaker_Bank");
            if (bank == null)
            {
                Debug.LogWarning("[FuseBoxPrefabFactory] 'Breaker_Bank' nicht gefunden — " +
                                 "Nischen-Kipphebel bleiben unanimiert.");
                pillar.AssignBank(null, null);
            }
            else
            {
                Renderer[] toggles = bank.Cast<Transform>()
                    .Where(t => t.name.StartsWith("Breaker_Tog_", System.StringComparison.Ordinal))
                    .OrderBy(t => t.name, System.StringComparer.Ordinal)
                    .Select(t => t.GetComponent<Renderer>())
                    .Where(r => r != null)
                    .ToArray();
                pillar.AssignBank(bank, toggles);
            }
        }

        /// <summary>
        /// Geteiltes Laufzeit-Material der Segmente. Das _EMISSION-Keyword muss am GETEILTEN
        /// Material aktiv sein: LoadPillar setzt per MaterialPropertyBlock nur Property-WERTE,
        /// Keywords lassen sich dort nicht schalten.
        /// </summary>
        public static Material GetOrCreateSegmentMaterial()
        {
            Color unlit = new Color(0.16f, 0.17f, 0.19f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(SegmentMaterialPath);
            if (existing != null)
            {
                ApplyColor(existing, unlit);
                existing.EnableKeyword("_EMISSION");
                existing.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_AITycoon/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/_AITycoon/Art", "Materials");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = "M_LoadSegment" };
            ApplyColor(mat, unlit);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            AssetDatabase.CreateAsset(mat, SegmentMaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void ApplyColor(Material mat, Color color)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
        }
    }
}
