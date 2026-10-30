using System;
using System.Collections.Generic;
using Spectator;

internal static class ToolbarLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (float scale in new[] { .75f, 1f, 1.5f, 2f, 3f })
        foreach (int count in new[] { 1,2,3,4,5,6,7,8 })
        {
            var alignment = new ToolbarRect(400,600,914*scale,166*scale);
            var frame = new ToolbarRect(400+114*scale,600+22*scale,800*scale,144*scale);
            var compact = ToolbarLayout.Arrange(alignment,frame,count,false);
            var expanded = ToolbarLayout.Arrange(alignment,frame,count,true);
            check(Math.Abs(frame.X-compact.Roster.Right-2*scale)<.001f,"Lord raster sits two native UI units left of the outer frame.");
            check(Math.Abs(alignment.Bottom-compact.Roster.Bottom)<.001f,"Portrait rows finish at the screen bottom, without control or grip rows.");
            check(!compact.Roster.Intersects(frame),"Placing never reserves space inside the Vanilla construction HUD.");
            check(compact.Tools.Empty && compact.Roster.X == expanded.Roster.X && compact.Roster.Y == expanded.Roster.Y,
                "Expanded/collapsed/placement modes keep the same left-hand raster.");
            check(expanded.Tools.X >= compact.Roster.Right && expanded.Tools.Y >= alignment.Bottom-125*scale,
                "Tools remain beside the raster and below the Vanilla info zone.");
            check(expanded.Bounds.X <= expanded.Roster.X && expanded.Bounds.Y <= expanded.Tools.Y &&
                expanded.Bounds.Right >= expanded.Tools.Right && expanded.Bounds.Bottom >= expanded.Roster.Bottom,
                "Common drag/clamp bounds enclose every visible part without clipping the stop button.");
            var floating = ToolbarLayout.Arrange(alignment,frame,count,true,true);
            check(floating.Roster.Width == 496 && floating.Roster.Height == 44 && floating.Tools.Y == floating.Roster.Bottom &&
                floating.Control.Right <= floating.Roster.Right && floating.Control.Bottom <= floating.Roster.Bottom,
                "Floating portraits/control share one header, directly above the tool surface.");
            check(compact.Control.Right < alignment.X+553*scale && compact.Control.Bottom < alignment.Bottom &&
                compact.Control.Width==60*scale && !compact.Control.Intersects(compact.Roster),
                "Dock control fits immediately left of the trumpet without enlarging the roster.");
            check(compact.Grip.Right == frame.X && compact.Grip.Y == compact.Roster.Y && compact.Grip.Height == compact.Roster.Height,
                "The existing gap is the full-height dock grip; no separate side strip remains.");
            var cells = new List<ToolbarRect>();
            for (int i=0;i<count;i++)
            {
                var cell = ToolbarLayout.OwnerCell(i);
                check(cell.Width==36 && cell.Height==36 && cell.Right<=80 && cell.Bottom<=ToolbarLayout.RosterHeight(count),
                    "All portraits fit in two columns and at most four rows at the selected size.");
                foreach (var previous in cells) check(!previous.Intersects(cell),"Packed portrait cells never overlap.");
                var floatingCell = ToolbarLayout.OwnerCell(i,true);
                check(floatingCell.Y == 6 && floatingCell.Right < floating.Control.X && floatingCell.Height == 36,
                    "All eight portraits fit left-to-right before the floating control.");
                check(!new ToolbarRect(compact.Roster.X+cell.X*scale,compact.Roster.Y+cell.Y*scale,
                    cell.Width*scale,cell.Height*scale).Intersects(compact.Grip),"Gap dragging cannot steal portrait clicks.");
                cells.Add(cell);
            }
            // Gaps in player IDs do not consume a cell: runtime passes the visible ordinal.
            int ordinal=0;
            foreach(int id in new[]{1,3,7})
            {
                var cell=ToolbarLayout.OwnerCell(ordinal++);
                check((id!=7 || cell.Y==38) && (id!=3 || cell.X==42),"Sparse rosters pack by occupied ordinal, retaining numeric order.");
            }
            foreach (float viewport in new[] { 960f, 1920f, 2560f, 3840f })
            {
                float fit=Math.Min(1,Math.Min(viewport/expanded.Bounds.Width,1080/expanded.Bounds.Height));
                var clamped=ToolbarLayout.Clamp(viewport+50,-30,expanded.Bounds.Width*fit,expanded.Bounds.Height*fit,viewport,1080);
                check(clamped.X>=0 && clamped.Right<=viewport+.001f && clamped.Y==0 && clamped.Bottom<=1080,
                    "The complete group, including portraits and stop control, stays reachable at screen edges.");
            }
        }
        foreach(float uiScale in new[]{.75f,1f,1.5f,2f,3f})
        foreach(float fraction in new[]{0f,.5f,1f})
        {
            var grip = new ToolbarRect(300,700,4*uiScale,150*uiScale);
            float targetX=grip.X+fraction*grip.Width, targetY=grip.Y+fraction*grip.Height;
            var drag=new ToolbarDragAnchor(); drag.Begin(targetX,targetY,grip);
            check(drag.Move(targetX+13.9f/uiScale,targetY,grip,uiScale,uiScale,uiScale,true,out var x,out var y),"Attached grip retains fourteen-pixel hysteresis.");
            check(!drag.Move(targetX+14.1f/uiScale,targetY,grip,uiScale,uiScale,uiScale,true,out x,out y),"Crossing release radius selects the row layout.");
            float firstX=x, firstY=y;
            for(int move=0;move<5;move++)
                check(!drag.Move(targetX+14.1f/uiScale,targetY,grip,uiScale,uiScale,uiScale,false,out x,out y) && x==firstX && y==firstY,
                    "Changing panel dimensions cannot feed back as pointer motion or snap chatter.");
            check(drag.Move(targetX+9.9f/uiScale,targetY,grip,uiScale,uiScale,uiScale,false,out x,out y),"Return to the original grip point acquires within ten rendered pixels.");
            var floatingGrip=new ToolbarRect(120,180,496*uiScale,4*uiScale);
            drag.Begin(floatingGrip.X+fraction*floatingGrip.Width,floatingGrip.Y+fraction*floatingGrip.Height,floatingGrip,false);
            check(drag.Move(grip.X+grip.Width/2,grip.Y+grip.Height/2,grip,uiScale,uiScale,uiScale,false,out x,out y),
                "A new floating drag always targets the visible dock centre, independently of the exact top-edge click.");
            var paper=new ToolbarRect(-150,-25,876*uiScale,103*uiScale);
            var slices=new ToolbarPaperSlices(paper,180*uiScale);
            float topV=(slices.Join-slices.TopImage.Y)/slices.TopImage.Height;
            float lowerV=(slices.Join-slices.LowerImage.Y)/slices.LowerImage.Height;
            check(Math.Abs(topV-lowerV)<.00001f && Math.Abs(topV-.8f)<.00001f &&
                slices.TopImage.X==slices.LowerImage.X && slices.TopImage.Width==slices.LowerImage.Width,
                "Paper strips meet at the same source texel and keep horizontal texture coordinates.");
            check(slices.LowerImage.Bottom>=180*uiScale-.001f,"The matching lower shading reaches the footer without a texture restart.");
        }
        var dock=new ToolbarRect(100,200,600,202);
        foreach(float scale in new[]{1f,1.5f,2f,3f})
        {
            check(ToolbarLayout.Snap(100+9.999f/scale,200,dock,scale,scale,false),"Acquire within ten rendered pixels.");
            check(!ToolbarLayout.Snap(100+10.1f/scale,200,dock,scale,scale,false),"Do not acquire beyond ten rendered pixels.");
            check(ToolbarLayout.Snap(100+13.9f/scale,200,dock,scale,scale,true),"Keep snap until fourteen rendered pixels.");
            check(!ToolbarLayout.Snap(100+14.1f/scale,200,dock,scale,scale,true),"Release beyond fourteen rendered pixels.");
            check(!ToolbarLayout.Snap(100+8/scale,200+8/scale,dock,scale,scale,false),"Snap uses a radius, not a square.");
        }
        foreach(DockHudDisplay display in Enum.GetValues(typeof(DockHudDisplay)))
        foreach(bool expanded in new[]{false,true})
            check(DockHudPolicy.CanCoverBuildCategories(display,expanded)==(display==DockHudDisplay.Tools && expanded),
                "Only an expanded tool surface on the actual blank HUD may cover construction categories.");
        var controls=new List<ToolbarRect>{new ToolbarRect(260,100,100,26),new ToolbarRect(450,94,35,40)};
        var band=new ToolbarRect(0,100,500,26);
        ToolbarLayout.Wings(band,controls,out var left,out var right);
        check(left.Width>230 && right.Width>0,"Covering inactive construction tabs leaves useful lower tool space.");
        foreach(var control in controls) check(!left.Intersects(control) && !right.Intersects(control),"Date, trumpet and crown retain real uncovered hit areas.");
        // Regression: previously only footer wings avoided controls. Moving the solid tool
        // body downward by even six pixels could cover the trumpet/crown above those wings.
        var solid = new List<ToolbarRect>{new ToolbarRect(0,0,80,202),new ToolbarRect(104,85,496,88)};
        var fixedControls = new List<ToolbarRect>{new ToolbarRect(760,1054,110,26),
            new ToolbarRect(919,1045,30,35),new ToolbarRect(954,1045,30,35),new ToolbarRect(1152,952,128,128)};
        check(ToolbarLayout.TryAvoidControls(392,872,600,202,1280,1080,solid,fixedControls,4,out var safe) &&
            safe.X==392 && safe.Y==872,"An unobstructed dock stays fixed; footer gaps do not repel its body.");
        foreach(var wanted in new[]{new ToolbarRect(392,902,600,202),new ToolbarRect(640,872,600,202),new ToolbarRect(500,900,600,202)})
        {
            check(ToolbarLayout.TryAvoidControls(wanted.X,wanted.Y,wanted.Width,wanted.Height,1280,1080,solid,fixedControls,4,out safe),
                "Find a nearby on-screen drag position without covering functional controls.");
            check(safe.X>=0 && safe.Y>=0 && safe.Right<=1280 && safe.Bottom<=1080,"Protected drag positioning remains inside the viewport.");
            foreach(var part in solid)
            foreach(var control in fixedControls)
                check(!new ToolbarRect(safe.X+part.X,safe.Y+part.Y,part.Width,part.Height).Intersects(control),
                    "Neither the fixed tool body nor the roster may cover a date, HUD button or minimap.");
        }
        var rosterOnly = new List<ToolbarRect>{new ToolbarRect(0,0,80,202)};
        var buildFrame = new List<ToolbarRect>{new ToolbarRect(480,936,800,144)};
        check(ToolbarLayout.TryAvoidControls(700,872,80,202,1280,1080,rosterOnly,buildFrame,4,out safe) &&
            !safe.Intersects(buildFrame[0]),"Dragging the placement selector never masks the unchanged Vanilla construction grid.");
        var fullScreen = new List<ToolbarRect>{new ToolbarRect(0,0,1280,1080)};
        check(!ToolbarLayout.TryAvoidControls(400,800,80,202,1280,1080,rosterOnly,fullScreen,4,out safe),
            "A fully occupied viewport reports no room, rather than accepting an unsafe input surface.");
        var adjacent=new List<ToolbarRect>{new ToolbarRect(100,100,90,30),new ToolbarRect(140,100,110,30),new ToolbarRect(240,100,60,30)};
        ToolbarLayout.Wings(band,adjacent,out left,out right);
        foreach(var item in adjacent) check(!left.Intersects(item) && !right.Intersects(item),"Overlapping functional reservations form one exclusion.");
    }
}
