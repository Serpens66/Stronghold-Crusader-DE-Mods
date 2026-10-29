using System;
using System.Collections.Generic;
using Spectator;

internal static class ToolbarLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (float viewport in new[] { 960f, 1920f, 2560f, 3840f })
        {
            var parts = new List<ToolbarRect> { new ToolbarRect(0, 0, 300, 32) };
            var obstacles = new List<ToolbarRect> { new ToolbarRect(200, 600, 450, 80) };
            check(ToolbarLayout.TryPlace(240, 620, 300, 32, viewport, 1080, parts, obstacles, out var moved),
                "A visible tooltip has room for the complete player/stop bar.");
            check(!moved.Intersects(obstacles[0]), "The bar must avoid all actual tooltip rows.");
            obstacles.Clear();
            check(ToolbarLayout.TryPlace(240, 620, 300, 32, viewport, 1080, parts, obstacles, out var restored) &&
                restored.X == 240 && restored.Y == 620, "Removing hover restores requested, not avoidance, coordinates.");
            check(ToolbarLayout.TryPlace(viewport + 50, -30, 300, 32, viewport, 1080, parts, obstacles, out var clamped) &&
                clamped.X >= 0 && clamped.Right <= viewport && clamped.Y >= 0, "Dragging/resizing keeps controls on screen.");
            obstacles.Add(new ToolbarRect(0, 100, viewport, 230));
            obstacles.Add(new ToolbarRect(0, 330, viewport, 280));
            check(ToolbarLayout.TryPlace(200, 300, 300, 32, viewport, 1080, parts, obstacles, out moved) &&
                !moved.Intersects(obstacles[0]) && !moved.Intersects(obstacles[1]), "Avoid adjoining multiline hosts together.");
        }
        var controls = new List<ToolbarRect> {
            new ToolbarRect(260,100,100,26), // date
            new ToolbarRect(450,94,35,40)   // active button
        };
        var band = new ToolbarRect(0,100,500,26);
        ToolbarLayout.Wings(band, controls, out var left, out var right);
        check(left.Width > 230 && right.Width > 0, "Usable content continues on both sides of the date.");
        foreach (var control in controls)
            check(!left.Intersects(control) && !right.Intersects(control), "Neither painted wing covers an original control.");
        var pieces = new List<ToolbarRect> { new ToolbarRect(0,0,500,90), left, right };
        check(ToolbarLayout.Fits(0,0,500,126,960,1080,pieces,controls), "Open notch must not repel a correctly docked panel.");
        controls.Add(new ToolbarRect(240,70,180,22));
        check(!ToolbarLayout.Fits(0,0,500,126,960,1080,pieces,controls), "A real body overlap cannot be mistaken for the notch.");
        var noSpace = new List<ToolbarRect> { new ToolbarRect(0,0,960,1080) };
        check(!ToolbarLayout.TryPlace(0,0,500,126,960,1080,pieces,noSpace,out _), "No-space layouts fail explicitly, not with negative coordinates.");
        var adjacent = new List<ToolbarRect> { new ToolbarRect(100,100,90,30),new ToolbarRect(140,100,110,30),new ToolbarRect(240,100,60,30) };
        ToolbarLayout.Wings(band,adjacent,out left,out right);
        foreach(var item in adjacent) check(!left.Intersects(item) && !right.Intersects(item), "Overlapping reserved intervals form one exclusion.");
    }
}
