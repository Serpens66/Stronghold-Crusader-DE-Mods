using System;
using ExtremePowers.API;

namespace ExtremePowers.Integration
{
    internal sealed class LocalExtremePowersApiClient : IExtremePowersApiClient
    {
        private readonly IExtremePowersApi api;
        internal LocalExtremePowersApiClient(IExtremePowersApi api) { this.api = api ?? throw new ArgumentNullException(nameof(api)); }
        internal static IExtremePowersApiClient Create(string dllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory, Func<bool> isSynchronizedSessionReady)
        {
            var options = new ExtremePowersBootstrapOptions { IsSynchronizedSessionReady = isSynchronizedSessionReady };
            return new LocalExtremePowersApiClient(ExtremePowersBootstrap.Initialize(dllPath, libraryHandle, libraryMemory, options));
        }
        public string Status => api.NativeBackendStatus;
        public void Apply(Settings.ExtremePowersSettings s)
        {
            var t = api.Vanilla.Clone(); t.RegenerationPercent = s.RegenerationPercent;
            t.Costs = new[] { s.ArrowCost, s.HealCost, s.SpearmenCost, s.EngineersCost, s.MacemenCost, s.GoldCost, s.KnightsCost, s.RockCost };
            t.ArrowVolley.Damage = s.ArrowDamage; t.ArrowVolley.Radius = s.ArrowRadius; t.ArrowVolley.ProjectileMode = s.ArrowMode;
            t.Heal.Amount = s.HealAmount; t.Heal.Radius = s.HealRadius; t.RockVolley.Damage = s.RockDamage; t.RockVolley.Radius = s.RockRadius; t.RockVolley.ProjectileMode = s.RockMode;
            Fill(t.Spearmen, s.SpearmenType, s.SpearmenCount); Fill(t.Engineers, s.EngineersType, s.EngineersCount); Fill(t.Macemen, s.MacemenType, s.MacemenCount); Fill(t.Knights, s.KnightsType, s.KnightsCount);
            t.Gold.Minimum = s.GoldMinimum; t.Gold.Maximum = Math.Max(s.GoldMinimum, s.GoldMaximum); api.Apply(t);
        }
        public void RestoreVanilla() => api.RestoreVanilla();
        public IDisposable InstallGoldDemo(Settings.ExtremePowersSettings s) => api.RegisterReplacement(ExtremePowerId.Gold,
            new ExtremePowerReplacement(s.DemoName, s.DemoTooltip, s.DemoSprite, ExtremePowerTargetKind.MapPoint,
                (in ExtremePowerExecutionContext context, out string reason) => Demo.GoldSpawnDemo.CanExecute(s, context.PlayerId, context.Target.TileIndex, out reason),
                (in ExtremePowerExecutionContext context) => Demo.GoldSpawnDemo.Execute(s, context.PlayerId, context.Target.TileIndex)));
        private static void Fill(SpawnConfiguration c, int type, int count) { c.UnitType = type; c.Count = count; }
    }
}
