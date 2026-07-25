using UnityEngine;
using AITycoon.Core.Interfaces;
using AITycoon.Features.LLM_API;

namespace AITycoon.Features.AI_Agents
{
    public class LLMTestAgent : MonoBehaviour
    {
        [Header("Input")]
        [TextArea(3, 5)]
        public string prompt = "Sag mir einen kurzen, lustigen Programmierer-Witz.";

        [Header("Output")]
        [TextArea(10, 20)]
        public string result = "";

        public async void SendTestRequest()
        {
            result = "Warte auf Antwort...";
            
            // Holt sich die Referenz auf den Manager
            var llmService = FindAnyObjectByType<LLMQueueManager>(FindObjectsInactive.Exclude) as ILLMService;
            if (llmService == null)
            {
                result = "Fehler: Kein LLMQueueManager (ILLMService) in der Szene gefunden!";
                return;
            }

            result = await llmService.EnqueueRequestAsync(prompt);
        }
    }
}