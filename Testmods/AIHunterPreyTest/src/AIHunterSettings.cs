using APIShared.ModSettings;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace AIHunterPreyTest
{
    public sealed class AIHunterSettings : PresetLobbyModSettingsViewModel
    {
        private bool enabled = true, aiv = true, automatic = true, retire = true;
        public AIHunterSettings() { ResetToDefaultCommand = new RelayCommand(Reset); }
        [SyncHostOnly] public bool EnableMod { get => enabled; set => Set(ref enabled, value, nameof(EnableMod)); }
        [SyncHostOnly] public bool ControlAivHunters { get => aiv; set => Set(ref aiv, value, nameof(ControlAivHunters)); }
        [SyncHostOnly] public bool AutoBuildHunters { get => automatic; set => Set(ref automatic, value, nameof(AutoBuildHunters)); }
        [SyncHostOnly] public bool RetireEmptyHunters { get => retire; set => Set(ref retire, value, nameof(RetireEmptyHunters)); }
        private void Set(ref bool field, bool value, string property)
        { if (!CanMutateSetting(property) || field == value) return; field = value; OnPropertyChanged(property); }
        private void Reset()
        { if (!CanEditHostSettings) return; EnableMod = ControlAivHunters = AutoBuildHunters = RetireEmptyHunters = true; }
        public RelayCommand ResetToDefaultCommand { get; }
        private static bool German
        {
            get { try { return (GameAssetManagerAPI.Instance.CurrentLanguage ?? "").StartsWith("de", System.StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        }
        // English is the fallback for every locale without a dedicated translation.
        private static string Text(string de, string en) => German ? de : en;
        public string EnableText => Text("KI-Jäger-Test aktivieren", "Enable AI hunter test");
        public string EnableHelp => Text("Nur KI-Gebäude. Einstellungen vor der Partie ändern. Menschliche Jäger bleiben unverändert.", "AI buildings only. Change settings before a mission. Human hunters remain unchanged.");
        public string AivText => Text("AIV-Jäger nur bei Beute bauen", "Build AIV hunters only while prey exists");
        public string AivHelp => Text("Eine Grundhütte ist bei Hühnern oder anderer Beute erlaubt. Weitere AIV-Jäger benötigen jagbare Tiere außer Hühnern. Vorhandene Hütten zählen mit.", "One baseline hut is allowed for chickens or other prey. Additional AIV hunters require huntable animals other than chickens. Existing huts count.");
        public string AutomaticText => Text("Jäger auch ohne AIV-Auftrag bauen", "Build hunters even without an AIV order");
        public string AutomaticHelp => Text("Ergänzt eine fehlende Grundhütte nahe dem Kornspeicher, wenn Beute oder aktiver Hühnernachschub vorhanden sind. Vorhandene AIV-Jäger zählen mit. Normale Baukosten gelten.", "Adds a missing baseline hut near the granary when prey or active chicken replenishment exists. Existing AIV hunters count. Normal construction costs apply.");
        public string RetireText => Text("Leere KI-Jäger ohne Beute abreißen", "Retire empty AI hunter huts without prey");
        public string RetireHelp => Text("Ohne andere Beute bleibt für Hühner höchstens eine Grundhütte nahe dem Kornspeicher erhalten, auch zwischen Hühner-Spawns. Weitere Hütten warten auf vollständige Fleischabgabe vor dem normalen KI-Abriss. Ohne Beute und Nachschub werden alle abgebaut.", "Without other prey, keeps at most one baseline hut near the granary for chickens, including between chicken spawns. Extra huts wait for complete meat delivery before normal AI retirement. Without prey or replenishment all huts retire.");
        public string ScopeText => Text("Prüfung alle 4800 Kartenticks (etwa zwei Minuten bei Tempo 40). Beute zählt kartenweit; keine Wegsuche. Unterstützt den APIShared-Beutekatalog und die Jagdeinstellungen von ImprovedHunters.", "Checks every 4800 map ticks (about two minutes at speed 40). Prey counts mapwide; no path search. Uses the APIShared prey catalog and ImprovedHunters hunting settings.");
        public string HostText => Text("HOST-OPTIONEN", "HOST OPTIONS");
        public string ResetToDefaultText => Text("Zurücksetzen", "Reset");
        public string ResetHelp => Text("Setzt die vier Testmod-Schalter auf die Standardwerte zurück.", "Restores the four test mod switches to their defaults.");
        public string PresetLabel => Text("Preset", "Preset");
    }
}
