# Features/AI_Agents

Alles rund um die simulierten Mitarbeiter/Agenten (Verhalten, Aussehen, Task-Abarbeitung).

## Richtig
- Agent-Skripte (Bewegung, Zustandsmaschine, Task-Queue-Verarbeitung)
- Agent-Prefabs und agentenspezifische Animationscontroller
- ScriptableObjects fuer Agenten-Rollen (z.B. "Analyst", "Kreativ-Schreiber")

## Falsch
- Grid-/Bauplatzierungslogik -> gehoert nach Features/GridBuilder/
- Rohe API-Calls an Ollama -> gehoeren nach Features/LLM_API/
  (Agent darf das Ergebnis NUTZEN, aber nicht selbst die HTTP-Logik enthalten)
