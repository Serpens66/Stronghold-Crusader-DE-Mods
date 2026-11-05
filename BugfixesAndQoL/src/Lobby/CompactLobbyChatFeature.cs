using System;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Noesis;

namespace BugfixesAndQoL
{
    // Process-owned by the runtime. Settings change UI data, never installed code.
    internal sealed class CompactLobbyChatFeature
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int mainThread;
        private readonly ILHook hook;
        private readonly ViewState[] views = new ViewState[5];
        private bool? lastEnabled;
        private bool failed;

        private sealed class ViewState
        {
            internal TextBlock View;
            internal Inline[] Original, Compact;
        }

        internal CompactLobbyChatFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            if (SynchronizationContext.Current?.GetType().FullName != "UnityEngine.UnitySynchronizationContext")
                throw new InvalidOperationException("Compact lobby chat must initialize on the Unity thread.");
            MethodInfo refresh = typeof(FRONT_Multiplayer).GetMethod("refreshLobbyChat",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            if (refresh == null || refresh.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "refreshLobbyChat(bool)");
            using (var definition = new DynamicMethodDefinition(refresh))
            using (var context = new ILContext(definition.Definition))
            {
                context.ReferenceBag = RuntimeILReferenceBag.Instance;
                context.Invoke(il => CompactLobbyChatIl.Patch(il, OnRebuilt));
                definition.Generate();
            }
            ILHook candidate = null;
            try
            {
                candidate = new ILHook(refresh, il => CompactLobbyChatIl.Patch(il, OnRebuilt),
                    new ILHookConfig { ManualApply = true, ID = "BugfixesAndQoL.CompactLobbyChat" });
                candidate.Apply();
                hook = candidate;
                candidate = null;
            }
            catch
            {
                candidate?.Dispose(); // Unpublished installation candidate only.
                throw;
            }
            // Logging must not turn a published hook into a failed constructor.
            try { Shared.DebugLogHelper.LogInfo(log, "Compact lobby chat installed; local presentation only."); }
            catch { }
        }

        private bool IsActive => settings.EnableClientFeatures && settings.EnableCompactLobbyChat;

        internal void ApplySetting()
        {
            if (failed || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            bool active = IsActive;
            if (lastEnabled == active) return;
            lastEnabled = active;
            if (!MainViewModel.viewModelLoaded) return;
            try
            {
                // Do not construct MainViewModel during early plugin startup.
                UpdateViews(MainViewModel.Instance.FRONTMultiplayer, false, active);
            }
            catch (Exception error) { Fail(error); }
        }

        private void OnRebuilt(object owner)
        {
            if (failed || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            try
            {
                UpdateViews((FRONT_Multiplayer)owner, true, IsActive);
            }
            catch (Exception error) { Fail(error); }
        }

        private void UpdateViews(FRONT_Multiplayer lobby, bool rebuilt, bool active)
        {
            UpdateView(0, lobby?.FindName("MP_ChatDisplay") as TextBlock, rebuilt, active);
            UpdateView(1, FRONT_CoopTrail1.Instance?.RefMP_ChatDisplay, rebuilt, active);
            UpdateView(2, FRONT_CoopTrail2.Instance?.RefMP_ChatDisplay, rebuilt, active);
            UpdateView(3, FRONT_CoopTrail3.Instance?.RefMP_ChatDisplay, rebuilt, active);
            UpdateView(4, FRONT_CoopTrail4.Instance?.RefMP_ChatDisplay, rebuilt, active);
        }

        private void UpdateView(int slot, TextBlock view, bool rebuilt, bool active)
        {
            var state = views[slot];
            if (rebuilt || !ReferenceEquals(state?.View, view)) views[slot] = state = null;
            if (view == null) return;
            if (state != null)
            {
                // A leave/reset or another mod may have replaced this history.
                // Never resurrect a saved history over a different current sequence.
                if (!CompactLobbyChatLayout.Matches(view.Inlines, state.Compact))
                {
                    views[slot] = null;
                    return;
                }
                if (!active)
                {
                    Replace(view, state.Original, state.Compact);
                    views[slot] = null;
                }
                return;
            }
            if (!active || view.Inlines.Count == 0) return;
            var original = new Inline[view.Inlines.Count];
            var tokens = new CompactChatToken[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = view.Inlines[i];
                tokens[i] = Classify(original[i]);
            }
            if (!CompactLobbyChatLayout.TryPlan(tokens, out var plan))
                throw new InvalidOperationException("Unexpected lobby chat inlines; preserving the current view.");
            var compact = new Inline[plan.Length];
            for (int i = 0; i < compact.Length; i++)
            {
                var operation = plan[i];
                var inline = original[operation.Index];
                if (operation.Action == CompactChatAction.Keep) compact[i] = inline;
                else
                {
                    var text = (TextBlock)((InlineUIContainer)inline).Child;
                    compact[i] = new Run(text.Text + (operation.Action == CompactChatAction.NameRun ? " " : ""))
                    { FontSize = text.FontSize };
                }
            }
            Replace(view, compact, original);
            views[slot] = new ViewState { View = view, Original = original, Compact = compact };
        }

        private static CompactChatToken Classify(Inline inline)
        {
            if (inline is LineBreak) return CompactChatToken.Break;
            if (inline is Run run && run.Text == Environment.NewLine) return CompactChatToken.End;
            if (!(inline is InlineUIContainer container)) return CompactChatToken.Unknown;
            if (container.Child is Image image && image.Width == 14f && image.Height == 14f)
                return CompactChatToken.Shield;
            if (!(container.Child is TextBlock text)) return CompactChatToken.Unknown;
            if (text.Width == 600f && text.Height == 14f && text.FontSize == 14f &&
                text.Text != null && text.Text.StartsWith(" ", StringComparison.Ordinal) &&
                text.Text.EndsWith(" :", StringComparison.Ordinal)) return CompactChatToken.Name;
            if (text.FontSize == 12f && text.TextWrapping == TextWrapping.WrapWithOverflow &&
                text.Margin == new Thickness(40f, 0f, 5f, 0f)) return CompactChatToken.Message;
            if (text.Width == 600f && text.Height == 16f && text.FontSize == 14f)
                return CompactChatToken.System;
            return CompactChatToken.Unknown;
        }

        private static void Replace(TextBlock view, Inline[] next, Inline[] previous)
        {
            try { Set(view, next); }
            catch
            {
                Set(view, previous);
                throw;
            }
        }

        private static void Set(TextBlock view, Inline[] sequence)
        {
            view.Inlines.Clear();
            foreach (Inline inline in sequence) view.Inlines.Add(inline);
        }

        private void Fail(Exception error)
        {
            failed = true;
            // Restore only our still-owned current histories. Other views remain untouched.
            foreach (var state in views)
            {
                if (state == null) continue;
                try
                {
                    if (CompactLobbyChatLayout.Matches(state.View.Inlines, state.Compact))
                        Replace(state.View, state.Original, state.Compact);
                }
                catch (Exception restore) { ReportError("CompactLobbyChat.Restore", restore.ToString()); }
            }
            Array.Clear(views, 0, views.Length);
            ReportError("CompactLobbyChat", "Compact lobby chat disabled; " + error);
        }

        private void ReportError(string key, string message)
        {
            // The injected callback must never interrupt Vanilla's remaining scroll/timing work.
            try { Shared.DebugLogHelper.LogErrorLimited(log, key, message); }
            catch { }
        }
    }
}
