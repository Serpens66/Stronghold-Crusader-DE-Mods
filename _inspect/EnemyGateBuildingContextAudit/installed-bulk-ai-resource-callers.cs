#define DEBUG
using System;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.Lua;

namespace SHCDESE.Detours;

public class BulkAIDetours
{
	public delegate bool TryOrderTribeToNearestEnemyGatehouseDelegate(IntPtr pAITargetSelectionResult, int tribeId);

	public delegate bool OrderSiegeAssassinsToGatehouseOrEnemyUnitDelegate(NativePointer<InternalAIC> pAIC, int playerId);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate bool c_game_ai_should_not_build_hovel_hook_delegate(IntPtr pLordManager, int playerId, eMappers eMappers);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void c_game_ai_build_wall_delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers eMappers);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void c_game_dll_importaiv_delegate(int bankIndex, int variantIndex, ulong aivData, int length, int custom);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate ulong c_game_ai_enqueue_message_wrapper_delegate(ulong pMessageManager, ulong playerId, AILords aiLord, ulong msgType);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate ulong c_game_ai_enqueue_message_delegate(ulong pMessageManager, ulong a2, ulong msgType, ulong pVideoPath, ulong pAudioPath, int playerId);

	public static TryOrderTribeToNearestEnemyGatehouseDelegate TryOrderTribeToNearestEnemyGatehouse;

	public static OrderSiegeAssassinsToGatehouseOrEnemyUnitDelegate OrderSiegeAssassinsToGatehouseOrEnemyUnit;

	public static IntPtr _newFilePathBufferVA;

	public static HookHandle<X64InlineHook> c_game_ai_enqueue_message_internal_pathreloc_hook = new HookHandle<X64InlineHook>();

	public static HookHandle<X64InlineHook> c_game_ai_setup_siege_pathing_stuff_hook = new HookHandle<X64InlineHook>();

	internal static DetourHandle<c_game_ai_build_wall_delegate> c_game_ai_build_wall_hook = new DetourHandle<c_game_ai_build_wall_delegate>();

	internal static DetourHandle<c_game_dll_importaiv_delegate> c_game_dll_importaiv_hook = new DetourHandle<c_game_dll_importaiv_delegate>();

	internal static DetourHandle<c_game_ai_enqueue_message_wrapper_delegate> c_game_ai_enqueue_message_wrapper_hook = new DetourHandle<c_game_ai_enqueue_message_wrapper_delegate>();

	internal static DetourHandle<c_game_ai_enqueue_message_delegate> c_game_ai_enqueue_message_hook = new DetourHandle<c_game_ai_enqueue_message_delegate>();

	public unsafe BulkAIDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
	{
		LogHelper.Information("Applying", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		ulong num = (ulong)(long)CrusaderLibrary.Instance.LibraryModuleHandle;
		tx.AddDetour(c_game_ai_enqueue_message_hook, "83 39 ? 0F 84 ? ? ? ? 48 63 81 ? ? ? ? 83 F8 ? 0F 84 ? ? ? ? C7 84 81", c_game_ai_enqueue_message_hook_impl, "c_game_ai_enqueue_message_hook");
		tx.AddDetour(c_game_ai_enqueue_message_wrapper_hook, "4C 8B DC 55 56 41 56", c_game_ai_enqueue_message_wrapper_hook_impl, "c_game_ai_enqueue_message_wrapper_hook");
		tx.AddDetour(c_game_dll_importaiv_hook, HookTarget.FromExport("DLL_ImportAIV", (IntPtr)0), c_game_dll_importaiv_hook_impl, "c_game_dll_importaiv_hook");
		tx.AddDetour(c_game_ai_build_wall_hook, "48 89 5C 24 ? 48 89 6C 24 ? 56 57 41 56 48 83 EC ? 0F B7 5C 24", c_game_ai_build_wall_hook_impl, "c_game_ai_build_wall_hook");
		tx.AddContextHook(c_game_ai_setup_siege_pathing_stuff_hook, HookTarget.FromPattern("BA ? ? ? ? E8 ? ? ? ? 48 8B 5C 24 ? 48 8B 6C 24 ? 48 8B 74 24 ? 48 83 C4 ? 41 5F 41 5E 5F", 5), delegate(NativePointer<X64SmartCPUContext> ctx)
		{
			int num2 = (int)ctx.Pointer->RDX;
			int num3 = (int)ctx.Pointer->R8;
			int num4 = (int)ctx.Pointer->R9;
			ulong* stackPtr = ctx.Pointer->GetStackPtr<ulong>(0);
			ulong num5 = *stackPtr;
			LogHelper.Debug($"c_game_ai_setup_siege_pathing_stuff_hook, maxSearchRange={num2}, preferredStandoffDist={num3}, pathConnectionLayer={num4}, playerId={num5}", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			AISelectSiegeRallypointEventArgs e = new AISelectSiegeRallypointEventArgs(EventHookPhase.Pre, num2, num3, num4, (int)num5);
			AIR3EventHooks.OnAISelectSiegeRallypoint.Raise(e);
			ctx.Pointer->RDX = (ulong)e.MaxSearchRange;
			ctx.Pointer->R8 = (ulong)e.PreferredStandoffDistance;
			ctx.Pointer->R9 = (ulong)e.PathConnectionLayer;
			*stackPtr = (ulong)e.PlayerId;
		}, new ContextHookOptions
		{
			Registers = X64SmartCPUContextRegs.Volatile
		}, "c_game_ai_setup_siege_pathing_stuff_hook");
		tx.AddDetour<c_game_ai_should_not_build_hovel_hook_delegate>("41 83 F8 ? 75 ? 48 63 C2 48 8D 15 ? ? ? ? 48 69 C8", c_game_ai_should_not_build_hovel_hook_impl, "c_game_ai_should_not_build_hovel_hook_impl");
		_newFilePathBufferVA = NativeMemoryManager.Alloc(256, MemoryProtection.ReadWrite);
		tx.AddInline(c_game_ai_enqueue_message_internal_pathreloc_hook, "48 8D 4C 24 ? E8 ? ? ? ? 48 8D 54 24 ? 48 8D 0D ? ? ? ? E8 ? ? ? ? FF 15", delegate(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			asm.mov(AssemblerRegisters.rcx, (ulong)(long)_newFilePathBufferVA);
			asm.AddInstruction(overwritten[1]);
			asm.mov(AssemblerRegisters.rdx, (ulong)(long)_newFilePathBufferVA);
		}, 15, "c_game_ai_enqueue_message_internal_pathreloc_hook");
		if (scanner.Scan("48 83 EC ? 48 63 CA 4C 8D 05").Found)
		{
			OrderSiegeAssassinsToGatehouseOrEnemyUnit = Marshal.GetDelegateForFunctionPointer<OrderSiegeAssassinsToGatehouseOrEnemyUnitDelegate>((IntPtr)(long)scanner.CurrentAddress);
		}
		else
		{
			LogHelper.Error("Could not retrieve function ptr to c_game_ai_order_siege_assassins_to_gatehouse_or_enemy_unit", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		}
		if (!scanner.Scan("E8 ? ? ? ? 85 C0 75 ? 8B D3 48 8D 0D").TryGetFunction<TryOrderTribeToNearestEnemyGatehouseDelegate>(out TryOrderTribeToNearestEnemyGatehouse))
		{
			LogHelper.Error("Could not retrieve function ptr to c_game_ai_order_siege_assassins_to_gatehouse_or_enemy_unit", ".ctor", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		}
	}

	public unsafe static bool c_game_ai_should_not_build_hovel_hook_impl(IntPtr pLordManager, int playerId, eMappers eMappers)
	{
		try
		{
			LogHelper.Verbose($"playerId={playerId}, eMappers={eMappers}", "c_game_ai_should_not_build_hovel_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			AIQueryBuildHovelEventArgs e = new AIQueryBuildHovelEventArgs(EventHookPhase.Pre, playerId, eMappers);
			AIR3EventHooks.OnAIQueryBuildHovelEventArgs.Raise(e);
			if (e.SkipOriginalFunction)
			{
				return e.ReturnValue;
			}
			if (eMappers != eMappers.MAPPER_HOVEL)
			{
				return false;
			}
			if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out var resources))
			{
				LogHelper.Warning($"Could not find playerresource by id: {playerId}", "c_game_ai_should_not_build_hovel_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
				return false;
			}
			int r_CivilianHousingSpace = (int)resources->r_CivilianHousingSpace;
			if (r_CivilianHousingSpace > 12 && (resources->r_TotalPopulation < r_CivilianHousingSpace || resources->r_IdlePeasantCurrent >= 5 || resources->r_IdlePeasantAverage >= 5 || resources->r_CurrentPopularity < 5000))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Exception during build wall handling for ai", "c_game_ai_should_not_build_hovel_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		}
		return false;
	}

	public static void c_game_ai_build_wall_hook_impl(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers eMappers)
	{
		AIBuildWallEventArgs e = null;
		try
		{
			LogHelper.Verbose($"playerId={playerId}, tileX={tileX}, tileY={tileY}, eMappers={eMappers}", "c_game_ai_build_wall_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			e = new AIBuildWallEventArgs(EventHookPhase.Pre, playerId, tileX, tileY, eMappers);
			AIR3EventHooks.OnAIBuildWall.Raise(e);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Exception during build wall handling for ai", "c_game_ai_build_wall_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		}
		finally
		{
			c_game_ai_build_wall_hook.Original(pTileManager, e?.PlayerId ?? playerId, e?.TileX ?? tileX, e?.TileY ?? tileY, e?.Mappers ?? eMappers);
			e = new AIBuildWallEventArgs(EventHookPhase.Post, playerId, tileX, tileY, eMappers);
			AIR3EventHooks.OnAIBuildWall.Raise(e);
		}
	}

	public static void c_game_dll_importaiv_hook_impl(int bankIndex, int variantIndex, ulong aivData, int elementCount, int isCustom)
	{
		LogHelper.Information($"Loaded AIV bank={bankIndex}, variant={variantIndex}, elementCount={elementCount}, isCustom={isCustom}", "c_game_dll_importaiv_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		c_game_dll_importaiv_hook.Original(bankIndex, variantIndex, aivData, elementCount, isCustom);
		if ((uint)bankIndex >= 8u)
		{
			return;
		}
		try
		{
			int playerId = bankIndex + 1;
			string customAILordNameByPlayerId = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId);
			if (GameAIManagerAPI.Instance.IsSupportedCustomLord(customAILordNameByPlayerId) && GameAIManagerAPI.Instance.TryGetLuaInitPath(customAILordNameByPlayerId, out string luaInitPath))
			{
				LuaManager.Instance.RegisterLordAI(customAILordNameByPlayerId, luaInitPath, playerId);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Error during importaiv callback", "c_game_dll_importaiv_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		}
	}

	public static ulong c_game_ai_enqueue_message_wrapper_hook_impl(ulong pMessageManager, ulong playerId, AILords aiLord, ulong msgType)
	{
		LogHelper.Debug(string.Format("c_game_ai_enqueue_message_wrapper_hook_impl, pMessageManager={0}, playerId={1}, aiLord={2}, msgType={3}", pMessageManager.ToString("X16"), playerId, aiLord, msgType), "c_game_ai_enqueue_message_wrapper_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
		ulong result = 0uL;
		try
		{
			GameSoundManagerAPI instance = GameSoundManagerAPI.Instance;
			if (instance.GetSuppressMessages())
			{
				return result;
			}
			result = c_game_ai_enqueue_message_wrapper_hook.Original(pMessageManager, playerId, aiLord, msgType);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Error during attempted message suppression", "c_game_ai_enqueue_message_wrapper_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			return c_game_ai_enqueue_message_wrapper_hook.Original(pMessageManager, playerId, aiLord, msgType);
		}
		return result;
	}

	public static ulong c_game_ai_enqueue_message_hook_impl(ulong pMessageManager, ulong a2, ulong msgType, ulong pVideoPath, ulong pAudioPath, int playerId)
	{
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Invalid comparison between Unknown and I4
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			LogHelper.Debug(string.Format("pMessageManager={0}, a2={1}, msgType={2}, pVideoPath={3}, pAudioPath={4}, playerId={5}", pMessageManager.ToString("X16"), a2, msgType, pVideoPath.ToString("X16"), pAudioPath.ToString("X16"), playerId), "c_game_ai_enqueue_message_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			string text = Marshal.PtrToStringAnsi((IntPtr)(long)pVideoPath);
			string text2 = Marshal.PtrToStringAnsi((IntPtr)(long)pAudioPath);
			LogHelper.Debug("VideoPath: [" + text + "], AudioPath: [" + text2 + "]", "c_game_ai_enqueue_message_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			AILords aILord = GamePlayerManagerAPI.Instance.GetAILord(Math.Abs(playerId));
			if (GameAIManagerAPI.Instance.TryGetMessageTypeFromIndex((int)msgType, out var messageType))
			{
				string text3 = "not-found";
				text3 = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(Math.Abs(playerId));
				LogHelper.Debug($"Category: {messageType} RealLord: {aILord}, lordName={text3}", "c_game_ai_enqueue_message_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
				AILords val = aILord;
				AILords val2 = val;
				if (val2 - 30 <= 7 && GameAIManagerAPI.Instance.TryGetVideoAndAudio(text3, messageType, out string videoPath, out string audioPath))
				{
					LogHelper.Information("new vid=" + videoPath + ", new aud=" + audioPath, "c_game_ai_enqueue_message_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
					if (!string.IsNullOrEmpty(videoPath))
					{
						intPtr = Marshal.StringToHGlobalAnsi(videoPath);
						pVideoPath = (ulong)(long)intPtr;
					}
					if (!string.IsNullOrEmpty(audioPath))
					{
						intPtr2 = Marshal.StringToHGlobalAnsi(audioPath);
						pAudioPath = (ulong)(long)intPtr2;
					}
				}
			}
			return c_game_ai_enqueue_message_hook.Original(pMessageManager, a2, msgType, pVideoPath, pAudioPath, playerId);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Exception", "c_game_ai_enqueue_message_hook_impl", "C:\\Users\\Serpens66\\AppData\\Local\\Temp\\shcdese-local-build-7081-32300\\source\\src\\SHCDESE.BepInEx\\Detours\\BulkAIDetours.cs");
			return c_game_ai_enqueue_message_hook.Original(pMessageManager, a2, msgType, pVideoPath, pAudioPath, playerId);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
			if (intPtr2 != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr2);
			}
		}
	}
}
