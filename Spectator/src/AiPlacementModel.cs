using System;
using System.ComponentModel;

namespace Spectator
{
    internal static class AiEditorPlacementPolicy
    {
        // Vanilla's editor unit mapper is action 7; action 5 belongs to live placement.
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

    public sealed class AiPlacementModel : INotifyPropertyChanged
    {
        private AiPlacementChoice[] choices = Array.Empty<AiPlacementChoice>();
        private string role = "None", feedback = "";
        private bool additional, applyInEditor;
        public event PropertyChangedEventHandler PropertyChanged;
        public AiPlacementChoice[] Choices => choices;
        public AiPlacementChoice Selected
        {
            get { foreach (var choice in choices) if (choice.Role == role) return choice; return null; }
            set { if (value == null || value.Role == role) return; role = value.Role;
                if (role == "None") additional = false;
                feedback = ""; Changed(nameof(Selected)); Changed(nameof(Additional)); Changed(nameof(CanCount)); Changed(nameof(CanPlace)); Changed(nameof(CanApplyInEditor)); Changed(nameof(Feedback)); }
        }
        public string Role => role;
        public bool Additional { get => additional; set { bool next = CanCount && value; if (additional == next) return; additional = next; Changed(nameof(Additional)); } }
        public bool ApplyInEditor { get => applyInEditor; set { if (applyInEditor == value) return; applyInEditor = value; Changed(nameof(ApplyInEditor)); } }
        public bool CanCount => role != "None";
        public bool CanPlace => Selected?.Available == true;
        // A deferred authored declaration can still request an explicit editor preview;
        // APIShared validates the editor's current AI/AIC independently at application.
        public bool CanApplyInEditor => Selected?.Available == true;
        public string Feedback { get => feedback; internal set { if (feedback == value) return; feedback = value ?? ""; Changed(nameof(Feedback)); } }
        internal void SetChoices(AiPlacementChoice[] next, string unavailableLabel, string unavailableHelp)
        {
            next = next ?? Array.Empty<AiPlacementChoice>();
            bool found = false; foreach (var choice in next) if (choice.Role == role) found = true;
            // Changing a unit/owner must never silently replace an explicitly selected AI assignment.
            if (!found)
            {
                var retained = new AiPlacementChoice[next.Length + 1]; Array.Copy(next, retained, next.Length);
                retained[next.Length] = new AiPlacementChoice(role, unavailableLabel, false, false, unavailableHelp); next = retained;
            }
            choices = next; Changed(nameof(Choices)); Changed(nameof(Selected)); Changed(nameof(CanPlace)); Changed(nameof(CanApplyInEditor));
        }
        internal void Reset()
        { role = "None"; additional = applyInEditor = false; feedback = ""; choices = Array.Empty<AiPlacementChoice>(); Changed(""); }
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
