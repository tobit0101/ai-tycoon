using UnityEngine;

namespace AITycoon.Features.LLM_API
{
    [CreateAssetMenu(fileName = "NewLLMConfig", menuName = "AI Tycoon/LLM Config")]
    public class LLMConfig : ScriptableObject
    {
        [Header("API Settings")]
        public string apiUrl = "https://api.openai.com/v1/chat/completions";
        public string apiKey = "DEIN_API_KEY";
        public string modelName = "gpt-4o-mini";
        
        [Header("Queue Settings")]
        [Tooltip("Wartezeit zwischen zwei Requests in Sekunden")]
        public float delayBetweenRequests = 0.5f;
    }
}