using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class VirtualUnitsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "VirtualUnitsPrototype_Serp";
        public const string PluginName = "Virtual Units Prototype";
        public const string PluginVersion = "0.1.0";
        internal const string DesertArcherId = "serp.virtual-units:desert-archer";
        internal const string DesertHovelId = "serp.virtual-units:desert-hovel";
        private static readonly VirtualSpriteTintProfile ArcherTint = new VirtualSpriteTintProfile(220, 240, byte.MaxValue, byte.MaxValue);
        private static readonly VirtualSpriteTintProfile HovelTint = new VirtualSpriteTintProfile(180, 220, byte.MaxValue, byte.MaxValue);
        private VirtualEntityRuntime runtime;

        private void Awake()
        {
            runtime = new VirtualEntityRuntime(Logger);
            VirtualEntityRuntime.Current = runtime;
            RegisterBuiltIns();
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded; definitions remain open until Script Extender initialization.");
        }

        private void RegisterBuiltIns()
        {
            VirtualApiResult unit = VirtualEntityApi.RegisterUnitDefinition(new VirtualUnitDefinition(
                DesertArcherId, 1, "Desert Archer", SHCDESE.Interop.eChimps.CHIMP_TYPE_ARCHER,
                ArcherTint,
                new VirtualStatProfile(new RationalFactor(2, 1), new RationalFactor(3, 2)),
                new VirtualSpawnOptions(true, true)));
            int hovelScale = BuildingScales.GetScale(SHCDESE.Interop.eMappers.MAPPER_HOVEL);
            VirtualApiResult building = VirtualEntityApi.RegisterBuildingDefinition(new VirtualBuildingDefinition(
                DesertHovelId, 1, "Desert Hovel", SHCDESE.Interop.eStructs.STRUCT_HOVEL,
                SHCDESE.Interop.eMappers.MAPPER_HOVEL, hovelScale,
                HovelTint,
                new VirtualStatProfile(new RationalFactor(2, 1), new RationalFactor(1, 1)),
                new VirtualSpawnOptions(true, true)));
            if (!unit.Succeeded || !building.Succeeded)
                Shared.DebugLogHelper.LogError(Logger, $"Built-in definition registration failed: unit={unit}; building={building}.");
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
            runtime.Initialize(this);
        }
    }
}
