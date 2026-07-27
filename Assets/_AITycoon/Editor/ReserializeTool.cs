using UnityEditor;
using UnityEngine;

namespace AITycoon.Editor
{
    /// <summary>
    /// Editor tool to force Unity to reserialize all assets in the project.
    /// This fixes "phantom changes" where Unity shows modified files in Git
    /// even though nothing was changed — by rewriting all .prefab, .unity,
    /// and .asset files in a consistent text format.
    ///
    /// Usage: Tools > AI Tycoon > Force Reserialize All Assets
    /// </summary>
    public static class ReserializeTool
    {
        private const string MenuPath = "Tools/AI Tycoon/Force Reserialize All Assets";

        [MenuItem(MenuPath, false, 100)]
        public static void ForceReserializeAllAssets()
        {
            EditorUtility.DisplayProgressBar(
                "Reserializing Assets",
                "Reserializing all assets. This may take a moment...",
                0f
            );

            try
            {
                AssetDatabase.ForceReserializeAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ReserializeTool] Successfully reserialized all assets.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}