using System;

namespace Spectator
{
    internal sealed class BuildMenuSnapshot
    {
        internal readonly object Hud;
        internal readonly int Page, SubMode;
        internal readonly bool?[] Tabs;
        internal BuildMenuSnapshot(object hud, int page, int subMode, bool?[] tabs)
        { Hud = hud; Page = page; SubMode = subMode; Tabs = tabs == null ? null : (bool?[])tabs.Clone(); }
    }

    // Only an explicit menu request may reclaim a page after the player chose a Vanilla category.
    internal sealed class BuildMenuLease
    {
        internal BuildMenuSnapshot Saved { get; private set; }
        internal bool Suppressed { get; private set; }
        internal void Request() => Suppressed = false;
        internal bool CanAcquire(bool activeTest, bool floating, bool expanded, bool placing, bool main, bool foreign) =>
            Saved == null && !Suppressed && activeTest && !floating && expanded && !placing && main && !foreign;
        internal void Acquire(object hud, int page, int subMode, bool?[] tabs)
        {
            if (Saved != null) throw new InvalidOperationException("Build menu is already leased.");
            Saved = new BuildMenuSnapshot(hud, page, subMode, tabs);
        }
        internal void ObserveNativePage(object hud, int page, bool placing)
        {
            if (Saved == null || !ReferenceEquals(hud, Saved.Hud) || placing || page == DockHudPolicy.BlankBuildScreen) return;
            Saved = null; Suppressed = true;
        }
        internal BuildMenuSnapshot Release(bool waitForMain = false, bool main = true)
        {
            if (waitForMain && !main) return null;
            var saved = Saved; Saved = null; return saved;
        }
        internal void Reset() { Saved = null; Suppressed = false; }
    }
}
