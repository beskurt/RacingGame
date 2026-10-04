# Racing Game – Game Development, Second Attempt

Ein kleines Rennspiel im Stil der Formel 1. Die Strecke ist vom **Red Bull Ring** inspiriert.
Gefahren werden 3 Runden gegen einen KI-Gegner.

- **Unity-Version:** 6000.3.9f1
- **Szene:** `Assets/Scenes/MainGame.unity`

## Steuerung

| Taste | Aktion |
|---|---|
| **W** / Pfeil hoch | Gas (bremst, wenn das Auto rückwärts rollt) |
| **S** / Pfeil runter | Bremsen, im Stand rückwärts fahren |
| **A / D** / Pfeil links/rechts | Lenken |
| **Leertaste** | Handbremse |
| **R** | Zurück zum zuletzt durchfahrenen Checkpoint |

## Umgesetzt

### Part 1 – Start your Engines!
- Geschlossene Strecke aus Kenney-Racing-Kit-Teilen mit langen Geraden, schnellen und engen Kurven sowie Boxengasse (keine Funktion). Eine Runde dauert ca. 30–40 s
- Alle Straßenteile haben Collider und liegen auf dem Layer **Track**. Die Strecke ist rundherum mit Leitplanken (Rails) und Barrieren begrenzt.
- Dekoration mit Modellen aus dem Kenney Racing Kit.
- `CarController`: Gas, Bremse, Rückwärtsgang, Höchstgeschwindigkeit, Ausrollen, tempoabhängige Lenkung (im Stand keine Drehung, bei Tempo großer Kurvenradius), Handbremse. Die Räder drehen sich sichtbar (wegen KenneyRacing Kit Asset nicht stark erkennbar), die Vorderräder lenken mit.
- Das Auto „schwebt“ auf vier Federn (SphereCasts), damit es an Kachelnähten nicht hängen bleibt.
- Die Kamera folgt dem Auto (Cinemachine).
- Sound: Motor-Loop mit tempoabhängiger Tonhöhe (0.6–2.0, weich interpoliert), Crash-Sound beim Aufprall, Hintergrundmusik

### Part 2 – Around the Track!
- Checkpoints (Trigger) mit eigenem Index melden sich beim `RaceManager`. Eine Runde zählt nur, wenn alle Checkpoints in der richtigen Reihenfolge durchfahren wurden. Rückwärts über die Ziellinie fahren zählt nicht.
- Die Rundenzahl ist im Inspector einstellbar (Standard: 3).
- **R** setzt das Auto zum letzten Checkpoint zurück: Position, Rotation und Geschwindigkeit.
- Countdown „3 – 2 – 1 – GO!“ mit Ein- und Ausblenden. Während des Countdowns kann man nicht fahren.
- Gesamtzeit, Zeit jeder Runde und beste Runde werden gemessen. Im Ziel wird die Steuerung abgeschaltet und das Auto rollt aus.
- Ergebnis-Bildschirm mit Gesamtzeit, allen Rundenzeiten und bester Runde sowie den Buttons **Neustart** und **Beenden**.
- HUD: Tacho in km/h, „Lap 1 / 3“, Rundenzeit und Gesamtzeit (mm:ss.mmm), beste Runde. Die beste Runde leuchtet bei Verbesserung grün auf. Ziffern mit fester Breite, damit nichts springt. Das HUD blendet mit „GO!“ ein und am Ende wieder aus.

### Part 3 – Rival on the Track!
- KI-Gegner mit **demselben `CarController`** und derselben Physik wie der Spieler.
- Ansatz: Eine Runde des Spielers wird als Ideallinie aufgezeichnet (`RacingLineRecorder`). Die KI fährt diese Linie physikalisch nach (`AIDriver`): Sie peilt einen Punkt voraus an und bremst vor Kurven anhand des aufgezeichneten Tempos.
- Die KI fährt dieselbe Rundenzahl mit derselben Checkpoint-Logik. Bleibt sie hängen, setzt sie zurück oder springt zum letzten Checkpoint.
- Die Stärke ist über **Speed Factor** am `AIDriver` einstellbar.
- Jedes Auto hat einen eigenen Rundenzähler und Fortschritt. Das HUD zeigt die Platzierung („P1 / 2“).
- Das Rennen endet, sobald das erste Auto die letzte Runde beendet. Danach erscheint ein **Sieg**- oder **Niederlage**-Bildschirm mit Zeit, Platz, Neustart und Beenden.

## Bewusst nicht umgesetzt / Abweichungen

- **Das Auto kann nicht umkippen:** Am Rigidbody sind Rotation X und Z gesperrt, damit es stabil fährt. **R** funktioniert trotzdem jederzeit, z. B. wenn man sich festgefahren hat.
- **Tacho:** Das Auto ist im Verhältnis zum Kenney-Kit sehr klein (ca. 0,2 m). Der Tacho rechnet m/s × 3,6 und multipliziert das mit einem Anzeigefaktor (`Speed Display Factor` im `RaceUI`), damit realistischere Werte angezeigt werden.

## Projektstruktur (Skripte)

| Skript | Aufgabe |
|---|---|
| `CarController.cs` | Fahrphysik, Federung, Räder, Eingabe (Spieler oder KI) |
| `CarSound.cs` | Motor- und Crash-Sound |
| `Checkpoint.cs` / `RaceManager.cs` | Checkpoints, Runden, Zeiten, Platzierung, Countdown, Reset |
| `RaceUI.cs` | HUD, Countdown, Ergebnis-Bildschirm |
| `AIDriver.cs` / `RacingLine.cs` / `RacingLineRecorder.cs` | KI-Gegner und Ideallinie |

## Quellen

- 3D-Modelle: [Kenney Racing Kit](https://kenney.nl/assets/racing-kit) (CC0)
- Sounds: [freesound.org](https://freesound.org)
  - Motor: „toyota-altezza-lexus-is200-1g-fe-cold-start-idle-loop“ von *qopsinonstudios* (#768640)
  - Musik: „race-track“ von *foolboymedia* (#237089)
  - Crash: „crash“ von *talktomegoose10* (#762967) sowie „sound-effect-car-crash“
