# Features/SystemLoad

Misst die reale System-Speicherlast und zeigt sie als segmentierte HUD-Leiste an.
Das ist die technische Grundlage fuer die "Sicherung" aus dem Konzept
(Docs/Konzept-Vorschlag-Claude.md, §2.3): Belegung, kein Tank.

## Plattform-Quellen

| Plattform | Quelle | API |
|---|---|---|
| macOS | Unified Memory (systemweit, wie Activity Monitor) | `host_statistics64` (Mach) + `sysctlbyname("hw.memsize")` |
| Windows | VRAM aller aktiven GPUs (Dedicated Usage, summiert) | PDH-Counter `\GPU Adapter Memory(*)\Dedicated Usage` |
| Windows (Fallback) | RAM, wenn GPU-Counter fehlen oder 0 liefern (z.B. iGPU) | `GlobalMemoryStatusEx` |

## Bildsprache

"Raum zeigt Klischee, Datenblatt zeigt Wahrheit":
- **Auf der Leiste:** nur Karikatur — Titel "STROMLAST", Segmente, Stimmungs-Text
  ("Laeuft entspannt" / "Gut zu tun" / "Am Anschlag!"). Keine GB, keine Quellen.
- **Im Datenblatt-Tooltip (Hover):** die echten Zahlen (GB, Quelle, Prozent).
  Braucht ein EventSystem in der Szene.
- Fuer Spiellogik/Akte: `SystemLoadBarUI.TechnicalInfo` bzw. `SystemLoadMonitor.Current`.

## Verwendung

Menue: **AI Tycoon → UI → System-Lastleiste erstellen** — legt Canvas (falls noetig),
`[System_Load_Monitor]` und die Leiste oben rechts an. Play druecken.

Oder manuell: `SystemLoadMonitor` auf ein GameObject, `SystemLoadBarUI` auf ein
UI-Panel mit Segment-Container (RectTransform), Status-Label und Tooltip-Panel.

## Bekannte Grenzen (v1)

- **Multi-GPU-Kapazitaet:** Unity liefert nur die VRAM-Groesse der primaeren GPU.
  Die *Nutzung* wird ueber alle GPUs summiert; die Kapazitaet waechst auf die
  hoechste beobachtete Nutzung mit, damit die Leiste nie ueber 100 % laeuft.
- **iGPUs ohne dediziertes VRAM** liefern 0 → automatischer RAM-Fallback.
- Die Windows-Counter existieren ab Windows 10; die Adapter-Ebene ist zuverlaessig,
  die Prozess-Ebene (hier nicht genutzt) hat bekannte Bugs.
- Ghost-Preview (schraffierte "wuerde belegt"-Segmente) ist Konzept, aber noch
  nicht implementiert — braucht die Kopplung an Auftrags-/Stations-Logik.

## Richtig

- Weitere Provider (z.B. Linux /proc/meminfo) als eigene `ISystemLoadProvider`-Klasse
- Spiellogik liest `SystemLoadMonitor.Current` (z.B. Sicherungs-Events)

## Falsch

- UI-Logik in die Provider mischen (Provider liefern nur Zahlen)
- Unity-Profiler-APIs verwenden (die messen nur den eigenen Prozess —
  Ollama/llama.cpp laufen aber als separater Prozess)
