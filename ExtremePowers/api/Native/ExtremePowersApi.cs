using System;
using System.Collections.Generic;

namespace ExtremePowers.API
{
    internal sealed class ExtremePowersApi : IExtremePowersApi
    {
        private readonly object gate = new object();
        private readonly Dictionary<ExtremePowerId, Registration> replacements = new Dictionary<ExtremePowerId, Registration>();
        private readonly NativeExtremePowersRuntime nativeRuntime;
        private readonly ExtremePowerNetworkRuntime networkRuntime;
        private readonly Func<string, ExtremePowersReadiness> sessionReadiness;
        private readonly Action<string> diagnostic;
        private readonly Dictionary<string, string> diagnosticStates = new Dictionary<string, string>(StringComparer.Ordinal);
        private string lastReadinessDiagnostic;
        private ExtremePowersTuning current;

        internal ExtremePowersApi(string dllPath)
        {
            NativeBuildGuard.IsSupported(dllPath, out string status); NativeBackendStatus = status;
            Vanilla = CreateVanillaPlaceholder(); current = Vanilla.Clone();
        }
        internal ExtremePowersApi(string dllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory, ExtremePowersBootstrapOptions options)
        {
            sessionReadiness = options?.GetSessionReadiness;
            diagnostic = options?.Diagnostic;
            bool recognizedBuild = NativeBuildGuard.IsSupported(dllPath, out string status);
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
        public string CompatibilityToken => ExtremePowersCompatibility.CreateToken(ProtocolVersion, NativeBuildGuard.SupportedSha256, NativeBackendAvailable, networkRuntime?.PacketId ?? -1);
        // Recognition alone is intentionally insufficient: every mutation signature must validate first.
        public bool NativeBackendAvailable => nativeRuntime != null;
        public string NativeBackendStatus { get; }
        // No canonical, uniquely validated native unit-pick hook is known for build 24816905 yet.
        public bool SupportsUnitTargeting => false;
        public VanillaExtremePowersConfiguration Vanilla { get; }
        public ExtremePowersTuning Current { get { lock (gate) return current.Clone(); } }
        public void Apply(ExtremePowersTuning tuning) { if (tuning == null) throw new ArgumentNullException(nameof(tuning)); tuning.Validate(); lock (gate) current = tuning.Clone(); }
        public void RestoreVanilla() { lock (gate) current = Vanilla.Clone(); }
        public IDisposable RegisterReplacement(ExtremePowerId power, ExtremePowerReplacement replacement)
        {
            ValidatePower(power); if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (replacement.TargetKind == ExtremePowerTargetKind.Unit && !SupportsUnitTargeting) throw new NotSupportedException("Unit targeting is unavailable because no canonical native unit-pick hook has been validated.");
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
        internal ExtremePowersReadiness GetSessionReadiness()
        {
            ExtremePowersReadiness value;
            try { value = sessionReadiness == null ? ExtremePowersReadiness.Available : sessionReadiness(CompatibilityToken); }
            catch (Exception ex) { value = ExtremePowersReadiness.Unavailable("Session readiness callback failed: " + ex.Message); }
            string message = value.Ready ? "Extreme Powers session is ready." : "Extreme Powers Vanilla fallback: " + (string.IsNullOrWhiteSpace(value.Reason) ? "unspecified readiness failure" : value.Reason);
            if (!string.Equals(lastReadinessDiagnostic, message, StringComparison.Ordinal)) { lastReadinessDiagnostic = message; Log(message); }
            return value;
        }
        internal void Log(string message)
        {
            try { diagnostic?.Invoke(message ?? string.Empty); } catch { }
        }
        internal void LogState(string key, string message)
        {
            lock (gate)
            {
                if (diagnosticStates.TryGetValue(key ?? string.Empty, out string previous) && string.Equals(previous, message, StringComparison.Ordinal)) return;
                diagnosticStates[key ?? string.Empty] = message;
            }
            Log(message);
        }
        public bool QueueReplacement(ExtremePowerId power, int playerId, ExtremePowerTarget target, out string rejectionReason)
        {
            if (networkRuntime == null) { rejectionReason = "Native/network backend is unavailable."; Log(rejectionReason); return false; }
            return networkRuntime.Queue(power, playerId, target, out rejectionReason);
        }
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
