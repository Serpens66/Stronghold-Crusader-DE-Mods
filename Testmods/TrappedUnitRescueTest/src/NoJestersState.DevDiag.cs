#if SERP_DEVDIAG
using System;

namespace TrappedUnitRescueTest
{
    // Pure ownership/write policy shared by the real native adapter and the
    // isolated fixture. No alternate/native-emulation implementation in tests.
    internal interface INoJestersWord
    {
        ushort ReadJester();
        ushort ReadLady();
        bool AllEightOriginalPairsEmpty();
        void WriteJesterWord(ushort value);
    }
    internal sealed class NoJestersState
    {
        private readonly INoJestersWord word;
        private long session,world;
        private ushort original,lady;
        private bool changed,source;
        internal NoJestersState(INoJestersWord word){this.word=word??throw new ArgumentNullException(nameof(word));}
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        private static void Stamp(long session,long world){Check(session>0 && world>0,"Missing exact native world");}
        private void Empty(){Check(word.AllEightOriginalPairsEmpty(),"Original Jester ID/global pair is already populated or inconsistent");}
        internal void ApplyFresh(long sid,long generation)
        {
            Stamp(sid,generation);Check(!source,"Fresh native-state write cannot repeat");
            ushort before=word.ReadJester(),beforeLady=word.ReadLady();
            Check(before==1,"Fresh Skirmish did not publish the audited Jester default1");Empty();
            session=sid;world=generation;original=before;lady=beforeLady;source=true;changed=true;
            word.WriteJesterWord(0); // Exactly the native UInt16 contract; never the Lady neighbor.
            Check(word.ReadJester()==0 && word.ReadLady()==lady,"Native-state write/readback changed the wrong word or failed");
        }
        internal void AdoptLoaded(long sid,long generation)
        {
            Stamp(sid,generation);Check(source,"Owned source world was never prepared");
            Check(sid!=session || generation!=world,"Owned load did not create a new native world");
            ushort loaded=word.ReadJester(),loadedLady=word.ReadLady();
            Check(loaded==0,"Owned native save did not preserve Jester0; no repair write is permitted");Empty();
            Check(loadedLady==lady,"Owned native save/load changed the original Lady word");
            session=sid;world=generation;original=loaded;lady=loadedLady;changed=false;
        }
        internal void Verify(long sid,long generation)
        {
            Stamp(sid,generation);Check(source && sid==session && generation==world,"Native-state fixture crossed its owned world");
            Check(word.ReadJester()==0 && word.ReadLady()==lady,"Foreign native-state change; no continuous enforcement is permitted");Empty();
        }
        internal bool Restore(long sid,long generation)
        {
            if(!changed || sid!=session || generation!=world || session==0 || world==0)return false;
            // A foreign word/Lady/lifetime change wins; never overwrite it.
            if(word.ReadJester()!=0 || word.ReadLady()!=lady || !word.AllEightOriginalPairsEmpty())return false;
            word.WriteJesterWord(original);changed=false;
            Check(word.ReadJester()==original && word.ReadLady()==lady,"Original native-state restore failed");
            return true;
        }
        // Only the separate exact-token/real-Start one-shot policy may call
        // this. It preserves the fresh original word and all native values.
        internal void RebindReady(long sid,long earlyGeneration,long readyGeneration)
        {
            Stamp(sid,readyGeneration);Verify(sid,earlyGeneration);
            world=readyGeneration;
        }
        internal void Retire(){session=world=0;changed=false;}
    }
    internal sealed class NoJestersReadyHandoff
    {
        private long session,earlyWorld;
        private object token,lastToken;
        private bool captured,started,bound,retired;
        internal bool IsBound=>bound && !retired;
        internal bool HasPendingCapture=>captured && !retired;
        internal long EarlyWorld=>earlyWorld;
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        internal void Capture(long sid,long generation,object nativeToken)
        {
            Check(sid>0 && generation>0 && nativeToken!=null,"Missing early native-world identity");
            Check(!captured || retired,"Repeated NativeLoaded before its world was retired");
            Check(!ReferenceEquals(lastToken,nativeToken),"Owned load reused the old native-world object");
            session=sid;earlyWorld=generation;token=lastToken=nativeToken;
            captured=true;started=bound=retired=false;
        }
        internal void RequireToken(long sid,object nativeToken)
        {Check(captured && !retired && sid==session && nativeToken!=null && ReferenceEquals(token,nativeToken),"Native-world identity changed");}
        internal void Started(long sid,object nativeToken)
        {
            RequireToken(sid,nativeToken);Check(!started && !bound,"Repeated or late ManagedReady handoff");started=true;
        }
        internal void Bind(long sid,long generation,object nativeToken,NoJestersState state)
        {
            RequireToken(sid,nativeToken);Check(started && !bound && generation>0,"Missing/repeated real ManagedReady handoff");
            state.RebindReady(sid,earlyWorld,generation);bound=true;
        }
        internal void Retire(){retired=true;bound=false;token=null;}
    }
}
#endif
