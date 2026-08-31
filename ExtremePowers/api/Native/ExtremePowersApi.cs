using System;
using System.Collections.Generic;

namespace ExtremePowers.API
{
    internal sealed class ExtremePowersApi : IExtremePowersApi
    {
        private readonly object gate = new object();
        private readonly Dictionary<ExtremePowerId, Registration> replacements = new Dictionary<ExtremePowerId, Registration>();
        private readonly bool recognizedBuild;
        private readonly NativeExtremePowersRuntime nativeRuntime;
        private readonly ExtremePowerNetworkRuntime networkRuntime;
        private readonly Func<bool> synchronizedSessionReady;
        private ExtremePowersTuning current;

        internal ExtremePowersApi(string dllPath)
        {
            recognizedBuild = NativeBuildGuard.IsSupported(dllPath, out string status); NativeBackendStatus = status;
            Vanilla = CreateVanillaPlaceholder(); current = Vanilla.Clone();
        }
        internal ExtremePowersApi(string dllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory, ExtremePowersBootstrapOptions options)
        {
            synchronizedSessionReady = options?.IsSynchronizedSessionReady;
            recognizedBuild = NativeBuildGuard.IsSupported(dllPath, out string status);
            Vanilla = CreateVanillaPlaceholder(); current = Vanilla.Clone();
            if (!recognizedBuild)
            {
                NativeBackendStatus = status;
                return;
            }

            try
            {
                ExtremePowerNetworkRuntime pendingNetwork = new ExtremePowerNetworkRuntime(this);
                NativeExtremePowersRuntime pendingNative = new NativeExtremePowersRuntime(this, libraryHandle, libraryMemory);
                networkRuntime = pendingNetwork;
                nativeRuntime = pendingNative;
                NativeBackendStatus = "Supported Steam build 24816905; validated Extreme Powers hooks are active.";
            }
            catch (Exception ex)
            {
                NativeBackendStatus = "Native hook validation failed; Vanilla fallback is active: " + ex.Message;
            }
        }
        public string ProtocolVersion => "1";
        // Recognition alone is intentionally insufficient: every mutation signature must validate first.
        public bool NativeBackendAvailable => nativeRuntime != null;
        public string NativeBackendStatus { get; }
        public VanillaExtremePowersConfiguration Vanilla { get; }
        public ExtremePowersTuning Current { get { lock (gate) return current.Clone(); } }
        public void Apply(ExtremePowersTuning tuning) { if (tuning == null) throw new ArgumentNullException(nameof(tuning)); tuning.Validate(); lock (gate) current = tuning.Clone(); }
        public void RestoreVanilla() { lock (gate) current = Vanilla.Clone(); }
        public IDisposable RegisterReplacement(ExtremePowerId power, ExtremePowerReplacement replacement)
        {
            ValidatePower(power); if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            lock (gate) { if (replacements.ContainsKey(power)) throw new InvalidOperationException("A replacement is already registered for " + power + "."); var r = new Registration(this, power, replacement); replacements.Add(power, r); return r; }
        }
        public bool TryGetReplacement(ExtremePowerId power, out ExtremePowerReplacement replacement) { lock (gate) { if (replacements.TryGetValue(power, out Registration r)) { replacement = r.Value; return true; } replacement = null; return false; } }
        public bool TryExecuteReplacement(ExtremePowerExecutionContext context, out string rejectionReason)
        {
            if ((uint)context.Power > 7 || context.PlayerId < 1 || context.PlayerId > 8) { rejectionReason = "Invalid power or player."; return false; }
            if (!ExtremePowerTargetValidator.IsValid(context.Target)) { rejectionReason = "Invalid target."; return false; }
            if (!TryGetReplacement(context.Power, out ExtremePowerReplacement replacement)) { rejectionReason = "No replacement is registered."; return false; }
            if (replacement.TargetKind != context.Target.Kind) { rejectionReason = "Target kind does not match the replacement contract."; return false; }
            try
            {
                if (!replacement.CanExecute(in context, out rejectionReason)) return false;
                replacement.Execute(in context); rejectionReason = null; return true;
            }
            catch (Exception ex)
            {
                rejectionReason = "Replacement callback failed: " + ex.Message;
                return false;
            }
        }
        internal ExtremePowersTuning Snapshot() { lock (gate) return current.Clone(); }
        internal bool IsSynchronizedSessionReady()
        {
            try { return synchronizedSessionReady == null || synchronizedSessionReady(); }
            catch { return false; }
        }
        public bool QueueReplacement(ExtremePowerId power, int playerId, ExtremePowerTarget target) => networkRuntime != null && networkRuntime.Queue(power, playerId, target);
        private void Remove(Registration registration) { lock (gate) if (replacements.TryGetValue(registration.Power, out Registration existing) && ReferenceEquals(existing, registration)) replacements.Remove(registration.Power); }
        private static void ValidatePower(ExtremePowerId power) { if ((int)power < 0 || (int)power > 7) throw new ArgumentOutOfRangeException(nameof(power)); }
        private static VanillaExtremePowersConfiguration CreateVanillaPlaceholder() => new VanillaExtremePowersConfiguration
        {
            Costs = new[] { 636, 1272, 1908, 2544, 3180, 3816, 4452, 5088 },
            RegenerationPercent = 100,
            Heal = new HealingConfiguration { Amount = 8000, Radius = 6 },
            Spearmen = new SpawnConfiguration { UnitType = 24, Count = 20 },
            Engineers = new SpawnConfiguration { UnitType = 30, Count = 14 },
            Macemen = new SpawnConfiguration { UnitType = 26, Count = 20 },
            Knights = new SpawnConfiguration { UnitType = 28, Count = 10 },
            Gold = new GoldConfiguration { Minimum = 1000, Maximum = 2499 }
            ,ArrowVolley = new VolleyConfiguration { Damage = 6000, Radius = 6, ProjectileMode = 1 }
            ,RockVolley = new VolleyConfiguration { Damage = 18000, Radius = 9, ProjectileMode = 0 }
        };
        private sealed class Registration : IDisposable { private ExtremePowersApi owner; internal Registration(ExtremePowersApi owner, ExtremePowerId power, ExtremePowerReplacement value) { this.owner = owner; Power = power; Value = value; } internal ExtremePowerId Power { get; } internal ExtremePowerReplacement Value { get; } public void Dispose() { var value = owner; if (value == null) return; owner = null; value.Remove(this); } }
    }
}
