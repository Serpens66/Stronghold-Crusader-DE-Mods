using System;
using System.Collections.Generic;
using Spectator;

internal static class ToolbarLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        CheckReachableDock(check);
        CheckDockControlProtection(check);
        CheckPaperFlow(check);
        CheckPaperFeather(check);
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
            var placing=ToolbarLayout.Arrange(alignment,frame,count,false,true,true);
            check(placing.Roster.Width-floating.Roster.Width==36 && placing.Control.Width==60,
                "Only the additional stop-label width extends the compact row during placement.");
            foreach(bool requestedDetails in new[]{false,true})
            foreach(bool requestedPlacement in new[]{false,true})
            {
                var unavailableDock=ToolbarLayout.Arrange(alignment,frame,count,requestedDetails,false,requestedPlacement,false);
                var unavailableFloating=ToolbarLayout.Arrange(alignment,frame,count,requestedDetails,true,requestedPlacement,false);
                var availableFloating=ToolbarLayout.Arrange(alignment,frame,count,false,true,requestedPlacement);
                var unavailableDesign=ToolbarLayout.FloatingDesignRect(count,requestedPlacement,false);
                check(unavailableDock.Control.Empty && unavailableFloating.Control.Empty &&
                    unavailableDock.Tools.Empty && unavailableFloating.Tools.Empty,
                    "Unavailable tools have no control or tool surface, even with stale expanded/placement requests or a reserved dock slot.");
                check(unavailableDock.Roster.X==compact.Roster.X && unavailableDock.Roster.Y==compact.Roster.Y &&
                    unavailableDock.Grip.Right==compact.Grip.Right && unavailableDock.Bounds.X==compact.Roster.X &&
                    unavailableDock.Bounds.Y==compact.Roster.Y && unavailableDock.Bounds.Right==compact.Grip.Right &&
                    unavailableDock.Bounds.Bottom==compact.Roster.Bottom,
                    "Removing tools preserves the dock raster/grip and excludes the entire old control gap from its bounds.");
                check(unavailableFloating.Bounds.Width==ToolbarLayout.FloatingWidth(count,requestedPlacement,false) &&
                    unavailableFloating.Bounds.Width==unavailableFloating.Roster.Width && unavailableFloating.Bounds.Height==40 &&
                    availableFloating.Roster.Width-unavailableFloating.Roster.Width==availableFloating.Control.Width+2,
                    "The floating row releases the unavailable control and its gap instead of retaining blank paper or hit bounds.");
                check(unavailableDesign.X==design.X && unavailableDesign.Right+2==unavailableFloating.Roster.Right &&
                    ToolbarLayout.OwnerCell(count-1,true).Right+2==unavailableDesign.X &&
                    !unavailableFloating.Grip.Intersects(unavailableDesign),
                    "The design button remains beside the portraits and ends the compact row when tools are unavailable.");
                check(unavailableFloating.Grip.X==floating.Grip.X && unavailableFloating.Grip.Width==floating.Grip.Width &&
                    unavailableFloating.Grip.Height==floating.Grip.Height,
                    "Tool availability never removes or expands the floating drag grip.");
            }

            check(floating.Roster.Width == ToolbarLayout.FloatingWidth(count,false) && floating.Roster.Height == 40 && floating.Tools.Y == floating.Roster.Bottom &&
                floating.Control.Right <= floating.Roster.Right && floating.Control.Bottom <= floating.Roster.Bottom,
                "Floating portraits/control share one header, directly above the tool surface.");
            check(Math.Abs(compact.Control.X + compact.Control.Width/2 - (compact.Roster.X + compact.Roster.Width/2))<.001f &&
                Math.Abs(compact.Roster.Y-compact.Control.Bottom)<.001f &&
                compact.Control.Width==60*scale && compact.Control.Height==22*scale,
                "Dock control touches the top of every unchanged two-column raster, centered with zero gap.");
            check(compact.Control.X==expanded.Control.X && compact.Control.Y==expanded.Control.Y &&
                compact.Bounds.Y==compact.Control.Y && compact.Bounds.Bottom==compact.Roster.Bottom &&
                compact.Bounds.X==compact.Roster.X && compact.Bounds.Right==compact.Grip.Right &&
                !compact.Control.Intersects(frame) && !compact.Control.Intersects(expanded.Tools),
                "Control participates in clamp bounds without reserving any Vanilla build-menu slot.");
            check(compact.Grip.Right == frame.X && compact.Grip.Y == compact.Roster.Y && compact.Grip.Height == compact.Roster.Height && compact.Grip.Width==8*scale,
                "The existing gap is the full-height dock grip; no separate side strip remains.");
            var placed = ToolbarLayout.Arrange(alignment,frame,count,false,true,true);
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
        }
        var dock=new ToolbarRect(100,200,600,202);
        foreach(float scale in new[]{1f,1.5f,2f,3f})
        {
            check(ToolbarLayout.WithinDetachThreshold(100+13.9f/scale,200,dock,scale,scale),"Keep dock until fourteen rendered pixels.");
            check(!ToolbarLayout.WithinDetachThreshold(100+14.1f/scale,200,dock,scale,scale),"Release beyond fourteen rendered pixels.");
        }
        foreach(DockHudDisplay display in Enum.GetValues(typeof(DockHudDisplay)))
        foreach(bool expanded in new[]{false,true})
        foreach(bool blank in new[]{false,true})
            check(DockHudPolicy.CanCoverBuildCategories(display,expanded,blank)==(display==DockHudDisplay.Tools && expanded && blank),
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
            var pose=ToolbarLayout.Arrange(new ToolbarRect(0,914,914,166),new ToolbarRect(114,936,800,144),count,details,true,placing);
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
        foreach(float scale in new[]{.75f,1f,1.5f,2f,3f}) foreach(int count in new[]{1,2,3,4,5,6,7,8})
        foreach(bool orders in new[]{false,true})
        {
            var alignment=new ToolbarRect(0,0,914*scale,166*scale);
            var frame=new ToolbarRect(114*scale,22*scale,800*scale,144*scale);
            var pose=ToolbarLayout.Arrange(alignment,frame,count,true);
            var body=new ToolbarRect(pose.Tools.X,pose.Tools.Y,pose.Tools.Width,(ToolbarLayout.Header+ToolbarLayout.Body)*scale);
            var footer=new ToolbarRect(body.X,body.Bottom,body.Width,alignment.Bottom-body.Bottom);
            var reserved=new List<ToolbarRect>{new ToolbarRect(553*scale,131*scale,30*scale,35*scale)};
            ToolbarLayout.FooterContent(footer,reserved,(orders?ToolbarLayout.OrderSummaryWidth:ToolbarLayout.GoodsWidth)*scale,freeParts);
            check(freeParts.Count==1&&freeParts[0].Bottom==alignment.Bottom,
                "Only the active feedback column gets a paper/hit footer, extending to the bottom edge.");
            var oldSlot=new ToolbarRect(491*scale,142*scale,60*scale,22*scale);
            foreach(var painted in new[]{body,pose.Roster,pose.Control,pose.Grip})
                check(!painted.Intersects(oldSlot),"No body, roster or control paper/hit rectangle reclaims the former Tools slot.");
            foreach(var cover in freeParts)check(!cover.Intersects(oldSlot),"The open Goods/Orders footer leaves the former slot free of paper and click covers.");
            reserved.Add(new ToolbarRect(footer.X+100*scale,footer.Y,30*scale,footer.Height));
            ToolbarLayout.FooterContent(footer,reserved,308*scale,freeParts);
            check(freeParts.Count==1,"Fragmented footer space paints only its single consumed interval.");
            foreach(var cover in freeParts)foreach(var control in reserved)check(!cover.Intersects(control),"Consumed footer excludes all native controls.");
        }
    }
    private static void CheckPaperFlow(Action<bool,string> check)
    {
        foreach(float scale in new[]{.75f,1f,1.5f,2f,3f}) foreach(int count in new[]{1,2,3,4,5,6,7,8})
        foreach(bool floating in new[]{false,true}) foreach(bool orders in new[]{false,true})
        {
            var alignment=new ToolbarRect(0,0,914*scale,166*scale);
            var pose=ToolbarLayout.Arrange(alignment,new ToolbarRect(114*scale,22*scale,800*scale,144*scale),count,true,floating);
            float units=floating?1:scale;
            var body=new ToolbarRect((pose.Tools.X-pose.Bounds.X)/units,(pose.Tools.Y-pose.Bounds.Y)/units,
                pose.Tools.Width/units,ToolbarLayout.Header+ToolbarLayout.Body);
            var footer=new ToolbarRect(body.X,body.Bottom,floating?body.Width:orders?ToolbarLayout.OrderSummaryWidth:ToolbarLayout.GoodsWidth,
                pose.Bounds.Height/units-body.Bottom);
            float width=pose.Bounds.Width/units,height=pose.Bounds.Height/units;
            var topImage=ToolbarLayout.PaperTexture(body,width,height);
            var lowerImage=ToolbarLayout.PaperTexture(footer,width,height);
            check(topImage.Width==width&&lowerImage.Width==width&&topImage.Height==height&&lowerImage.Height==height,
                "Body and narrow/wide footer retain one common root-sized parchment scale in every toolbar form.");
            foreach(float fraction in new[]{0f,.25f,.5f,.75f,1f})
            {
                float x=footer.X+footer.Width*fraction,y=footer.Y;
                float topU=(x-body.X-topImage.X)/topImage.Width,topV=(y-body.Y-topImage.Y)/topImage.Height;
                float lowerU=(x-footer.X-lowerImage.X)/lowerImage.Width,lowerV=(y-footer.Y-lowerImage.Y)/lowerImage.Height;
                check(Math.Abs(topU-lowerU)<.00001f&&Math.Abs(topV-lowerV)<.00001f,
                    "Both sides of every actual body/footer seam sample the identical texture coordinate, without a pattern restart.");
                check(Math.Abs(lowerU-x/width)<.00001f&&Math.Abs(lowerV-y/height)<.00001f,
                    "Clipping preserves the global toolbar texture coordinates rather than independently stretching each used surface.");
            }
        }
    }
    private static float PaperAlpha(ToolbarPaperFade fade, float x, float y)
    {
        if(fade==ToolbarPaperFade.None)return 1;
        ToolbarLayout.PaperFadeGradient(fade,out float endX,out float endY,out float hold);
        float t=(x*endX+y*endY)/(endX*endX+endY*endY);
        return Math.Max(0,Math.Min(1,(1-t)/(1-hold)));
    }
    private static void CheckPaperFeather(Action<bool,string> check)
    {
        foreach(float scale in new[]{.75f,1f,1.5f,2f,3f}) foreach(int count in new[]{1,2,3,4,5,6,7,8})
        foreach(bool orders in new[]{false,true})
        {
            var pose=ToolbarLayout.Arrange(new ToolbarRect(0,0,914*scale,166*scale),
                new ToolbarRect(114*scale,22*scale,800*scale,144*scale),count,true);
            var body=new ToolbarRect((pose.Tools.X-pose.Bounds.X)/scale,(pose.Tools.Y-pose.Bounds.Y)/scale,496,88);
            var footer=new ToolbarRect(body.X,body.Bottom,orders?352:308,(pose.Bounds.Bottom-pose.Tools.Y)/scale-88);
            var pieces=new List<ToolbarRect>{footer,body};
            var visual=new List<ToolbarPaperRegion>();
            ToolbarLayout.PaperRegions(pieces,true,visual);
            check(visual.Count==6,"Only the joined body/footer background is split into bounded opaque and feathered pieces.");
            var oldSlot=new ToolbarRect(491-pose.Bounds.X/scale,142-pose.Bounds.Y/scale,60,22);
            float total=0;int fades=0;
            foreach(var part in visual)
            {
                var area=part.Bounds;total+=area.Width*area.Height;
                check(area.X>=body.X&&area.Right<=body.Right&&area.Y>=body.Y&&area.Bottom<=footer.Bottom,
                    "Background feathering never expands the paper footprint or reclaims other HUD space.");
                check(pieces.Exists(original=>area.X>=original.X&&area.Y>=original.Y&&area.Right<=original.Right&&area.Bottom<=original.Bottom)&&
                    !area.Intersects(oldSlot),"Each feather region remains inside one original paper piece and leaves the former Tools slot clear.");
                if(part.Fade!=ToolbarPaperFade.None)
                {
                    fades++;
                    check(part.Fade==ToolbarPaperFade.Bottom?area.Height==10:area.Width==10,
                        "Only a ten-UI-unit exposed notch edge is feathered.");
                }
                var image=ToolbarLayout.PaperTexture(area,pose.Bounds.Width/scale,pose.Bounds.Height/scale);
                check(Math.Abs(area.X+image.X)<.0001f&&Math.Abs(area.Y+image.Y)<.0001f,
                    "Every feathered subregion retains the continuous global texture transform.");
            }
            check(fades==3&&Math.Abs(total-body.Width*body.Height-footer.Width*footer.Height)<.1f,
                "Visual regions cover exactly the existing paper areas, without adding an input or paint region.");
            for(int i=0;i<visual.Count;i++)for(int j=i+1;j<visual.Count;j++)
                check(!visual[i].Bounds.Intersects(visual[j].Bounds),"Internal feather splits do not overlap and darken the shared texture.");
            foreach(float fraction in new[]{0f,.1f,.25f,.5f,.75f,.9f,1f})
            {
                check(PaperAlpha(ToolbarPaperFade.Corner,fraction,0)==1&&PaperAlpha(ToolbarPaperFade.Corner,0,fraction)==1,
                    "Corner mask meets both opaque body regions without an interior seam.");
                check(Math.Abs(PaperAlpha(ToolbarPaperFade.Corner,fraction,1)-PaperAlpha(ToolbarPaperFade.Right,fraction,.5f))<.0001f&&
                    Math.Abs(PaperAlpha(ToolbarPaperFade.Corner,1,fraction)-PaperAlpha(ToolbarPaperFade.Bottom,.5f,fraction))<.0001f,
                    "Corner opacity matches footer-right and exposed body-bottom gradients at every join.");
            }
            ToolbarLayout.PaperRegions(pieces,false,visual);
            check(visual.Count==pieces.Count&&visual.TrueForAll(p=>p.Fade==ToolbarPaperFade.None),
                "Floating/classic paper bypasses all feathering and retains its existing visual layers.");
        }
    }
    private static void CheckDockControlProtection(Action<bool,string> check)
    {
        foreach(float scale in new[]{.75f,1f,1.5f,2f,3f}) foreach(int count in new[]{1,2,3,4,5,6,7,8})
        foreach(bool nativeFrameVisible in new[]{false,true})
        {
            float vw=1920*scale,vh=1080*scale;
            var alignment=new ToolbarRect(vw-914*scale,vh-166*scale,914*scale,166*scale);
            // When HUD_Main is hidden, production reconstructs this same native frame
            // from the alignment fallback; a competing troop/building HUD owns its area.
            var frame=new ToolbarRect(alignment.X+114*scale,alignment.Bottom-144*scale,800*scale,144*scale);
            var pose=ToolbarLayout.Arrange(alignment,frame,count,false);
            var solids=new List<ToolbarRect>();
            foreach(var part in new[]{pose.Roster,pose.Grip,pose.Control})
                solids.Add(new ToolbarRect(part.X-pose.Bounds.X,part.Y-pose.Bounds.Y,part.Width,part.Height));
            var controls=new List<ToolbarRect>{
                new ToolbarRect(alignment.X+380*scale,vh-26*scale,110*scale,26*scale),
                new ToolbarRect(alignment.X+553*scale,vh-35*scale,30*scale,35*scale),
                new ToolbarRect(alignment.X+588*scale,vh-35*scale,30*scale,35*scale),
                new ToolbarRect(vw-128*scale,vh-128*scale,128*scale,128*scale)};
            // Both the genuine and reconstructed frame must leave the fixed roster
            // control untouched, rather than make the entire toolbar disappear.
            if(nativeFrameVisible) controls.Add(frame);
            else controls.Add(new ToolbarRect(frame.X,frame.Y,frame.Width,frame.Height));
            check(ToolbarLayout.Position(false,pose.Bounds.X,pose.Bounds.Y,pose.Bounds.Width,pose.Bounds.Height,
                vw,vh,solids,controls,4*scale,out var safe) && Math.Abs(safe.X-pose.Bounds.X)<.001f &&
                Math.Abs(safe.Y-pose.Bounds.Y)<.001f,
                "Docked control remains fixed and clear with visible/hidden build frame and protected Vanilla footer controls.");
            controls.Clear();controls.Add(pose.Control);
            bool canStay=ToolbarLayout.Position(false,pose.Bounds.X,pose.Bounds.Y,pose.Bounds.Width,pose.Bounds.Height,
                vw,vh,solids,controls,4*scale,out safe) && Math.Abs(safe.X-pose.Bounds.X)<.001f && Math.Abs(safe.Y-pose.Bounds.Y)<.001f;
            check(!canStay,"The control itself participates in protected-surface collision checks, including the former empty space above portraits.");
        }
    }
    private static void CheckReachableDock(Action<bool,string> check)
    {
        foreach(float uiScale in new[]{.75f,1f,1.5f,2f,3f})
        foreach(float screenScale in new[]{1f,1.5f,2f})
        foreach(int count in new[]{1,8}) foreach(bool expanded in new[]{false,true})
        {
            const float viewportHeight=1080;
            var alignment=new ToolbarRect(400,viewportHeight-166*uiScale,914*uiScale,166*uiScale);
            var frame=new ToolbarRect(400+114*uiScale,viewportHeight-144*uiScale,800*uiScale,144*uiScale);
            var pose=ToolbarLayout.Arrange(alignment,frame,count,expanded,true);
            var original=ToolbarLayout.DockDropTarget(alignment,frame);
            float height=pose.Bounds.Height*uiScale;
            var target=ToolbarLayout.ReachableDockDropTarget(original,height,uiScale,viewportHeight,screenScale);
            check(target.X==original.X && target.Width==original.Width,"screen-edge docking never widens the horizontal snap zone");
            var clamped=ToolbarLayout.Clamp(original.X,viewportHeight,pose.Bounds.Width*uiScale,height,1920,viewportHeight);
            check(clamped.Bottom<=viewportHeight+.001f,"expanded tools remain entirely on screen during docking");
            var grip=ToolbarLayout.FloatingGripRect;
            float centreOffset=(grip.Y+grip.Height/2)*uiScale;
            float centreY=Math.Min(original.Bottom,Math.Max(original.Y,clamped.Y+centreOffset));
            var drag=new ToolbarDragAnchor();
            drag.Begin(0,0,new ToolbarRect(0,0,grip.Width*uiScale,grip.Height*uiScale),false);
            float px=original.X+original.Width/2-grip.Width*uiScale/2;
            float py=centreY-grip.Height*uiScale/2;
            drag.Move(px,py,target,screenScale,screenScale,uiScale,out _,out _);
            check(drag.Preview,"clamped expanded/collapsed row can preview docking with sparse/full roster");
            check(drag.Drop(px,py,target,screenScale,screenScale,uiScale,out _,out _),"release uses the same reachable zone as preview");

            float newCentre=(target.Y+original.Y)/2;
            check(newCentre<original.Y && newCentre>=centreOffset && newCentre<=clamped.Y+centreOffset,
                "real expanded/collapsed geometry gains a reachable snap band above the original dock zone");
            drag.Begin(0,0,new ToolbarRect(0,0,grip.Width*uiScale,grip.Height*uiScale),false);
            py=newCentre-grip.Height*uiScale/2;
            drag.Move(px,py,target,screenScale,screenScale,uiScale,out _,out _);
            check(drag.Preview && drag.Drop(px,py,target,screenScale,screenScale,uiScale,out _,out _),
                "preview and drop work in the added band with the actual current toolbar height");

            // A target entirely below the reachable grip range must remain reachable at the limit.
            float tallHeight=220*uiScale;
            float limit=viewportHeight-tallHeight+centreOffset;
            target=ToolbarLayout.ReachableDockDropTarget(original,tallHeight,uiScale,viewportHeight,screenScale);
            check(Math.Abs(target.Y-Math.Max(centreOffset,limit-original.Height))<.01f,
                "unreachable dock target retains its height within the available viewport grip range");
            drag.Begin(0,0,new ToolbarRect(0,0,grip.Width*uiScale,grip.Height*uiScale),false);
            py=limit-grip.Height*uiScale/2;
            drag.Move(px,py,target,screenScale,screenScale,uiScale,out _,out _);
            check(drag.Preview,"expanded window need not leave the viewport to reach a low dock target");
            drag.Move(px,target.Y-1-grip.Height*uiScale/2,target,screenScale,screenScale,uiScale,out _,out _);
            check(!drag.Preview,"above the projected band must not snap");
            check(!drag.Drop(original.X-1-grip.Width*uiScale/2,py,target,screenScale,screenScale,uiScale,out _,out _),"horizontal miss rejects a stale projected preview");
            target=ToolbarLayout.ReachableDockDropTarget(original,viewportHeight,uiScale,viewportHeight,screenScale);
            check(Math.Abs(target.Y-(centreOffset-14/screenScale))<.01f,
                "a full-height window projects to a line with fourteen physical pixels of tolerance");
        }
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
