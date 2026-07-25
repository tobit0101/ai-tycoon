using UnityEngine;
using TMPro; // Wichtig für TextMeshPro
using AITycoon.Core.Interfaces;

namespace AITycoon.Features.Interactables
{
    public class ComputerTerminal : MonoBehaviour
    {
        public enum TerminalState
        {
            Idle,
            Processing,
            Finished
        }

        [Header("References")]
        [Tooltip("Der Text auf dem Bildschirm des Computers")]
        [SerializeField] private TMP_Text screenText;

        [Header("Settings")]
        [TextArea(2, 4)]
        [SerializeField] private string taskPrompt = "Berechne die optimale Route für den nächsten Agenten.";

        // Öffentliche Property, damit andere Skripte den Status abfragen können
        public TerminalState CurrentState { get; private set; } = TerminalState.Idle;

        private ILLMService llmService;

        private void Start()
        {
            // Manager suchen (pragmatischer Ansatz)
            var manager = FindAnyObjectByType<LLM_API.LLMQueueManager>(FindObjectsInactive.Exclude);
            llmService = manager as ILLMService;

            if (screenText != null)
            {
                screenText.text = "System bereit.\nWarte auf Eingabe...";
            }
        }

        // Diese Methode kann z.B. aufgerufen werden, wenn der Spieler "E" drückt 
        // oder ein Agent an den Computer herantritt.
        public async void Interact()
        {
            if (CurrentState == TerminalState.Processing)
            {
                Debug.Log("Computer ist bereits beschäftigt!");
                return;
            }

            if (llmService == null)
            {
                Debug.LogError("Kein LLM Service gefunden!");
                return;
            }

            // --- WAITING STATE START ---
            CurrentState = TerminalState.Processing;
            UpdateScreen("Verbinde mit Server...\nAnfrage wird verarbeitet...");
            
            // Optional: Hier könntest du Sound-Effekte (Tastaturklappern) oder Partikel starten
            // ...
            
            // Hier wartet das Skript asynchron auf die Queue und das LLM.
            // Der Main Thread wird NICHT blockiert.
            string response = await llmService.EnqueueRequestAsync(taskPrompt);
            
            // --- WAITING STATE ENDE ---
            CurrentState = TerminalState.Finished;
            
            // Ergebnis anzeigen
            UpdateScreen(response);
            
            // Optional: Hier könnten wir die Antwort jetzt an einen wartenden Agenten übergeben
            // oder ein Event abfeuern.
        }

        private void UpdateScreen(string message)
        {
            if (screenText != null)
            {
                screenText.text = message;
            }
        }

        // Hilfs-Button für den Inspector, damit du es ohne Spieler-Setup testen kannst
        [ContextMenu("Simulate Interaction (Play Mode)")]
        private void EditorTestInteraction()
        {
            if (Application.isPlaying)
            {
                Interact();
            }
            else
            {
                Debug.LogWarning("Bitte starte das Spiel, um die Interaktion zu testen.");
            }
        }
    }
}