using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Units;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescueOrderFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private static UnitMoveContext Context(int unit,string owner=null)=>(UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),
            BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{1L,0L,7L,9L,unit,40,50,0,owner},null);
        private static UnitMoveCompletion Completion(UnitMoveContext context,bool invoked=true,bool returned=true,long result=1,bool current=true)
        {
            var value=(UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(UnitMoveCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,new object[]{invoked,returned,result,null,current});return value;
        }
        internal static void Run()
        {
            checks=0;bool recovering=true;var pre=new List<int>();var post=new List<string>();
            var observer=new RescueOrderObserver("rescue",()=>recovering,id=>pre.Add(id),(id,result)=>post.Add(id+":"+result));
            Check(observer.TryBegin(Context(1,"rescue"))==null && pre.Count==0,"only the bound own recovery call is ignored");
            var foreign=Context(2);var receipt=observer.TryBegin(foreign);
            Check(receipt!=null && pre.Count==1 && pre[0]==2,"independent foreign command remains authoritative during recovery");
            receipt.Complete(Completion(foreign,result:0));receipt.Complete(Completion(foreign));
            Check(post.Count==1 && post[0]=="2:0","actual native refusal is delivered exactly once for the effective unit");
            recovering=false;var ownFixture=Context(3,"rescue");observer.TryBegin(ownFixture).Complete(Completion(ownFixture));
            Check(post.Count==2 && post[1]=="3:1","explicit non-recovery commands from this mod are still authoritative");
            var replaced=Context(4);observer.TryBegin(replaced).Complete(Completion(Context(4)));
            Check(post.Count==2,"a different context cannot consume this receipt");
            var stale=Context(5);observer.TryBegin(stale).Complete(Completion(stale,current:false));
            Check(post.Count==2,"retired world completion cannot hand off current ownership");
            var failed=Context(6);observer.TryBegin(failed).Complete(Completion(failed,returned:false));
            Check(post.Count==2,"throwing original is not claimed as a native returned order");
            Check(observer.TryBegin(Context(0))==null && observer.TryBegin(Context(10000))==null,"invalid unit domains never reach callbacks");
            var filtered=new RescueOrderObserver("rescue",()=>false,id=>pre.Add(id),(id,result)=>post.Add(id+":"+result),id=>id==2);
            int prior=pre.Count;Check(filtered.TryBegin(Context(500))==null && pre.Count==prior,"unrelated commands allocate no consumer receipt or scan pending units");
            var skipped=Context(7);observer.TryBegin(skipped).Complete(Completion(skipped,invoked:false));
            Check(post.Count==2,"uninvoked originals are not treated as executed new orders");
            Console.WriteLine("PASS: final native order consumer checks="+checks+"; effective identities, own-origin distinction and exact native receipts; no native calls.");
        }
    }
}
