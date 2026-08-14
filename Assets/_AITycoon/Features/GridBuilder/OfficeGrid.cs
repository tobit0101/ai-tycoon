using UnityEngine;

namespace AITycoon.Features.GridBuilder
{
    /// <summary>Was in einer Grid-Zelle steht. Rein architektonisch — Moebel sind kein Zellen-Typ.</summary>
    public enum OfficeCell
    {
        Outside,
        Floor,
        Wall,
        WallDoor,
        WallWindow
    }

    /// <summary>
    /// Der Grundriss des M1-Bueros als Daten plus die Grid-zu-Welt-Mathematik.
    ///
    /// Bewusst ohne Editor-Abhaengigkeiten: dieselben Funktionen braucht spaeter der echte
    /// Bau-Modus zur Laufzeit (Snapping, Ghost-Preview, Belegungspruefung). Der Builder in
    /// Editor/OfficeLayoutBuilder.cs instanziiert nur, gerechnet wird hier.
    ///
    /// Grundriss-Absicht (siehe Docs/AI-Tycoon Konzept.md, §2.1 "Latenz ist Gameplay"):
    /// L-Form, damit Tuer, Regal und Denk-Station nicht gleichzeitig nah beieinander liegen
    /// koennen.
    ///
    /// Entscheidend ist der Durchgang: die Trennwand auf z=4 laesst nur die Zellen x=1,2 offen.
    /// Ohne diese Wand waeren beide Schenkel ueber die volle Breite verbunden, und die L-Form
    /// waere blosse Dekoration — gemessen war der Weg Inbox->Station dann 11.9 m gegenueber
    /// 11.7 m Luftlinie, also gar kein Umweg. Mit der Wand liegt der Engpass im Westen, waehrend
    /// die Station im Nordosten sitzt: jeder Auftrag laeuft die volle Diagonale und alle Agenten
    /// muessen durch dieselben zwei Zellen.
    /// </summary>
    public static class OfficeGrid
    {
        /// <summary>
        /// Kantenlaenge einer Zelle in Weltmetern.
        ///
        /// Gemessen (Renderer.bounds der Prefabs, Stand 2026-08-14):
        ///   Mini-Toon-Office Boden-Tile = 1.027 breit (Schritt exakt 1.0, 0.027 Ueberstand als
        ///                                 Fugen-Kaschierung), Wand = 2.0 lang x 2.0 hoch.
        ///   nappin Desk1                = 0.95 hoch -> das Pack ist Realmassstab (1 Unit = 1 m).
        ///   puppet_kid                  = ca. 1.95 hoch.
        /// Das Toon-Kit ist gegenueber nappin zu klein: bei KitScale 1.5 wird die Wand 3.0 hoch
        /// und die Tuer 2.2 hoch — passend zu einem 1.95 m grossen Agenten. Damit ist die Zelle
        /// 1.5 m gross, und ein 2 Zellen langes Wandstueck deckt exakt 3.0 m ab.
        /// </summary>
        public const float Tile = 1.5f;

        /// <summary>Uniformer Skalierungsfaktor fuer alle Mini-Toon-Office-Architektur-Teile.</summary>
        public const float KitScale = 1.5f;

        /// <summary>Dicke des Boden-Tiles nach Skalierung. Tiles liegen so, dass ihre Oberkante y=0 ist.</summary>
        public const float FloorThickness = 0.5f * KitScale;

        /// <summary>Dicke eines Wandstuecks nach Skalierung — mittig auf der Zellachse.</summary>
        public const float WallThickness = 0.5f * KitScale;

        /// <summary>Hoehe eines Wandstuecks nach Skalierung.</summary>
        public const float WallHeight = 2.0f * KitScale;

        /// <summary>Montagehoehe der Unterkante des Sicherungskastens ueber dem Boden.
        /// Bei 1.45 m Modellhoehe reicht die Lastsaeule damit bis 2.20 m — ueber Kopfhoehe
        /// eines 1.95 m grossen Agenten, also aus der Iso-Perspektive nie verdeckt.</summary>
        public const float FuseBoxMountHeight = 0.75f;

        /// <summary>
        /// Der Grundriss. Zeile 0 ist z = Depth-1 (Norden), damit das Array genauso aussieht
        /// wie der Grundriss im Konzept-Dokument.
        ///   #  Wand      W  Wand mit Fenster    D  Wand mit Tuer
        ///   .  Boden     (Leerzeichen)          ausserhalb des Gebaeudes
        /// </summary>
        private static readonly string[] Rows =
        {
            "######WWWW##", // z=8  Nordwand, Fenster ueber der Denk-Station
            "#..........#", // z=7  Denk-Station (10) + Turm (9) + Warte-Zone (8,7,6)
            "#..........#", // z=6
            "#..........#", // z=5
            "#..#########", // z=4  Trennwand — Durchgang NUR bei x=1,2
            "#.....#",      // z=3
            "#.....#",      // z=2
            "#.....#",      // z=1
            "##DD###"       // z=0  Suedwand mit Tuer bei x=2..3
        };

