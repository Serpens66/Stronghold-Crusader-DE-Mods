using System;
using System.Collections.Generic;

namespace PopularityTest
{
    internal sealed class OverrideRequest
    {
        internal readonly string Name;
        internal readonly int? Value;
        internal readonly Func<IOverrideValue> Resolve;
        internal OverrideRequest(string name, int? value, Func<IOverrideValue> resolve)
        { Name = name; Value = value; Resolve = resolve; }
    }

    internal sealed class OverrideBatch
    {
        private readonly Action<string> ownershipError;
        private readonly List<KeyValuePair<string, OwnedOverride>> leases = new List<KeyValuePair<string, OwnedOverride>>();
        private OverrideRequest[] applied;
        internal int Count => leases.Count;
        internal OverrideBatch(Action<string> ownershipError) { this.ownershipError = ownershipError; }
        internal bool Apply(OverrideRequest[] requests)
        {
            bool same = applied != null && applied.Length == requests.Length;
            for (int i = 0; same && i < requests.Length; i++)
                same = applied[i].Name == requests[i].Name && applied[i].Value == requests[i].Value;
            if (same) return false;
            Release();
            try
            {
                foreach (var request in requests)
                {
                    if (!request.Value.HasValue) continue; // No getter or write for disabled settings.
                    var lease = new OwnedOverride(request.Resolve(), request.Value.Value);
                    leases.Add(new KeyValuePair<string, OwnedOverride>(request.Name, lease));
                    if (!lease.IsCurrent) throw new InvalidOperationException(request.Name + ": pushed override did not read back.");
                }
                applied = (OverrideRequest[])requests.Clone(); return true;
            }
            catch { Release(); throw; }
        }
        internal bool Release()
        {
            bool released = true;
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!leases[i].Value.Release()) { released = false; Report(leases[i].Key + ": ownership changed; foreign value preserved."); }
                }
                catch (Exception ex) { released = false; Report(leases[i].Key + ": release failed: " + ex); }
            }
            leases.Clear(); applied = null;
            return released;
        }
        private void Report(string message) { try { ownershipError(message); } catch { } }
    }
}
