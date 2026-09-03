# ADR-026 — Welche Sprachmodelle Callora benutzt und wo sie rechnen

**Status:** Proposed
**Datum:** 2026-09-03
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* callora#157 — Sprach-Plugin (TTS/STT) als eigene Vertikale, ausdrücklich nicht Teil von Communication
* ADR-016 — Communication als Medienschicht; die Vertikale weiß, *was* gesagt wird
* ADR-020 — Ein Repo je verkaufbarer Einheit; das Sprach-Plugin ist `tier: application`
* `Pbx:Voice:Command` / `AnnouncementVoiceOptions` — die Naht, an der heute `espeak-ng` hängt

---

## 1. Kontext

Die Telefonanlage kann sprechen und nicht hören. Gesprochen wird über einen konfigurierbaren
Prozess (`Pbx:Voice:Command` plus Argumente, Ausgabe über einen Resampler auf 8 kHz µ-law);
voreingestellt ist `espeak-ng`, und das klingt wie 1995. Erkennung gibt es nicht.

Das Leitszenario braucht beides: Ein umgeleiteter Anruf wird angenommen, gefragt, worum es
geht, und je nach Antwort weitergestellt oder mit einer Nachricht beendet. Ohne Erkennung
gibt es keine Antwort, ohne Synthese keine Frage.

Zwei Randbedingungen, die die Auswahl bestimmen und die vorher nicht ausgesprochen waren:

**Callora läuft später auf eigenen Servern, nicht beim Kunden.** Was hier gewählt wird, muss
der Hersteller für alle Mandanten betreiben — die Frage ist also nicht „läuft es auf einer
starken Maschine", sondern „was kostet ein gleichzeitiges Gespräch".

**Die Einschaltdauer ist winzig.** Erkannt wird die Eingangsfrage, nicht das Gespräch: rund
fünf Sekunden je Anruf. Ansagen entstehen einmal beim Anlegen einer Person und liegen danach
als µ-law-Bytes in der Datenbank. Latenz ist für die Synthese damit gar kein Kriterium und
für die Erkennung ein mildes.

---

## 2. Entscheidung

