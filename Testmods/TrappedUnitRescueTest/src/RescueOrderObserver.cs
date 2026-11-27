using System;
using System.Threading;
using APIShared.Units;

namespace TrappedUnitRescueTest
{
    // The shared publisher supplies final effective arguments and actual native results.
    // Only the bound recovery call is ours; unrelated reentrant commands remain authoritative.
    internal sealed class RescueOrderObserver : IUnitMoveObserver
    {
        private readonly string owner;
        private readonly Func<bool> ownRecovery;
        private readonly Action<int> before;
        private readonly Action<int,long> after;
        private readonly Action<int> aborted;
        private readonly Func<int,bool> interested;
        internal RescueOrderObserver(string owner,Func<bool> ownRecovery,Action<int> before,Action<int,long> after,Func<int,bool> interested=null,Action<int> aborted=null)
        {this.owner=owner;this.ownRecovery=ownRecovery;this.before=before;this.after=after;this.interested=interested;this.aborted=aborted;}
        public IUnitMoveInvocation TryBegin(UnitMoveContext context)
        {
            if(context==null || context.UnitId<1 || context.UnitId>NativeRelocation.MaximumUnitId ||
                interested!=null && !interested(context.UnitId) ||
                context.OriginOwnerGuid==owner && ownRecovery())return null;
            var completion=new Completion(context,after,aborted);
            try{before(context.UnitId);return completion;}
            catch
            {
                // No receipt reaches the broker when Begin fails. Close any
                // partially opened observer scope through the same abort path.
                try{aborted?.Invoke(context.UnitId);}catch{}
                throw;
            }
        }
        private sealed class Completion : IUnitMoveInvocation
        {
            private readonly UnitMoveContext context;
            private readonly Action<int,long> after;
            private readonly Action<int> aborted;
            private int completed;
            internal Completion(UnitMoveContext context,Action<int,long> after,Action<int> aborted){this.context=context;this.after=after;this.aborted=aborted;}
            public void Complete(UnitMoveCompletion completion)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || completion==null || !ReferenceEquals(context,completion.Context))return;
                if(!completion.NativeInvoked || !completion.NativeReturned || !completion.IsCurrentWorld){aborted?.Invoke(context.UnitId);return;}
                after(context.UnitId,completion.NativeResult);
            }
        }
    }
}
