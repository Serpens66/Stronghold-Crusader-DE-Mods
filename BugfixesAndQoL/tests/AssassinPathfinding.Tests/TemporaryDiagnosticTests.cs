// TEMP_GATE_ROUTE_ACCEPTANCE: production handoff observer guards and exact callback counts.
using APIShared;
internal static partial class Program
{
    private sealed class HandoffObserver : ITemporaryGateRouteAcceptanceObserver, ITemporaryAssassinGateObserver
    {
        internal bool Throw; internal readonly List<string> Stages=new();
        public object BeginRoute(int p,int t,uint tg,int u,uint ug,int type,int x,int y,int tx,int ty,string c)=>null;
        public void RouteEdge(object t,int f,int to,int d) {}
        public void EndRoute(object t,string s,int r) {}
        public void Raid(int p,int role,int t,uint tg,int b,uint bg,string s,string r,string d) {}
        public void ObserveAssassinDecision(object t,int p,int f,int to,int d,bool prepared,AssassinTransitionKind m,bool a,int g,uint global,string e) {}
        public void ObserveAssassinStage(object t,int p,string s,string r,string d)
        { if(Throw) throw new InvalidOperationException("observer-fixture"); Stages.Add(r); }
    }
    private static void TestTemporaryHandoffEvidence()
    {
        var observer=new HandoffObserver(); TemporaryGateRouteAcceptanceBridge.Register(observer);
        IntPtr context=new(199); int identities=0,validations=0,published=0; bool live=true,allowed=true;
        Check(!AssassinRouteHandoff.TryResolve(context,1,2,3,4,out _,out _)&&AssassinRouteHandoff.TemporaryRequestReason=="missing-output-context","no frame has explicit source-independent reason");
        var frame=new AssassinRouteHandoff(context,1,2,3,4,5,()=>{identities++;return live;},(b,n)=>{published++;return n;},5,1,
            (stage,result,detail)=>observer.ObserveAssassinStage(null,5,stage,result,detail));
        try {
            Check(!AssassinRouteHandoff.TryResolve(context,1,2,4,4,out _,out _)&&identities==0&&AssassinRouteHandoff.TemporaryRequestReason=="request-mismatch","mismatch never probes identity");
            live=false;
            Check(!AssassinRouteHandoff.TryResolve(context,1,2,3,4,out _,out _)&&identities==1&&AssassinRouteHandoff.TemporaryRequestReason=="invalid-identity","invalid identity is tested exactly once");
            live=true;
            Check(!AssassinRouteHandoff.Stage(context,1,2,3,4,5,new byte[]{15},1,()=>true)&&identities==1,"bad directions do not invoke identity or route validation");
            Func<bool> validator=()=>{validations++;return allowed;};
            allowed=false;
            Check(!AssassinRouteHandoff.Stage(context,1,2,3,4,5,new byte[]{2},1,validator)&&identities==2&&validations==1&&AssassinRouteHandoff.TemporaryRequestReason=="route-validation-failed","original route guard evaluated once");
            allowed=true;
            Check(AssassinRouteHandoff.Stage(context,1,2,3,4,5,new byte[]{2},1,validator),"valid weighted route staged");
            allowed=false;Check(frame.Complete(8)==8&&published==0&&observer.Stages.Contains("discarded-route-validation"),"discarded stage preserves native result");
            allowed=true;Check(AssassinRouteHandoff.Stage(context,1,2,3,4,5,new byte[]{2},1,validator),"replacement staged");
            int before=identities;int routeBefore=validations;observer.Throw=true;
            Check(frame.Complete(8)==1&&published==1&&identities-before==2&&validations-routeBefore==1,"throwing observer preserves publication and exact validator invocation counts");
            Check(TemporaryGateRouteAcceptanceBridge.Failures>0,"observer exception is counted");
        } finally {frame.Leave();}
        Check(!AssassinRouteHandoff.HasFrame,"completed diagnostic frame cannot leak into a subsequent call");
    }
}
