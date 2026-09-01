using System;
using ExtremePowers.API;

namespace ExtremePowers.Integration
{
    internal sealed class LocalExtremePowersApiClient : IExtremePowersApiClient
    {
        private readonly IExtremePowersApi api;
        private readonly Action<string> diagnostic;
        internal LocalExtremePowersApiClient(IExtremePowersApi api, Action<string> diagnostic) { this.api = api ?? throw new ArgumentNullException(nameof(api)); this.diagnostic = diagnostic; }
        internal static IExtremePowersApiClient Create(string dllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory, Func<string, ApiReadiness> getSessionReadiness, Action<string> diagnostic)
        {
            var options = new ExtremePowersBootstrapOptions
            {
                GetSessionReadiness = token =>
                {
                    ApiReadiness readiness = getSessionReadiness == null ? ApiReadiness.Available : getSessionReadiness(token);
                    return new ExtremePowersReadiness(readiness.Ready, readiness.Reason);
                },
                Diagnostic = diagnostic
            };
            return new LocalExtremePowersApiClient(ExtremePowersBootstrap.Initialize(dllPath, libraryHandle, libraryMemory, options), diagnostic);
        }
        public string Status => api.NativeBackendStatus;
        public string CompatibilityToken => api.CompatibilityToken;
        public ApiReadiness EvaluateSession(bool realMultiplayer, string[] reports, int[] players)
        {
            ExtremePowersReadiness value = ExtremePowersCompatibility.EvaluateSession(realMultiplayer, CompatibilityToken, reports, players);
            return new ApiReadiness(value.Ready, value.Reason);
        }
        public void Apply(Settings.ExtremePowersSettings s)
        {
            var t = api.Vanilla.Clone(); t.RegenerationPercent = s.RegenerationPercent;
            // The example keeps Vanilla costs because the stock HUD labels are fixed to their slot values.
            t.Costs = (int[])api.Vanilla.Costs.Clone();
            t.ArrowVolley.Damage = s.ArrowDamage; t.ArrowVolley.Radius = s.ArrowRadius; t.ArrowVolley.ProjectileKind = (ExtremePowerProjectileKind)s.ArrowMode;
            t.Heal.Amount = s.HealAmount; t.Heal.Radius = s.HealRadius; t.RockVolley.Damage = s.RockDamage; t.RockVolley.Radius = s.RockRadius; t.RockVolley.ProjectileKind = (ExtremePowerProjectileKind)s.RockMode;
            Fill(t.Spearmen, s.SpearmenType, s.SpearmenCount); Fill(t.Engineers, s.EngineersType, s.EngineersCount); Fill(t.Macemen, s.MacemenType, s.MacemenCount); Fill(t.Knights, s.KnightsType, s.KnightsCount);
            t.Gold.Minimum = s.GoldMinimum; t.Gold.Maximum = Math.Max(s.GoldMinimum, s.GoldMaximum); api.Apply(t);
        }
        public void RestoreVanilla() => api.RestoreVanilla();
        public IDisposable InstallGoldDemo(Settings.ExtremePowersSettings s) => api.RegisterReplacement(ExtremePowerId.Gold,
            new ExtremePowerReplacement(s.DemoName, s.DemoTooltip, s.DemoSprite, ExtremePowerTargetKind.MapPoint,
                (in ExtremePowerExecutionContext context, out string reason) => Demo.GoldSpawnDemo.CanExecute(s, context.PlayerId, context.Target.TileIndex, out reason),
                (in ExtremePowerExecutionContext context) =>
                {
                    int ownerPlayerId = s.ResolveDemoOwner(context.PlayerId);
                    ExtremePowerSpawnResult result = api.SpawnUnitGroup(ownerPlayerId, context.Target.TileIndex, s.DemoUnitType, s.DemoSpawnCount);
                    diagnostic?.Invoke("Gold replacement spawn activatingPlayer=" + context.PlayerId + " owner=" + result.OwnerPlayerId + " unitType=" + s.DemoUnitType + " requested=" + result.RequestedCount + " spawned=" + result.SpawnedUnitCount + " groupId=" + result.GroupUnitId + ".");
                }));
        private static void Fill(SpawnConfiguration c, int type, int count) { c.UnitType = type; c.Count = count; }
    }
}
