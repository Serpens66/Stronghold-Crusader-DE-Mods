using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Spectator
{
    internal static class AiEditorPlacementPolicy
    {
        // Vanilla's unit mapper is action 7, including live test placement.
        internal static bool Enabled(bool enabled, bool editor, bool multiplayer, int action, int owner, bool unitMapper) =>
            enabled && editor && !multiplayer && action == 7 && owner >= 1 && owner <= 8 && unitMapper;
    }

    // Presentation intent only. APIShared owns validation, identity, persistence and native changes.
    public sealed class AiPlacementChoice
    {
        public string Role { get; }
        public string Name { get; }
        public bool Available { get; }
        public bool Deferred { get; }
        public string Help { get; }
        public AiPlacementChoice(string role, string name, bool available, bool deferred, string help)
        { Role = role; Name = name; Available = available; Deferred = deferred; Help = help ?? ""; }
    }

    // The selection is remembered per unit type (choice key) for the current mission. A type whose
    // remembered role is absent or not selectable for the current owner shows No AI instead
    // (user decision 2026-11-19); the remembered role returns once it is selectable again.
    // Placed troops count as additional to the AI's planned army by default (user decision 2026-11-19).
    public sealed class AiPlacementModel : INotifyPropertyChanged
    {
        private sealed class Remembered { internal string Role = "None"; internal bool Additional = true; }
        private readonly Dictionary<string, Remembered> remembered = new Dictionary<string, Remembered>(StringComparer.Ordinal);
        private AiPlacementChoice[] choices = Array.Empty<AiPlacementChoice>();
        private string role = "None", feedback = "", key = "";
        private bool additionalPreference = true, applyInEditor;
        public event PropertyChangedEventHandler PropertyChanged;
        public AiPlacementChoice[] Choices => choices;
        public AiPlacementChoice Selected
        {
            get { foreach (var choice in choices) if (choice.Role == role) return choice; return null; }
            set { if (value == null || value.Role == role) return; role = value.Role; Current().Role = role;
                feedback = ""; Changed(nameof(Selected)); Changed(nameof(Additional)); Changed(nameof(CanCount)); Changed(nameof(CanPlace)); Changed(nameof(CanApplyInEditor)); Changed(nameof(Feedback)); }
        }
        public string Role => role;
        // No AI has no recruitment mode; the preference is kept for the next AI role of this unit type.
        public bool Additional
        {
            get => CanCount && additionalPreference;
            set { if (!CanCount || additionalPreference == value) return; additionalPreference = value; Current().Additional = value; Changed(nameof(Additional)); }
        }
        public bool ApplyInEditor { get => applyInEditor; set { if (applyInEditor == value) return; applyInEditor = value; Changed(nameof(ApplyInEditor)); } }
        public bool CanCount => role != "None";
        public bool CanPlace => Selected?.Available == true;
        // A deferred authored declaration can still request an explicit editor preview;
        // APIShared validates the editor's current AI/AIC independently at application.
        public bool CanApplyInEditor => Selected?.Available == true;
        public string Feedback { get => feedback; internal set { if (feedback == value) return; feedback = value ?? ""; Changed(nameof(Feedback)); } }
        // choiceKey identifies the unit type; the owner and locale may change without a new key.
        internal void SetChoices(string choiceKey, AiPlacementChoice[] next)
        {
            next = next ?? Array.Empty<AiPlacementChoice>();
            key = choiceKey ?? "";
            var wanted = Current();
            role = "None";
            foreach (var choice in next) if (choice.Role == wanted.Role && choice.Available) role = wanted.Role;
            additionalPreference = wanted.Additional;
            // Without a selectable None row (no AI service) the unavailable None row still blocks placement.
            choices = next; Changed(nameof(Choices)); Changed(nameof(Selected)); Changed(nameof(Additional)); Changed(nameof(CanCount));
            Changed(nameof(CanPlace)); Changed(nameof(CanApplyInEditor));
        }
        internal void Reset()
        {
            role = "None"; additionalPreference = true; applyInEditor = false; feedback = ""; key = "";
            remembered.Clear(); choices = Array.Empty<AiPlacementChoice>(); Changed("");
        }
        private Remembered Current()
        {
            if (!remembered.TryGetValue(key, out var entry)) remembered[key] = entry = new Remembered();
            return entry;
        }
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
