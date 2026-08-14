using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AITycoon.Art.EditorTools
{
    /// <summary>
    /// Erzwingt die Import-Einstellungen fuer FuseBox.fbx bei jedem Re-Import.
    ///
    /// Bewusst ein Postprocessor und keine einmalige Menue-Aktion: das Modell wird aus Blender
    /// neu exportiert, sobald am Sicherungskasten etwas geaendert wird. Wuerden die Einstellungen
    /// nur einmal per Hand im Inspector gesetzt, gingen sie beim naechsten Re-Import wieder
    /// verloren (Unity erzeugt dann ein neues .meta bzw. setzt Importer-Defaults). Als
    /// Postprocessor ueberleben sie jeden Re-Import automatisch, ohne dass jemand daran denken
    /// muss, die Einstellungen manuell nachzuziehen.
    /// </summary>
    public class FuseBoxModelPostprocessor : AssetPostprocessor
    {
        private const string TargetAssetPath = "Assets/_AITycoon/Art/Models/FuseBox.fbx";

        /// <summary>
        /// Materialbibliotheken, aus denen die Bauteil-Materialien per Namen gezogen werden.
        /// Seit der Textur-Stufe (ASSET_VERSION 3) traegt der Kasten eigene Materialien
        /// (M_FuseBox, M_FuseBox_Labels, M_FuseBox_Circuit) aus dem _AITycoon-Ordner;
        /// (Mat)Glass kommt weiterhin aus dem nappin-Pack.
        /// </summary>
        private static readonly string[] MaterialLibraryFolders =
        {
            "Assets/ThirdParty/nappin/OfficeEssentialsPack/Materials",
            "Assets/_AITycoon/Art/Materials",
        };

        // Name -> Material-Asset. Statisch, weil OnAssignMaterialModel pro Import einmal je Renderer
        // aufgerufen wird (hier 34 Mal) und der Ordner sich waehrend eines Imports nicht aendert.
        // Ein Domain-Reload (jede Skript-Kompilierung) verwirft den Cache automatisch.
        private static Dictionary<string, Material> _materialLibrary;

        /// <summary>
        /// Muss hochgezaehlt werden, sobald sich die Logik dieses Postprocessors aendert.
        /// Ohne Version haelt Unity das zwischengespeicherte Import-Ergebnis fuer gueltig, obwohl
        /// der Postprocessor inzwischen ein anderes liefert — das aeussert sich als
        /// "Importer generated inconsistent result".
        /// </summary>
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (assetPath != TargetAssetPath)
                return;

            ModelImporter importer = (ModelImporter)assetImporter;

            // Materialien bleiben im Prefab eingebettet; die Bindung an die vorhandenen
            // nappin-Materialien passiert unten in OnAssignMaterialModel. Bewusst NICHT ueber
            // materialLocation = External ("Use External Materials (Legacy)"): dieser Modus
            // extrahiert Materialien als eigene .mat-Dateien neben das FBX und ist genau der Weg,
            // auf dem Dubletten der nappin-Materialien entstehen.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.addCollider = false;
        }

        /// <summary>
        /// Bindet jedes FBX-Material ueber seinen NAMEN an das gleichnamige Material der
        /// nappin-Bibliothek. Rueckgabe null bedeutet "Unity macht es selbst" — genau das soll fuer
        /// (Mat)FuseSegment_Off passieren, das es im Projekt nicht gibt und das der
        /// OfficeLayoutBuilder zur Laufzeit ohnehin durch M_LoadSegment ersetzt.
        ///
        /// Warum dieser Hook und nicht importer.materialSearch: materialSearch wertet Unity
        /// ausschliesslich im Legacy-Modus materialLocation = External aus. Bei InPrefab (dem
        /// Default) ist die Einstellung wirkungslos, und alle Slots blieben auf den eingebetteten
        /// Ersatzmaterialien haengen — das Modell erscheint dann flach grau.
        /// </summary>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (assetPath != TargetAssetPath)
                return null;

            if (material == null || string.IsNullOrEmpty(material.name))
                return null;

            return GetMaterialLibrary().GetValueOrDefault(material.name);
        }

        private static Dictionary<string, Material> GetMaterialLibrary()
        {
            if (_materialLibrary != null)
                return _materialLibrary;

            _materialLibrary = new Dictionary<string, Material>();

            // Ueber den Ordnerinhalt statt per AssetDatabase-Suchstring: die Materialnamen
            // enthalten Klammern ("(Mat)GradientGrey"), die in FindAssets-Queries als
            // Sonderzeichen interpretiert werden.
            foreach (string guid in AssetDatabase.FindAssets("t:Material", MaterialLibraryFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;

                _materialLibrary[Path.GetFileNameWithoutExtension(path)] = mat;
            }

            return _materialLibrary;
        }
    }
}
