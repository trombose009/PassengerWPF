# PassengerWPF

PassengerWPF ist eine Windows-WPF-Anwendung zur Visualisierung einer Flugzeugkabine
mit Passagieren, Sitzlogik und Catering-Abläufen.
Das Projekt entstand als experimentelles Airline- / Simulator-nahes Visualisierungstool.

![Kabinenansicht](examples/cabin.png)

---

## ✈️ Funktionen

- Darstellung einer Flugzeugkabine mit Positionierung von Passagieren
- Vielflieger-Ewigkeitsliste
- Catering-Animation mit Bestell-Bubbles über den Passagieren für Implementierung von Essensbestellungen
- CSV-basierte Datenquellen für Passagiere und Boarding-Status
- WPF-Oberfläche mit XAML, Animationen und Layout-Steuerung
- Overlay für Streams mit Anzeige von Flugparametern (simconnect-Anbindung)

---
  
## Voraussetzung

- Streamer.bot zum Befüllen der Passagierliste
- getestet mit MSFS 2020 (bezieht sich nur auf simconnect)

---

## 📦 Download

Die kompilierte Anwendung ist unter **Releases** verfügbar:

👉 [https://github.com/trombose009/PassengerWPF/v14.0](https://github.com/trombose009/PassengerWPF/releases/tag/v14.0)

Einfach das ZIP herunterladen, entpacken und die `PassengerWPF.exe` starten.

---

## 🛠 Systemvoraussetzungen

- Windows 10 oder Windows 11
- .NET Runtime (je nach Build)
- Empfohlen: Full-HD-Auflösung oder höher

---

## ▶️ Verwendung

1. Release-ZIP herunterladen
2. ZIP entpacken
3. `PassengerWPF.exe` starten
4. Passagier- und Boardingdaten werden aus CSV-Dateien geladen

---

## 🧩 Technische Details

- Sprache: VB.NET / C#
- Framework: WPF
- UI: XAML
- Datenhaltung: CSV-Dateien
- Fokus: Boarding-System für Streams in Kombination mit streamer.bot

---

## 📄 Lizenz / Hinweis

keine Garantie für nichts und niemanden
