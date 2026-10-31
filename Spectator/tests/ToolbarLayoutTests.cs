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
            check(Math.Abs(frame.X-compact.Roster.Right-6*scale)<.001f,"Lord raster sits six native UI units left of the outer frame.");
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
            var dockTarget=ToolbarLayout.DockDropTarget(alignment,frame);
            check(dockTarget.Width==80*scale && dockTarget.Height==150*scale && dockTarget.Bottom==alignment.Bottom,
                "The target always reserves the complete eight-player raster, independent of occupied rows.");
            var design=ToolbarLayout.FloatingDesignRect(count,false);
            check(design.Width==22 && design.Right+2==floating.Control.X &&
                ToolbarLayout.OwnerCell(count-1,true).Right+2==design.X,"Design button fits precisely between portraits and toggle.");
            var placing=ToolbarLayout.Arrange(alignment,frame,count,false,true,default,true);
            check(placing.Roster.Width-floating.Roster.Width==36 && placing.Control.Width==60,
                "Only the additional stop-label width extends the compact row during placement.");

            check(floating.Roster.Width == ToolbarLayout.FloatingWidth(count,false) && floating.Roster.Height == 40 && floating.Tools.Y == floating.Roster.Bottom &&
                floating.Control.Right <= floating.Roster.Right && floating.Control.Bottom <= floating.Roster.Bottom,
                "Floating portraits/control share one header, directly above the tool surface.");
            check(compact.Control.Right < alignment.X+553*scale && compact.Control.Bottom < alignment.Bottom &&
                compact.Control.Width==60*scale && !compact.Control.Intersects(compact.Roster),
                "Dock control fits immediately left of the trumpet without enlarging the roster.");
            check(compact.Grip.Right == frame.X && compact.Grip.Y == compact.Roster.Y && compact.Grip.Height == compact.Roster.Height && compact.Grip.Width==8*scale,
                "The existing gap is the full-height dock grip; no separate side strip remains.");
            var placed = ToolbarLayout.Arrange(alignment,frame,count,false,true,default,true);
            var closed = ToolbarLayout.Arrange(alignment,frame,count,false,true);
            check(floating.Roster.Width==closed.Roster.Width && floating.Tools.Width==496 && closed.Bounds.Width==closed.Roster.Width,
                "An open menu retains its width without stretching the compact header or the closed hit bounds.");
            check(placed.Control.Width==60 && closed.Control.Width==24 && placed.Roster.Width-closed.Roster.Width==36,
                "Only the placement label adds necessary width; plus/minus stay compact.");
            check(floating.Grip.Right<=ToolbarLayout.OwnerCell(0,true).X && floating.Grip.Y==2 && floating.Grip.Height==36,
                "The floating drag area is a small left-hand grip, never a long top strip.");
            foreach(var pose in new[]{compact,expanded,floating,placing,closed})
            {
                check(!pose.Grip.Intersects(pose.Control) && !pose.Grip.Intersects(pose.Tools),
                    "The raised grip never steals control or tool clicks in any toolbar form.");
            }
            foreach(var pose in new[]{floating,placing,closed})
                check(!pose.Grip.Intersects(ToolbarLayout.FloatingDesignRect(count,pose.Control.Width==60)),
                    "The raised floating grip leaves the design button reachable.");
            var cells = new List<ToolbarRect>();
            for (int i=0;i<count;i++)
            {
                var cell = ToolbarLayout.OwnerCell(i);
                check(cell.Width==36 && cell.Height==36 && cell.Right<=80 && cell.Bottom<=ToolbarLayout.RosterHeight(count),
                    "All portraits fit in two columns and at most four rows at the selected size.");
                foreach (var previous in cells) check(!previous.Intersects(cell),"Packed portrait cells never overlap.");
                var floatingCell = ToolbarLayout.OwnerCell(i,true);
                check(floatingCell.Y == 2 && floatingCell.Right < floating.Control.X && floatingCell.Height == 36,
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
        foreach(float screenScale in new[]{1f,1.5f,2f})
        foreach(float fraction in new[]{0f,.5f,1f})
        {
            var grip = new ToolbarRect(300,700,8*uiScale,150*uiScale);
            var target=new ToolbarRect(200,500,80*uiScale,150*uiScale);
            float startX=grip.X+fraction*grip.Width,startY=grip.Y+fraction*grip.Height;
            var drag=new ToolbarDragAnchor(); drag.Begin(startX,startY,grip,true);
            check(!drag.Move(startX+13.9f/screenScale,startY,target,screenScale,screenScale,uiScale,out var x,out var y),"Docked view stays fixed inside fourteen physical pixels.");
            check(drag.Move(startX+14.1f/screenScale,startY,target,screenScale,screenScale,uiScale,out x,out y),"Crossing fourteen physical pixels detaches the view.");
            float firstX=x,firstY=y;
            for(int move=0;move<5;move++)
                check(drag.Move(startX+14.1f/screenScale,startY,target,screenScale,screenScale,uiScale,out x,out y) && x==firstX && y==firstY,
                    "Reflow cannot feed back as pointer motion or cause repeated attach/detach.");
            var small=ToolbarLayout.FloatingGripRect;
            foreach(float fx in new[]{0f,.5f,1f})
            foreach(float fy in new[]{0f,.5f,1f})
            {
                // The cursor can grab any part of the grip. Its centre, not the cursor,
                // must enter the entire fixed raster, including the four corners.
                float centreX=target.X+target.Width*fx, centreY=target.Y+target.Height*fy;
                float px=centreX+(fraction-.5f)*small.Width*uiScale;
                float py=centreY+(fraction-.5f)*small.Height*uiScale;
                check(drag.Move(px,py,target,screenScale,screenScale,uiScale,out x,out y) && drag.Preview && drag.Floating,
                    "Any point in the full 2x4 target previews while the row remains floating.");
                check(Math.Abs(x+(small.X+fraction*small.Width)*uiScale-px)<.001f,
                    "The real grab offset remains under the pointer independently of header width.");
            }
            float dropX=target.Right+(fraction-.5f)*small.Width*uiScale;
            float dropY=target.Bottom+(fraction-.5f)*small.Height*uiScale;
            check(drag.Drop(dropX,dropY,target,screenScale,screenScale,uiScale,out x,out y) && !drag.Floating && !drag.Preview,
                "Release at the target edge commits the grid and clears preview.");
            var floatingGrip=new ToolbarRect(120,180,small.Width*uiScale,small.Height*uiScale);
            drag.Begin(floatingGrip.X+fraction*floatingGrip.Width,floatingGrip.Y+fraction*floatingGrip.Height,floatingGrip,false);
            float insideX=target.X+target.Width/2+(fraction-.5f)*small.Width*uiScale;
            float insideY=target.Y+target.Height/2+(fraction-.5f)*small.Height*uiScale;
            drag.Move(insideX,insideY,target,screenScale,screenScale,uiScale,out x,out y);
            check(drag.Preview && !drag.Drop(dropX+1,dropY,target,screenScale,screenScale,uiScale,out x,out y) && drag.Floating,
                "Release checks the final position and rejects a stale preview outside the target.");
            foreach(var outside in new[]{new ToolbarRect(target.X-1,target.Y,1,1),new ToolbarRect(target.Right+1,target.Y,1,1),
                new ToolbarRect(target.X,target.Y-1,1,1),new ToolbarRect(target.X,target.Bottom+1,1,1)})
            {
                drag.Move(outside.X+(fraction-.5f)*small.Width*uiScale,outside.Y+(fraction-.5f)*small.Height*uiScale,
                    target,screenScale,screenScale,uiScale,out x,out y);
                check(!drag.Preview,"Every outside target edge rejects the preview.");
            }
            drag.Move(insideX,insideY,target,screenScale,screenScale,uiScale,out x,out y);drag.Cancel();
            check(!drag.Preview && drag.Floating,"Hiding or losing capture cancels preview without committing a dock.");
            var paper=new ToolbarRect(-150,-25,876*uiScale,103*uiScale);
            var slices=new ToolbarPaperSlices(paper,180*uiScale);
            float topV=(slices.Join-slices.TopImage.Y)/slices.TopImage.Height;
            float lowerV=(slices.Join-slices.LowerImage.Y)/slices.LowerImage.Height;
            check(Math.Abs(topV-lowerV)<.00001f && Math.Abs(topV-.8f)<.00001f &&
                slices.TopImage.X==slices.LowerImage.X && slices.TopImage.Width==slices.LowerImage.Width,
                "Paper strips meet at the same source texel and retain horizontal texture coordinates.");
            check(slices.LowerImage.Bottom>=180*uiScale-.001f,"Paper reaches the bottom edge without restarting the texture.");
        }
        var dock=new ToolbarRect(100,200,600,202);
        foreach(float scale in new[]{1f,1.5f,2f,3f})
        {
            check(ToolbarLayout.WithinDetachThreshold(100+13.9f/scale,200,dock,scale,scale),"Keep dock until fourteen rendered pixels.");
            check(!ToolbarLayout.WithinDetachThreshold(100+14.1f/scale,200,dock,scale,scale),"Release beyond fourteen rendered pixels.");
        }
        foreach(DockHudDisplay display in Enum.GetValues(typeof(DockHudDisplay)))
        foreach(bool expanded in new[]{false,true})
            check(DockHudPolicy.CanCoverBuildCategories(display,expanded)==(display==DockHudDisplay.Tools && expanded),
                "Only an expanded tool surface on the actual blank HUD may cover construction categories.");
        var controls=new List<ToolbarRect>{new ToolbarRect(260,100,100,26),new ToolbarRect(450,94,35,40)};
        var band=new ToolbarRect(0,100,500,26);
        var freeParts=new List<ToolbarRect>();
        ToolbarLayout.FooterSegments(band,controls,freeParts);
        check(freeParts.Count==3,"All free intervals survive, including the small remainder previously discarded.");
        CheckCoverage(band,controls,freeParts,check);
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
        foreach(int count in new[]{1,2,3,4,5,6,7,8})
        foreach(bool details in new[]{false,true})
        foreach(bool placing in new[]{false,true})
        {
            var pose=ToolbarLayout.Arrange(new ToolbarRect(0,914,914,166),new ToolbarRect(114,936,800,144),count,details,true,default,placing);
            foreach(var wanted in new[]{new ToolbarRect(0,0,1,1),new ToolbarRect(650,850,1,1),new ToolbarRect(1600,1200,1,1)})
            {
                check(ToolbarLayout.Position(true,wanted.X,wanted.Y,pose.Bounds.Width,pose.Bounds.Height,1920,1080,
                    rosterOnly,fullScreen,4,out safe),"Floating toolbar ignores every HUD obstacle, including placement and open tools.");
                var expected=ToolbarLayout.Clamp(wanted.X,wanted.Y,pose.Bounds.Width,pose.Bounds.Height,1920,1080);
                check(safe.X==expected.X && safe.Y==expected.Y,"Only viewport edges alter a requested floating position.");
            }
        }
        var adjacent=new List<ToolbarRect>{new ToolbarRect(100,100,90,30),new ToolbarRect(140,100,110,30),new ToolbarRect(240,100,60,30)};
        ToolbarLayout.FooterSegments(band,adjacent,freeParts);
        CheckCoverage(band,adjacent,freeParts,check);
        var fullBand=new ToolbarRect(0,131,496,35);
        ToolbarLayout.FooterSegments(fullBand,controls,freeParts);
        CheckCoverage(fullBand,controls,freeParts,check);
        foreach(var part in freeParts) check(part.Bottom==166,"Every free strip reaches the screen bottom, including the old nine-unit gap.");
    }
    private static void CheckCoverage(ToolbarRect band,List<ToolbarRect> controls,List<ToolbarRect> parts,Action<bool,string> check)
    {
        foreach(var part in parts) foreach(var control in controls)
            check(!part.Intersects(control),"Coverage leaves every functional HUD surface clear.");
        for(float x=band.X+.25f;x<band.Right;x+=.5f)
        {
            bool reserved=false,covered=false;
            foreach(var c in controls) if(c.Y<band.Bottom && c.Bottom>band.Y && x>=c.X-4 && x<c.Right+4) reserved=true;
            foreach(var p in parts) if(x>=p.X && x<p.Right) covered=true;
            check(covered==!reserved,"Coverage and hit regions fill every unreserved footer interval, not just content slots.");
        }
    }
}
