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
        public string AivHelp => Text("Sperrt Jäger-Bauaufträge aus dem Burgbauplan ohne jagbare Tiere. Neue Beute erlaubt den normalen Wiederaufbau.", "Blocks hunter orders in the castle plan without huntable animals. New prey permits normal rebuilding.");
        public string AutomaticText => Text("Jäger auch ohne AIV-Auftrag bauen", "Build hunters even without an AIV order");
        public string AutomaticHelp => Text("Baut bei Beute und ausreichenden Ressourcen bis zu einer Jägerhütte je KI nahe dem Kornspeicher. Vorhandene AIV-Jäger zählen mit.", "With prey and sufficient resources, builds up to one hunter hut per AI near its granary. Existing AIV huts count towards this limit.");
        public string RetireText => Text("Leere KI-Jäger ohne Beute abreißen", "Retire empty AI hunter huts without prey");
        public string RetireHelp => Text("Wartet auf Verarbeitung und vollständige Fleischabgabe, dann normaler KI-Rückbau mit der Vanilla-Erstattung (im normalen Spiel 50 Prozent). Blockierte Lieferungen verzögern den Abriss.", "Waits for processing and complete meat delivery, then performs normal AI retirement with the Vanilla refund (50 percent in regular games). Blocked deliveries delay removal.");
        public string ScopeText => Text("Prüfung alle 4800 Kartenticks (etwa zwei Minuten bei Tempo 40). Beute zählt kartenweit; keine Wegsuche. Unterstützt den APIShared-Beutekatalog und die Jagdeinstellungen von ImprovedHunters.", "Checks every 4800 map ticks (about two minutes at speed 40). Prey counts mapwide; no path search. Uses the APIShared prey catalog and ImprovedHunters hunting settings.");
        public string HostText => Text("HOST-OPTIONEN", "HOST OPTIONS");
        public string ResetToDefaultText => Text("Zurücksetzen", "Reset");
        public string ResetHelp => Text("Setzt die vier Testmod-Schalter auf die Standardwerte zurück.", "Restores the four test mod switches to their defaults.");
        public string PresetLabel => Text("Preset", "Preset");
    }
}
