# RimWorldAccess Universal Patcher

**DEUTSCH**
Dieses Mod-Projekt dient als Brücke zwischen "RimWorldAccess" und anderen Mods aus dem Steam Workshop. 
Da viele Mods eigene Fenster und Buttons hinzufügen, weiß der Screenreader (Tolk) oft nicht, was vorgelesen werden soll.
Diese Mod klinkt sich mit *Harmony* in die grundlegenden Zeichenfunktionen von RimWorld (wie `Widgets.ButtonText` und `Widgets.Label`) ein und sendet den Text an Tolk weiter. 
Das führt dazu, dass Menüs anderer Mods barrierefrei werden, ohne dass jede Mod einzeln gepatcht werden muss.

*Installation:*
1. Kompiliere das `Source` Verzeichnis zu einer DLL (benötigt RimWorld, Harmony und RimWorldAccess Assemblies).
2. Lege die kompilierte DLL in den Ordner `Assemblies/`.
3. Aktiviere die Mod im Spiel (stelle sicher, dass sie NACH Harmony und RimWorld Access geladen wird).

**ENGLISH**
This mod project serves as a bridge between "RimWorldAccess" and other Steam Workshop mods.
Because many mods add their own custom windows and buttons, the screen reader (Tolk) often doesn't know what to read aloud.
This mod uses *Harmony* to hook into RimWorld's basic drawing functions (like `Widgets.ButtonText` and `Widgets.Label`) and forwards the text to Tolk.
This makes menus from other mods accessible without having to patch each mod individually.

*Installation:*
1. Compile the `Source` directory to a DLL (requires RimWorld, Harmony, and RimWorldAccess assemblies).
2. Place the compiled DLL in the `Assemblies/` folder.
3. Enable the mod in the game (ensure it loads AFTER Harmony and RimWorld Access).