**Erkennung:** [`nvidia/nemotron-3.5-asr-streaming-0.6b`](https://huggingface.co/nvidia/nemotron-3.5-asr-streaming-0.6b),
600M Parameter, OpenMDW-1.1.

**Synthese:** [`nvidia/magpie_tts_multilingual_357m`](https://huggingface.co/nvidia/magpie_tts_multilingual_357m),
364M Parameter, NVIDIA Open Model License, plus NanoCodec-Decoder.

**Laufzeit für beide:** [NeMo-Speech.cpp](https://github.com/NVIDIA/NeMo-Speech.cpp) mit
GGUF-Gewichten. **Eine** native Abhängigkeit für Erkennung und Synthese, angesprochen wie
`espeak-ng` heute: ein Prozess, Argumente aus der Konfiguration, Audio über Pipes.

**Das Rechengerät ist eine Betriebsentscheidung, keine Codeentscheidung.** In der Entwicklung
läuft beides auf der Grafikkarte, auf dem Server auf der CPU. Derselbe Aufruf, dieselbe
Binärdatei, dieselben Gewichtsdateien — nur ein Schalter unterscheidet sich.

---

## 3. Warum so

### Warum nicht Whisper

Whisper transkribiert einen Puffer, kein Fließband. Für Barge-in (Stufe 4) braucht es
Chunk-Streaming, und Nemotron liefert es nativ mit 80 ms bis 1,12 s. Dazu gemessen auf
CPU-only Hardware: 1 m 46 s Audio in 32 s über `parakeet.cpp`, wofür Whisper small auf
derselben Maschine 7 m 34 s brauchte — Faktor 14 bei vergleichbarer Genauigkeit.

### Warum die CPU auf dem Server reicht

Bei einem Echtzeitfaktor um 0,3 und fünf Sekunden Audio je Anruf kostet die Eingangsfrage
rund anderthalb Kernsekunden. Ein einzelner Kern trägt damit die Größenordnung von zweitausend
Anrufen pro Stunde. Selbst das dauerhafte Mithören eines Vollduplex-Agenten (Stufe 4) bleibt
mit rund drei gleichzeitigen Gesprächen je Kern im Bereich gewöhnlicher Server; eine
GPU-Stufe lohnt erst bei Hunderten gleichzeitiger Gespräche.

Die Quantisierung ist dabei gratis: q8_0 gegen f32 zeigt 0,0000 % Agreement-WER.

### Warum dieselbe Binärdatei und dieselben Gewichte in beiden Umgebungen

Der verlockende Fehler wäre, in der Entwicklung f16 auf der GPU und auf dem Server q8_0 auf
der CPU zu fahren. Dann erzeugen die beiden Umgebungen **verschiedene Transkripte und
verschiedenes Audio**, und ein Erkennungsfehler aus dem Betrieb lässt sich lokal nicht
nachstellen — man debuggt ein anderes Modell als das, das gescheitert ist.

Deshalb: eine Quantisierung, in beiden Umgebungen dieselbe. Die Grafikkarte in der
Entwicklung ist eine Bequemlichkeit beim Iterieren, kein zweiter Pfad.

### Warum nicht die naheliegenden Alternativen für die Synthese

**Piper** (MIT, ONNX, deutsches Modell vorhanden) war die frühere Wahl aus callora#157 und
bleibt fachlich tragfähig: Über eine auf 3,4 kHz bandbegrenzte Leitung hört niemand den
Unterschied zu einem 22-kHz-Neuronalmodell. Magpie gewinnt aus genau einem Grund — es läuft
in derselben Laufzeit wie die Erkennung. Fiele die Erkennungsentscheidung anders aus, wäre
Piper wieder richtig.

**NeuTTS Nano German** ist technisch reizvoll und scheidet an der Beschaffung aus: Das Modell
ist gated und verlangt Kontaktdaten, die Lizenz steht auf „other", und die Ausgaben tragen ein
Wasserzeichen. Ein Gate bricht „Modelle zur Bauzeit holen"; die anderen beiden Punkte sind für
ein verkauftes Plugin Fragen, die niemand beantworten will.

### Was an den Lizenzen zu prüfen bleibt

OpenMDW-1.1 (Erkennung) ist unkritisch: uneingeschränkte, lizenzgebührenfreie Nutzung ohne
Copyleft, einzige Auflage ist die Mitgabe von Lizenztext und Urhebervermerken. Die NVIDIA Open
Model License (Synthese) sagt „ready for commercial use", ist aber **nicht** dasselbe Dokument
und wird vor der ersten Auslieferung gelesen, nicht danach.

---

## 4. Konsequenzen

* Das Sprach-Plugin bekommt eine Naht je Richtung — `ISpeechToText` und `ITextToSpeech` —, und
  die Modellwahl steht dahinter. Ein Wechsel ist Konfiguration, kein Umbau. Das ist auch der
  Weg, auf dem `espeak-ng` verschwindet.
* Das Rechengerät kommt aus der Konfiguration, nicht aus dem Code. Kein `if (dev)`.
* Die Ausgabe der Synthese ist 22,05 kHz PCM und wird auf 8 kHz µ-law gerechnet — derselbe
  Resampler-Schritt, den `AnnouncementVoiceOptions` heute für `espeak-ng` fährt.
* Beide Modelle brauchen ein Beiwerk: die Synthese den NanoCodec-Decoder, beide die
  GGUF-Dateien. Die gehören zur Bauzeit geholt und mit ausgeliefert, nicht zur Laufzeit
  nachgeladen — ein Kunde ohne Internetzugang zur Modellablage hätte sonst eine Telefonanlage,
  die nicht spricht.
* Magpie erzeugt im Standardmodus höchstens 20 s am Stück und verlangt normalisierten Text.
  Beides trifft die heutige Ansage nicht (ein Satz aus einer Vorlage), gilt aber, sobald
  Ansagen frei getippt werden.

---

## 5. Abgrenzung

Diese ADR wählt Modelle und legt fest, wo sie rechnen. Sie sagt **nicht**, wie der Audiopfad
in den Anruf kommt — das ist Communications Medien-Naht (ADR-016) — und sie sagt nichts über
den KI-Dialog, der auf der Erkennung aufsetzt. Beides bekommt eigene Entscheidungen, wenn es
so weit ist.

---

## 6. Offen

**Die eine Zahl, die noch fehlt: Deutsch über 8 kHz Telefonie.** Alle veröffentlichten Werte
stammen von sauber vorgelesener Sprache bei 16 kHz (Deutsch 8,3 % WER bei 1,12 s Chunks,
9,8 % bei 80 ms). Euer Material ist auf ~3,4 kHz bandbegrenzt, und Hochrechnen erfindet die
fehlenden Frequenzen nicht. Wie weit die Werte auseinanderliegen, sagt kein Benchmark — und die
Antwort entscheidet nicht über die Auswahl (jedes ASR hat dasselbe Problem), sondern darüber,
ob die Absichtserkennung überhaupt trägt oder ob eine DTMF-Rückfallebene stehen muss.

Der Prüfstein liegt bereits vor: Das Anrufprotokoll des Dev-Stacks führt echte eingehende
Anrufe auf deutschen Telefonleitungen. Sobald Aufnahme existiert (Stufe 1e), ist das die
Messreihe — und sie kommt **vor** dem ersten Flow-Schritt, der sich auf Erkennung verlässt.

Ebenfalls offen, aber kleiner: der tatsächliche Echtzeitfaktor beim Streaming mit 80-ms-Chunks.
Die gemessenen 0,3 stammen aus Stapelverarbeitung eines langen Clips; Streaming trägt
Overhead je Chunk.
