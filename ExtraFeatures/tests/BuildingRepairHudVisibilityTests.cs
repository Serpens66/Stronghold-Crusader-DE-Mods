using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal static class BuildingRepairHudVisibilityTests
    {
        internal static void Run(byte[] image, Action<bool, string> check)
        {
            var policy = new BuildingRepairHudVisibilityPolicy();
            int ordinary = (int)Enums.InBuildingModes.INSIDE_NULL;
            int market = (int)Enums.InBuildingModes.INSIDE_TRADEPOST;
            check(!policy.ShouldShow((int)eStructs.STRUCT_HOVEL, ordinary), "Repair HUD waits for native validation");
            IntPtr table = Marshal.AllocHGlobal(BuildingRepairHudVisibilityPolicy.DamageTableByteLength);
            try
            {
                Marshal.Copy(image, BuildingRepairHudVisibilityPolicy.DamageTableRva, table,
                    BuildingRepairHudVisibilityPolicy.DamageTableByteLength);
                IntPtr module = IntPtr.Subtract(table, BuildingRepairHudVisibilityPolicy.DamageTableRva);
                int length = BuildingRepairHudVisibilityPolicy.DamageTableRva + BuildingRepairHudVisibilityPolicy.DamageTableByteLength;
                check(!policy.InitializeNative(module, length, false), "Unknown native image rejected");
                check(!policy.InitializeNative(module, length - 1, true), "Truncated damage table rejected");
                check(!policy.InitializeNative(IntPtr.Zero, length, true), "Missing native module rejected");
                check(policy.InitializeNative(module, length, true), "Validated damage table accepted");
                check(!policy.ShouldShow((int)eStructs.STRUCT_GOODS_YARD, (int)Enums.InBuildingModes.INSIDE_GOODS_YARD),
                    "Stockpile hidden despite its positive maximum health");
                check(!policy.ShouldShow((int)eStructs.STRUCT_WITCH_HOIST, ordinary), "Other indestructible building hidden");
                check(policy.ShouldShow((int)eStructs.STRUCT_HOVEL, ordinary), "Damageable house retains repair HUD");
                check(policy.ShouldShow((int)eStructs.STRUCT_GRANARY, (int)Enums.InBuildingModes.INSIDE_GRANARY),
                    "Granary is damageable and remains visible");
                check(policy.ShouldShow((int)eStructs.STRUCT_TRADEPOST, market), "Market overview visible");
                check(!policy.ShouldShow((int)eStructs.STRUCT_GRANARY, market),
                    "Previous building quote cannot occupy the market overview during UI transition");
                foreach (Enums.InBuildingModes panel in Enum.GetValues(typeof(Enums.InBuildingModes)))
                    check(policy.ShouldShow((int)eStructs.STRUCT_TRADEPOST, (int)panel) == ((int)panel == market),
                        "Market only visible in overview: " + panel);
                foreach (var tradePanel in new[] {
                    Enums.InBuildingModes.INSIDE_TRADEPOST_PRICES,
                    Enums.InBuildingModes.INSIDE_TRADEPOST_FOOD,
                    Enums.InBuildingModes.INSIDE_TRADEPOST_BULK,
                    Enums.InBuildingModes.INSIDE_TRADEPOST_ARMS,
                    Enums.InBuildingModes.INSIDE_TRADEPOST_DO_THE_TRADE })
                    check(!policy.ShouldShow((int)eStructs.STRUCT_GRANARY, (int)tradePanel),
                        "Shared trade screen hidden when opened from another building: " + tradePanel);
                check(!policy.ShouldShow(-1, ordinary) && !policy.ShouldShow((int)eStructs.STRUCT_NULL, ordinary) &&
                    !policy.ShouldShow((int)eStructs.STRUCT_GARDEN_LARGE + 1, ordinary), "Invalid type indices rejected");
                Marshal.WriteInt32(table, (int)eStructs.STRUCT_GOODS_YARD * sizeof(int), 1);
                check(policy.ShouldShow((int)eStructs.STRUCT_GOODS_YARD, ordinary), "Live damage eligibility override respected");
                Marshal.WriteInt32(table, (int)eStructs.STRUCT_HOVEL * sizeof(int), 0);
                check(!policy.ShouldShow((int)eStructs.STRUCT_HOVEL, ordinary), "Live invulnerability override respected");
                policy.InitializeNative(module, length, false);
                check(!policy.ShouldShow((int)eStructs.STRUCT_TRADEPOST, market), "Failed validation hides HUD after earlier success");
            }
            finally { Marshal.FreeHGlobal(table); }
        }
    }
}
