using System;
using System.Collections;
using System.Threading;

namespace Shared
{
    /// <summary>
    /// Shares the state of one nested MakeTroop detour chain through AppDomain data.
    /// Each mod embeds its own copy of this source file, so only framework collection
    /// types may cross the assembly boundary.
    /// </summary>
    internal static class RecruitmentHookContext
    {
        private const string ContextDataKey = "SerpsMods.Shared.RecruitmentHookContext.v1";
        private const string DepthKey = "Depth";
        private const string RootCtrlKey = "RootCtrl";
        private const string ConcreteKey = "Concrete";
        private const string FinalAmountKey = "FinalAmount";

        public static Scope Enter(int incomingAmount)
        {
            IDictionary states = GetThreadStates();
            int threadId = Thread.CurrentThread.ManagedThreadId;
            lock (states)
            {
                IDictionary state = states[threadId] as IDictionary;
                if (state == null)
                {
                    state = new Hashtable
                    {
                        [DepthKey] = 0,
                        [RootCtrlKey] = incomingAmount == RecruitmentRequestPolicy.VanillaCtrlAllAmount,
                        [ConcreteKey] = false,
                        [FinalAmountKey] = incomingAmount
                    };
                    states[threadId] = state;
                }

                state[DepthKey] = GetInt(state, DepthKey) + 1;
            }

            return new Scope(states, threadId);
        }

        public static bool ShouldInterpretCtrlSentinel(int incomingAmount)
        {
            if (incomingAmount != RecruitmentRequestPolicy.VanillaCtrlAllAmount)
                return false;

            IDictionary states = GetThreadStates();
            lock (states)
            {
                IDictionary state = states[Thread.CurrentThread.ManagedThreadId] as IDictionary;
                return state != null &&
                    GetBool(state, RootCtrlKey) &&
                    !GetBool(state, ConcreteKey);
            }
        }

        public static void RecordForwardedAmount(int amount)
        {
            IDictionary states = GetThreadStates();
            lock (states)
            {
                IDictionary state = states[Thread.CurrentThread.ManagedThreadId] as IDictionary;
                if (state == null)
                    return;

                state[ConcreteKey] = true;
                state[FinalAmountKey] = Math.Max(0, amount);
            }
        }

        public static void RecordBlocked()
        {
            RecordForwardedAmount(0);
        }

        public static Result GetResult()
        {
            IDictionary states = GetThreadStates();
            lock (states)
            {
                IDictionary state = states[Thread.CurrentThread.ManagedThreadId] as IDictionary;
                return state == null
                    ? new Result(0, false)
                    : new Result(
                        Math.Max(0, GetInt(state, FinalAmountKey)),
                        GetBool(state, ConcreteKey));
            }
        }

        private static IDictionary GetThreadStates()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            lock (domain)
            {
                IDictionary states = domain.GetData(ContextDataKey) as IDictionary;
                if (states != null)
                    return states;

                states = new Hashtable();
                domain.SetData(ContextDataKey, states);
                return states;
            }
        }

        private static int GetInt(IDictionary state, string key)
        {
            object value = state[key];
            return value is int number ? number : 0;
        }

        private static bool GetBool(IDictionary state, string key)
        {
            object value = state[key];
            return value is bool flag && flag;
        }

        internal readonly struct Result
        {
            public Result(int finalAmount, bool hasConcreteAmount)
            {
                FinalAmount = finalAmount;
                HasConcreteAmount = hasConcreteAmount;
            }

            public int FinalAmount { get; }
            public bool HasConcreteAmount { get; }
        }

        internal sealed class Scope : IDisposable
        {
            private readonly IDictionary states;
            private readonly int threadId;
            private bool disposed;

            public Scope(IDictionary states, int threadId)
            {
                this.states = states;
                this.threadId = threadId;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                lock (states)
                {
                    IDictionary state = states[threadId] as IDictionary;
                    if (state == null)
                        return;

                    int depth = GetInt(state, DepthKey) - 1;
                    if (depth <= 0)
                        states.Remove(threadId);
                    else
                        state[DepthKey] = depth;
                }
            }
        }
    }
}