        public const int Width = 12;
        public const int Depth = 9;

        // --- Anker: die Pole des Grundrisses. Bewusst Konstanten, nicht im ASCII versteckt. ---

        /// <summary>
        /// Denk-Station (Starkstrom-Ecke): maximal weit von der Tuer entfernt.
        ///
        /// Bewusst auf der Nordreihe z=7 und nicht weiter vorne: eine 3 m hohe Wand verdeckt bei
        /// 45 Grad Kameraneigung genau 2 Zellen hinter sich. Die Suedwand des oberen Schenkels
        /// (z=4) schluckt damit z=5 und z=6 — eine Warte-Zone dort waere unsichtbar, und
        /// "Schlange = Engpass" liesse sich nicht ohne Erklaerung lesen.
        /// </summary>
        public static readonly Vector2Int Station = new Vector2Int(10, 7);

        /// <summary>
        /// Der Denkkiste-Turm direkt neben der Station — die sichtbare Identitaet der Maschine.
        /// </summary>
        public static readonly Vector2Int StationTower = new Vector2Int(9, 7);

        /// <summary>
        /// Warte-Zone der Station, laeuft nach Westen bis an den Knick.
        /// Wer sie zubaut, schiebt die Schlange in den Knick.
        /// </summary>
        public static readonly Vector2Int[] QueueSlots =
        {
            new Vector2Int(8, 7),
            new Vector2Int(7, 7),
            new Vector2Int(6, 7)
        };

        /// <summary>
        /// Die beiden einzigen Zellen, die den unteren mit dem oberen Schenkel verbinden.
        /// Hier stauen sich die Agenten — der Engpass des Grundrisses.
        /// </summary>
        public static readonly Vector2Int[] Passage =
        {
            new Vector2Int(1, 4),
            new Vector2Int(2, 4)
        };

        /// <summary>Wissens-Regal (RAG): nah am Eingang, weit von der Station.</summary>
        public static readonly Vector2Int Shelf = new Vector2Int(1, 2);

        /// <summary>Inbox / Triage-Schreibtisch, direkt an der Tuer.</summary>
        public static readonly Vector2Int Inbox = new Vector2Int(3, 2);

        /// <summary>Postausgang.</summary>
        public static readonly Vector2Int Outbox = new Vector2Int(5, 1);

        /// <summary>Wandzelle fuer Sicherungskasten und Lastsaeule — im Blickfeld der Station.</summary>
        public static readonly Vector2Int FuseBox = new Vector2Int(10, 8);

        public static OfficeCell Get(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Depth)
                return OfficeCell.Outside;

            string row = Rows[Depth - 1 - z];
            if (x >= row.Length)
                return OfficeCell.Outside;

            switch (row[x])
            {
                case '#': return OfficeCell.Wall;
                case 'W': return OfficeCell.WallWindow;
                case 'D': return OfficeCell.WallDoor;
                case '.': return OfficeCell.Floor;
                default: return OfficeCell.Outside;
            }
        }

        /// <summary>Alles, was als Wand-Lauf zaehlt — auch Tuer- und Fensterstuecke.</summary>
        public static bool IsWallLike(int x, int z)
        {
            OfficeCell c = Get(x, z);
            return c == OfficeCell.Wall || c == OfficeCell.WallDoor || c == OfficeCell.WallWindow;
        }

        /// <summary>Begehbar: Boden — und die Tuerzelle, damit unter der Tuer NavMesh entsteht.</summary>
        public static bool IsWalkable(int x, int z)
        {
            OfficeCell c = Get(x, z);
            return c == OfficeCell.Floor || c == OfficeCell.WallDoor;
        }

        /// <summary>
        /// Zellmittelpunkt in Weltkoordinaten, y=0 (Bodenoberkante).
        /// Das Grid ist um den Weltnullpunkt zentriert.
        /// </summary>
        public static Vector3 CellCenter(int x, int z)
        {
            return new Vector3(
                (x - (Width - 1) * 0.5f) * Tile,
                0f,
                (z - (Depth - 1) * 0.5f) * Tile);
        }

        public static Vector3 CellCenter(Vector2Int cell) => CellCenter(cell.x, cell.y);

        /// <summary>Ausdehnung des Gebaeudes inklusive Waende — fuer Kamera-Grenzen und NavMesh-Volumen.</summary>
        public static Bounds BuildingBounds()
        {
            Vector3 min = CellCenter(0, 0) - new Vector3(Tile * 0.5f, 0f, Tile * 0.5f);
            Vector3 max = CellCenter(Width - 1, Depth - 1) + new Vector3(Tile * 0.5f, 0f, Tile * 0.5f);

            Bounds b = new Bounds();
            b.SetMinMax(
                new Vector3(min.x, -FloorThickness, min.z),
                new Vector3(max.x, WallHeight, max.z));
            return b;
        }
    }
}
