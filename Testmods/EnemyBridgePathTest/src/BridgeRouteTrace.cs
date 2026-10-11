using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace EnemyBridgePathTest
{
    // Borrowed public views are retained only while their slot's GlobalId matches.
    // No setter, search, order or native hook is used by this observer.
    internal sealed unsafe class BridgeRouteTrace
    {
        internal sealed class Bridge
        {
            internal int Id, Parent; internal uint Global, ParentGlobal;
            internal string ParentLink;
            internal readonly HashSet<int> Deck=new HashSet<int>(), Gate=new HashSet<int>();
            internal static int XY(int x,int y) => y*800+x;
        }
        internal readonly struct Header : IEquatable<Header>
        {
            internal readonly uint Global;
            internal readonly int X,Y,OriginX,OriginY,Length,Cursor,Flags,Substep,NextX,NextY,Command,TargetX,TargetY,SegmentX,SegmentY;
            internal Header(GameUnit* u) : this(u->r_GlobalId,u->r_CurrentTilePositionX,u->r_CurrentTilePositionY,
                u->r_PreviousTilePositionX,u->r_PreviousTilePositionY,u->r_PathPlanLength,u->r_CurrentPathPlanIndex,
                u->r_PathPlanStateBitFlags,u->r_MovementSubstep,u->r_NextTilePositionX2,u->r_NextTilePositionY2,
                u->r_AI_LastIssuedTribeCommand,u->r_ContextTargetTileX,u->r_ContextTargetTileY,u->r_TargetTilePositionX,u->r_TargetTilePositionY) { }
            internal Header(uint g,int x,int y,int ox,int oy,int length,int cursor,int flags=2,int substep=0,int nx=0,int ny=0,int command=3,int tx=0,int ty=0,int sx=-1,int sy=-1)
            {Global=g;X=x;Y=y;OriginX=ox;OriginY=oy;Length=length;Cursor=cursor;Flags=flags;Substep=substep;NextX=nx;NextY=ny;Command=command;TargetX=tx;TargetY=ty;SegmentX=sx;SegmentY=sy;}
            public bool Equals(Header h) => Global==h.Global&&X==h.X&&Y==h.Y&&OriginX==h.OriginX&&OriginY==h.OriginY&&Length==h.Length&&Cursor==h.Cursor&&Flags==h.Flags&&Substep==h.Substep&&NextX==h.NextX&&NextY==h.NextY&&Command==h.Command&&TargetX==h.TargetX&&TargetY==h.TargetY&&SegmentX==h.SegmentX&&SegmentY==h.SegmentY;
        }
        internal sealed class Track
        {
            internal int Unit,Player,Tribe,ControlRaw;
            internal uint Global;
            internal long Command,ParentEvent,Consumer,PlanningRoot,CandidatePlan,Session,Definition,LastProgress,LastScan,CommandStarted,Decision,Binding;
            internal int Phase;
            internal bool HasHeader,HasPlan,Stall,Relevant,PreCaptured,ReturnKnown,CompareCommand,PathChangedByCommand;
            internal Header BeforeCommand, InvalidHeader;
            internal bool HasInvalidHeader, BindingPublished;
            internal long ReturnRaw;
            internal Header Last;
            internal int OriginX,OriginY,Length,SegmentX,SegmentY;
            internal int FirstDeck=-1,LastDeck=-1,LastProgressClass=-1;
            internal PathContent Content;
            internal long Repeats,FirstRepeatClock,LastRepeatClock;internal int FirstRepeatCursor,LastRepeatCursor;
            internal bool HasObservation;internal Header ObservedHeader;
            internal readonly byte[] Bytes=new byte[1000],BeforeBytes=new byte[1000],SampleBytes=new byte[1000];
                        internal GameUnitPathPlanView View;
        }
        internal sealed class PathContent
        {
            internal Header Header; internal byte[] Bytes; internal long Id,Geometry;
            internal bool Complete,Relevant; internal int First=-1,Last=-1;
            internal string Bridges; internal Bridge[] Touched;
        }
        private readonly Dictionary<long,List<PathContent>> contents=new Dictionary<long,List<PathContent>>();
        private int contentCount;
        private long bindingId;
        private readonly StringBuilder bindingRows=new StringBuilder(),observationRows=new StringBuilder();
        private int bindingCount,observationCount;
        private long repeats;
        private static long Mix(long hash,int value) => unchecked((hash^value)*1099511628211L);
        private static long GeometryKey(Bridge[] bridges)
        {
            long hash=17;foreach(var b in bridges) {hash=Mix(hash,b.Id);hash=Mix(hash,(int)b.Global);hash=Mix(hash,b.Parent);hash=Mix(hash,(int)b.ParentGlobal);
                foreach(int tile in b.Deck)hash=Mix(hash,tile);hash=Mix(hash,-1);foreach(int tile in b.Gate)hash=Mix(hash,tile);}
            return hash;
        }
        private void PublishBinding(Track t)
        {
            if(t.BindingPublished)return;t.BindingPublished=true;t.Binding=++bindingId;
            bindingRows.Append(t.Binding).Append('/').Append(t.Unit).Append('/').Append(t.Global).Append('/').Append(t.Player).Append('/').Append(t.ControlRaw).Append('/').Append(t.Tribe).Append('/').Append(t.Command).Append('/').Append(t.ParentEvent).Append('/').Append(t.Consumer).Append('/').Append(t.PlanningRoot).Append('/').Append(t.Phase).Append('/').Append(t.CandidatePlan).Append('/').Append(t.Decision).Append('/').Append(t.CommandStarted).Append(';');
            if(++bindingCount==32)FlushBindings();
        }
        private void FlushBindings()
        {
            if(bindingCount==0)return;
            emit("route-bindings","columns=binding/unit/global/player/controlRaw/tribe/commandOp/parentEvent/consumer/planningRoot/entryPhase/candidatePlan/decisionState/commandClock,rows=["+bindingRows+"],candidateLink=chronological-only,decisionLink=retained-completed-caller-state");bindingRows.Clear();bindingCount=0;
        }
        private static int ProgressClass(Track t,Header h,Bridge[] bridges)
        {
            if(!t.Relevant)return 0;
            foreach(var b in bridges)if(b.Deck.Contains(Bridge.XY(h.X,h.Y)))return 2;
            if(h.Cursor<=t.FirstDeck)return 1;
            if(h.Cursor<=t.LastDeck+1)return 3;
            return 4; // Cursor passed the deck, not proof that the unit executed it.
        }
        private void Observation(Track t,Header h,long now,bool changed,Bridge[] bridges)
        {
            PublishBinding(t);int progress=ProgressClass(t,h,bridges);
            bool context=t.HasObservation&&(h.Command!=t.ObservedHeader.Command||h.TargetX!=t.ObservedHeader.TargetX||h.TargetY!=t.ObservedHeader.TargetY);
            if(!changed&&!context&&progress==t.LastProgressClass) {if(t.Repeats++==0) {t.FirstRepeatClock=now;t.FirstRepeatCursor=h.Cursor;}t.LastRepeatClock=now;t.LastRepeatCursor=h.Cursor;return;}
            FlushRepeats(t);t.LastProgressClass=progress;t.HasObservation=true;t.ObservedHeader=h;
            long state=t.Relevant||!t.Content.Complete?capture():0;
            observationRows.Append(t.Binding).Append('/').Append(t.Definition).Append('/').Append(now).Append('/').Append(h.X).Append('/').Append(h.Y).Append('/').Append(h.Cursor).Append('/').Append(h.Flags).Append('/').Append(h.Substep).Append('/').Append(t.ReturnKnown?1:0).Append('/').Append(t.ReturnRaw).Append('/').Append(t.PreCaptured&&t.ReturnKnown?(t.PathChangedByCommand?1:0):-1).Append('/').Append(state).Append('/').Append(progress).Append('/').Append(h.Command).Append('/').Append(h.TargetX).Append('/').Append(h.TargetY).Append(';');
            if(++observationCount==32)Flush();
        }
        private readonly StringBuilder repeatRows=new StringBuilder();private int repeatCount;
        private readonly long[] backgroundRepeats=new long[9];
        private readonly StringBuilder replacementRows=new StringBuilder(),backgroundPathRows=new StringBuilder();
        private int replacementCount,backgroundPathCount;
        private void Replaced(Track t,long command)
        {
            replacementRows.Append(t.Unit).Append('/').Append(t.Global).Append('/').Append(t.Command).Append('/').Append(command).Append('/').Append(Stopwatch.GetTimestamp()).Append(';');
            if(++replacementCount==32)FlushReplacements();
        }
        private void FlushReplacements()
        {
            if(replacementCount==0)return;
            emit("route-replacement-batch","columns=unit/global/oldCommand/newCommand/observationClock,rows=["+replacementRows+"],samePlanMustBeObserved=True,envelopeTiming=batch-flush");replacementRows.Clear();replacementCount=0;
        }
        private readonly StringBuilder relevantPathRows=new StringBuilder();private int relevantPathCount;
        private void FlushRelevantPaths()
        {
            if(relevantPathCount==0)return;
            emit("stored-route-definition-batch","schema=1,columns=definition/clock/originX/originY/length/cursor/flags/substep/decodedX/decodedY/segmentX/segmentY/bridges/packedHex,fieldSeparator=colon,rowSeparator=pipe,rows=["+relevantPathRows+"],complete=True,format=2,envelopeTiming=batch-flush,physicalAtCapture=not-recorded,threadAtCapture=not-recorded;binding-and-synchronous-observation-retain-context");relevantPathRows.Clear();relevantPathCount=0;
        }
        private void FlushBackgroundPaths()
        {
            if(backgroundPathCount==0)return;
            emit("stored-route-background-batch","columns=definition/captureClock/originX/originY/length/cursor/flags/substep/decodedX/decodedY/segmentX/segmentY,rows=["+backgroundPathRows+"],complete=True,bridges=[],packedHex=,execution=not-proven,envelopeTiming=batch-flush,physicalAtCapture=not-recorded");backgroundPathRows.Clear();backgroundPathCount=0;
        }
        private void FlushRepeats(Track t)
        {
            if(t.Repeats==0)return;
            if(t.Content!=null&&t.Content.Complete&&!t.Content.Relevant)
            {backgroundRepeats[(uint)t.Player<9?t.Player:0]+=t.Repeats;t.Repeats=0;return;}
            repeatRows.Append(t.Binding).Append('/').Append(t.Definition).Append('/').Append(t.Repeats).Append('/').Append(t.FirstRepeatClock).Append('/').Append(t.LastRepeatClock).Append('/').Append(t.FirstRepeatCursor).Append('/').Append(t.LastRepeatCursor).Append(';');t.Repeats=0;
            if(++repeatCount==32)FlushRepeatRows();
        }
        private void FlushRepeatRows()
        {if(repeatCount==0)return;emit("route-repeat-batch","columns=binding/pathDefinition/count/firstClock/lastClock/firstCursor/lastCursor,rows=["+repeatRows+"],coverage=aggregate-not-movement-proof");repeatRows.Clear();repeatCount=0;}
        internal void Flush()
        {
            lock(gate) {foreach(var track in tracks.Values)FlushRepeats(track);FlushRepeatRows();FlushReplacements();FlushBackgroundPaths();FlushRelevantPaths();
                var counts=new StringBuilder();for(int player=0;player<9;player++)if(backgroundRepeats[player]!=0) {counts.Append(player).Append('/').Append(backgroundRepeats[player]).Append(';');backgroundRepeats[player]=0;}
                if(counts.Length!=0)emit("route-background-repeat-batch","columns=player/count,rows=["+counts+"],coverage=complete-decoded-no-deck-observation-attempts-not-movement");
                FlushBindings();if(observationCount==0)return;
                emit("route-observations","columns=binding/pathDefinition/observationClock/x/y/cursor/flags/substep/returnKnown/commandReturn/pathChangedSinceCommandPre/state/progressClass/issuedCommand/contextX/contextY,progressClasses=0-no-deck:1-before-cursor:2-current-position-on-deck:3-cursor-boundary-uncertain:4-cursor-past-not-execution,rows=["+observationRows+"]");observationRows.Clear();observationCount=0;}
        }
        private readonly object gate=new object();
        private readonly Dictionary<int,Track> tracks=new Dictionary<int,Track>();
        private readonly Action<string,string> emit;
        private readonly Func<Bridge[]> geometry;
        private readonly Func<long> capture;
        private readonly Action<int,uint,int,long,long,long,long,bool> evidence;
        private readonly Action<long,int,long,long> unboundChange;
        private readonly Func<int,IntPtr> testUnit;
        private readonly Func<int,GameUnitPathPlanView> testView;
        private bool movementConfirmed;
        private long session,definition,calls,commandReturns,scans,changes,readTicks,bytesRead;
        private long lastCalls,lastCommandReturns,lastScans,lastChanges,lastReadTicks,lastBytes;
        internal BridgeRouteTrace(Action<string,string> emit,Func<Bridge[]> geometry,Func<long> capture,Func<int,IntPtr> testUnit=null,Func<int,GameUnitPathPlanView> testView=null,Action<int,uint,int,long,long,long,long,bool> evidence=null,Action<long,int,long,long> unboundChange=null)
        {this.emit=emit;this.geometry=geometry;this.capture=capture;this.testUnit=testUnit;this.testView=testView;this.evidence=evidence;this.unboundChange=unboundChange;}
        internal void ForceReobserve() {lock(gate)foreach(var track in tracks.Values)track.HasPlan=false;}
        internal void Begin(long value) {lock(gate) {Flush();tracks.Clear();contents.Clear();contentCount=0;session=value;movementConfirmed=false;emit("route-format","format=2,completeness=decoded-stored-transitions,consistency=single-copy-header-stability-only,bridgeColumns=building/global/firstStep/lastStep/entryX/entryY/exitX/exitY/classification,packedEncoding=low-nibble-first,parentAssociation=candidate-unless-native-link,execution=not-proven,coverage=stored-plan-only");}}
        internal string Summary() {lock(gate)return "movementCallbacks="+calls+",commandReturns="+commandReturns+",tracked="+tracks.Count+",pathScans="+scans+",pathChanges="+changes+",routeReadMs="+(readTicks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);}
        internal string Interval()
        {
            lock(gate)
            {
                string value="movementCallbacks="+(calls-lastCalls)+",commandReturns="+(commandReturns-lastCommandReturns)+",pathScans="+(scans-lastScans)+",pathChanges="+(changes-lastChanges)+",packedBytesRead="+(bytesRead-lastBytes)+",tracked="+tracks.Count+",readAndCaptureMs="+((readTicks-lastReadTicks)*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",mayOverlapCaptureMs=True";
                lastCalls=calls;lastCommandReturns=commandReturns;lastScans=scans;lastChanges=changes;lastReadTicks=readTicks;lastBytes=bytesRead;return value;
            }
        }
        internal void End() {lock(gate) {Flush();emit("route-coverage",Summary()+",stage=session-end,execution=only-observed-transitions");tracks.Clear();session=0;}}
        internal bool Bind(int unit,uint global,int player,int tribe,long command,long parentEvent,long consumer,long planningRoot,long plan,int phase,int controlRaw=0,long decision=0)
        {
            lock(gate)
            {
                if(session==0||unit<=0||global==0)return false;
                if(tracks.TryGetValue(unit,out Track old))
                {
                    if(old.Global!=global) {emit("route-identity-gap","unit="+unit+",oldGlobal="+old.Global+",newGlobal="+global+",oldCommand="+old.Command);FlushRepeats(old);tracks.Remove(unit);}
                    else if(old.Command==command)return false;
                    else Replaced(old,command);
                }
                var track=old!=null&&old.Global==global?old:new Track {Unit=unit,Global=global,LastProgress=Stopwatch.GetTimestamp()};
                track.Player=player;track.ControlRaw=controlRaw;track.Tribe=tribe;track.Command=command;track.ParentEvent=parentEvent;track.Consumer=consumer;track.PlanningRoot=planningRoot;track.CandidatePlan=plan;track.Phase=phase;track.Session=session;track.Decision=decision;track.CommandStarted=Stopwatch.GetTimestamp();track.BindingPublished=false;track.Stall=false;track.LastProgressClass=-1;
                FlushRepeats(track);track.HasObservation=false;
                track.HasPlan=false;track.PreCaptured=false;track.ReturnKnown=false;track.CompareCommand=false;track.PathChangedByCommand=false;
                tracks[unit]=track;
                try
                {
                    GameUnit* before=Lookup(unit);
                    if(before!=null&&before->r_GlobalId==global)
                    {
                        track.View=PathView(unit);track.BeforeCommand=new Header(before);
                        int count=(track.BeforeCommand.Length+1)/2;
                        if(track.View!=null&&count<=1000) {var packed=track.View.PackedBytes;for(int i=0;i<count;i++)track.BeforeBytes[i]=packed[i];track.PreCaptured=new Header(before).Equals(track.BeforeCommand);track.CompareCommand=track.PreCaptured;
                            if(!track.PreCaptured)emit("route-unresolved",Link(track)+",stage=command-pre,reason=header-changed-during-read");}
                    }
                }
                catch(Exception error) {emit("route-capture-error",Link(track)+",stage=command-pre,type="+error.GetType().Name);}
                return true;
            }
        }
        internal bool IsTracked(int unit) {lock(gate)return tracks.ContainsKey(unit);}
        private GameUnit* Lookup(int unit)
        {
            if(testUnit!=null)return (GameUnit*)testUnit(unit);
            var api=GameUnitManagerAPI.Instance;
            return APIShared.UnitAccess.TryGetById(api, unit,out GameUnit* value, out _) ? value : null;
        }
        private GameUnitPathPlanView PathView(int unit)
        {
            if(testView!=null)return testView(unit);
            GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(unit,out GameUnitPathPlanView view);return view;
        }
        internal void Observe(int unit,bool post,bool commandReturn=false,long returnRaw=0,long expectedCommand=0,bool bound=true)
        {
            long started=Stopwatch.GetTimestamp();
            lock(gate)
            {
                if(commandReturn)commandReturns++;else calls++;
                if(session==0||!tracks.TryGetValue(unit,out Track track))return;
                try
                {
                    if(commandReturn&&bound&&expectedCommand!=0&&expectedCommand!=track.Command) {emit("route-command-boundary",Link(track)+",returningCommand="+expectedCommand+",reason=intervening-command,return="+returnRaw);return;}
                    GameUnit* value=Lookup(unit);
                    if(commandReturn&&bound) {track.ReturnKnown=true;track.ReturnRaw=returnRaw;track.HasPlan=false;}
                    if(value==null||value->r_GlobalId!=track.Global)
                    {emit("route-identity-gap",Link(track)+",reason=missing-or-reused-unit");FlushRepeats(track);tracks.Remove(unit);return;}
                    if(track.View==null)track.View=PathView(unit);
                    if(track.View==null)
                    {emit("route-unresolved",Link(track)+",reason=public-path-view-unavailable");return;}
                    Header h=new Header(value);
                    if(!commandReturn&&!movementConfirmed) {movementConfirmed=true;emit("movement-publisher-confirmed",Link(track)+",position="+h.X+"/"+h.Y+",cursor="+h.Cursor+",eventPhase="+(post?"post":"pre")+",publisher=installed-OnUnitMovement,afterStartupCleanup=True");}
                    // Every callback observes scalar progress. Full bytes are read on header
                    // changes/command return and once per second for same-header replacement.
                    bool scan=commandReturn||!track.HasHeader||h.OriginX!=track.Last.OriginX||h.OriginY!=track.Last.OriginY||h.Length!=track.Last.Length||h.Cursor!=track.Last.Cursor||h.Flags!=track.Last.Flags||h.SegmentX!=track.Last.SegmentX||h.SegmentY!=track.Last.SegmentY||started-track.LastScan>=Stopwatch.Frequency;
                    if(scan)
                    {
                        int count=(h.Length+1)/2;
                        if(count<=1000) {var packed=track.View.PackedBytes;for(int i=0;i<count;i++)track.SampleBytes[i]=packed[i];}
                        Header after=new Header(value);
                        if(!after.Equals(h)) {emit("route-unresolved",Link(track)+",reason=header-changed-during-read,attribution=non-atomic");track.HasPlan=false;ObserveMovement(track,after,post,started);return;}
                        long prior=track.Definition;
                        ObservePlan(track,h,track.SampleBytes,started);track.LastScan=started;
                        if(commandReturn&&!bound&&prior!=track.Definition&&track.Content!=null&&track.Content.Relevant)
                            unboundChange?.Invoke(expectedCommand,unit,track.Definition,track.Command);
                    }
                    ObserveMovement(track,h,post,started);
                }
                catch(Exception error) {emit("route-capture-error",Link(track)+",type="+error.GetType().Name+",message="+error.Message);}
                finally {readTicks+=Stopwatch.GetTimestamp()-started;}
            }
        }
        internal static bool ValidXY(int x,int y) => (uint)x<800&&(uint)y<800;
        internal static bool Step(int raw,ref int x,ref int y)
        {
            if((uint)raw>7)return false;
            if(raw==1||raw==2||raw==3)x++;else if(raw==5||raw==6||raw==7)x--;
            if(raw==0||raw==1||raw==7)y--;else if(raw==3||raw==4||raw==5)y++;
            return ValidXY(x,y);
        }
        internal void ObservePlan(Track t,Header h,ReadOnlySpan<byte> packed,long now)
        {
            scans++;bytesRead+=Math.Min(packed.Length,Math.Max(0,(h.Length+1)/2));
            bool valid=h.Length>=0&&h.Length<=2000&&h.Cursor>=0&&h.Cursor<=h.Length&&packed.Length>=(h.Length+1)/2&&ValidXY(h.OriginX,h.OriginY);
            if(!valid) {if(!t.HasInvalidHeader||!t.InvalidHeader.Equals(h))emit("route-unresolved",Link(t)+",reason=invalid-length-cursor-or-origin,length="+h.Length+",cursor="+h.Cursor+",origin="+h.OriginX+"/"+h.OriginY);t.InvalidHeader=h;t.HasInvalidHeader=true;t.HasPlan=false;t.Content=null;t.Relevant=false;return;}
            t.HasInvalidHeader=false;
            if(t.CompareCommand&&t.ReturnKnown)
            {
                bool same=t.PreCaptured&&t.BeforeCommand.Length==h.Length&&t.BeforeCommand.OriginX==h.OriginX&&t.BeforeCommand.OriginY==h.OriginY;
                for(int i=0;same&&i<(h.Length+1)/2;i++) {int mask=(i==h.Length/2&&(h.Length&1)!=0)?15:255;same=(t.BeforeBytes[i]&mask)==(packed[i]&mask);}
                t.PathChangedByCommand=!same;t.CompareCommand=false;
            }
            bool equal=t.HasPlan&&t.Length==h.Length&&t.OriginX==h.OriginX&&t.OriginY==h.OriginY&&t.SegmentX==h.SegmentX&&t.SegmentY==h.SegmentY;
            for(int i=0;equal&&i<(h.Length+1)/2;i++)
            {int mask=(i==h.Length/2&&(h.Length&1)!=0)?15:255;equal=(t.Bytes[i]&mask)==(packed[i]&mask);}
            if(equal) {repeats++;if(t.Content!=null)Observation(t,h,now,false,t.Content.Touched);return;}
            t.HasPlan=true;t.Length=h.Length;t.OriginX=h.OriginX;t.OriginY=h.OriginY;t.SegmentX=h.SegmentX;t.SegmentY=h.SegmentY;changes++;t.Relevant=false;
            for(int i=0;i<(h.Length+1)/2;i++)t.Bytes[i]=packed[i];
            Bridge[] bridges=geometry();long geo=GeometryKey(bridges),hash=Mix(Mix(Mix(Mix(Mix(Mix(geo,h.OriginX),h.OriginY),h.Length),h.SegmentX),h.SegmentY),0);
            for(int i=0;i<(h.Length+1)/2;i++)hash=Mix(hash,packed[i]&((i==h.Length/2&&(h.Length&1)!=0)?15:255));
            PathContent known=null;
            if(contents.TryGetValue(hash,out List<PathContent> bucket))foreach(var c in bucket)
            {
                Header ch=c.Header;if(c.Geometry!=geo||ch.OriginX!=h.OriginX||ch.OriginY!=h.OriginY||ch.Length!=h.Length||ch.SegmentX!=h.SegmentX||ch.SegmentY!=h.SegmentY)continue;
                bool same=true;for(int i=0;same&&i<(h.Length+1)/2;i++)same=(c.Bytes[i]&((i==h.Length/2&&(h.Length&1)!=0)?15:255))==(packed[i]&((i==h.Length/2&&(h.Length&1)!=0)?15:255));
                if(same) {known=c;break;}
            }
            if(known!=null)
            {
                t.Content=known;t.Definition=known.Id;t.Relevant=known.Relevant;t.FirstDeck=known.First;t.LastDeck=known.Last;
                Observation(t,h,now,true,bridges);if(known.Complete&&ProgressClass(t,h,known.Touched)!=4)foreach(var bridge in known.Touched)evidence?.Invoke(bridge.Id,bridge.Global,t.Player,t.CandidatePlan,t.PlanningRoot,t.Command,t.Decision,false);return;
            }
            int x=h.OriginX,y=h.OriginY;bool complete=true;
            for(int i=0;i<h.Length;i++)if(!Step((packed[i>>1]>>((i&1)*4))&15,ref x,ref y)) {complete=false;break;}
            int decodedX=x,decodedY=y;
            string endpointMatch=!ValidXY(h.SegmentX,h.SegmentY)?"unobserved":(!complete?"undecodable":(decodedX==h.SegmentX&&decodedY==h.SegmentY).ToString());
            var intersections=new StringBuilder();var touched=new List<Bridge>();t.FirstDeck=int.MaxValue;t.LastDeck=-1;
            foreach(Bridge bridge in bridges)
            {
                x=h.OriginX;y=h.OriginY;int first=-1,last=-1,entryX=x,entryY=y,exitX=x,exitY=y;bool gateSeen=bridge.Gate.Contains(Bridge.XY(x,y));bool full=true;
                for(int i=0;i<h.Length;i++)
                {
                    int beforeX=x,beforeY=y,raw=(packed[i>>1]>>((i&1)*4))&15;
                    if(!Step(raw,ref x,ref y)) {full=false;complete=false;break;}
                    gateSeen|=bridge.Gate.Contains(Bridge.XY(x,y));
                    if(bridge.Deck.Contains(Bridge.XY(x,y))) {if(first<0) {first=i;entryX=beforeX;entryY=beforeY;}last=i;exitX=x;exitY=y;}
                    else if(last==i-1) {exitX=x;exitY=y;}
                }
                if(first<0)continue;
                t.Relevant=true;touched.Add(bridge);t.FirstDeck=Math.Min(t.FirstDeck,first);t.LastDeck=Math.Max(t.LastDeck,last);
                intersections.Append(bridge.Id).Append('/').Append(bridge.Global).Append('/').Append(first).Append('/').Append(last).Append('/').Append(entryX).Append('/').Append(entryY).Append('/').Append(exitX).Append('/').Append(exitY).Append('/').Append(gateSeen?"gate-footprint-observed":bridge.Parent==0?"unknown-parent":full?"deck-without-parent-footprint":"incomplete").Append(';');
            }
            var directions=new StringBuilder();if(intersections.Length!=0||!complete)for(int i=0;i<(h.Length+1)/2;i++)directions.Append(packed[i].ToString("X2"));
            t.Content=new PathContent {Header=h,Bytes=packed.Slice(0,(h.Length+1)/2).ToArray(),Id=++definition,Geometry=geo,Complete=complete,Relevant=t.Relevant,First=t.FirstDeck,Last=t.LastDeck,Bridges=intersections.ToString(),Touched=touched.ToArray()};t.Definition=t.Content.Id;
            if(contentCount>=4096) {contents.Clear();contentCount=0;bucket=null;}if(bucket==null) {bucket=new List<PathContent>();contents[hash]=bucket;}bucket.Add(t.Content);contentCount++;
            // Relevant and undecodable paths retain full reconstructible definitions.
            // Background paths previously carried no bytes; retain all their numeric
            // definition fields in rows rather than duplicating descriptive text.
            if(complete&&!t.Relevant)
            {
                backgroundPathRows.Append(t.Definition).Append('/').Append(now).Append('/').Append(h.OriginX).Append('/').Append(h.OriginY).Append('/').Append(h.Length).Append('/').Append(h.Cursor).Append('/').Append(h.Flags).Append('/').Append(h.Substep).Append('/').Append(decodedX).Append('/').Append(decodedY).Append('/').Append(h.SegmentX).Append('/').Append(h.SegmentY).Append(';');
                if(++backgroundPathCount==32)FlushBackgroundPaths();FlushRelevantPaths();
            }
            else if(complete)
            {
                relevantPathRows.Append(t.Definition).Append(':').Append(now).Append(':').Append(h.OriginX).Append(':').Append(h.OriginY).Append(':').Append(h.Length).Append(':').Append(h.Cursor).Append(':').Append(h.Flags).Append(':').Append(h.Substep).Append(':').Append(decodedX).Append(':').Append(decodedY).Append(':').Append(h.SegmentX).Append(':').Append(h.SegmentY).Append(':').Append(intersections).Append(':').Append(directions).Append('|');
                if(++relevantPathCount==32)FlushRelevantPaths();
            }
            else emit("stored-route","format=2,definition="+t.Definition+",captureClock="+now+",origin="+h.OriginX+"/"+h.OriginY+",length="+h.Length+",cursor="+h.Cursor+",flags="+h.Flags+",substep="+h.Substep+",complete="+complete+",decodedEndpoint="+decodedX+"/"+decodedY+",nativeSegmentTarget="+h.SegmentX+"/"+h.SegmentY+",endpointMatchesTarget="+endpointMatch+",bridges=["+intersections+"],packedHex="+directions );
            Observation(t,h,now,true,bridges);
            if(complete&&ProgressClass(t,h,t.Content.Touched)!=4)foreach(var bridge in touched)evidence?.Invoke(bridge.Id,bridge.Global,t.Player,t.CandidatePlan,t.PlanningRoot,t.Command,t.Decision,false);
            if(!complete)emit("route-unresolved",Link(t)+",reason=invalid-direction-or-coordinate,definition="+t.Definition);
        }
        internal void ObserveMovement(Track t,Header h,bool post,long now)
        {
            bool moved=t.HasHeader&&(h.X!=t.Last.X||h.Y!=t.Last.Y);
            bool adjacent=moved&&Math.Abs(h.X-t.Last.X)<=1&&Math.Abs(h.Y-t.Last.Y)<=1;
            if(moved)
            {
                t.LastProgress=now;t.Stall=false;
                foreach(Bridge b in geometry())
                {
                    bool inside=b.Deck.Contains(Bridge.XY(h.X,h.Y)),was=b.Deck.Contains(Bridge.XY(t.Last.X,t.Last.Y));
                    if(inside||was) {if(adjacent)evidence?.Invoke(b.Id,b.Global,t.Player,t.CandidatePlan,t.PlanningRoot,t.Command,t.Decision,true);emit("bridge-movement",Link(t)+",definition="+t.Definition+",building="+b.Id+"/g"+b.Global+",from="+t.Last.X+"/"+t.Last.Y+",to="+h.X+"/"+h.Y+",stage="+(inside&&!was?"enter":was&&!inside?"exit":"deck-step")+",eventPhase="+(post?"post":"pre")+",cursor="+h.Cursor+",transition="+(adjacent?"adjacent-tile-step":"non-adjacent-position-change")+",execution=observed-positions");}
                }
            }
            if(t.HasHeader&&t.Content!=null&&!t.Stall&&(t.Relevant||h.Length>h.Cursor)&&now-t.LastProgress>=5*Stopwatch.Frequency&&now-t.CommandStarted>=5*Stopwatch.Frequency)
            {t.Stall=true;emit("route-progress-gap",Link(t)+",definition="+t.Definition+",position="+h.X+"/"+h.Y+",cursor="+h.Cursor+",length="+h.Length+",secondsWithoutTileChange="+((now-t.LastProgress)/(double)Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",commandAgeSeconds="+((now-t.CommandStarted)/(double)Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",cause=unproven");}

            if(t.Content!=null)Observation(t,h,now,false,t.Content.Touched);
            t.Last=h;t.HasHeader=true;
        }
        private static string Link(Track t) => "binding="+t.Binding+",decisionState="+t.Decision+",unit="+t.Unit+"/g"+t.Global+",player="+t.Player+",unitControlRaw="+t.ControlRaw+",playerSource="+(t.PlanningRoot!=0?"military-root":"unit-context")+",tribe="+t.Tribe+",commandOp="+t.Command+",parentEvent="+t.ParentEvent+",consumer="+t.Consumer+",planningRoot="+t.PlanningRoot+",entryPhase="+t.Phase+",candidatePlan="+t.CandidatePlan+",candidateLink=last-completed-plan-not-proven-causality,commandReturn="+(t.ReturnKnown?t.ReturnRaw.ToString():"pending")+",pathChangedSinceCommandPre="+(t.PreCaptured&&t.ReturnKnown?t.PathChangedByCommand.ToString():"unobserved")+",pathAssociation=observed-unit-state-not-necessarily-new-search";
    }
}
