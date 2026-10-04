#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Assembly.InstructionWalker;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.VTables;
using SHCDESE.Logging;
using Serilog;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public class BulkUnitDetours
{
	public delegate void c_game_unit_hunter_query_for_target_delegate(IntPtr pGameUnitManager, int unitId);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_dll_troopselection_delegate(int mouseState, byte rightDown, byte rightUp, uint count, ulong pSelectedChimps, byte selectionOn, byte selectionEstablished, uint underCursorCount, ulong pUnderCursorChimps, int mousePosX, int mousePosY, byte overTopHalf, uint onScreenCount, ulong pOnScreenChimps);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_calculate_worker_good_yield_delegate(int unitId, int goodAmount, int b50PercentBonus);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_issueorder_movehere_delegate(NativePointer<GameUnitManager> pGameUnitManager, int unitId, int tileX, int tileY, int unknown);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_delete_hook_delegate(NativePointer<GameUnitManager> pGameUnitManager, uint unitId);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void c_game_unit_handle_movement_delegate(NativePointer<GameUnitManager> pGameUnitManager, uint unitId);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_takedamage_projectile_Delegate(NativePointer<GameUnitManager> pGameUnitManager, int attackedUnitId, int projectileId, int value);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_takedamage_melee_Delegate(NativePointer<GameUnitManager> pGameUnitManager, int attackingUnitId, int damagedUnitId, int value);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_unit_spawn_ex_delegate(NativePointer<GameUnitManager> pGameUnitManager, int playerOwnerId, int playerColorId, int worldTileX, int worldTileY, int heightElevation, eChimps unitType);

	internal static NativeStateArray<int> defaultInteractRangeArray;

	internal static NativeStateArray<int> customInteractRangeArray;

	internal static NativeFlagArray customInteractRangeArrayFlag;

	public static HookHandle<X64InlineHook> c_game_unit_control_capability_interactrange_hook = new HookHandle<X64InlineHook>();

	internal static NativeStateArray<short> defaultEngageRangeArray;

	internal static NativeStateArray<short> customEngageRangeArray;

	internal static NativeFlagArray customEngageRangeArrayFlag;

	public static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall3 = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall2 = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_transition_hook1_c_game_player_buy_mercenary = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_transition_hook3_c_game_building_assign_worker = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_transition_hook4_c_game_unit_disband = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_control_capability_unknown = new HookHandle<X64InlineHook>();

	public static List<HookHandle<X64FunctionCloneHook>> c_game_unit_aistatetracker_list = new List<HookHandle<X64FunctionCloneHook>>();

	public static c_game_unit_hunter_query_for_target_delegate c_game_unit_hunter_query_for_target;

	public static HookHandle<X64InlineHook> c_game_unit_hunter_query_for_target_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_is_over_killingpit_update_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_player_buy_mercenary_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_damaged_by_projectile_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_killed_by_projectile_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_killed_by_melee_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_take_fire_damage = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_bedouin_healer_heal_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_woodcutter_update_pickup_planks_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_woodcutter_update_dropoff_planks_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_cattle_update_pickup_cheese_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_cattle_update_dropoff_cheese_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_apple_update_pickup_apple_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_apple_update_dropoff_apple_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_hemp_update_pickup_hemp_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_hemp_update_dropoff_hemp_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_baker_update_pickup_flour_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_baker_update_pickup_bread_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_baker_update_dropoff_bread_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_wheat_update_pickup_wheat_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_farmer_wheat_update_dropoff_wheat_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_miller_update_pickup_wheat_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_miller_update_dropoff_flour_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_miller_update_pickup_flour_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_brewer_update_pickup_hemp_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_brewer_update_dropoff_hemp_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_brewer_update_pickup_ale_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_brewer_update_dropoff_ale_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_brewer_update_produced_ale_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_innkeeper_update_pickup_ale_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_innkeeper_update_dropoff_ale_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_fletcher_update_pickup_wood_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_fletcher_update_dropoff_wood_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_fletcher_update_dropoff_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_poleturner_update_pickup_wood_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_poleturner_update_dropoff_wood_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_poleturner_update_dropoff_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_pickup_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_dropoff_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_dropoff_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_tanner_update_store_cowhides_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_tanner_update_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_tanner_update_dropoff_cowhides_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_pickup_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_dropoff_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_store_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_dropoff_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_armourer_update_pickup_produce_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_hunter_update_pickup_meat_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_hunter_update_dropoff_meat_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_quarry_grunt_update_pickup_stone_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_quarry_grunt_update_dropoff_stone_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_quarry_ox_update_depart_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_quarry_ox_update_dropoff_stone_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_miner2_update_pickup_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_miner2_update_dropoff_iron_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_pitcher_update_pickup_rawpitch_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_unit_pitcher_update_dropoff_rawpitch_hook = new HookHandle<X64InlineHook>();

	internal static DetourHandle<c_game_dll_troopselection_delegate> c_game_dll_troopselection_hook = new DetourHandle<c_game_dll_troopselection_delegate>();

	public static DetourHandle<c_game_unit_calculate_worker_good_yield_delegate> c_game_unit_calculate_worker_good_yield_hook = new DetourHandle<c_game_unit_calculate_worker_good_yield_delegate>();

	public static DetourHandle<c_game_unit_issueorder_movehere_delegate> c_game_unit_issueorder_movehere_hook = new DetourHandle<c_game_unit_issueorder_movehere_delegate>();

	public static DetourHandle<c_game_unit_delete_hook_delegate> c_game_unit_delete_hook = new DetourHandle<c_game_unit_delete_hook_delegate>();

	public static DetourHandle<c_game_unit_handle_movement_delegate> c_game_unit_handle_movement_hook = new DetourHandle<c_game_unit_handle_movement_delegate>();

	public static DetourHandle<c_game_unit_takedamage_projectile_Delegate> c_game_unit_takedamage_projectile_hook = new DetourHandle<c_game_unit_takedamage_projectile_Delegate>();

	public static DetourHandle<c_game_unit_takedamage_melee_Delegate> c_game_unit_takedamage_melee_hook = new DetourHandle<c_game_unit_takedamage_melee_Delegate>();

	public static DetourHandle<c_game_unit_spawn_ex_delegate> c_game_unit_spawn_ex_hook = new DetourHandle<c_game_unit_spawn_ex_delegate>();

	public static Dictionary<eChimps, List<HookHandle<X64InlineHook>>> UnitEngageRangedHooks { get; } = new Dictionary<eChimps, List<HookHandle<X64InlineHook>>>();

	public unsafe BulkUnitDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner, NativeStateBlock nativeStateBlock)
	{
		LogHelper.Information("Applying", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		ulong num = (ulong)(long)CrusaderLibrary.Instance.LibraryModuleHandle;
		ContextHookOptions options = new ContextHookOptions();
		tx.AddDetour(c_game_unit_takedamage_melee_hook, "48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30 49", c_game_unit_takedamage_melee_hook_impl, "c_game_unit_takedamage_melee_hook");
		tx.AddDetour(c_game_unit_takedamage_projectile_hook, "48 89 5C 24 ?? 48 89 4C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 45 33 D2", c_game_unit_takedamage_projectile_hook_impl, "c_game_unit_takedamage_projectile_hook");
		tx.AddDetour(c_game_unit_spawn_ex_hook, "40 53 55 56 48 83 EC ?? 44 8B 1D", c_game_unit_spawn_ex_hook_impl, "c_game_unit_spawn_ex_hook");
		tx.AddDetour(c_game_unit_handle_movement_hook, "48 63 C2 4C 69 C0 ?? ?? ?? ?? 41 83 BC 08", c_game_unit_handle_movement_hook_impl, "c_game_unit_handle_movement_hook");
		tx.AddDetour(c_game_unit_delete_hook, "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 48 63 FA 48 8D 2D", c_game_unit_delete_hook_impl, "c_game_unit_delete_hook");
		tx.AddDetour(c_game_unit_issueorder_movehere_hook, "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 63 F2", c_game_unit_issueorder_movehere_hook_impl, "c_game_unit_issueorder_movehere_hook");
		tx.AddDetour(c_game_unit_calculate_worker_good_yield_hook, "48 63 C1 4C 8D 1D ?? ?? ?? ?? 4C 69 C8", c_game_unit_calculate_worker_good_yield_hook_impl, "c_game_unit_calculate_worker_good_yield_hook");
		tx.AddDetour(c_game_dll_troopselection_hook, HookTarget.FromExport("DLL_TroopSelection", (IntPtr)0), c_game_dll_troopselection_hook_impl, "c_game_dll_troopselection_hook");
		tx.AddContextHook(c_game_unit_killed_by_projectile_hook, "42 8B 84 0E ? ? ? ? 46 89 94 0E", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int* stackPtr = ctx.Pointer->GetStackPtr<int>(80);
			int* stackPtr2 = ctx.Pointer->GetStackPtr<int>(48);
			Log.Verbose($"c_game_unit_killed_by_projectile_hook, attackedUnitId={*stackPtr2}, projectileId={*stackPtr}");
			UnitKilledByProjectileEventArgs eventArgs = new UnitKilledByProjectileEventArgs(EventHookPhase.Pre, *stackPtr2, *stackPtr);
			UnitR3EventHooks.OnUnitKilledByProjectile.Raise(eventArgs);
		}, options, "c_game_unit_killed_by_projectile_hook");
		tx.AddContextHook(c_game_unit_killed_by_melee_hook, "66 41 89 84 34 ?? ?? ?? ?? 66 45 89 B4 34", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int* stackPtr = ctx.Pointer->GetStackPtr<int>(80);
			int* stackPtr2 = ctx.Pointer->GetStackPtr<int>(48);
			Log.Verbose($"c_game_unit_killed_by_melee_hook, attackingUnitId={*stackPtr2}, damagedUnitId={*stackPtr}");
			UnitKilledByMeleeEventArgs eventArgs = new UnitKilledByMeleeEventArgs(EventHookPhase.Pre, *stackPtr2, *stackPtr);
			UnitR3EventHooks.OnUnitKilledByMelee.Raise(eventArgs);
		}, options, "c_game_unit_killed_by_melee_hook");
		tx.AddContextHook(c_game_unit_damaged_by_projectile_hook, "42 0F B7 8C 0E ? ? ? ? 66 85 C9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			ushort* stackPtr = ctx.Pointer->GetStackPtr<ushort>(128);
			int num4 = (int)ctx.Pointer->RBX;
			int* stackPtr2 = ctx.Pointer->GetStackPtr<int>(80);
			int* stackPtr3 = ctx.Pointer->GetStackPtr<int>(48);
			Log.Verbose($"c_game_unit_damaged_by_projectile_hook: attackingUnitId={*stackPtr}, damage={num4}, attackedUnitId={*stackPtr3}, projectileId={*stackPtr2}");
			UnitTakeDamageByProjectileExEventArgs e = new UnitTakeDamageByProjectileExEventArgs(EventHookPhase.Pre, *stackPtr3, *stackPtr, *stackPtr2, num4);
			UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Raise(e);
			ctx.Pointer->RBX = (ulong)e.Damage;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_damaged_by_projectile_hook");
		tx.AddContextHook(c_game_player_buy_mercenary_hook, "4D 69 FE ? ? ? ? 41 39 9C 0F", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			eChimps eChimps2 = (eChimps)ctx.Pointer->RDX;
			int num4 = (int)ctx.Pointer->RBX;
			int unitGoldCost = GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps2);
			Log.Verbose($"c_game_player_buy_mercenary_hook: {eChimps2} -> original goldCost: {num4}, new: {unitGoldCost}");
			ctx.Pointer->RBX = (ulong)unitGoldCost;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_player_buy_mercenary_hook");
		tx.AddContextHook(c_game_unit_take_fire_damage, "41 8B C8 D1 E9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			eChimps eChimps2 = (eChimps)ctx.Pointer->R9;
			int fireDamageInternal = GameUnitManagerAPI.GetFireDamageInternal(eChimps2);
			Log.Verbose($"c_game_unit_take_fire_damage: {eChimps2} -> fire damage: {fireDamageInternal}");
			ctx.Pointer->R8 = (ulong)fireDamageInternal;
		}, options, "c_game_unit_take_fire_damage");
		tx.AddContextHook(c_game_unit_bedouin_healer_heal_hook, "83 C0 ? C6 01", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			ulong r = ctx.Pointer->R9;
			int num4 = (int)r / sizeof(GameUnit);
			int num5 = *(int*)GameGlobalsManager.Instance.CurrentContextUnitValueVA;
			int bedouinHealInternal = GameUnitManagerAPI.GetBedouinHealInternal(GameUnitManagerAPI.Instance.GetType(num4));
			Log.Verbose($"c_game_unit_bedouin_healer_heal_hook: healedUnitId={num4}, healerUnitId={num5}, heal={bedouinHealInternal}");
			UnitHealByBedouinHealerEventArgs e = new UnitHealByBedouinHealerEventArgs(EventHookPhase.Pre, num4, num5, bedouinHealInternal);
			UnitR3EventHooks.OnUnitHealByBedouinHealer.Raise(e);
			ctx.Pointer->RAX = ctx.Pointer->RAX + (ulong)e.Heal;
		}, new ContextHookOptions
		{
			Registers = X64SmartCPUContextRegs.Volatile,
			InstructionSelector = (IReadOnlyList<Instruction> x) => x.Skip(1)
		}, "c_game_unit_bedouin_healer_heal_hook");
		tx.AddContextHook(c_game_unit_woodcutter_update_pickup_planks_hook, "48 69 D9 ?? ?? ?? ?? 66 46 89 B4 23", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_woodcutter_update_pickup_planks_hook: unitId={num4}");
			UnitWoodcutterPickUpPlanksEventArgs eventArgs = new UnitWoodcutterPickUpPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnWoodcutterPickUpPlanks.Raise(eventArgs);
		}, options, "c_game_unit_woodcutter_update_pickup_planks_hook");
		tx.AddContextHook(c_game_unit_woodcutter_update_dropoff_planks_hook, "46 0F BF 8C 21 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 84 82 ?? ?? ?? ?? 8B D7", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)(ctx.Pointer->RCX / (ulong)sizeof(GameUnit));
			Log.Debug($"c_game_unit_woodcutter_update_dropoff_planks_hook: unitId={num4}");
			UnitWoodcutterDropOffPlanksEventArgs eventArgs = new UnitWoodcutterDropOffPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnWoodcutterDropOffPlanks.Raise(eventArgs);
		}, options, "c_game_unit_woodcutter_update_dropoff_planks_hook");
		tx.AddContextHook(c_game_unit_farmer_cattle_update_pickup_cheese_hook, "48 69 D9 ?? ?? ?? ?? 66 42 89 AC 2B", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_farmer_cattle_update_pickup_cheese_hook: unitId={num4}");
			UnitCattleFarmerPickUpCheeseEventArgs eventArgs = new UnitCattleFarmerPickUpCheeseEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnCattleFarmerPickUpCheese.Raise(eventArgs);
		}, options, "c_game_unit_farmer_cattle_update_pickup_cheese_hook");
		tx.AddContextHook(c_game_unit_farmer_cattle_update_dropoff_cheese_hook, "46 0F BF 8C 29 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 84 82 ?? ?? ?? ?? 41 8B D4", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)(ctx.Pointer->RCX / (ulong)sizeof(GameUnit));
			Log.Debug($"c_game_unit_farmer_cattle_update_dropoff_cheese_hook: unitId={num4}");
			UnitCattleFarmerDropOffCheeseEventArgs eventArgs = new UnitCattleFarmerDropOffCheeseEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnCattleFarmerDropOffCheese.Raise(eventArgs);
		}, options, "c_game_unit_farmer_cattle_update_dropoff_cheese_hook");
		tx.AddContextHook(c_game_unit_farmer_apple_update_pickup_apple_hook, "B8 ?? ?? ?? ?? 48 69 D9 ?? ?? ?? ?? 44 8B C5", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_farmer_apple_update_pickup_apple_hook: unitId={num4}");
			UnitAppleFarmerPickUpAppleEventArgs eventArgs = new UnitAppleFarmerPickUpAppleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnAppleFarmerPickUpApple.Raise(eventArgs);
		}, options, "c_game_unit_farmer_apple_update_pickup_apple_hook");
		tx.AddContextHook(c_game_unit_farmer_apple_update_dropoff_apple_hook, "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 8B C5", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_farmer_apple_update_dropoff_apple_hook: unitId={num4}");
			UnitAppleFarmerDropOffAppleEventArgs eventArgs = new UnitAppleFarmerDropOffAppleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnAppleFarmerDropOffApple.Raise(eventArgs);
		}, options, "c_game_unit_farmer_apple_update_dropoff_apple_hook");
		tx.AddContextHook(c_game_unit_farmer_hemp_update_pickup_hemp_hook, "48 69 D1 ?? ?? ?? ?? 39 3D", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_farmer_hemp_update_pickup_hemp_hook: unitId={num4}");
			UnitHempFarmerPickUpHempEventArgs eventArgs = new UnitHempFarmerPickUpHempEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnHempFarmerPickUpHemp.Raise(eventArgs);
		}, options, "c_game_unit_farmer_hemp_update_pickup_hemp_hook");
		tx.AddContextHook(c_game_unit_farmer_hemp_update_dropoff_hemp_hook, "48 69 CA ?? ?? ?? ?? 66 42 FF 8C 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_farmer_hemp_update_dropoff_hemp_hook: unitId={num4}");
			UnitHempFarmerDropOffHempEventArgs eventArgs = new UnitHempFarmerDropOffHempEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnHempFarmerDropOffHemp.Raise(eventArgs);
		}, options, "c_game_unit_farmer_hemp_update_dropoff_hemp_hook");
		tx.AddContextHook(c_game_unit_baker_update_pickup_flour_hook, "E8 ?? ?? ?? ?? 45 8B C7 66 42 89 84 36", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_baker_update_pickup_flour_hook: unitId={num4}");
			UnitBakerPickUpFlourEventArgs eventArgs = new UnitBakerPickUpFlourEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBakerPickUpFlour.Raise(eventArgs);
		}, options, "c_game_unit_baker_update_pickup_flour_hook");
		tx.AddContextHook(c_game_unit_baker_update_pickup_bread_hook, "45 33 C0 48 69 DF", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDI;
			Log.Debug($"c_game_unit_baker_update_pickup_bread_hook: unitId={num4}");
			UnitBakerPickUpBreadEventArgs eventArgs = new UnitBakerPickUpBreadEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBakerPickUpBread.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI)
		}, "c_game_unit_baker_update_pickup_bread_hook");
		tx.AddContextHook(c_game_unit_baker_update_dropoff_bread_hook, "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 45 8B C7", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_baker_update_dropoff_bread_hook: unitId={num4}");
			UnitBakerDropOffBreadEventArgs eventArgs = new UnitBakerDropOffBreadEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBakerDropOffBread.Raise(eventArgs);
		}, options, "c_game_unit_baker_update_dropoff_bread_hook");
		tx.AddContextHook(c_game_unit_farmer_wheat_update_pickup_wheat_hook, "49 8B CD 49 69 F6", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDX;
			Log.Debug($"c_game_unit_farmer_wheat_update_pickup_wheat_hook: unitId={num4}");
			UnitWheatFarmerPickUpWheatEventArgs eventArgs = new UnitWheatFarmerPickUpWheatEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnWheatFarmerPickUpWheat.Raise(eventArgs);
		}, options, "c_game_unit_farmer_wheat_update_pickup_wheat_hook");
		tx.AddContextHook(c_game_unit_farmer_wheat_update_dropoff_wheat_hook, "48 69 CA ?? ?? ?? ?? 66 42 FF 8C 29", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDX;
			Log.Debug($"c_game_unit_farmer_wheat_update_dropoff_wheat_hook: unitId={num4}");
			UnitWheatFarmerDropOffWheatEventArgs eventArgs = new UnitWheatFarmerDropOffWheatEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnWheatFarmerDropOffWheat.Raise(eventArgs);
		}, options, "c_game_unit_farmer_wheat_update_dropoff_wheat_hook");
		tx.AddContextHook(c_game_unit_miller_update_pickup_wheat_hook, "E8 ?? ?? ?? ?? 66 42 89 84 3B ?? ?? ?? ?? 8B D7 46 0F BF 84 3B", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_miller_update_pickup_wheat_hook: unitId={num4}");
			UnitMillerPickUpWheatEventArgs eventArgs = new UnitMillerPickUpWheatEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnMillerPickUpWheat.Raise(eventArgs);
		}, options, "c_game_unit_miller_update_pickup_wheat_hook");
		tx.AddContextHook(c_game_unit_miller_update_dropoff_flour_hook, "45 8D 4E ?? 48 69 C8", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_miller_update_dropoff_flour_hook: unitId={num4}");
			UnitMillerDropOffFlourEventArgs eventArgs = new UnitMillerDropOffFlourEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnMillerDropOffFlour.Raise(eventArgs);
		}, options, "c_game_unit_miller_update_dropoff_flour_hook");
		tx.AddContextHook(c_game_unit_miller_update_pickup_flour_hook, "48 69 C8 ?? ?? ?? ?? 42 8B 84 27 ?? ?? ?? ?? 66 42 89 9C 39", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_miller_update_pickup_flour_hook: unitId={num4}");
			UnitMillerPickUpFlourEventArgs eventArgs = new UnitMillerPickUpFlourEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnMillerPickUpFlour.Raise(eventArgs);
		}, options, "c_game_unit_miller_update_pickup_flour_hook");
		tx.AddContextHook(c_game_unit_brewer_update_pickup_hemp_hook, "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 44 89 64 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_brewer_update_pickup_hemp_hook: unitId={num4}");
			UnitBrewerPickUpHempEventArgs eventArgs = new UnitBrewerPickUpHempEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBrewerPickUpHemp.Raise(eventArgs);
		}, options, "c_game_unit_brewer_update_pickup_hemp_hook");
		tx.AddContextHook(c_game_unit_brewer_update_dropoff_hemp_hook, "48 69 C8 ?? ?? ?? ?? 66 42 89 B4 39 ?? ?? ?? ?? 42 89 B4 39", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_brewer_update_dropoff_hemp_hook: unitId={num4}");
			UnitBrewerDropOffHempEventArgs eventArgs = new UnitBrewerDropOffHempEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBrewerDropOffHemp.Raise(eventArgs);
		}, options, "c_game_unit_brewer_update_dropoff_hemp_hook");
		tx.AddContextHook(c_game_unit_brewer_update_pickup_ale_hook, "33 F6 48 69 DF ?? ?? ?? ?? 45 8B C4", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDI;
			Log.Debug($"c_game_unit_brewer_update_pickup_ale_hook: unitId={num4}");
			UnitBrewerPickUpAleEventArgs eventArgs = new UnitBrewerPickUpAleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBrewerPickUpAle.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI)
		}, "c_game_unit_brewer_update_pickup_ale_hook");
		tx.AddContextHook(c_game_unit_brewer_update_dropoff_ale_hook, "48 69 CB ?? ?? ?? ?? 66 42 FF 8C 39", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBX;
			Log.Debug($"c_game_unit_brewer_update_dropoff_ale_hook: unitId={num4}");
			UnitBrewerDropOffAleEventArgs eventArgs = new UnitBrewerDropOffAleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBrewerDropOffAle.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_brewer_update_dropoff_ale_hook");
		tx.AddContextHook(c_game_unit_brewer_update_produced_ale_hook, "41 B9 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 89 64 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_brewer_update_produced_ale_hook: unitId={num4}");
			UnitBrewerProducedAleEventArgs eventArgs = new UnitBrewerProducedAleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBrewerProduceAle.Raise(eventArgs);
		}, options, "c_game_unit_brewer_update_produced_ale_hook");
		tx.AddContextHook(c_game_unit_innkeeper_update_pickup_ale_hook, "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 7C 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_innkeeper_update_pickup_ale_hook: unitId={num4}");
			UnitInnkeeperPickUpAleEventArgs eventArgs = new UnitInnkeeperPickUpAleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnInnkeperPickUpAle.Raise(eventArgs);
		}, options, "c_game_unit_innkeeper_update_pickup_ale_hook");
		tx.AddContextHook(c_game_unit_innkeeper_update_dropoff_ale_hook, "66 46 89 A4 3B ?? ?? ?? ?? 66 46 89 A4 3B ?? ?? ?? ?? 66 01 84 31", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)(ctx.Pointer->RDX / (ulong)sizeof(GameUnit));
			Log.Debug($"c_game_unit_innkeeper_update_dropoff_ale_hook: unitId={num4}");
			UnitInnkeeperDropOffAleEventArgs eventArgs = new UnitInnkeeperDropOffAleEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnInnkeeperDropOffAle.Raise(eventArgs);
		}, options, "c_game_unit_innkeeper_update_dropoff_ale_hook");
		tx.AddContextHook(c_game_unit_fletcher_update_pickup_wood_hook, "44 8B C6 48 69 CA", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDX;
			Log.Debug($"c_game_unit_fletcher_update_pickup_wood_hook: unitId={num4}");
			UnitFletcherPickUpPlanksEventArgs eventArgs = new UnitFletcherPickUpPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnFletcherPickUpPlanks.Raise(eventArgs);
		}, options, "c_game_unit_fletcher_update_pickup_wood_hook");
		tx.AddContextHook(c_game_unit_fletcher_update_dropoff_wood_hook, "49 69 D2 ?? ?? ?? ?? 66 42 89 BC 22", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->R10;
			Log.Debug($"c_game_unit_fletcher_update_dropoff_wood_hook: unitId={num4}");
			UnitFletcherDropOffPlanksEventArgs eventArgs = new UnitFletcherDropOffPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnFletcherDropOffPlanks.Raise(eventArgs);
		}, options, "c_game_unit_fletcher_update_dropoff_wood_hook");
		tx.AddContextHook(c_game_unit_fletcher_update_dropoff_produce_hook, "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 8B C6 4A 0F BF 84 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_fletcher_update_dropoff_produce_hook: unitId={num4}");
			UnitFletcherDropOffProduceEventArg eventArgs = new UnitFletcherDropOffProduceEventArg(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnFletcherDropOffProduce.Raise(eventArgs);
		}, options, "c_game_unit_fletcher_update_dropoff_produce_hook");
		tx.AddContextHook(c_game_unit_poleturner_update_pickup_wood_hook, "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 44 89 6C 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_poleturner_update_pickup_wood_hook: unitId={num4}");
			UnitPoleturnerPickUpPlanksEventArgs eventArgs = new UnitPoleturnerPickUpPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnPoleturnerPickUpPlanks.Raise(eventArgs);
		}, options, "c_game_unit_poleturner_update_pickup_wood_hook");
		tx.AddContextHook(c_game_unit_poleturner_update_dropoff_wood_hook, "48 69 D0 ?? ?? ?? ?? 66 42 89 B4 22", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_poleturner_update_dropoff_wood_hook: unitId={num4}");
			UnitPoleturnerDropOffPlanksEventArgs eventArgs = new UnitPoleturnerDropOffPlanksEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnPoleturnerDropOffPlanks.Raise(eventArgs);
		}, options, "c_game_unit_poleturner_update_dropoff_wood_hook");
		tx.AddContextHook(c_game_unit_poleturner_update_dropoff_produce_hook, "4C 69 C0 ?? ?? ?? ?? 44 89 6C 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_poleturner_update_dropoff_produce_hook: unitId={num4}");
			UnitPoleturnerDropOffProduceEventArgs eventArgs = new UnitPoleturnerDropOffProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnPoleturnerDropOffProduce.Raise(eventArgs);
		}, options, "c_game_unit_poleturner_update_dropoff_produce_hook");
		tx.AddContextHook(c_game_unit_blacksmith_update_pickup_iron_hook, "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_blacksmith_update_pickup_iron_hook: unitId={num4}");
			UnitBlacksmithPickUpIronEventArgs eventArgs = new UnitBlacksmithPickUpIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBlacksmithPickUpIron.Raise(eventArgs);
		}, options, "c_game_unit_blacksmith_update_pickup_iron_hook");
		tx.AddContextHook(c_game_unit_blacksmith_update_dropoff_iron_hook, "48 69 CA ?? ?? ?? ?? 66 46 89 AC 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDX;
			Log.Debug($"c_game_unit_blacksmith_update_dropoff_iron_hook: unitId={num4}");
			UnitBlacksmithDropOffIronEventArgs eventArgs = new UnitBlacksmithDropOffIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBlacksmithDropOffIron.Raise(eventArgs);
		}, options, "c_game_unit_blacksmith_update_dropoff_iron_hook");
		tx.AddContextHook(c_game_unit_blacksmith_update_produce_hook, "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 46 89 AC 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_blacksmith_update_produce_hook: unitId={num4}");
			UnitBlacksmithProduceEventArgs eventArgs = new UnitBlacksmithProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBlacksmithProduce.Raise(eventArgs);
		}, options, "c_game_unit_blacksmith_update_produce_hook");
		tx.AddContextHook(c_game_unit_blacksmith_update_dropoff_produce_hook, "4C 69 C0 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_blacksmith_update_dropoff_produce_hook: unitId={num4}");
			UnitBlacksmithDropOffProduceEventArgs eventArgs = new UnitBlacksmithDropOffProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnBlacksmithDropOffProduce.Raise(eventArgs);
		}, options, "c_game_unit_blacksmith_update_dropoff_produce_hook");
		tx.AddContextHook(c_game_unit_tanner_update_store_cowhides_hook, "48 69 C8 ?? ?? ?? ?? 42 C7 84 21 ?? ?? ?? ?? ?? ?? ?? ?? 66 42 C7 84 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_tanner_update_store_cowhides_hook: unitId={num4}");
			UnitTannerStoreCowHidesEventArgs eventArgs = new UnitTannerStoreCowHidesEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnTannerStoreCowHides.Raise(eventArgs);
		}, options, "c_game_unit_tanner_update_store_cowhides_hook");
		tx.AddContextHook(c_game_unit_tanner_update_produce_hook, "48 69 C8 ?? ?? ?? ?? 33 FF 66 42 89 BC 21 ?? ?? ?? ?? 66 42 89 9C 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_tanner_update_produce_hook: unitId={num4}");
			UnitTannerProduceEventArgs eventArgs = new UnitTannerProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnTannerProduce.Raise(eventArgs);
		}, options, "c_game_unit_tanner_update_produce_hook");
		tx.AddContextHook(c_game_unit_tanner_update_dropoff_cowhides_hook, "48 69 CB ?? ?? ?? ?? 66 42 FF 8C 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBX;
			Log.Debug($"c_game_unit_tanner_update_dropoff_cowhides_hook: unitId={num4}");
			UnitTannerDropOffCowHidesEventArgs eventArgs = new UnitTannerDropOffCowHidesEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnTannerDropOffCowHides.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_tanner_update_dropoff_cowhides_hook");
		tx.AddContextHook(c_game_unit_armourer_update_pickup_iron_hook, "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 74 24 ?? C7 44 24", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_pickup_iron_hook: unitId={num4}");
			UnitArmourerPickUpIronEventArgs eventArgs = new UnitArmourerPickUpIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerPickUpIron.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_pickup_iron_hook");
		tx.AddContextHook(c_game_unit_armourer_update_dropoff_iron_hook, "48 69 C8 ?? ?? ?? ?? 66 42 89 AC 29 ?? ?? ?? ?? 66 42 89 AC 29", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_dropoff_iron_hook: unitId={num4}");
			UnitArmourerDropOffIronEventArgs eventArgs = new UnitArmourerDropOffIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerDropOffIron.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_dropoff_iron_hook");
		tx.AddContextHook(c_game_unit_armourer_update_store_produce_hook, "41 BA ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 66 46 89 94 29", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_store_produce_hook: unitId={num4}");
			UnitArmourerStoreProduceEventArgs eventArgs = new UnitArmourerStoreProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerStoreProduce.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_store_produce_hook");
		tx.AddContextHook(c_game_unit_armourer_update_produce_hook, "45 8B C7 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_produce_hook: unitId={num4}");
			UnitArmourerProduceEventArgs eventArgs = new UnitArmourerProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerProduce.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_produce_hook");
		tx.AddContextHook(c_game_unit_armourer_update_dropoff_produce_hook, "44 8B C6 48 69 C8 ?? ?? ?? ?? 4A 0F BF 94 29", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_dropoff_produce_hook: unitId={num4}");
			UnitArmourerDropOffProduceEventArgs eventArgs = new UnitArmourerDropOffProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerDropOffProduce.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_dropoff_produce_hook");
		tx.AddContextHook(c_game_unit_armourer_update_pickup_produce_hook, "45 33 C0 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_armourer_update_pickup_produce_hook: unitId={num4}");
			UnitArmourerPickUpProduceEventArgs eventArgs = new UnitArmourerPickUpProduceEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnArmourerPickUpProduce.Raise(eventArgs);
		}, options, "c_game_unit_armourer_update_pickup_produce_hook");
		tx.AddContextHook(c_game_unit_hunter_update_pickup_meat_hook, "45 8B 84 3E", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->R9;
			Log.Debug($"c_game_unit_hunter_update_pickup_meat_hook: unitId={num4}");
			UnitHunterPickUpMeatEventArgs eventArgs = new UnitHunterPickUpMeatEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnHunterPickUpMeat.Raise(eventArgs);
		}, options, "c_game_unit_hunter_update_pickup_meat_hook");
		tx.AddContextHook(c_game_unit_hunter_update_dropoff_meat_hook, "48 69 CB ?? ?? ?? ?? 42 38 AC 29", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBX;
			Log.Debug($"c_game_unit_hunter_update_dropoff_meat_hook: unitId={num4}");
			UnitHunterDropOffMeatEventArgs eventArgs = new UnitHunterDropOffMeatEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnHunterDropOffMeat.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_hunter_update_dropoff_meat_hook");
		tx.AddContextHook(c_game_unit_quarry_grunt_update_pickup_stone_hook, "41 BA ?? ?? ?? ?? 48 69 CE", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RSI;
			Log.Debug($"c_game_unit_quarry_grunt_update_pickup_stone_hook: unitId={num4}");
			UnitQuarryGruntPickUpStoneEventArgs eventArgs = new UnitQuarryGruntPickUpStoneEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnQuarryGruntPickUpStone.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RSI)
		}, "c_game_unit_quarry_grunt_update_pickup_stone_hook");
		tx.AddContextHook(c_game_unit_quarry_grunt_update_dropoff_stone_hook, "44 8D 4E ?? 48 69 C8 ?? ?? ?? ?? 66 42 89 BC 39", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_quarry_grunt_update_dropoff_stone_hook: unitId={num4}");
			UnitQuarryGruntDropOffStoneEventArgs eventArgs = new UnitQuarryGruntDropOffStoneEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnQuarryGruntDropOffStone.Raise(eventArgs);
		}, options, "c_game_unit_quarry_grunt_update_dropoff_stone_hook");
		tx.AddContextHook(c_game_unit_quarry_ox_update_depart_hook, "49 8B CE 48 69 F3", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDX;
			Log.Debug($"c_game_unit_quarry_ox_update_depart_hook: unitId={num4}");
			UnitQuarryOxDepartEventArgs eventArgs = new UnitQuarryOxDepartEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnQuarryOxDepart.Raise(eventArgs);
		}, options, "c_game_unit_quarry_ox_update_depart_hook");
		tx.AddContextHook(c_game_unit_quarry_ox_update_dropoff_stone_hook, "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 4A 0F BF 84 31", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RAX;
			Log.Debug($"c_game_unit_quarry_ox_update_dropoff_hook: unitId={num4}");
			UnitQuarryOxDropOffStoneEventArgs eventArgs = new UnitQuarryOxDropOffStoneEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnQuarryOxDropOffStone.Raise(eventArgs);
		}, options, "c_game_unit_quarry_ox_update_dropoff_stone_hook");
		tx.AddContextHook(c_game_unit_miner2_update_pickup_iron_hook, "48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 39", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBX;
			Log.Debug($"c_game_unit_miner2_update_pickup_iron_hook: unitId={num4}");
			UnitMiner2PickUpIronEventArgs eventArgs = new UnitMiner2PickUpIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnMiner2PickUpIron.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_miner2_update_pickup_iron_hook");
		tx.AddContextHook(c_game_unit_miner2_update_dropoff_iron_hook, "41 8B D4 49 69 D9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->R9;
			Log.Debug($"c_game_unit_miner2_update_dropoff_iron_hook: unitId={num4}");
			UnitMiner2DropOffIronEventArgs eventArgs = new UnitMiner2DropOffIronEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnMiner2DropOffIron.Raise(eventArgs);
		}, options, "c_game_unit_miner2_update_dropoff_iron_hook");
		tx.AddContextHook(c_game_unit_pitcher_update_pickup_rawpitch_hook, "8B D7 48 69 D9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RCX;
			Log.Debug($"c_game_unit_pitcher_update_pickup_rawpitch_hook: unitId={num4}");
			UnitPitcherPickUpRawPitchEventArgs eventArgs = new UnitPitcherPickUpRawPitchEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnPitcherPickUpRawPitch.Raise(eventArgs);
		}, options, "c_game_unit_pitcher_update_pickup_rawpitch_hook");
		tx.AddContextHook(c_game_unit_pitcher_update_dropoff_rawpitch_hook, "48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 21", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBX;
			Log.Debug($"c_game_unit_pitcher_update_dropoff_rawpitch_hook: unitId={num4}");
			UnitPitcherDropOffRawPitchEventArgs eventArgs = new UnitPitcherDropOffRawPitchEventArgs(EventHookPhase.Pre, num4);
			UnitR3EventHooks.OnPitcherDropOffRawPitch.Raise(eventArgs);
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX)
		}, "c_game_unit_pitcher_update_dropoff_rawpitch_hook");
		tx.AddInline(c_game_unit_is_over_killingpit_update_hook, "41 BA ? ? ? ? 05", delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			asm.AddInstruction(overwritten[0]);
			asm.X64FastcallSafe((ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, ulong, ulong, ulong>>(delegate(ulong currentHealth, ulong unitId, ulong buildingOffset)
			{
				int num4 = (int)buildingOffset / sizeof(GameBuilding);
				Log.Debug($"c_game_unit_is_over_killingpit_update_hook: unitId={unitId}, buildingId={num4}, currentHealth={currentHealth}");
				int damage = GameBuildingManagerAPI.Instance.KillingPitDamage;
				UnitEnterKillingPitEventArgs e = new UnitEnterKillingPitEventArgs(EventHookPhase.Pre, (int)unitId, num4, (int)currentHealth, damage);
				UnitR3EventHooks.OnUnitEnterKillingPit.Raise(e);
				damage = e.Damage;
				return currentHealth - (ulong)damage;
			}), 3, delegate(Assembler a)
			{
				a.mov(AssemblerRegisters.rcx, AssemblerRegisters.rax);
				a.mov(AssemblerRegisters.rdx, AssemblerRegisters.rbx);
			}, false, false);
			asm.AddInstruction(overwritten[2]);
		}, 14, "c_game_unit_is_over_killingpit_update_hook");
		tx.AddContextHook(c_game_unit_transition_hook1_c_game_player_buy_mercenary, "48 63 C5 48 8D 15 ? ? ? ? 48 69 F0", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RBP;
			eChimps eChimps2 = (eChimps)ctx.Pointer->RDI;
			int num5 = (int)ctx.Pointer->R14;
			Log.Verbose($"c_game_unit_transition_hook1_c_game_player_buy_mercenary: unitId={num4}, nextUnitType={eChimps2}, playerId={num5}");
			UnitTransitionEventArgs e = new UnitTransitionEventArgs(EventHookPhase.Pre, num4, num5, eChimps2, UnitTransitionSource.MercenaryOutpost);
			UnitR3EventHooks.OnUnitTransition.Raise(e);
			ctx.Pointer->RDI = (ulong)e.NextUnitType;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RBP | X64SmartCPUContextRegs.R14)
		}, "c_game_unit_transition_hook1_c_game_player_buy_mercenary");
		tx.AddContextHook(c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary, "48 63 C7 48 69 C8 ? ? ? ? 48 03 D9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDI;
			eChimps eChimps2 = (eChimps)ctx.Pointer->R9;
			int num5 = (int)ctx.Pointer->RBP;
			Log.Verbose($"c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary: unitId={num4}, nextUnitType={eChimps2}, playerId={num5}");
			UnitTransitionEventArgs e = new UnitTransitionEventArgs(EventHookPhase.Pre, num4, num5, eChimps2, UnitTransitionSource.EuropeanBarracks);
			UnitR3EventHooks.OnUnitTransition.Raise(e);
			ctx.Pointer->R9 = (ulong)e.NextUnitType;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RBP)
		}, "c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary");
		tx.AddContextHook(c_game_unit_transition_hook3_c_game_building_assign_worker, "48 63 C7 45 33 C9", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDI;
			eChimps eChimps2 = (eChimps)ctx.Pointer->RBP;
			int owner = GameUnitManagerAPI.Instance.GetOwner(num4);
			Log.Verbose($"c_game_unit_transition_hook3_c_game_building_assign_worker: unitId={num4}, nextUnitType={eChimps2}, playerId={owner}");
			UnitTransitionEventArgs e = new UnitTransitionEventArgs(EventHookPhase.Pre, num4, owner, eChimps2, UnitTransitionSource.Worker);
			UnitR3EventHooks.OnUnitTransition.Raise(e);
			ctx.Pointer->RBP = (ulong)e.NextUnitType;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RBP)
		}, "c_game_unit_transition_hook3_c_game_building_assign_worker");
		tx.AddContextHook(c_game_unit_transition_hook4_c_game_unit_disband, "44 89 AB ? ? ? ? C7 83 ? ? ? ? ? ? ? ? 66 C7 83", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->RDI;
			eChimps eChimps2 = (eChimps)ctx.Pointer->R13;
			int owner = GameUnitManagerAPI.Instance.GetOwner(num4);
			Log.Verbose($"c_game_unit_transition_hook4_c_game_unit_disband: unitId={num4}, nextUnitType={eChimps2}, playerId={owner}");
			UnitTransitionEventArgs e = new UnitTransitionEventArgs(EventHookPhase.Pre, num4, owner, eChimps2, UnitTransitionSource.Disband);
			UnitR3EventHooks.OnUnitTransition.Raise(e);
			ctx.Pointer->R13 = (ulong)e.NextUnitType;
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.R13)
		}, "c_game_unit_transition_hook4_c_game_unit_disband");
		if (scanner.Scan("E8 ? ? ? ? 49 0F BF 8C 3E").TryReadFlowControlTarget(out var target))
		{
			c_game_unit_hunter_query_for_target = Marshal.GetDelegateForFunctionPointer<c_game_unit_hunter_query_for_target_delegate>((IntPtr)(long)target);
			LogHelper.Information("c_game_unit_hunter_query_for_target found at " + target.ToString("X16"), ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
			InstructionWalker instructionWalker = new InstructionWalker(target, new InstructionWalkerConfig
			{
				WalkRules = new InstructionWalkRule[1]
				{
					new InstructionWalkRule
					{
						InstructionSkip = 0,
						StartFrom = InstructionWalkerStartMode.Begin,
						Predicate = (Instruction instr) => instr.Mnemonic == Mnemonic.Movzx,
						Extractor = InstructionWalkerHelpers.ExtractInstructionAddress
					}
				}
			}, Plugin.Instance.LoggerFactory.CreateLogger("InstructionWalker"));
			if (instructionWalker.TryWalk<ulong>(out var result))
			{
				LogHelper.Information("Anchor point found: " + result.ToString("X16"), ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
				tx.AddInline(c_game_unit_hunter_query_for_target_hook, result, delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					asm.X64FastcallSafe((ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, ulong, bool>>(delegate(ulong queryChimpAddressVA_p0x29C, ulong hunterId)
					{
						bool result3 = false;
						GameUnitManagerAPI instance = GameUnitManagerAPI.Instance;
						ulong num4 = queryChimpAddressVA_p0x29C - 668;
						int queryUnitId = instance.GetUnitArray().GetIndexByAddress(num4) + 1;
						if (num4 != 0)
						{
							GameUnit* ptr = (GameUnit*)num4;
							if (ptr->r_UnitChimp == eChimps.CHIMP_TYPE_DEER || ptr->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT)
							{
								result3 = true;
							}
						}
						UnitHunterQueryTargetEventArgs e = new UnitHunterQueryTargetEventArgs(EventHookPhase.Pre, queryUnitId, (int)hunterId);
						UnitR3EventHooks.OnUnitHunterQueryTarget.Raise(e);
						if (e.IsValidTarget.HasValue)
						{
							result3 = e.IsValidTarget.Value;
						}
						return result3;
					}), 2, delegate(Assembler a)
					{
						a.mov(AssemblerRegisters.rcx, AssemblerRegisters.rbx);
						a.mov(AssemblerRegisters.rdx, AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp + 240L]);
					}, false, false);
					asm.cmp(AssemblerRegisters.ax, 1);
				}, 14, "c_game_unit_hunter_query_for_target_hook");
			}
			else
			{
				LogHelper.Error("Could not find anchor point", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
			}
		}
		else
		{
			LogHelper.Error("Could not retrieve function ptr to c_game_unit_hunter_query_for_target", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		}
		Microsoft.Extensions.Logging.ILogger logger = Plugin.Instance.LoggerFactory.CreateLogger("AIStateTracker");
		Array values = Enum.GetValues(typeof(eChimps));
		for (int num2 = 0; num2 < values.Length; num2++)
		{
			eChimps chimpValue = (eChimps)num2;
			if (!GameUnitManagerAPI.Instance.IsAIStateTracked(chimpValue))
			{
				continue;
			}
			HookHandle<X64FunctionCloneHook> newCloneHook = new HookHandle<X64FunctionCloneHook>();
			ulong num3 = ((ulong*)GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer)[(int)chimpValue];
			LogHelper.Information(string.Format("Installing AIStateTracker hook for [{0}] at [{1}]", chimpValue, num3.ToString("X16")), ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
			tx.AddCloneHook(newCloneHook, ((ulong*)GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer)[(int)chimpValue], new global::<>z__ReadOnlySingleElementList<FunctionClonePatch>(new FunctionClonePatch
			{
				Predicate = (Instruction instr) => instr.Mnemonic == Mnemonic.Mov && instr.HasOpKind(OpKind.Memory) && instr.MemoryDisplacement32 == 2328,
				Generator = delegate(Assembler asm, Instruction original, ref bool suppress)
				{
					Register opRegister = original.GetOpRegister(1);
					bool flag = opRegister == Register.None;
					Register memoryBase = original.MemoryBase;
					Register memoryIndex = original.MemoryIndex;
					AssemblerRegister64 src = new AssemblerRegister64(memoryBase.GetFullRegister());
					AssemblerRegister64 src2 = ((memoryIndex != Register.None) ? new AssemblerRegister64(memoryIndex.GetFullRegister()) : default(AssemblerRegister64));
					asm.push(AssemblerRegisters.r10);
					asm.push(AssemblerRegisters.r11);
					asm.push(AssemblerRegisters.rcx);
					asm.mov(AssemblerRegisters.r10, src);
					if (memoryIndex != Register.None)
					{
						asm.mov(AssemblerRegisters.r11, src2);
					}
					if (!flag)
					{
						asm.movzx(AssemblerRegisters.rcx, new AssemblerRegister16(opRegister));
					}
					else
					{
						asm.mov(AssemblerRegisters.rcx, original.GetImmediate(1));
					}
					Func<ulong, ushort> callback = delegate(ulong num4)
					{
						int currentContextUnitId = GameUnitManagerAPI.Instance.GetCurrentContextUnitId();
						UnitAIStateEventArgs e = new UnitAIStateEventArgs(EventHookPhase.Pre, currentContextUnitId, chimpValue, (int)num4);
						UnitR3EventHooks.OnUnitAIStateChange.Raise(e);
						return (ushort)e.State;
					};
					ulong targetAddress = (ulong)(long)(newCloneHook?.Hook?.PinDelegate(callback)).Value;
					asm.X64FastcallSafe(targetAddress, 1, null, false, false);
					if (memoryIndex != Register.None)
					{
						asm.lea(AssemblerRegisters.rcx, AssemblerRegisters.__[AssemblerRegisters.r10 + AssemblerRegisters.r11 + 2328L]);
					}
					else
					{
						asm.lea(AssemblerRegisters.rcx, AssemblerRegisters.__[AssemblerRegisters.r10 + 2328L]);
					}
					asm.mov(AssemblerRegisters.__word_ptr[AssemblerRegisters.rcx], AssemblerRegisters.ax);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.r11);
					asm.pop(AssemblerRegisters.r10);
					suppress = true;
				}
			}), $"unitStateTracker_{chimpValue}");
		}
		tx.AddContextHook(c_game_unit_control_capability_unknown, "49 63 C1 48 8D 2D ? ? ? ? 48 0F BF 94 45 ? ? ? ? 42 0F BF 84 26", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			GameUnitManagerAPI instance = GameUnitManagerAPI.Instance;
			int indexByOffset = instance.GetUnitArray().GetIndexByOffset(ctx.Pointer->RSI);
			eChimps type = instance.GetType(indexByOffset);
			int canAttackWalls = instance.GetCanAttackWalls(type);
			if (canAttackWalls != -2)
			{
				ctx.Pointer->RDI = (ulong)canAttackWalls;
			}
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RSI | X64SmartCPUContextRegs.RDI)
		}, "c_game_unit_control_capability_unknown");
		tx.AddContextHook(c_game_unit_fsm_arabslave_attack_capability_wall, "4A 63 84 21 ? ? ? ? 4C 0F BF B4 45", delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num4 = (int)ctx.Pointer->R14;
			int num5 = (int)ctx.Pointer->RAX;
			if (num5 != 0)
			{
				GameTileManagerAPI instance = GameTileManagerAPI.Instance;
				if (num4 == 0 && instance.HasTilePropertyFlag(num5, TilePropertyFlag.IsWall))
				{
					ctx.Pointer->R14 = (ulong)num5;
				}
			}
		}, new ContextHookOptions
		{
			Registers = (X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.R14),
			Placement = OverwrittenInstructionPlacement.BeforeCallback
		}, "c_game_unit_fsm_arabslave_attack_capability_wall");
		tx.AddInline(c_game_unit_fsm_arabslave_attack_capability_wall2, "4D 69 FE ? ? ? ? 48 8D 1D", delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			Label label = asm.CreateLabel("lblSkip");
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r14);
			asm.X64FastcallSafe((ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, bool>>(delegate(ulong rcx)
			{
				int buildingId = (int)rcx;
				return GameBuildingManagerAPI.Instance.IsValid(buildingId) ? true : false;
			}), 1, null, false, false);
			asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.je(label);
			asm.AddInstructions(overwritten);
			asm.Label(ref label);
		}, 22, "c_game_unit_fsm_arabslave_attack_capability_wall2");
		tx.AddInline(c_game_unit_fsm_arabslave_attack_capability_wall3, "41 8B D6 48 8B CB E8 ? ? ? ? 85 C0 48 63 05", delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			Label label = asm.CreateLabel("lblSkip");
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r14);
			asm.X64FastcallSafe((ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, bool>>(delegate(ulong rcx)
			{
				int buildingId = (int)rcx;
				return GameBuildingManagerAPI.Instance.IsValid(buildingId) ? true : false;
			}), 1, null, false, false);
			asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.je(label);
			asm.AddInstructions(overwritten.Slice(0, overwritten.Length - 1));
			asm.Label(ref label);
			asm.AddInstruction(overwritten[overwritten.Length - 1]);
		}, 14, "c_game_unit_fsm_arabslave_attack_capability_wall3");
		customEngageRangeArrayFlag = nativeStateBlock.AllocateFlags(89);
		customEngageRangeArray = nativeStateBlock.AllocateArray(89, (short)0);
		defaultEngageRangeArray = nativeStateBlock.AllocateArray(89, (short)0);
		UnitFunctionsVTable* pointer = GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer;
		(eChimps, ulong)[] source = new(eChimps, ulong)[13]
		{
			(eChimps.CHIMP_TYPE_ARCHER, (ulong)pointer->ArcherUpdate),
			(eChimps.CHIMP_TYPE_ARAB_BALLISTA, (ulong)pointer->ArabBallistaUpdate),
			(eChimps.CHIMP_TYPE_ARAB_BOW, (ulong)pointer->ArabBowUpdate),
			(eChimps.CHIMP_TYPE_BALLISTA, (ulong)pointer->BallistaUpdate),
			(eChimps.CHIMP_TYPE_TREBUCHET, (ulong)pointer->TrebuchetUpdate),
			(eChimps.CHIMP_TYPE_CATAPULT, (ulong)pointer->CatapultUpdate),
			(eChimps.CHIMP_TYPE_MANGONEL, (ulong)pointer->MangonelUpdate),
			(eChimps.CHIMP_TYPE_ARAB_SLINGER, (ulong)pointer->ArabSlingerUpdate),
			(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, (ulong)pointer->BedouinHeavyCamelUpdate),
			(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, (ulong)pointer->BedouinSkirmisherUpdate),
			(eChimps.CHIMP_TYPE_XBOWMAN, (ulong)pointer->XbowmanUpdate),
			(eChimps.CHIMP_TYPE_ARAB_GRENADIER, (ulong)pointer->ArabGrenadierUpdate),
			(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, (ulong)pointer->ArabHorsemanUpdate)
		};
		foreach (IGrouping<ulong, (eChimps, ulong)> item in from x in source
			group x by x.Address)
		{
			ulong key = item.Key;
			Log.Information($"Hooking unit update function at address: 0x{key:X} chimp: {item.First().Item1}");
			InstructionWalker instructionWalker2 = new InstructionWalker(key, new InstructionWalkerConfig
			{
				Bitness = 64,
				WalkRules = new InstructionWalkRule[1]
				{
					new InstructionWalkRule
					{
						Predicate = (Instruction x) => x.Mnemonic == Mnemonic.Cmp && x.Op0Kind == OpKind.Memory && x.MemoryDisplacement32 == 2302 && x.Op1Kind == OpKind.Register,
						Extractor = InstructionWalkerHelpers.ExtractInstructionAddress
					}
				},
				EndDetection = FunctionEndDetection.ReachableTerminator
			});
			ulong result2;
			while (instructionWalker2.TryWalk<ulong>(out result2))
			{
				Log.Information($"Installing dynamic inline hook at: 0x{result2:X16} for Dynamic Unit Range Support");
				HookHandle<X64InlineHook> hookHandle = new HookHandle<X64InlineHook>();
				tx.AddInline(hookHandle, result2, delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					Span<Instruction> span = overwritten.CloneInstructionsWithoutIP();
					AssemblerRegister64 dst = new AssemblerRegister64(span[0].Op1Register.GetFullRegister());
					asm.AddInstruction(overwritten[0]);
					Label label = asm.CreateLabel();
					Label label2 = asm.CreateLabel();
					Label label3 = asm.CreateLabel();
					asm.push(AssemblerRegisters.r13);
					asm.push(AssemblerRegisters.r14);
					asm.push(AssemblerRegisters.r15);
					asm.pushfq();
					asm.mov(AssemblerRegisters.r13, GameGlobalsManager.Instance.CurrentContextUnitIdVA);
					asm.mov(AssemblerRegisters.r13, AssemblerRegisters.__dword_ptr[AssemblerRegisters.r13]);
					asm.mov(AssemblerRegisters.r15, GameGlobalsManager.Instance.GameUnitManagerVA);
					asm.add(AssemblerRegisters.r15, 1766);
					asm.imul(AssemblerRegisters.r14, AssemblerRegisters.r13, 1168);
					asm.add(AssemblerRegisters.r15, AssemblerRegisters.r14);
					asm.movsx(AssemblerRegisters.r15d, AssemblerRegisters.__word_ptr[AssemblerRegisters.r15]);
					asm.mov(AssemblerRegisters.r14, defaultEngageRangeArray.Address);
					asm.cmp(AssemblerRegisters.__word_ptr[AssemblerRegisters.r14 + AssemblerRegisters.r15 * 2], 0);
					asm.jnz(label3);
					asm.AddInstruction(Instruction.Create(Code.Mov_rm16_r16, new MemoryOperand(Register.R14, Register.R15, 2), span[0].Op1Register));
					asm.Label(ref label3);
					asm.TestNativeFlag(customEngageRangeArrayFlag, AssemblerRegisters.r15, AssemblerRegisters.r14);
					asm.jz(label);
					asm.push(dst);
					asm.mov(AssemblerRegisters.r14, customEngageRangeArray.Address);
					asm.movsx(dst, AssemblerRegisters.__word_ptr[AssemblerRegisters.r14 + AssemblerRegisters.r15 * 2]);
					asm.AddInstruction(span[0]);
					asm.pop(dst);
					asm.lea(AssemblerRegisters.rsp, AssemblerRegisters.__[AssemblerRegisters.rsp + 8L]);
					asm.jmp(label2);
					asm.Label(ref label);
					asm.popfq();
					asm.Label(ref label2);
					asm.pop(AssemblerRegisters.r15);
					asm.pop(AssemblerRegisters.r14);
					asm.pop(AssemblerRegisters.r13);
					asm.AddInstructions(overwritten.Slice(1));
				}, 14, $"UnitUpdateEngageRange_{key:X}_{result2:X}");
				foreach (var item2 in item)
				{
					if (!UnitEngageRangedHooks.TryGetValue(item2.Item1, out List<HookHandle<X64InlineHook>> value))
					{
						value = (UnitEngageRangedHooks[item2.Item1] = new List<HookHandle<X64InlineHook>>());
					}
					value.Add(hookHandle);
				}
			}
		}
		customInteractRangeArrayFlag = nativeStateBlock.AllocateFlags(89);
		customInteractRangeArray = nativeStateBlock.AllocateArray(89, 0);
		defaultInteractRangeArray = nativeStateBlock.AllocateArray(89, 0);
		tx.AddInline(c_game_unit_control_capability_interactrange_hook, "42 0F BF 84 23 ? ? ? ? 42 0F BF 94 26", delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			Label label = asm.CreateLabel();
			Label label2 = asm.CreateLabel();
			asm.push(AssemblerRegisters.r8);
			asm.push(AssemblerRegisters.r9);
			asm.mov(AssemblerRegisters.r8, GameGlobalsManager.Instance.GameUnitManagerVA);
			asm.add(AssemblerRegisters.r8, 1766);
			asm.add(AssemblerRegisters.r8, AssemblerRegisters.rsi);
			asm.movsx(AssemblerRegisters.r8d, AssemblerRegisters.__word_ptr[AssemblerRegisters.r8]);
			asm.mov(AssemblerRegisters.r9, defaultInteractRangeArray.Address);
			asm.cmp(AssemblerRegisters.__dword_ptr[AssemblerRegisters.r9 + AssemblerRegisters.r8 * 4], 0);
			asm.jnz(label2);
			asm.mov(AssemblerRegisters.__dword_ptr[AssemblerRegisters.r9 + AssemblerRegisters.r8 * 4], AssemblerRegisters.r15d);
			asm.Label(ref label2);
			asm.TestNativeFlag(customInteractRangeArrayFlag, AssemblerRegisters.r8, AssemblerRegisters.r9);
			asm.jz(label);
			asm.mov(AssemblerRegisters.r9, customInteractRangeArray.Address);
			asm.mov(AssemblerRegisters.r15, AssemblerRegisters.__qword_ptr[AssemblerRegisters.r9 + AssemblerRegisters.r8 * 4]);
			asm.Label(ref label);
			asm.pop(AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.r8);
			asm.AddInstructions(overwritten);
		}, 14, "c_game_unit_control_capability_interactrange_hook");
	}

	public static long c_game_dll_troopselection_hook_impl(int mouseState, byte rightDown, byte rightUp, uint selectedChimpsCount, ulong pSelectedChimps, byte selectionOn, byte selectionEstablished, uint underCursorCount, ulong pUnderCursorChimps, int mousePosX, int mousePosY, byte overTopHalf, uint onScreenCount, ulong pOnScreenChimps)
	{
		try
		{
			GameUnitManagerAPI.FilterUnselectableUnits(pSelectedChimps, selectedChimpsCount);
			GameUnitManagerAPI.FilterUnselectableUnits(pUnderCursorChimps, underCursorCount);
			GameUnitManagerAPI.FilterUnselectableUnits(pOnScreenChimps, onScreenCount);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Error while filtering unselectable units", "c_game_dll_troopselection_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		}
		return c_game_dll_troopselection_hook.Original(mouseState, rightDown, rightUp, selectedChimpsCount, pSelectedChimps, selectionOn, selectionEstablished, underCursorCount, pUnderCursorChimps, mousePosX, mousePosY, overTopHalf, onScreenCount, pOnScreenChimps);
	}

	public static long c_game_unit_calculate_worker_good_yield_hook_impl(int unitId, int goodAmount, int b50PercentBonus)
	{
		LogHelper.Debug($"unitId={unitId}, goodAmount={goodAmount}, b50PercentBonus={b50PercentBonus}", "c_game_unit_calculate_worker_good_yield_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitCalculateBonusYieldEventArgs e = new UnitCalculateBonusYieldEventArgs(EventHookPhase.Pre, unitId, goodAmount, b50PercentBonus);
		UnitR3EventHooks.OnCalculateBonusYield.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_calculate_worker_good_yield_hook.Original(e.UnitId, e.GoodAmount, e.b50PercentBonus));
			UnitCalculateBonusYieldEventArgs e2 = new UnitCalculateBonusYieldEventArgs(EventHookPhase.Post, unitId, goodAmount, b50PercentBonus)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnCalculateBonusYield.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}

	public unsafe static long c_game_unit_issueorder_movehere_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int unitId, int tileX, int tileY, int unknown)
	{
		LogHelper.Verbose(string.Format("pGameUnitManager={0}, unitId={1}, tileX={2}, tileY={3}, unknown={4}", new IntPtr((GameUnitManager*)pGameUnitManager).ToString("X16"), unitId, tileX, tileY, unknown), "c_game_unit_issueorder_movehere_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitMoveHereEventArgs e = new UnitMoveHereEventArgs(EventHookPhase.Pre, unitId, tileX, tileY, unknown);
		UnitR3EventHooks.OnUnitMoveHere.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_issueorder_movehere_hook.Original(pGameUnitManager, e.UnitId, e.TileX, e.TileY, e.Unknown));
			UnitMoveHereEventArgs e2 = new UnitMoveHereEventArgs(EventHookPhase.Post, unitId, tileX, tileY, unknown)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnUnitMoveHere.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}

	public unsafe static long c_game_unit_delete_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, uint unitId)
	{
		LogHelper.Verbose(string.Format("pGameUnitManager={0}, unitId={1}", new IntPtr((GameUnitManager*)pGameUnitManager).ToString("X16"), unitId), "c_game_unit_delete_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitDeleteEventArgs e = new UnitDeleteEventArgs(EventHookPhase.Pre, unitId);
		UnitR3EventHooks.OnUnitDelete.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_delete_hook.Original(pGameUnitManager, e.UnitId));
			UnitDeleteEventArgs e2 = new UnitDeleteEventArgs(EventHookPhase.Post, unitId)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnUnitDelete.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}

	public static void c_game_unit_handle_movement_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, uint unitId)
	{
		UnitMovementEventArgs e = new UnitMovementEventArgs(EventHookPhase.Pre, unitId);
		UnitR3EventHooks.OnUnitMovement.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			c_game_unit_handle_movement_hook.Original(pGameUnitManager, e.UnitId);
			UnitMovementEventArgs eventArgs = new UnitMovementEventArgs(EventHookPhase.Post, unitId);
			UnitR3EventHooks.OnUnitMovement.Raise(eventArgs);
		}
	}

	public unsafe static long c_game_unit_takedamage_projectile_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int attackedUnitId, int projectileId, int value)
	{
		LogHelper.Verbose(string.Format("pGameUnitManager={0}, attackedUnitId={1}, projectileId={2}, value={3}", new IntPtr((GameUnitManager*)pGameUnitManager).ToString("X16"), attackedUnitId, projectileId, value), "c_game_unit_takedamage_projectile_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitTakeDamageByProjectileEventArgs e = new UnitTakeDamageByProjectileEventArgs(EventHookPhase.Pre, attackedUnitId, projectileId, value);
		UnitR3EventHooks.OnUnitTakeProjectileDamage.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_takedamage_projectile_hook.Original(pGameUnitManager, e.AttackedUnitId, e.ProjectileId, e.UnknownBool));
			UnitTakeDamageByProjectileEventArgs e2 = new UnitTakeDamageByProjectileEventArgs(EventHookPhase.Post, attackedUnitId, projectileId, value)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnUnitTakeProjectileDamage.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}

	public unsafe static long c_game_unit_takedamage_melee_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int attackingUnitId, int damagedUnitId, int value)
	{
		LogHelper.Verbose(string.Format("pGameUnitManager={0}, attackingUnitId={1}, damagedUnitId={2}, value={3}", new IntPtr((GameUnitManager*)pGameUnitManager).ToString("X16"), attackingUnitId, damagedUnitId, value), "c_game_unit_takedamage_melee_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitTakeDamageByMeleeEventArgs e = new UnitTakeDamageByMeleeEventArgs(EventHookPhase.Pre, attackingUnitId, damagedUnitId, value);
		UnitR3EventHooks.OnUnitTakeMeleeDamage.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_takedamage_melee_hook.Original(pGameUnitManager, e.AttackingUnitId, e.DamagedUnitId, e.Damage));
			UnitTakeDamageByMeleeEventArgs e2 = new UnitTakeDamageByMeleeEventArgs(EventHookPhase.Post, attackingUnitId, damagedUnitId, value)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnUnitTakeMeleeDamage.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}

	public unsafe static long c_game_unit_spawn_ex_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int playerOwnerId, int playerColorId, int worldTileX, int worldTileY, int heightElevation, eChimps unitType)
	{
		LogHelper.Debug(string.Format("pGameUnitManager={0}, playerOwnerId={1}, playerColorId={2}, worldTileX={3}, worldTileY={4}, heightElevation={5}, unitType={6}", new IntPtr((GameUnitManager*)pGameUnitManager).ToString("X16"), playerOwnerId, playerColorId, worldTileX, worldTileY, heightElevation, unitType), "c_game_unit_spawn_ex_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-19079-6972\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkUnitDetours.cs");
		UnitCreateEventArgs e = new UnitCreateEventArgs(EventHookPhase.Pre, playerColorId, playerOwnerId, worldTileX, worldTileY, heightElevation, unitType);
		UnitR3EventHooks.OnUnitCreate.Raise(e);
		if (!e.SkipOriginalFunction)
		{
			long returnValue = (e.ReturnValue = c_game_unit_spawn_ex_hook.Original(pGameUnitManager, e.PlayerOwnerId, e.PlayerColorId, e.WorldTileX, e.WorldTileY, e.HeightElevation, e.UnitType));
			UnitCreateEventArgs e2 = new UnitCreateEventArgs(EventHookPhase.Post, playerColorId, playerOwnerId, worldTileX, worldTileY, heightElevation, unitType)
			{
				ReturnValue = returnValue
			};
			UnitR3EventHooks.OnUnitCreate.Raise(e2);
			e.ReturnValue = e2.ReturnValue;
		}
		return e.ReturnValue;
	}
}
