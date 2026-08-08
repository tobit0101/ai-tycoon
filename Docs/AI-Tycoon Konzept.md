# AI Tycoon — Konzeptvorschlag „Wie ich es bauen würde"

> **Status:** Diskussionsgrundlage, kein Beschluss. Basiert auf eurem Design-Thinking-/Blue-Ocean-Dokument,
> den Klarstellungen von Tobias und dem gemeinsamen Symbolik-Brainstorming (Stand 2026-08-09) sowie dem aktuellen Repo-Stand.
> **Autor:** Claude (auf Anfrage: „Wie würdest du es machen?")
> **v3:** Kapazitäts-System fertig auskonvergiert — Agent als Prozess, Denk-Stationen mit Betriebsmodi,
> Sicherungskasten + Belegungsleiste als VRAM-Budget, Warteschlangen als Auslastungsanzeige.
> Weiterhin: zeitgenössisches Setting, Vollständigkeit statt Qualitäts-Sterne, differenzierter Versandraum.

---

## 0. Die Leitidee in einem Satz

> **Ein Two-Point-Hospital-artiges Büro-Tycoon, in dem der eingerichtete Raum der System-Prompt ist —
> die Kampagne ist reine Fiktion und läuft gratis auf lokaler Hardware, der produktive Real-Modus ist
> die Endgame-Belohnung und das Abo-Produkt für Automation-Nerds.**

### Die drei Säulen (alles andere ordnet sich unter)

| # | Säule | Bedeutung |
|---|---|---|
| 1 | **Das Spiel muss ohne den Nutzen funktionieren** | Wenn morgen alle LLMs abgeschaltet würden, muss die Kampagne immer noch ein gutes Tycoon-Spiel sein. Der reale Output ist Kür, nie Pflicht. |
| 2 | **Latenz ist Gameplay** | Inferenz dauert Sekunden bis Minuten, Modell-Laden ebenso — im Tycoon ist Warten diegetisch: Agenten laufen, stehen Schlange, Stationen rüsten um. Wir kaschieren die größte Schwäche lokaler LLMs nicht, wir *inszenieren* sie. |
| 3 | **Der Raum ist der Prompt** | Outfit, Möbel, Regale, Geräte kompilieren zu einem strukturierten System-Prompt. Der Spieler betreibt Prompt-Engineering, ohne es zu merken. |

### Metaphern-Prüfstein (Erkenntnisse aus dem Symbolik-Brainstorming)

Eine Spielmetapher taugt nur, wenn:

1. **Mechanische Ehrlichkeit:** Die Alltagsphysik der Metapher verhält sich wie die Technik dahinter. (VRAM wird *belegt und wieder frei* — nichts verbraucht sich über Zeit.)
2. **Semantische Ehrlichkeit:** Das Objekt muss im Realwelt-Verständnis *das sein*, was es mechanisch tut — falsche Vertrautheit führt die Intuition in die Irre.
3. **Visuelle Differenzierbarkeit:** Auf einen Blick unterscheidbar, aus der Iso-Perspektive lesbar.
4. **Erzeugt Gameplay:** Entscheidungen und Animationen, nicht nur Deko.

Dazu zwei Arbeitsprinzipien:

- **Klischee-Lizenz (Two-Point-Prinzip):** Die *Welt* ist zeitgenössisch, die *Maschinen* dürfen Karikaturen sein. Klischees sind eine geteilte Sprache — der überhitzte Laptop, die Warteschlange am Drucker, „das hätte auch eine E-Mail sein können".
- **Arbeitsteilung Welt/HUD (Genre-Standard, vgl. SimCity/Anno/Frostpunk):** Das Weltobjekt gibt der Ressource ein Zuhause und eine Geschichte, die HUD-Leiste liefert die Präzision. Kein einzelnes Objekt muss beides leisten.

---

## 1. Kernentscheidungen (Übersicht)

| Thema | Entscheidung | Warum |
|---|---|---|
| Geschäftsmodell | **Buy2Play (~15–20 €) + optionales Abo „Cloud-Anschluss" + BYOK gratis** | F2P ist auf Steam für Indies ein Discovery- und Vertrauens-Nachteil. Das Tycoon-Publikum kauft Spiele. |
| Agenten-Begriff | **Agent = Prozess mit Properties** — trägt Auftrag, Identität, Ergebnis; holt, bringt, startet, wartet. Das LLM ist ein *Service*, den der Agent aufsucht — §2.1 | Entspricht der echten Architektur (Queue + Service) und entkoppelt Belegschaftsgröße vom VRAM: beliebig viele Agenten, begrenzt nur durchs Gehalt. |
| Compute-Ressource | **Sicherungskasten + Belegungsleiste (Lastlogik)** — §2.3 | Die Sicherung ist das einzige Alltagsobjekt mit exakt richtigem Verhalten: Begrenzung *gleichzeitiger Last*, nicht Verbrauch. Sogar der Fehlerfall stimmt: VRAM-Überlauf crasht alles — wie eine geflogene Sicherung. |
| Modellgrößen-Wechsel | **Betriebsmodi derselben Denk-Station** (Rollläden runter = Großauftrag) — §2.2 | „3 kleine oder 1 großes" darf keine Bau-Falle sein: Real ist Modellwechsel eine Sache von Sekunden. Gebaut wird nur Gesamtkapazität — die ändert sich real auch nicht minütlich. |
| Erfolgs-Bewertung | **Vollständigkeits-Check: Auftrag = Komponenten-Checkliste, LLM-Text wird nie bewertet** — §3 | Kein LLM-als-Richter (keine Zusatzlast, kein Stocken), deterministisch testbar. Lustiger Output kleiner Modelle ist Feature, nicht Mangel. |
| Output-Symbolik | **Tor = Story-Moment; Kanäle = Versandraum mit je einem Möbel pro Kanal** — §4.3, §5 | Ein einzelnes Sammel-Objekt wäre visuell nicht differenzierbar. Tor (einmalig) und Kanäle (laufend, pro Ziel) sind zwei verschiedene Absichten. |
| Art Direction | **Zeitgenössisch: Startup-Satire; Retro-Look als späteres Kosmetik-DLC** | Gegenwart macht die Metaphern wörtlich und trifft die Tier-2-Zielgruppe. Futuristisches (Roboter etc.) bräche die Büro-Bildsprache. |
| Cloud-Agenten | **Remote-Berater im Videocall** — §5 | Braucht sichtbar keinen Platz und keine Sicherungs-Last („er wohnt nicht bei dir"), loggt sich zum Feierabend aus. Die Kostenbremse wird zur Pointe. |
| Realer Output v1 | **Nur Markdown-Dateien via Aktenschrank (Schublade = Zielordner)** | Jede Integration ist ein Mini-Produkt mit Wartungspflicht. Mail kommt später mit Kurier-Mechanik (§5). |
| Cloud-Kostendeckel | **Hartes Tagesbudget, diegetisch als „Arbeitstag/Feierabend"** | Animationen und Warteschleifen deckeln nur die Rate, nicht das Volumen. AFK-Grinden über Nacht würde Cloud-Kosten sprengen. Idle-Progression: nur lokal. |
| Engine | **Unity** | Durch Faktenlage entschieden: Repo, Assets, NavMesh, funktionierender LLM-Call. |

---

## 2. Der Kern-Loop der Kampagne

### 2.1 Der Agent als Prozess

Ein Agent ist ein **Prozess mit Properties**: Er trägt seinen Auftrag, seine Identität (Outfit = Rolle)
und sein Zwischenergebnis mit sich. Das meiste, was er tut, ist gar keine KI — er holt, bringt,
startet, wartet. Die sichtbare Pipeline jedes Auftrags:

```
Auftrag aus der Inbox holen
   → Wissen aus dem Regal ziehen (RAG)
      → an der Denk-Station anstehen (Queue)
         → denken lassen (Inferenz — Lüfter laufen, Agent wartet)
            → Ergebnis abholen
               → ausliefern (Postausgang / Rohrpost zum nächsten Agenten)
```

Jeder Schritt dieser Pipeline ist echte LLM-Realität, als Laufweg erzählt. Aus Wegen und Schlangen
entsteht von selbst das Factorio-Optimierungsspiel: Regal näher an die Station, zweite Station bauen,
Stationsmix ans Auftragsprofil anpassen.

**Wichtig:** Die Belegschaftsgröße hängt NICHT am VRAM. Agenten kosten Gehalt — man kann viele
beschäftigen und viele Aufträge gleichzeitig „in der Schwebe" halten. Nur das *Denken* ist knapp;
dort bilden sich die Schlangen.

Die interessanten Entscheidungen:

1. **Triage unter Druck:** Mehr Aufträge als Denk-Kapazität. Annehmen, ablehnen, liegen lassen?
2. **Stations-Ökonomie:** Wo klemmt es? Zweite Denkkiste oder die Große ausbauen?
3. **Batching:** Großaufträge einzeln durchtröpfeln lassen kostet Umschaltzeit — wer sie sammelt und am Stück abarbeitet, maximiert Durchsatz. (Der Spieler erfindet reale GPU-Scheduling-Praxis von selbst.)
4. **Komponenten-Logistik:** Welche Regale, Geräte, Rollen halte ich vor? (→ §3)

### 2.2 Denk-Stationen mit Betriebsmodi

Karikatur-Maschinen in Räumen (Klischee-Lizenz: größer + leuchtender + lauter = denkt besser):

- **Denkkiste (klein):** eine Maschine, eine Schlange, kleine Aufträge — einer nach dem anderen.
- **Denkzentrale (groß):** ein Raum mit mehreren Arbeitsbuchten und **Betriebsmodi**:
  - *Normalbetrieb:* alle Buchten offen, mehrere Schlangen, Klein-Aufträge laufen parallel.
  - *Großauftrag:* laufende Klein-Jobs laufen aus, dann Rollläden runter, Schild springt um —
    **„GROSSAUFTRAG LÄUFT — BITTE NICHT STÖREN"**, rote Lampe. Der ganze Raum denkt an einer Sache.
  - Die **Umschaltzeit ist wörtlich die echte Modell-Ladezeit** — als Umrüst-Moment inszeniert statt versteckt.

**Es gibt keine Bau-Falle „3 kleine oder 1 großes":** Gebaut wird nur *Gesamtkapazität* (mehr Buchten,
zweite Station) — das entspricht dem realen VRAM, das sich auch nicht minütlich ändert. Die *Aufteilung*
ist Betriebszustand und fließt mit der Auftragslage. Man baut nie falsch; man wartet höchstens auf eine
Umschaltung.

**Warteschlangen sind die Auslastungsanzeige.** Agenten mit Aktenmappe und Kaffeebecher vor der Station
(das Drucker-Klischee jedes Büros) zeigen Engpässe physischer und intuitiver als jede Zahl. Bei voller
Kapazität scheitert nichts — es wartet. Modell-Boot nach Umschaltung: „Update 3 von 7 — nicht ausschalten."

### 2.3 Die Sicherung: das VRAM-Budget

Das globale Limit „wie viel darf gleichzeitig gedacht werden" bekommt die Arbeitsteilung Welt/HUD:

- **Weltobjekt: der Hauptsicherungskasten.** Beim Spielstart wird er installiert — eine einzige,
  karikaturhaft übertriebene Hauptsicherung mit einer großen **Lastsäule** daneben (VU-Meter-Optik:
  gestapelte Segmente, grün → gelb → rot). Jeder laufende Denkvorgang lässt Segmente aufleuchten;
  fertig = Segmente erlöschen. Aus der Iso-Perspektive lesbar.
- **HUD: segmentierte Belegungsleiste — Belegung, kein Tank.** Segmente werden belegt und wieder frei;
  nichts leert sich über Zeit, nichts wird nachgefüllt (sonst wäre es doch wieder eine verbrauchbare Ressource).
  **Ghost-Preview:** Beim Anvisieren eines Auftrags oder Baus schraffiert die Leiste die Segmente,
  die belegt *würden* — „wenn ich das starte, bin ich am Limit" steht wörtlich da, bevor etwas startet.
- **Sicherung fliegt** nur als seltenes Comedy-/Story-Event (Büro zwei Sekunden dunkel, alle Maschinen
  booten neu) — mechanisch ehrlich (realer VRAM-Überlauf crasht auch alles), aber nie Bestrafung im
  Normalbetrieb, denn der Normalbetrieb kennt nur Warteschlangen.
- **Kampagne:** Ausbau per Elektriker-Event („kommt zwischen 8 und 17 Uhr").
  **Real-Modus:** Die Sicherung = gemessenes (V)RAM. Der In-Game-Elektriker kann sie *nicht* aufrüsten —
  „da müssten Sie schon echten Speicher kaufen." Tooltip zeigt die realen GB.
  **Raum zeigt Klischee, Datenblatt zeigt Wahrheit.**

### Knappheiten (Kampagne, alle fiktiv)

| Ressource | Symbol | Druck erzeugt durch |
|---|---|---|
| Denklast (VRAM) | Sicherungskasten + Lastsäule + HUD-Leiste | Summe gleichzeitiger Inferenzen gedeckelt |
| Denk-Durchsatz | Denk-Stationen + Warteschlangen | Schlangen wachsen, Deadlines drohen |
| Maximale Denkgröße | Stations-Tier + Betriebsmodus | Großaufträge brauchen die Große; Umschaltzeit |
| Wissen | Regale/Archiv (RAG) | Mehr Themen = mehr Regale = mehr Raum = mehr Miete |
| Personal | Agenten mit Traits, Laune, Gehalt | Gehälter, Kaffee-Ökonomie, Laufwege |
| Zeit | Aufträge mit Deadlines | Wartende Aufträge blockieren nichts, aber die Uhr tickt |
| Ruf | Kunden-Tiers | Schaltet bessere/skurrilere Klienten und Module frei |

---

## 3. Auftrags-Logik: Vollständigkeit statt Qualität

Das Kern-Designgesetz: **Der LLM-Text wird nie geparst, nie bewertet, nie abgewertet.**
Kein Qualitäts-Score, keine Sterne, kein LLM-als-Richter (Zusatzlast, würde das Spielgeschehen
ins Stocken bringen). Stattdessen:

### Auftrag = Komponenten-Checkliste

```
JobDef "Vereinssatzung zusammenfassen"
  Komponenten:
    ☐ Rolle:        Texter (Hoodie o. Journalisten-Weste)
    ☐ Wissen:       Regal-Tag "Recht/Vereinswesen" (Item „Satzungs-Ordner" liegt bei)
    ☐ Denken:       kleine Station reicht
  Optional (Bonus-Honorar):
    ☐ Gerät:        Layout-Monitor (schönere Formatierung)
  Honorar: 80 ₪ (+20 ₪ Bonus) · Deadline: 2 Spieltage
```

- **Alle Pflicht-Komponenten verfügbar → Auftrag läuft und gilt bei Lieferung als erfüllt.** Punkt.
- **Fehlt eine Komponente → der Auftrag *wartet* sichtbar:** Mappe im „Wartet auf…"-Fach, Ampel am
  Schreibtisch. Der Spieler beschafft, baut, schaltet frei — oder lässt einen anderen Agenten die
  Vorleistung produzieren (Rohrpost!).
- **Optionale Komponenten** differenzieren die Belohnung — immer noch Checklisten-Logik, keine Textbewertung.
- **Schwierigkeit = Länge und Seltenheit der Checkliste** (inkl. „braucht die große Station"), nicht Textqualität.

### Warum das besser ist als jede Qualitäts-Bewertung

1. **Deterministisch und testbar:** Quest-Logik hängt nur an Verfügbarkeits-Zuständen.
2. **Keine Zusatz-Inferenz:** Kein Judge-Modell, das Kapazität frisst.
3. **Komik wird nie bestraft:** Was ein kleines Modell produziert, ist oft unfreiwillig lustig — das ist
   *Content*. Klienten reagieren im Flavor variabel, das Honorar hängt an der Checkliste.
4. **Der Optimierungsdruck sitzt an der richtigen Stelle:** Logistik und Durchsatz, nicht „schreibe besser".

### Der PromptAssembler bleibt trotzdem ehrlich

Der System-Prompt wird aus dem *tatsächlichen* Setup kompiliert (Outfit + Regal-Snippets + Auftragstext +
Agenten-Zustand). Optionale Lücken und Eigenheiten fließen echt ein: Ein übermüdeter Agent bekommt einen
„schreibe fahrig"-Zusatz. Fiktion und Mechanik bleiben synchron, ohne dass je Text bewertet wird.

### Beispiel-Quest, einmal durchgespielt

1. Auftrag trifft ein: *„Hallo! Frau Vogel vom Kleingartenverein hier. Ich bräuchte eine verständliche
   Zusammenfassung unserer neuen Vereinssatzung. Dokument hängt an."* (+ Item „Satzungs-Ordner")
2. Checkliste: Rolle ✓ (Greta trägt den Hoodie), Wissen ☐, Denken ✓. Spieler stellt den Satzungs-Ordner
   ins Regal → Wissen ✓.
3. Greta holt den Ordner (Laufweg), stellt sich an der Denkkiste an — vor ihr wartet noch Carl mit
   Kaffeebecher. Die Lastsäule zeigt: gut gefüllt.
4. Greta ist dran: Lüfter drehen hoch, Segment leuchtet auf. Real: Inferenz mit kompiliertem Prompt
   inkl. Satzungstext.
5. Lieferung: Das Dokument (echter LLM-Text) als aufklappbare Mappe. Frau Vogel antwortet erfreut und
   leicht verwirrt über die Formulierungen — Flavor, kein Malus.
6. Honorar, XP, der nächste Auftrag liegt schon in der Inbox — der braucht die große Station, und die
   fährt gerade einen Großauftrag…

---

## 4. Progression: Kampagne als verstecktes Tutorial

### Rahmen (zeitgenössisch)

Spieler übernimmt ein **pleitegegangenes KI-Startup**: verlassenes Hype-Büro, Kickertisch, vertrocknete
Monstera, Motivationsposter („DISRUPT!"), im Keller eine museumsreife Denkkiste. Ton: liebevolle
Startup-Satire in Two-Point-Tradition. Klienten sind Karikaturen der Gegenwart (Dropshipper,
Vereinsvorstand, Influencerin, überforderter Handwerksbetrieb).

*Notiert für später: „Retro-Office"-Kosmetik-DLC — gleiche Mechanik, 50er-Elektronengehirn-Skin.*

### Akt-Struktur

| Akt | Freischaltung | Lehrt (verdeckt) |
|---|---|---|
| 1 — Die Übernahme | 1 Agent, 1 Denkkiste, Sicherungskasten hängt ab Tag 1, Outfits, einfache Aufträge | Rolle = Prompt; Triage; Agent-Pipeline (holen → anstehen → liefern) |
| 2 — Das Archiv | Regale, Archivraum, Wissens-Ordner | RAG: fehlende Quelle = wartender Auftrag |
| 3 — Wachstum | Mehr Agenten, zweite Denkkiste, Elektriker vergrößert die Sicherung | Parallelität vs. Denklast; Schlangen lesen |
| 4 — Die Große | Denkzentrale mit Betriebsmodi, erste Großaufträge | Modellgrößen; Umschaltzeit; Batching entdecken |
| 5 — Die Rohrpost | Multi-Agent-Ketten: Output von A ist Pflicht-Komponente für B | Pipelines / Workflow-Komposition (bewusst physisch statt Kabel — Kabelziehen ist das n8n-Gefühl, das wir eliminieren) |
| 6 — Endgame | **Das Tor zum Real-Modus** (§4.3) | Automatisierung, Daueraufträge, Durchsatz-Optimierung |

### 4.3 Das Tor zum Real-Modus

Zwei getrennte Absichten, zwei getrennte Lösungen:

- **Das Tor (einmalig, Story-Moment):** Am Ende der Kampagne wird der Spieler selbst zum Klienten —
  **„Das erste echte Mandat"**. Ein besonderer Vertrag schaltet eine neue Etage frei. Der Modus-Wechsel
  ist ein bewusster, gefeierter Moment — kein Schalter im Menü.
- **Die Kanäle (laufend, differenzierbar):** Die neue Etage enthält den **Versandraum** (§5) —
  pro Ausgabe-Kanal ein eigenes, unterscheidbares Möbel. Kein einzelnes Sammel-Objekt.

---

## 5. Der Real-Modus (das Tier-2-Produkt)

**Zielgruppe ehrlich benannt:** Automation-Nerds (Factorio + Homelab). Nicht „alle Non-Devs" — das wäre
für v1 eine Überdehnung.

### Eingänge (gleiche Metaphern wie die Kampagne)

- **Aufträge verfasst der Spieler selbst** (diegetisches Textfeld = der Prompt).
- **Regale zeigen auf echte lokale Ordner** (Markdown/PDF → einfaches RAG). Regal-Beschriftung = Ordnername.
- **Die Sicherung zeigt das echte VRAM** — Stationsausbau ist entsprechend gedeckelt, Tooltip mit realen GB.

### Der Versandraum: ein Möbel pro Ausgabe-Kanal

| Kanal | Objekt | Mechanik |
|---|---|---|
| **Markdown → Ordner (v1)** | **Aktenschrank** — jede Schublade ist ein realer Zielordner, beschriftet und farbmarkiert | Agent sortiert das fertige Dokument ein = Datei wird geschrieben. Mehr Ziele = mehr Schubladen. |
| E-Mail (v2+) | **Postausgang + Kurier**, der zu festen Zeiten abholt | Nichts verlässt das Haus sofort: Sendungen liegen sichtbar im Ausgang und können vor der Abholung herausgenommen werden — **das Undo-Fenster ist Spielmechanik** und löst das Vertrauensproblem. |
| Bulk/Webhooks/Exporte (v2+) | **Laderampe + Lieferwagen** mit Aufdrucken und Farben pro Route | Batch-Versand; Abfahrtsplan = Sende-Frequenz. |
| Externe Tools (v2+) | Geräte via **MCP** (Model Context Protocol) | Das Spiel wird diegetischer MCP-Client — Integrationen müssen wir nicht selbst pflegen. |

### Qualitätskontrolle als Gameplay: der Prüfer-Agent

Im Real-Modus will man verlässliche Ergebnisse — aber wir bewerten weiterhin nie heimlich. Stattdessen
kann der Spieler eine **Lektorin/einen Korrektor** engagieren: ein sichtbarer Agent, der Lieferungen
gegenliest, Prüfvermerke in die Akte heftet und Nacharbeit anstößt. LLM-as-judge als **diegetisches,
bezahltes, lastverursachendes Gameplay-Element**: Die Prüfung belegt sichtbar Sicherungs-Segmente
(lokal) oder frisst Premium-Budget (Cloud). Wer Kontrolle will, sieht ihre Kosten.

### Observability statt Comedy

Jedes Mandat erzeugt eine **„Akte"** — vollständiger, lesbarer Trace (Prompt, Quellen, Modell, Antwort,
Dauer, ggf. Prüfvermerk) als In-Game-Dokument. Humor im Ton, Vollständigkeit in der Sache.
Comedy-Fehlerbilder bleiben in der Kampagne.

### Agenten-Typen im Real-Modus

| | Lokale Verarbeitung | Remote-Berater (Cloud) |
|---|---|---|
| Präsenz | Agenten + Denk-Stationen im Büro | **im Videocall auf einem Monitor** — braucht keinen Platz und keine Sicherungs-Last („er wohnt nicht bei dir") |
| Modelle | gebündelte q4-Modelle, Größe nach erkanntem VRAM | Cloud-Modelle via Abo oder BYOK |
| Kosten für uns | 0 | variabel → gedeckelt |
| Arbeitszeit | unbegrenzt, arbeitet auch idle/offline weiter | **nur bei anwesendem Spieler, harter „Arbeitstag"** (Tagesbudget) — loggt sich zum Feierabend sichtbar aus dem Call aus |
| Ladezeit-Gag | „Update 3 von 7…" | „Können alle mich hören?" |

Die Feierabend-Mechanik ist die diegetische Verpackung des Kosten-Deckels — sie *ist* die
Monetarisierungs-Sicherung, thematisch stimmig statt als Token-Zähler spürbar. Und der Kontrast erzählt
nebenbei den echten Local-vs-Cloud-Tradeoff: **Platz und Last gegen Miete und Feierabend.**

---

## 6. Geschäftsmodell & Unit Economics

### Struktur

1. **Buy2Play-Grundspiel** (~15–20 €, Steam): Kampagne komplett + Real-Modus mit lokaler Verarbeitung. Haupterlösquelle.
2. **Abo „Cloud-Anschluss"** (~9,99 €/Monat): Remote-Berater ohne eigenen API-Key. Convenience-Produkt.
3. **BYOK gratis:** Eigener API-Key = eigene Kosten, keine Grenzen. Vertrauensaufbau bei genau der Zielgruppe, kein Margenrisiko. Das Abo verkauft *Bequemlichkeit*, nicht Exklusivität.
4. *(Später: Kosmetik-DLCs wie das Retro-Office — reine Skins, nie Mechanik.)*

### Beispielrechnung (Pacing wird hieraus abgeleitet, nicht umgekehrt!)

- Ziel-COGS ≤ 30 % vom Abo → ≤ 3 €/Monat/Abonnent.
- Mittlere Cloud-Klasse (Haiku/4o-mini-Niveau), ø ~1.500 Token rein + ~800 raus je Mandat ≈ 0,5–1 ct.
- → **~300–600 Mandate/Monat ≈ 12–20 Mandate pro Tag.** Diegetisch: „Der Berater schafft 12 Mandate, dann ist Feierabend."
- Frontier-Klasse je Mandat ~10–20× teurer → eigener Berater-Typ „Gutachter": **2–3 Gutachten/Tag**. Qualität statt Quantität — wörtlich.
- **Die Lektorin verdoppelt die Inferenz pro Mandat** — bei Cloud-Prüfung entsprechend im Tagesbudget eingepreist (sichtbar: zwei Instanzen arbeiten).

Diese Zahlen sind Platzhalter; der Mechanismus ist der Punkt: **Zyklusdauer, Slots und Tagesbudget werden
rückwärts aus dem COGS-Ziel gerechnet und bei Preisänderungen der Anbieter nachjustiert** (server-seitige
Config, nicht hart im Client).

---

## 7. Technik & Architektur

### Systeme (Mapping auf den bestehenden Code)

| System | Aufgabe | Bestehender Code |
|---|---|---|
| `ILLMService` + Provider-Layer | Backends: Ollama (jetzt) → embedded llama.cpp (später) → Cloud-APIs | `ILLMService`, `LLMQueueManager` — **ist bereits wörtlich das Denk-Stations-Modell: Service mit Warteschlange** |
| **PromptAssembler** | Kompiliert Setup (Outfit + Regal-Snippets + Auftragstext + Agenten-Zustand) → strukturierter Prompt. **Das Herzstück des Spiels.** | neu |
| **Komponenten-System** | `JobDef` = Checkliste; Verfügbarkeits-Logik; Wartezustände; optionale Boni | neu |
| **Kapazitäts-System** | Sicherungs-Budget (Segmente), Stations-Betriebsmodi, Umschalt-/Bootzeiten, Warteschlangen; Real-Modus: VRAM-Erkennung → Segment-Zahl | neu, Queue-Logik vorhanden |
| Content als Daten | `OutfitDef`, `FurnitureDef`, `JobDef`, `ClientDef`, `StationDef` als ScriptableObjects mit Prompt-Fragmenten + Komponenten-Tags | Muster wie `LLMConfig` |
| GridBuilder | Bau-/Placement-System (Raster, Snapping, Ghost) — Ghost-Preview zahlt direkt auf die Belegungsleiste ein | **nur README — größte Baustelle** |
| Agenten-FSM | Wander → Auftrag holen → Komponenten einsammeln → anstehen → warten → liefern → Pause | `NPCWander`, `NPCGoToTerminal` als Basis |
| Denk-Station | Maschine + Queue-Punkte + Betriebsmodi + Lastsäulen-Anbindung | `ComputerTerminal` als Keimzelle |

### Modellstrategie

- Auslieferung: kleines Instruct-Modell in **q4** (nicht fp16 — die aktuelle `Ollama_Config` fährt fp16, ~4× mehr VRAM als nötig), Auto-Download beim ersten Start, Quant-Wahl nach erkanntem (V)RAM.
- Min-Spec: 8 GB gemeinsames Budget (Spiel + Modell); Mac Unified Memory und dediziertes VRAM getrennt behandeln.
- Kampagnen-Prompts auf kurze Formate auslegen (Notizen, Mails, Listen) — kleine Modelle glänzen bei kurz. Format-Design ist Qualitäts-Management.
- **Kampagnen-Fiktion:** Stationsgrößen sind fiktiv — real läuft immer dasselbe kleine Modell. Echte Modellvielfalt gibt es erst im Real-Modus. Progression bleibt dadurch auf jeder Hardware identisch.

### Sofort fixen (unabhängig vom Konzept)

- `LLMQueueManager.cs:87`: JSON-Payload wird per String-Interpolation gebaut — Anführungszeichen/Umlaute im Prompt zerschießen den Request. Auf serialisierte Objekte (`JsonUtility.ToJson`) umstellen. Mit Regal-Inhalten im Prompt wird das sonst sofort zum Dauer-Bug.

---

## 8. Roadmap

| Meilenstein | Inhalt | Erfolgskriterium |
|---|---|---|
| **M1 — Spaß-Slice** (der nächste Schritt) | GridBuilder v1 (1 Raum, Möbel platzieren), PromptAssembler v1, Komponenten-System v1, 1 Denkkiste mit Warteschlange, Sicherungskasten + HUD-Leiste, 3 Outfits, 2 Wissens-Möbel, 5-Quest-Kette, 2 Comedy-Fehlerbilder. Kein Real-Modus. | Playtest mit 5–10 Leuten. **Kill-/Pivot-Kriterien vorab festlegen**, z. B.: ≥ 7/10 verstehen „Outfit = Rolle" und „Schlange = Engpass" ohne Erklärung; ≥ 5/10 starten freiwillig Session 2; mindestens einmal lautes Lachen pro Test. |
| **M2 — Kampagnen-Systeme** | Wirtschaft (Geld/Ruf), Hiring, Sicherungs-Ausbau, Denkzentrale mit Betriebsmodi, Akt 1–3, Inbox-Triage, Wartezustände | Spieler beschäftigen sich ≥ 45 min am Stück; Batching wird von mindestens einem Tester selbst entdeckt |
| **M3 — Real-Modus Alpha** | Versandraum mit Aktenschrank (Markdown-Ordner), echte Regal-Ordner, Akten, BYOK, Lektorin | 5 Tier-2-Tester nutzen es ≥ 2× pro Woche *freiwillig* für echte Aufgaben |
| **M4 — Produktisierung** | Embedded Runtime, Cloud-Abo mit Tagesbudget, Steam-Demo (Next Fest), AI-Disclosure | Wishlist-/Demo-Kennzahlen |

Der bestehende Tech-Spike (NPC läuft zum Terminal, lokales LLM antwortet auf dem Bildschirm) hat seine
Aufgabe erfüllt: **Machbarkeit ✓.** M1 beantwortet die einzig noch offene Existenzfrage: **Spaß?**

---

## 9. Risiken & Gegenmittel

| Risiko | Gegenmittel |
|---|---|
| Kleine Modelle liefern blande Texte → Woche-2-Churn | Kurze Output-Formate; Komik & Charaktere tragen die Texte (und werden nie abgewertet); Tycoon-Systeme (nicht der KI-Gimmick) tragen die Retention |
| Vollständigkeits-Logik zu flach → Optimierungsdruck fehlt | Logistik-Tiefe: Laufwege, Schlangen, Batching, seltene Komponenten, Vorleistungs-Ketten (Rohrpost), optionale Boni |
| Warteschlangen frustrieren statt zu motivieren | Schlangen müssen *lösbar* sein (bauen, umrüsten, Triage) und komisch aussehen — Frust entsteht nur, wenn der Spieler keinen Hebel hat |
| Whale sprengt Cloud-Kosten | Hartes Tagesbudget (Feierabend), server-seitig konfigurierbar; Idle-Arbeit nur lokal; Lektorin eingepreist |
| LLM-Nichtdeterminismus macht Quests untestbar | Eiserne Regel: Spiel-Logik hängt nur an Verfügbarkeits-Zuständen (Checkliste), LLM nur Flavor |
| Scope-Explosion durch Integrationen | v1 nur Aktenschrank/Markdown; alles Weitere über MCP statt Eigenbau |
| Steam AI-Content-Regeln | Früh und offensiv deklarieren; „Local-First & Privacy" als Marketing-Stärke drehen |
| Startup-Satire altert schlecht | Humor an zeitlosen Büro-Archetypen aufhängen (Meetings, Drucker, Kuriere), nicht an tagesaktuellen KI-Memes |
| Metaphern tragen nicht | Genau dafür ist M1 da — mit Kill-Kriterien, nicht mit Hoffnung. Prüfstein aus §0 auf jede neue Metapher anwenden. |

---

## 10. Offene Fragen fürs Gespräch

1. **B2P + Abo + BYOK** — bestätigt ihr das Geschäftsmodell? (Größte Tragweite für Marketing und Steam-Auftritt.)
2. Setting bestätigen: pleitegegangenes KI-Startup (Startup-Satire) — oder neutralere Agentur?
3. Designgesetz bestätigen: **LLM-Text ist nie spielentscheidend** — Spiel-Logik hängt ausschließlich an der Komponenten-Checkliste. (Folgenreichste Regel im Konzept.)
4. Sicherungs-Feintuning: Wie granular sind die Segmente (grobe Blöcke à „ein kleines Modell" vs. feine GB-Skala)? Grob = lesbarer, fein = ehrlicher.
5. Betriebsmodi der Denkzentrale: Umschaltung vollautomatisch nach Warteschlange, oder darf der Spieler einen Modus „pinnen"? (Automatisch = weniger Mikromanagement; Pinnen = mehr Kontrolle für Optimierer. Vorschlag: automatisch mit optionalem Pin.)
6. Ist Akt 5 (Multi-Agent-Rohrpost) Launch-Scope oder Post-Launch? (Bauchgefühl: Launch — Vorleistungs-Ketten sind der Factorio-Moment des Spiels.)
7. Wer von euch beiden übernimmt Content-Design (JobDefs, Klienten, Auftragstexte)? Größte laufende Arbeit — mit LLM-Unterstützung gut skalierbar.
8. Playtest-Pool für M1: Wen habt ihr griffbereit? (5–10 Personen, Mischung aus Gamern und n8n-Nutzern.)
