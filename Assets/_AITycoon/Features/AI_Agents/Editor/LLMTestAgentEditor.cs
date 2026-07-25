using UnityEditor;
using UnityEngine;

namespace AITycoon.Features.AI_Agents.Editor
{
    [CustomEditor(typeof(LLMTestAgent))]
    public class LLMTestAgentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Zeichnet die normalen Felder (Prompt und Result)
            DrawDefaultInspector();

            LLMTestAgent agent = (LLMTestAgent)target;

            GUILayout.Space(15);
            
            // Zeichnet den Button
            if (GUILayout.Button("Sende Request an Queue", GUILayout.Height(35)))
            {
                // WebRequests über den Manager funktionieren am besten im PlayMode
                if (Application.isPlaying)
                {
                    agent.SendTestRequest();
                }
                else
                {
                    Debug.LogWarning("Bitte starte das Spiel (Play Mode), um die Queue zu testen.");
                }
            }
        }
    }
}