"""One-time asset preparation for this new testmod; no existing mod is edited."""
from pathlib import Path
import re

mod = Path(__file__).resolve().parents[1]
workspace = mod.parents[1]
xaml = mod / "Override/ScriptExtenderUI/CrenelatedWallTestSettings.xaml"
text = xaml.read_text(encoding="utf-8")
text = text.replace(' IsEnabled="{Binding System_CanConfirmPresetLoad}"', '')
text = text.replace("System_PresetLoadDescription", "System_SelectedPresetLoadEntry.Description")
text = text.replace("System_DeleteSelectedPresetCommand", "System_DeletePresetCommand")
text = text.replace("System_PresetSaveEntries", "System_PresetSaveTargets")
text = text.replace("System_SelectedPresetSaveEntry", "System_SelectedPresetSaveTarget")
with xaml.open("w", encoding="utf-8", newline="\r\n") as f:
    f.write(text)
source = workspace / "Testmods/ArabianIdleSoundTest/Locales"
destination = mod / "Locales"
destination.mkdir(exist_ok=True)
en = {
    "Crenel.Scope": "New human crenel construction corrects the connected own run. The editor supports manual construction for every player. Automatic AI builds and unrelated existing walls stay unchanged. Corrections persist in maps and saves. All multiplayer peers require this testmod.",
    "Crenel.Rule": "Straight runs alternate, including endpieces. A single crenel is high; a pair alternates. Corners and junctions retain Vanilla geometry. Filled 2x2 blocks and unavoidable equal triples are rejected as complete commands. Activation changes apply to the next session.",
}
de = {
    "Crenel.Scope": "Neue menschliche Zinnenbauten korrigieren den angeschlossenen eigenen Zug. Im Editor gilt dies für manuelle Bauten aller Spieler. Automatische KI-Bauten und andere bestehende Mauern bleiben unverändert. Korrekturen bleiben in Karten und Saves erhalten. Alle Multiplayer-Teilnehmer benötigen diesen Testmod.",
    "Crenel.Rule": "Gerade Reihen wechseln einschließlich ihrer Endstücke. Eine einzelne Zinne ist hoch; ein Paar wechselt. Ecken und Verzweigungen behalten ihre Vanilla-Form. Gefüllte 2x2-Blöcke und unvermeidbare Dreierfolgen werden als ganzer Bauzug abgelehnt. Aktivierungsänderungen gelten ab der nächsten Sitzung.",
}
english_common = dict(line.split("=", 1) for line in (source / "en-US.txt").read_text(encoding="utf-8-sig").splitlines() if line.startswith("Common.") and "=" in line)
for template in sorted(source.glob("*.txt")):
    translated = dict(line.split("=", 1) for line in template.read_text(encoding="utf-8-sig").splitlines() if line.startswith("Common.") and "=" in line)
    common = [key + "=" + translated.get(key, value) for key, value in english_common.items()]
    extra = de if template.name == "de-DE.txt" else en
    body = "\n".join(common + [key + "=" + value for key, value in extra.items()]) + "\n"
    with (destination / template.name).open("w", encoding="utf-8", newline="\r\n") as f:
        f.write(body)
print("Prepared own preset XAML and", len(list(destination.glob("*.txt"))), "locale files; all unknown translations use English.")
