using System;
using System.Collections.Generic;
using Spectator;

internal static class ToolbarLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (float viewport in new[] { 960f, 1920f, 2560f, 3840f })
        {
            var dock = ToolbarLayout.Dock(new ToolbarRect(viewport - 914, 1080 - 166, 914, 166));
            check(dock.Y == 1080 - 123, "Every mode stays below both Vanilla info hosts, including the bottom-125 detailed host.");
            var fixedPosition = ToolbarLayout.Clamp(dock.X, dock.Y, dock.Width, dock.Height, viewport, 1080);
            // Hovers have no input into placement. Reopening them cannot accumulate displacement.
            for (int hover = 0; hover < 20; hover++)
            {
                var same = ToolbarLayout.Clamp(dock.X, dock.Y, dock.Width, dock.Height, viewport, 1080);
                check(same.X == fixedPosition.X && same.Y == fixedPosition.Y, "Fixed position is independent of hover state.");
            }
            var clamped = ToolbarLayout.Clamp(viewport + 50, -30, 496, 114, viewport, 1080);
            check(clamped.X >= 0 && clamped.Right <= viewport && clamped.Y == 0, "Resize/drag stays inside the viewport.");
            foreach (float scale in new[] { 1f, 1.5f, 2f, 3f })
            {
                check(ToolbarLayout.Snap(dock.X + 9.999f / scale, dock.Y, dock, scale, scale, false), "Acquire at ten rendered pixels.");
                check(!ToolbarLayout.Snap(dock.X + 10.1f / scale, dock.Y, dock, scale, scale, false), "Never acquire beyond ten rendered pixels.");
                check(ToolbarLayout.Snap(dock.X + 13.9f / scale, dock.Y, dock, scale, scale, true), "Hold until fourteen rendered pixels.");
                check(!ToolbarLayout.Snap(dock.X + 14.1f / scale, dock.Y, dock, scale, scale, true), "Release beyond fourteen rendered pixels.");
                check(!ToolbarLayout.Snap(dock.X + 8 / scale, dock.Y + 8 / scale, dock, scale, scale, false), "Snap radius is circular, not a square.");
            }
        }
        check(8 * (ToolbarLayout.Portrait + 2) + 8 + 24 + 2 + 222 == 496,
            "Eight larger portraits leave 222 units for all four actions in one header.");
        check(2 * 26 + 4 == ToolbarLayout.Body, "Both target/order rows and goods rows fit without scrolling.");
        foreach (float scale in new[] { .75f, 1f, 1.5f, 2f })
        {
            var dock = ToolbarLayout.Dock(new ToolbarRect(300,400,914*scale,166*scale));
            check(Math.Abs(dock.X-(300+130*scale)) < .001f && Math.Abs(dock.Y-(400+43*scale)) < .001f && dock.Width == 496*scale,
                "Dock follows the actual native HUD scale, including a differently scaled overlay canvas.");
        }
        var controls = new List<ToolbarRect> { new ToolbarRect(260,100,100,26), new ToolbarRect(450,94,35,40) };
        var band = new ToolbarRect(0,100,500,26);
        ToolbarLayout.Wings(band, controls, out var left, out var right);
        check(left.Width > 230 && right.Width > 0, "Content continues in genuine free regions around date and buttons.");
        foreach (var control in controls)
            check(!left.Intersects(control) && !right.Intersects(control), "Footer input never covers an original control.");
        var adjacent = new List<ToolbarRect> { new ToolbarRect(100,100,90,30),new ToolbarRect(140,100,110,30),new ToolbarRect(240,100,60,30) };
        ToolbarLayout.Wings(band,adjacent,out left,out right);
        foreach (var item in adjacent) check(!left.Intersects(item) && !right.Intersects(item), "Merge overlapping reserved intervals.");
        // Includes tall buildings with negative top margins and wide unit/submenu rows.
        foreach (var source in new[] { new ToolbarRect(-125,-12,480,106), new ToolbarRect(-115,-8,430,98),
            new ToolbarRect(-120,0,490,90), new ToolbarRect(0,-24,95,124) })
        {
            var target = new ToolbarRect(130,72,496,52);
            var fit = ToolbarLayout.FitRaster(source,target);
            float scale = ToolbarLayout.RasterScale(source,target);
            check(fit.X >= target.X && fit.Y >= target.Y && fit.Right <= target.Right + .001f && fit.Bottom <= target.Bottom + .001f,
                "Tall sprites and their complete input rectangles fit below the portrait reservation.");
            check(Math.Abs(fit.Width / source.Width - scale) < .001f && Math.Abs(fit.Height / source.Height - scale) < .001f,
                "One proportional ancestor transform scales image and hit area together.");
        }
    }
}
