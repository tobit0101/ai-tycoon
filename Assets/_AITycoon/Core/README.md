# Core

Grundgerueste, die das gesamte Spiel zusammenhalten und von (fast) jedem Feature gebraucht werden.

## Richtig
- GameManager, Bootstrapper, Szenen-Loader
- Globale Event-Systeme / Messaging (z.B. EventBus.cs)
- Interfaces, die von mehreren Features implementiert werden (z.B. IInteractable.cs)
- Singletons oder Service-Locator Setup

## Falsch
- Feature-spezifische Logik (z.B. Agenten-Verhalten) -> gehoert nach Features/AI_Agents/
- UI-Skripte -> gehoeren nach UI/
- Alles, was nur EIN System betrifft. Wenn du unsicher bist: "Wird das von 2+ Features gebraucht?" Wenn nein -> nicht hier rein.
