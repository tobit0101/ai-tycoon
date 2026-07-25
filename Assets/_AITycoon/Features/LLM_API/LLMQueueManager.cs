using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AITycoon.Core.Interfaces;

namespace AITycoon.Features.LLM_API
{
    // --- 1. Datenstruktur für Unitys JsonUtility ---
    // Die Variablennamen MÜSSEN exakt so heißen wie die Keys im JSON!
    [System.Serializable]
    public class LLMResponseData
    {
        public LLMChoice[] choices;
    }

    [System.Serializable]
    public class LLMChoice
    {
        public LLMMessage message;
    }

    [System.Serializable]
    public class LLMMessage
    {
        public string content;
    }
    // ----------------------------------------------

    public class LLMQueueManager : MonoBehaviour, ILLMService
    {
        [Header("Settings")]
        [SerializeField] private LLMConfig config;

        private struct PendingRequest
        {
            public string prompt;
            public AwaitableCompletionSource<string> completionSource;
        }

        private Queue<PendingRequest> requestQueue = new Queue<PendingRequest>();
        private bool isProcessing = false;

        public async Awaitable<string> EnqueueRequestAsync(string prompt)
        {
            var tcs = new AwaitableCompletionSource<string>();
            
            requestQueue.Enqueue(new PendingRequest 
            { 
                prompt = prompt, 
                completionSource = tcs 
            });

            if (!isProcessing)
            {
                _ = ProcessQueueAsync();
            }

            return await tcs.Awaitable;
        }

        private async Awaitable ProcessQueueAsync()
        {
            isProcessing = true;

            while (requestQueue.Count > 0)
            {
                var request = requestQueue.Dequeue();
                
                string result = await SendRestRequestAsync(request.prompt);
                request.completionSource.SetResult(result);

                await Awaitable.WaitForSecondsAsync(config.delayBetweenRequests);
            }

            isProcessing = false;
        }

        private async Awaitable<string> SendRestRequestAsync(string prompt)
        {
            if (config == null)
            {
                Debug.LogError("LLMConfig fehlt im LLMQueueManager!");
                return "Error: Missing Config";
            }

            string jsonPayload = $"{{\"model\": \"{config.modelName}\", \"messages\": [{{\"role\": \"user\", \"content\": \"{prompt}\"}}]}}";

            using (UnityWebRequest www = new UnityWebRequest(config.apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");

                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[LLM API Error] {www.error}\nResponse: {www.downloadHandler.text}");
                    return $"Error: {www.error}";
                }

                string rawJson = www.downloadHandler.text;

                // --- 2. JSON Auspacken ---
                try 
                {
                    LLMResponseData parsedData = JsonUtility.FromJson<LLMResponseData>(rawJson);
                    
                    if (parsedData != null && parsedData.choices != null && parsedData.choices.Length > 0)
                    {
                        // Wir geben dem Agenten NUR den sauberen Text zurück
                        return parsedData.choices[0].message.content;
                    }
                    
                    return "Fehler: Unerwartetes JSON Format erhalten.";
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LLM JSON Parse Error] {e.Message}");
                    return rawJson; // Fallback: Gib das rohe JSON zurück, falls etwas schiefgeht
                }
            }
        }
    }
}