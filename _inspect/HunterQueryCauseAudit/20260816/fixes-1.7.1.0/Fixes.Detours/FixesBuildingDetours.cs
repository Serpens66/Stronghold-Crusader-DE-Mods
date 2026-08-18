using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Fixes.Boostrap;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Query;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Memory.Scanners;

namespace Fixes.Detours;

[SuppressUnmanagedCodeSecurity]
public class FixesBuildingDetours : IDisposable
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate long c_game_player_build_structure_delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree);

	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static InlineHookGenerator _003C_003E9__4_0;

		public static InlineHookGenerator _003C_003E9__4_1;

		public static Func<eMappers, int, int> _003C_003E9__4_4;

		public static InlineHookGenerator _003C_003E9__4_2;

		public static Func<IntPtr, int, int, int, eMappers, int, int, byte, bool> _003C_003E9__4_5;

		public static Action _003C_003E9__4_6;

		public static InlineHookGenerator _003C_003E9__4_3;

		internal unsafe void _003C_002Ector_003Eb__4_0(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			asm.AddInstruction(System.Runtime.CompilerServices.Unsafe.Read<Instruction>((void*)instrs[0]));
			asm.cmp(AssemblerRegisters.r11, 46);
			Label val = asm.CreateLabel((string)null);
			asm.jne(val);
			asm.shl(AssemblerRegisters.eax, (sbyte)1);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(1));
		}

		internal void _003C_002Ector_003Eb__4_1(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			asm.push(AssemblerRegisters.rax);
			asm.AddInstruction(IcedExtensions.CloneInstructionsWithoutIP(instrs)[2]);
			asm.cmp(AssemblerRegisters.eax, 46);
			asm.pop(AssemblerRegisters.rax);
			Label val = asm.CreateLabel((string)null);
			asm.jne(val);
			asm.shl(AssemblerRegisters.eax, (sbyte)1);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, instrs);
		}

		internal void _003C_002Ector_003Eb__4_2(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r9);
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r14);
			asm.mov(AssemblerRegisters.rdx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rbx + 33875800L]);
			AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<eMappers, int, int>>((eMappers mv, int limit) => ((int)mv == 46) ? (limit * 2) : limit), 2, (Action<Assembler>)null, false, false);
			asm.mov(AssemblerRegisters.r9, AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rax);
			asm.cmp(AssemblerRegisters.rax, AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(1));
		}

		internal int _003C_002Ector_003Eb__4_4(eMappers mv, int limit)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			if ((int)mv == 46)
			{
				return limit * 2;
			}
			return limit;
		}

		internal bool _003C_002Ector_003Eb__4_5(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree)
		{
			bool flag = Plugin.Instance.LobbySettingsViewModel.PlaceGoodsyardData[playerId];
			Log.Information($"Build goodsyard for player {playerId}: {flag}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
			return flag;
		}

		internal unsafe void _003C_002Ector_003Eb__4_3(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action>(delegate
			{
				GameBuildingManagerAPI instance = GameBuildingManagerAPI.Instance;
				int buildingId = instance.GetCurrentContextBuildingId();
				GameGatehouseEntry* ptr = default(GameGatehouseEntry*);
				if (!instance.TryGetGatehouseEntryById(buildingId, ref ptr))
				{
					Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
				}
				else
				{
					uint r_IsOpen = ((GameGatehouseEntry)ptr).r_IsOpen;
					if (_gatehouseLastKnownState.TryGetValue(buildingId, out var value) && (value != 1 || r_IsOpen != 0))
					{
						_gatehouseLastKnownState[buildingId] = r_IsOpen;
					}
					else
					{
						_gatehouseLastKnownState[buildingId] = r_IsOpen;
						GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(2000, (Action)delegate
						{
							//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
							//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
							//IL_00da: Unknown result type (might be due to invalid IL or missing references)
							//IL_00df: Unknown result type (might be due to invalid IL or missing references)
							//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
							//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
							//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
							//IL_0100: Unknown result type (might be due to invalid IL or missing references)
							//IL_010d: Unknown result type (might be due to invalid IL or missing references)
							//IL_0112: Unknown result type (might be due to invalid IL or missing references)
							GameBuildingManagerAPI instance2 = GameBuildingManagerAPI.Instance;
							GameBuilding* ptr2 = default(GameBuilding*);
							GameGatehouseEntry* ptr3 = default(GameGatehouseEntry*);
							if (!instance2.TryGetBuildingById(buildingId, ref ptr2))
							{
								Log.Warning($"Could not find building by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
							}
							else if (!instance2.TryGetGatehouseEntryById(buildingId, ref ptr3))
							{
								Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
							}
							else
							{
								int num = (int)(((GameBuilding)ptr2).r_OccupyTileGridSize * ((GameBuilding)ptr2).r_OccupyTileGridSize);
								uint* ptr4 = &((GameBuilding)ptr2).r_OccupiedTileIdsArrayBegin;
								ushort exitX = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionX;
								ushort exitY = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionY;
								UnmanagedVector2<ushort> exitPos = new UnmanagedVector2<ushort>(exitX, exitY);
								GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
								unitApi.QueryUnits().Where(UnitPredicates.IsAlive).Where(UnitPredicates.IsOfAnyType(_stuckFarmerTypes))
									.Where(UnitPredicates.IsWithinOTA(ptr4, num))
									.ForEach((RefAction<GameUnit>)delegate(in GameUnit unit, int unitIndex)
									{
										//IL_0046: Unknown result type (might be due to invalid IL or missing references)
										int num2 = unitIndex + 1;
										Log.Information($"Teleporting stuck unit. UnitId: {num2} to exit ({exitX}, {exitY})", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
										unitApi.SetCurrentLocalTilePosition(num2, exitPos);
									});
							}
						}, string.Empty);
					}
				}
			}), 0, (Action<Assembler>)null, true, false);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal unsafe void _003C_002Ector_003Eb__4_6()
		{
			GameBuildingManagerAPI instance = GameBuildingManagerAPI.Instance;
			int buildingId = instance.GetCurrentContextBuildingId();
			GameGatehouseEntry* ptr = default(GameGatehouseEntry*);
			if (!instance.TryGetGatehouseEntryById(buildingId, ref ptr))
			{
				Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
				return;
			}
			uint r_IsOpen = ((GameGatehouseEntry)ptr).r_IsOpen;
			if (_gatehouseLastKnownState.TryGetValue(buildingId, out var value) && (value != 1 || r_IsOpen != 0))
			{
				_gatehouseLastKnownState[buildingId] = r_IsOpen;
				return;
			}
			_gatehouseLastKnownState[buildingId] = r_IsOpen;
			GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(2000, (Action)delegate
			{
				//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
				//IL_00da: Unknown result type (might be due to invalid IL or missing references)
				//IL_00df: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
				//IL_0100: Unknown result type (might be due to invalid IL or missing references)
				//IL_010d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0112: Unknown result type (might be due to invalid IL or missing references)
				GameBuildingManagerAPI instance2 = GameBuildingManagerAPI.Instance;
				GameBuilding* ptr2 = default(GameBuilding*);
				GameGatehouseEntry* ptr3 = default(GameGatehouseEntry*);
				if (!instance2.TryGetBuildingById(buildingId, ref ptr2))
				{
					Log.Warning($"Could not find building by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
				}
				else if (!instance2.TryGetGatehouseEntryById(buildingId, ref ptr3))
				{
					Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
				}
				else
				{
					int num = (int)(((GameBuilding)ptr2).r_OccupyTileGridSize * ((GameBuilding)ptr2).r_OccupyTileGridSize);
					uint* ptr4 = &((GameBuilding)ptr2).r_OccupiedTileIdsArrayBegin;
					ushort exitX = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionX;
					ushort exitY = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionY;
					UnmanagedVector2<ushort> exitPos = new UnmanagedVector2<ushort>(exitX, exitY);
					GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
					unitApi.QueryUnits().Where(UnitPredicates.IsAlive).Where(UnitPredicates.IsOfAnyType(_stuckFarmerTypes))
						.Where(UnitPredicates.IsWithinOTA(ptr4, num))
						.ForEach((RefAction<GameUnit>)delegate(in GameUnit unit, int unitIndex)
						{
							//IL_0046: Unknown result type (might be due to invalid IL or missing references)
							int num2 = unitIndex + 1;
							Log.Information($"Teleporting stuck unit. UnitId: {num2} to exit ({exitX}, {exitY})", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
							unitApi.SetCurrentLocalTilePosition(num2, exitPos);
						});
				}
			}, string.Empty);
		}

		internal ModLogHelper _003C_002Ecctor_003Eb__15_0()
		{
			return ModLoggerFactory.CreateHelper("Fixes");
		}
	}

	private HookTransaction? tx;

	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	internal static HookRef<X64InlineHook> c_game_build_wall_hook = new HookRef<X64InlineHook>();

	internal static HookRef<X64InlineHook> c_game_player_queue_build_wall_hook = new HookRef<X64InlineHook>();

	internal static HookRef<X64InlineHook> c_game_player_placing_walls_hook = new HookRef<X64InlineHook>();

	internal static HookRef<X64InlineHook> c_game_gatehouse_query_farmerstuck_fix_hook = new HookRef<X64InlineHook>();

	private static readonly HashSet<eChimps> _stuckFarmerTypes = new HashSet<eChimps>
	{
		(eChimps)11,
		(eChimps)14
	};

	private static readonly Dictionary<int, uint> _gatehouseLastKnownState = new Dictionary<int, uint>();

	internal static HookRef<X64ManagedFunctionDetourAOB<c_game_player_build_structure_delegate>> c_game_player_build_structure_hook = new HookRef<X64ManagedFunctionDetourAOB<c_game_player_build_structure_delegate>>();

	internal static X64JmpTargetPatch c_game_player_build_structure_tchook;

	private static ModLogHelper Log => _log.Value;

	public unsafe FixesBuildingDetours(IntPtr libraryHandle, System.ReadOnlySpan<byte> memory)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Expected O, but got Unknown
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Expected O, but got Unknown
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Expected O, but got Unknown
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Expected O, but got Unknown
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		ILogger val = Plugin.Instance.LoggerFactory.CreateLogger("Fixes-BuildingDetours");
		Log.Information("Applying", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
		ulong num = (ulong)(long)libraryHandle;
		if (tx == null)
		{
			tx = new HookTransaction(memory, num, Plugin.Instance.LoggerFactory, (TransactionFailureMode)1);
		}
		if (Plugin.Instance.EnableProperLowWallCostFix.Value)
		{
			HookTransaction? obj = tx;
			object obj2 = _003C_003Ec._003C_003E9__4_0;
			if (obj2 == null)
			{
				InlineHookGenerator val2 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_0029: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					asm.AddInstruction(System.Runtime.CompilerServices.Unsafe.Read<Instruction>((void*)instrs[0]));
					asm.cmp(AssemblerRegisters.r11, 46);
					Label val9 = asm.CreateLabel((string)null);
					asm.jne(val9);
					asm.shl(AssemblerRegisters.eax, (sbyte)1);
					asm.Label(ref val9);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(1));
				};
				_003C_003Ec._003C_003E9__4_0 = val2;
				obj2 = (object)val2;
			}
			obj.AddInline(ref c_game_build_wall_hook, "E8 ? ? ? ? 41 89 85", (InlineHookGenerator)obj2, 0, 14, "c_game_build_wall_hook");
			HookTransaction? obj3 = tx;
			object obj4 = _003C_003Ec._003C_003E9__4_1;
			if (obj4 == null)
			{
				InlineHookGenerator val3 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					//IL_002b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0037: Unknown result type (might be due to invalid IL or missing references)
					//IL_003c: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					asm.push(AssemblerRegisters.rax);
					asm.AddInstruction(IcedExtensions.CloneInstructionsWithoutIP(instrs)[2]);
					asm.cmp(AssemblerRegisters.eax, 46);
					asm.pop(AssemblerRegisters.rax);
					Label val9 = asm.CreateLabel((string)null);
					asm.jne(val9);
					asm.shl(AssemblerRegisters.eax, (sbyte)1);
					asm.Label(ref val9);
					AssemblerExtensions.AddInstructions(asm, instrs);
				};
				_003C_003Ec._003C_003E9__4_1 = val3;
				obj4 = (object)val3;
			}
			obj3.AddInline(ref c_game_player_queue_build_wall_hook, "89 44 24 ? 89 05 ? ? ? ? 0F B7 05", (InlineHookGenerator)obj4, 0, 14, "c_game_player_queue_build_wall_hook");
			HookTransaction? obj5 = tx;
			object obj6 = _003C_003Ec._003C_003E9__4_2;
			if (obj6 == null)
			{
				InlineHookGenerator val4 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_002d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0032: Unknown result type (might be due to invalid IL or missing references)
					//IL_003d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0047: Unknown result type (might be due to invalid IL or missing references)
					//IL_0052: Unknown result type (might be due to invalid IL or missing references)
					//IL_0057: Unknown result type (might be due to invalid IL or missing references)
					//IL_0095: Unknown result type (might be due to invalid IL or missing references)
					//IL_009a: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r9);
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r14);
					asm.mov(AssemblerRegisters.rdx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rbx + 33875800L]);
					AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<eMappers, int, int>>((eMappers mv, int limit) => ((int)mv == 46) ? (limit * 2) : limit), 2, (Action<Assembler>)null, false, false);
					asm.mov(AssemblerRegisters.r9, AssemblerRegisters.rax);
					asm.pop(AssemblerRegisters.rax);
					asm.cmp(AssemblerRegisters.rax, AssemblerRegisters.r9);
					asm.pop(AssemblerRegisters.r9);
					asm.pop(AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rcx);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(1));
				};
				_003C_003Ec._003C_003E9__4_2 = val4;
				obj6 = (object)val4;
			}
			obj5.AddInline(ref c_game_player_placing_walls_hook, "3B 83 ? ? ? ? 0F 8D ? ? ? ? 33 C9", (InlineHookGenerator)obj6, 0, 14, "c_game_player_placing_walls_hook");
		}
		if (Plugin.Instance.ModularGoodsyardPlacement.Value)
		{
			long num2 = PatternScanner.FindPattern(memory, "66 44 89 BC 24 ? ? ? ? 48 81 C4");
			int num3 = 28;
			if (num2 != 0L)
			{
				long num4 = (long)libraryHandle + num2 + num3;
				c_game_player_build_structure_tchook = new X64JmpTargetPatch((ulong)num4, val);
				IntPtr intPtr = VirtualAllocUtil.AllocateStub((ulong)num4, 128);
				c_game_player_build_structure_tchook.Apply((ulong)(long)intPtr);
				Assembler val5 = new Assembler(64);
				val5.push(AssemblerRegisters.rax);
				AssemblerExtensions.X64FastcallSafeEx2(val5, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<IntPtr, int, int, int, eMappers, int, int, byte, bool>>(delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree)
				{
					bool flag = Plugin.Instance.LobbySettingsViewModel.PlaceGoodsyardData[playerId];
					Log.Information($"Build goodsyard for player {playerId}: {flag}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
					return flag;
				}), 8, (Action<Assembler>)null, false, false, (X64StackArgument[])(object)new X64StackArgument[4]
				{
					new X64StackArgument((X64ArgumentSourceType)0, 0uL),
					new X64StackArgument((X64ArgumentSourceType)0, 1uL),
					new X64StackArgument((X64ArgumentSourceType)0, 2uL),
					new X64StackArgument((X64ArgumentSourceType)0, 3uL)
				});
				Label val6 = val5.CreateLabel("lblSkip");
				val5.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
				val5.pop(AssemblerRegisters.rax);
				val5.jz(val6);
				val5.jmp(c_game_player_build_structure_tchook.TrampolineAddress);
				val5.Label(ref val6);
				val5.ret();
				string text = default(string);
				AssemblerResult val7 = default(AssemblerResult);
				if (!val5.TryAssemble((CodeWriter)new OSMemoryCodeWriter((byte*)(void*)intPtr, 128, val), (ulong)(long)intPtr, ref text, ref val7, (BlockEncoderOptions)0))
				{
					Log.Error("Failed to assemble code stub: " + text, ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
				}
			}
			else
			{
				Log.Warning("Could not find anchor point for c_game_player_build_structure_goodsyard", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
			}
		}
		if (Plugin.Instance.EnableGatehouseFarmerFix.Value)
		{
			HookTransaction? obj7 = tx;
			object obj8 = _003C_003Ec._003C_003E9__4_3;
			if (obj8 == null)
			{
				InlineHookGenerator val8 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action>(delegate
					{
						GameBuildingManagerAPI instance = GameBuildingManagerAPI.Instance;
						int buildingId = instance.GetCurrentContextBuildingId();
						GameGatehouseEntry* ptr = default(GameGatehouseEntry*);
						if (!instance.TryGetGatehouseEntryById(buildingId, ref ptr))
						{
							Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
						}
						else
						{
							uint r_IsOpen = ((GameGatehouseEntry)ptr).r_IsOpen;
							if (_gatehouseLastKnownState.TryGetValue(buildingId, out var value) && (value != 1 || r_IsOpen != 0))
							{
								_gatehouseLastKnownState[buildingId] = r_IsOpen;
							}
							else
							{
								_gatehouseLastKnownState[buildingId] = r_IsOpen;
								GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(2000, (Action)delegate
								{
									//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
									//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
									//IL_00da: Unknown result type (might be due to invalid IL or missing references)
									//IL_00df: Unknown result type (might be due to invalid IL or missing references)
									//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
									//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
									//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
									//IL_0100: Unknown result type (might be due to invalid IL or missing references)
									//IL_010d: Unknown result type (might be due to invalid IL or missing references)
									//IL_0112: Unknown result type (might be due to invalid IL or missing references)
									GameBuildingManagerAPI instance2 = GameBuildingManagerAPI.Instance;
									GameBuilding* ptr2 = default(GameBuilding*);
									GameGatehouseEntry* ptr3 = default(GameGatehouseEntry*);
									if (!instance2.TryGetBuildingById(buildingId, ref ptr2))
									{
										Log.Warning($"Could not find building by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
									}
									else if (!instance2.TryGetGatehouseEntryById(buildingId, ref ptr3))
									{
										Log.Warning($"Could not find gatehouse entry by building id: {buildingId}", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
									}
									else
									{
										int num5 = (int)(((GameBuilding)ptr2).r_OccupyTileGridSize * ((GameBuilding)ptr2).r_OccupyTileGridSize);
										uint* ptr4 = &((GameBuilding)ptr2).r_OccupiedTileIdsArrayBegin;
										ushort exitX = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionX;
										ushort exitY = (ushort)((GameGatehouseEntry)ptr3).r_ExitDoorTilePositionY;
										UnmanagedVector2<ushort> exitPos = new UnmanagedVector2<ushort>(exitX, exitY);
										GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
										unitApi.QueryUnits().Where(UnitPredicates.IsAlive).Where(UnitPredicates.IsOfAnyType(_stuckFarmerTypes))
											.Where(UnitPredicates.IsWithinOTA(ptr4, num5))
											.ForEach((RefAction<GameUnit>)delegate(in GameUnit unit, int unitIndex)
											{
												//IL_0046: Unknown result type (might be due to invalid IL or missing references)
												int num6 = unitIndex + 1;
												Log.Information($"Teleporting stuck unit. UnitId: {num6} to exit ({exitX}, {exitY})", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\BuildingDetours.cs");
												unitApi.SetCurrentLocalTilePosition(num6, exitPos);
											});
									}
								}, string.Empty);
							}
						}
					}), 0, (Action<Assembler>)null, true, false);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				_003C_003Ec._003C_003E9__4_3 = val8;
				obj8 = (object)val8;
			}
			obj7.AddInline(ref c_game_gatehouse_query_farmerstuck_fix_hook, "48 8D 2D ? ? ? ? 66 89 84 2B", (InlineHookGenerator)obj8, 0, 14, "c_game_gatehouse_query_farmerstuck_fix_hook");
		}
		tx.Commit();
	}

	public void Dispose()
	{
		HookTransaction? obj = tx;
		if (obj != null)
		{
			obj.Unload();
		}
	}
}
