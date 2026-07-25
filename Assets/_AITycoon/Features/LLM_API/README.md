# Features/LLM_API

Die Bruecke zwischen Unity und lokalen KI-Modellen (z.B. Ollama). Reine Kommunikations- und Parsing-Logik.

## Richtig
- Async/await bzw. UniTask Methoden fuer HTTP-Requests an Ollama
- Request-/Response-Datenklassen (DTOs) fuer JSON (de)serialisierung
- Fehlerbehandlung fuer Timeouts, ueberlastung etc. (reine Logik, KEINE visuelle Reaktion)

## Falsch
- MonoBehaviours, die direkt Agenten-GameObjects bewegen -> das gehoert in AI_Agents als "Presenter"
- UI-Code -> gehoert nach UI/
- Idealerweise: Kein "using UnityEngine" in den Kernklassen, damit die Logik testbar bleibt
