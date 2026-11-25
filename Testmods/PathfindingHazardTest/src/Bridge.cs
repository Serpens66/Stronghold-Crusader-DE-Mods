using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Pathfinding;
using BepInEx.Bootstrap;

namespace PathfindingHazardTest
{
    // Reflection boundary to Pathfinding's engine (contract checked offline in tests/Program.cs against the
    // compiled mod). The hazard options are release-gated in EngineOptions.Capture; the test installs a private
    // copy of the mission options (lease) and restores the original reference and movement cases afterwards.
    // Called on the simulation tick only.
    internal sealed class Bridge
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object provider, original;
        private readonly FieldInfo settings;
        private readonly MethodInfo apply, capture, restore, resetWatch, resetEscapes;
        private readonly Dictionary<string, byte[]> originalCases;
        private readonly int originalOrderSequence;
        private object lease;
        internal bool OwnsLease => lease != null && ReferenceEquals(settings.GetValue(provider), lease);

        internal Bridge()
        {
            var assembly = Chainloader.PluginInfos["Pathfinding_Serp"].Instance.GetType().Assembly;
            var host = assembly.GetType("Pathfinding.Engine.EngineHost", throwOnError: true);
            provider = host.GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ?? throw new InvalidOperationException("Pathfinding provider unavailable.");
            Type type = provider.GetType();
            settings = type.GetField("settings", Instance) ?? throw new MissingFieldException(type.FullName, "settings");
            original = settings.GetValue(provider) ?? throw new InvalidOperationException("No engine options.");
            apply = Method(type, "ApplyMissionOptions", original.GetType());
            capture = Method(type, "CaptureMovementCases", typeof(Action<string, byte[]>));
            restore = Method(type, "RestoreMovementCases", typeof(IReadOnlyDictionary<string, byte[]>));
            resetWatch = Method(type, "ResetReplanWatch"); resetEscapes = Method(type, "ResetFireEscapes");
            foreach (string name in new[] { "fireEscapes", "replanWaits", "replanSolos" })
                Require(Count(name) == 0, "Active movement cases before activation; the case needs a clean start.");
            Require((bool)Property(original, "EnablePathfindingCore").GetValue(original) && Value<bool>(provider, "anchorAvailable"), "Enable the base engine and provide a ready anchor core.");
            originalCases = Capture();
            originalOrderSequence = Value<int>(provider, "tribeOrderSequence");
        }

        internal void Activate(HazardCase item, int waitSeconds)
        {
            lease = Clone(original);
            bool fire = !item.Plague, plague = item.Plague;
            Set(lease, "AnchorSingleMoves", true); Set(lease, "AvoidFire", fire); Set(lease, "AvoidPlague", plague);
            Set(lease, "EscapeFire", item.Escape && fire); Set(lease, "EscapePlague", item.Escape && plague); Set(lease, "EscapePlagueRaised", false);
            Set(lease, "FireWaitIndefinitely", item.Unlimited && fire); Set(lease, "PlagueWaitIndefinitely", item.Unlimited && plague);
            Set(lease, "FireWaitSeconds", waitSeconds); Set(lease, "PlagueWaitSeconds", waitSeconds);
            // Fixed defaults instead of the user's personal values: the fixture geometry relies on them.
            Set(lease, "FireReturnSeconds", 120); Set(lease, "PlagueReturnSeconds", 180);
            Set(lease, "FireDetourPercent", 50); Set(lease, "FireDetourMinTiles", 10);
            apply.Invoke(provider, new[] { lease });
            Require(OwnsLease, "Temporary option snapshot was not installed.");
            Require(Value<bool>(provider, plague ? "plagueRulesActive" : "fireRulesActive"),
                "Selected hazard scanner/rule is not bound and active; inspect the main mod's binding diagnostics.");
        }

        internal object Case(string name, int id)
        {
            object dictionary = Field(name).GetValue(provider);
            return (bool)dictionary.GetType().GetMethod("ContainsKey").Invoke(dictionary, new object[] { id })
                ? dictionary.GetType().GetProperty("Item").GetValue(dictionary, new object[] { id }) : null;
        }

        internal bool Hazard(bool plague, int x, int y)
        {
            if ((uint)x >= RouteTileRules.MapSize || (uint)y >= RouteTileRules.MapSize) return false;
            object tiles = Field(plague ? "plagueTiles" : "fireTiles").GetValue(provider);
            return (bool)tiles.GetType().GetMethod("Contains", Instance).Invoke(tiles, new object[] { y * RouteTileRules.MapSize + x });
        }

        internal void Restore()
        {
            if (!OwnsLease) return;
            Dictionary<string, byte[]> current = Capture();
            object finishing = Clone(lease);
            Set(finishing, "AnchorSingleMoves", false); Set(finishing, "AvoidFire", false); Set(finishing, "AvoidPlague", false);
            try
            {
                apply.Invoke(provider, new[] { finishing });
                resetWatch.Invoke(provider, null); resetEscapes.Invoke(provider, null);
                restore.Invoke(provider, new object[] { current }); // Disabled cases end/rejoin rather than orphaning temporary groups.
                resetWatch.Invoke(provider, null); resetEscapes.Invoke(provider, null);
                Field("tribeOrderSequence").SetValue(provider, originalOrderSequence);
            }
            finally { apply.Invoke(provider, new[] { original }); }
            restore.Invoke(provider, new object[] { originalCases });
            Require(ReferenceEquals(settings.GetValue(provider), original), "Original option reference was not restored.");
            Require(Equal(originalCases, Capture()), "Original movement case bytes were not restored.");
            lease = null;
        }

        private int Count(string name)
        { object dictionary = Field(name).GetValue(provider); return (int)dictionary.GetType().GetProperty("Count").GetValue(dictionary); }
        private FieldInfo Field(string name) => provider.GetType().GetField(name, Instance) ?? throw new MissingFieldException(provider.GetType().FullName, name);
        private Dictionary<string, byte[]> Capture()
        {
            var values = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            capture.Invoke(provider, new object[] { new Action<string, byte[]>((name, bytes) => values.Add(name, (byte[])bytes.Clone())) });
            return values;
        }
        private static bool Equal(Dictionary<string, byte[]> a, Dictionary<string, byte[]> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var item in a)
            {
                if (!b.TryGetValue(item.Key, out byte[] bytes) || bytes.Length != item.Value.Length) return false;
                for (int i = 0; i < bytes.Length; i++) if (bytes[i] != item.Value[i]) return false;
            }
            return true;
        }
        internal static T Value<T>(object target, string name) => (T)(target.GetType().GetField(name, Instance) ?? throw new MissingFieldException(target.GetType().FullName, name)).GetValue(target);
        private static PropertyInfo Property(object target, string name) => target.GetType().GetProperty(name, Instance) ?? throw new MissingMemberException(target.GetType().FullName, name);
        private static void Set(object target, string name, object value) => Property(target, name).SetValue(target, value);
        private static object Clone(object value) => typeof(object).GetMethod("MemberwiseClone", Instance).Invoke(value, null);
        private static MethodInfo Method(Type type, string name, params Type[] arguments) => type.GetMethod(name, Instance, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
