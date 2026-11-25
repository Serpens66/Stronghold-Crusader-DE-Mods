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
        private readonly Func<int,bool> interested;
        internal RescueOrderObserver(string owner,Func<bool> ownRecovery,Action<int> before,Action<int,long> after,Func<int,bool> interested=null)
        {this.owner=owner;this.ownRecovery=ownRecovery;this.before=before;this.after=after;this.interested=interested;}
        public IUnitMoveInvocation TryBegin(UnitMoveContext context)
        {
            if(context==null || context.UnitId<1 || context.UnitId>NativeRelocation.MaximumUnitId ||
                interested!=null && !interested(context.UnitId) ||
                context.OriginOwnerGuid==owner && ownRecovery())return null;
            before(context.UnitId);return new Completion(context,after);
        }
        private sealed class Completion : IUnitMoveInvocation
        {
            private readonly UnitMoveContext context;
            private readonly Action<int,long> after;
            private int completed;
            internal Completion(UnitMoveContext context,Action<int,long> after){this.context=context;this.after=after;}
            public void Complete(UnitMoveCompletion completion)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || completion==null || !ReferenceEquals(context,completion.Context) ||
                    !completion.NativeInvoked || !completion.NativeReturned || !completion.IsCurrentWorld)return;
                after(context.UnitId,completion.NativeResult);
            }
        }
    }
}
