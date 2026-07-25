using UnityEditor;
using UnityEngine;

namespace AITycoon.Core.Editor
{
    public static class ReserializeMainSceneMenu
    {
        private const string MainScenePath =
            "Assets/_AITycoon/Scenes/MainScene.unity";

        [MenuItem("AI Tycoon/Maintenance/Reserialize Main Scene as Text")]
        private static void ReserializeMainScene()
        {
            if (!AssetDatabase.LoadMainAssetAtPath(MainScenePath))
            {
                Debug.LogError(
                    $"[AI Tycoon] Szene nicht gefunden: {MainScenePath}");
                return;
            }

            AssetDatabase.ForceReserializeAssets(
                new[] { MainScenePath },
                ForceReserializeAssetsOptions.ReserializeAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[AI Tycoon] Re-Serialisierung abgeschlossen: {MainScenePath}");
        }
    }
}