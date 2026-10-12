using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Fixes.Boostrap;
using Fixes.Components;
using Fixes.Config;
using Fixes.UI;
using Iced.Intel;
using MessagePack;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Assembly;
using RedBird.X64.Assembly.InstructionWalker;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.API.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Fixes.Detours;

[SuppressUnmanagedCodeSecurity]
public class FixesAIDetours
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate int LadderSelectDelegate(IntPtr selection, int unitId);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate int LadderAttachDelegate(int approachTileId, int rowDirectionBase);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void c_game_ai_siege_master_handler_delegate(IntPtr pLordManager);

	[CompilerGenerated]
	private static class <>O
	{
		public static c_game_ai_siege_master_handler_delegate <0>__c_game_ai_siege_master_handler_hook_impl;

		public static InstructionExtractor <1>__ExtractInstructionAddress;

		public static LadderSelectDelegate <2>__SelectLadderObjective;

		public static LadderAttachDelegate <3>__SelectLadderAttachment;
	}

	[Serializable]
	[CompilerGenerated]
	private sealed class <>c
	{
		public static readonly <>c <>9 = new <>c();

		public static Action<ulong, ulong, ulong, int, int, int> <>9__7_33;

		public static InlineHookGenerator <>9__7_0;

		public static Func<int, int, int, int> <>9__7_35;

		public static InlineHookGenerator <>9__7_1;

		public static Func<ulong, bool> <>9__7_36;

		public static InlineHookGenerator <>9__7_2;

		public static InlineHookGenerator <>9__7_3;

		public static InlineHookGenerator <>9__7_37;

		public static InlineHookGenerator <>9__7_38;

		public static InlineHookGenerator <>9__7_39;

		public static InlineHookGenerator <>9__7_40;

		public static InstructionPredicate <>9__7_44;

		public static InlineHookGenerator <>9__7_41;

		public static InlineHookGenerator <>9__7_42;

		public static InstructionPredicate <>9__7_45;

		public static InlineHookGenerator <>9__7_43;

		public static InlineHookGenerator <>9__7_4;

		public static Func<int, int> <>9__7_46;

		public static InlineHookGenerator <>9__7_5;

		public static InlineHookGenerator <>9__7_6;

		public static Func<int, int, bool> <>9__7_49;

		public static InlineHookGenerator <>9__7_47;

		public static InlineHookGenerator <>9__7_48;

		public static AssemblyPatchGenerator <>9__7_9;

		public static AssemblyPatchGenerator <>9__7_10;

		public static AssemblyPatchGenerator <>9__7_11;

		public static AssemblyPatchGenerator <>9__7_12;

		public static AssemblyPatchGenerator <>9__7_13;

		public static InlineHookGenerator <>9__7_20;

		public static InlineHookGenerator <>9__7_21;

		public static InlineHookGenerator <>9__7_52;

		public static InlineHookGenerator <>9__7_22;

		public static InlineHookGenerator <>9__7_23;

		public static InlineHookGenerator <>9__7_24;

		public static InlineHookGenerator <>9__7_25;

		public static InlineHookGenerator <>9__7_26;

		public static InlineHookGenerator <>9__7_27;

		public static InlineHookGenerator <>9__7_28;

		public static InlineHookGenerator <>9__7_29;

		public static InlineHookGenerator <>9__7_30;

		public static InlineHookGenerator <>9__7_31;

		public static InlineHookGenerator <>9__7_32;

		public static Func<int, int, int> <>9__7_57;

		internal void <.ctor>b__7_0(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
			AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action<ulong, ulong, ulong, int, int, int>>(delegate(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
			{
				GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(Plugin.Instance.HarassingTentIdleEngineersFixDelayAmount.Value, (Action)delegate
				{
					GameTribeManagerAPI.Instance.IssueTargettedCommand((int)tribeId, (TribeAICommand)aiCommandType, targetValue1, targetValue2, a6);
				}, string.Empty);
			}), 6, (Action<Assembler>)null, true, false, (X64StackArgument[])(object)new X64StackArgument[2]
			{
				new X64StackArgument((X64ArgumentSourceType)0, 0uL),
				new X64StackArgument((X64ArgumentSourceType)0, 1uL)
			});
		}

		internal void <.ctor>b__7_33(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
		{
			GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(Plugin.Instance.HarassingTentIdleEngineersFixDelayAmount.Value, (Action)delegate
			{
				GameTribeManagerAPI.Instance.IssueTargettedCommand((int)tribeId, (TribeAICommand)aiCommandType, targetValue1, targetValue2, a6);
			}, string.Empty);
		}

		internal unsafe void <.ctor>b__7_1(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			AssemblerExtensions.AddInstructions(asm, instrs);
			asm.jnz(((Instruction)instrs[1]).IPRelativeMemoryAddress);
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r8);
			asm.push(AssemblerRegisters.r9);
			asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.edi);
			asm.mov(AssemblerRegisters.edx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 64L]);
			asm.mov(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 208L]);
			AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<int, int, int, int>>(delegate(int footprintTileId, int centerTileId, int playerId)
			{
				//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
				//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
				//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
				//IL_01e7: Invalid comparison between Unknown and I4
				//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ed: Invalid comparison between Unknown and I4
				//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f3: Invalid comparison between Unknown and I4
				//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f9: Invalid comparison between Unknown and I4
				GameTileManagerAPI instance = GameTileManagerAPI.Instance;
				GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
				int num = instance.MapColumnLookupTable[footprintTileId];
				int num2 = instance.MapColumnLookupTable[centerTileId];
				if ((uint)num >= 800u || (uint)num2 >= 800u)
				{
					return 0;
				}
				int num3 = footprintTileId - instance.MapRowLookupTable[3 * num];
				int num4 = centerTileId - instance.MapRowLookupTable[3 * num2];
				if ((uint)num3 >= 800u || (uint)num4 >= 800u)
				{
					return 0;
				}
				for (int i = Math.Max(0, num - 2); i <= Math.Min(799, num + 2); i++)
				{
					int num5 = instance.MapRowLookupTable[3 * i];
					for (int j = Math.Max(0, num3 - 2); j <= Math.Min(799, num3 + 2); j++)
					{
						if (instance.TileManager.StructureGrid[num5 + j] != 0)
						{
							return 0;
						}
					}
				}
				if (!instance2.IsPlayerIdValid(playerId))
				{
					return 1;
				}
				GamePlayerResources* ptr = default(GamePlayerResources*);
				if (!instance2.TryGetPlayerResourcesById(playerId, ref ptr))
				{
					return 1;
				}
				int r_SiegeAttackTargetPlayerId = (int)((GamePlayerResources)ptr).r_SiegeAttackTargetPlayerId;
				GamePlayerResources* ptr2 = default(GamePlayerResources*);
				if (instance2.IsPlayerIdValid(r_SiegeAttackTargetPlayerId) || r_SiegeAttackTargetPlayerId == playerId || !instance2.TryGetPlayerResourcesById(r_SiegeAttackTargetPlayerId, ref ptr2))
				{
					return 1;
				}
				int r_KeepDoorTilePositionX = (int)((GamePlayerResources)ptr2).r_KeepDoorTilePositionX;
				int r_KeepDoorTilePositionY = (int)((GamePlayerResources)ptr2).r_KeepDoorTilePositionY;
				if ((uint)r_KeepDoorTilePositionX >= 800u || (uint)r_KeepDoorTilePositionY >= 800u || (r_KeepDoorTilePositionX | r_KeepDoorTilePositionY) == 0)
				{
					return 1;
				}
				System.Span<GameBuilding> buildingsAsSpan = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
				for (int k = Math.Max(0, num2 - 8); k <= Math.Min(799, num2 + 8); k++)
				{
					int num6 = instance.MapRowLookupTable[3 * k];
					for (int l = Math.Max(0, num4 - 8); l <= Math.Min(799, num4 + 8); l++)
					{
						int num7 = instance.TileManager.StructureGrid[num6 + l];
						if (num7 != 0 && num7 <= buildingsAsSpan.Length)
						{
							ref GameBuilding reference = ref buildingsAsSpan[num7 - 1];
							if (reference.r_PlayerIdOwner == playerId)
							{
								eStructs r_BuildingType = reference.r_BuildingType;
								if ((int)r_BuildingType == 29 || (int)r_BuildingType == 54 || ((int)r_BuildingType >= 80 && (int)r_BuildingType <= 84))
								{
									int num8 = reference.r_TilePositionXBegin + 1;
									int num9 = reference.r_TilePositionYBegin + 1;
									int num10 = r_KeepDoorTilePositionX - num8;
									int num11 = r_KeepDoorTilePositionY - num9;
									int num12 = Math.Max(Math.Abs(num10), Math.Abs(num11));
									if (num12 >= 4 && (num4 - num8) * num10 + (num2 - num9) * num11 > num12)
									{
										return 0;
									}
								}
							}
						}
					}
				}
				return 1;
			}), 3, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
			asm.pop(AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.r8);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			asm.cmp(AssemblerRegisters.rax, 1);
		}

		internal unsafe int <.ctor>b__7_35(int footprintTileId, int centerTileId, int playerId)
		{
			//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Invalid comparison between Unknown and I4
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Invalid comparison between Unknown and I4
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Invalid comparison between Unknown and I4
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f9: Invalid comparison between Unknown and I4
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
			int num = instance.MapColumnLookupTable[footprintTileId];
			int num2 = instance.MapColumnLookupTable[centerTileId];
			if ((uint)num >= 800u || (uint)num2 >= 800u)
			{
				return 0;
			}
			int num3 = footprintTileId - instance.MapRowLookupTable[3 * num];
			int num4 = centerTileId - instance.MapRowLookupTable[3 * num2];
			if ((uint)num3 >= 800u || (uint)num4 >= 800u)
			{
				return 0;
			}
			for (int i = Math.Max(0, num - 2); i <= Math.Min(799, num + 2); i++)
			{
				int num5 = instance.MapRowLookupTable[3 * i];
				for (int j = Math.Max(0, num3 - 2); j <= Math.Min(799, num3 + 2); j++)
				{
					if (instance.TileManager.StructureGrid[num5 + j] != 0)
					{
						return 0;
					}
				}
			}
			if (!instance2.IsPlayerIdValid(playerId))
			{
				return 1;
			}
			GamePlayerResources* ptr = default(GamePlayerResources*);
			if (!instance2.TryGetPlayerResourcesById(playerId, ref ptr))
			{
				return 1;
			}
			int r_SiegeAttackTargetPlayerId = (int)((GamePlayerResources)ptr).r_SiegeAttackTargetPlayerId;
			GamePlayerResources* ptr2 = default(GamePlayerResources*);
			if (instance2.IsPlayerIdValid(r_SiegeAttackTargetPlayerId) || r_SiegeAttackTargetPlayerId == playerId || !instance2.TryGetPlayerResourcesById(r_SiegeAttackTargetPlayerId, ref ptr2))
			{
				return 1;
			}
			int r_KeepDoorTilePositionX = (int)((GamePlayerResources)ptr2).r_KeepDoorTilePositionX;
			int r_KeepDoorTilePositionY = (int)((GamePlayerResources)ptr2).r_KeepDoorTilePositionY;
			if ((uint)r_KeepDoorTilePositionX >= 800u || (uint)r_KeepDoorTilePositionY >= 800u || (r_KeepDoorTilePositionX | r_KeepDoorTilePositionY) == 0)
			{
				return 1;
			}
			System.Span<GameBuilding> buildingsAsSpan = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
			for (int k = Math.Max(0, num2 - 8); k <= Math.Min(799, num2 + 8); k++)
			{
				int num6 = instance.MapRowLookupTable[3 * k];
				for (int l = Math.Max(0, num4 - 8); l <= Math.Min(799, num4 + 8); l++)
				{
					int num7 = instance.TileManager.StructureGrid[num6 + l];
					if (num7 == 0 || num7 > buildingsAsSpan.Length)
					{
						continue;
					}
					ref GameBuilding reference = ref buildingsAsSpan[num7 - 1];
					if (reference.r_PlayerIdOwner != playerId)
					{
						continue;
					}
					eStructs r_BuildingType = reference.r_BuildingType;
					if ((int)r_BuildingType == 29 || (int)r_BuildingType == 54 || ((int)r_BuildingType >= 80 && (int)r_BuildingType <= 84))
					{
						int num8 = reference.r_TilePositionXBegin + 1;
						int num9 = reference.r_TilePositionYBegin + 1;
						int num10 = r_KeepDoorTilePositionX - num8;
						int num11 = r_KeepDoorTilePositionY - num9;
						int num12 = Math.Max(Math.Abs(num10), Math.Abs(num11));
						if (num12 >= 4 && (num4 - num8) * num10 + (num2 - num9) * num11 > num12)
						{
							return 0;
						}
					}
				}
			}
			return 1;
		}

		internal void <.ctor>b__7_2(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel("lblSkip");
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
			AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<ulong, bool>)delegate(ulong playerId)
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0029: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				Plugin instance = Plugin.Instance;
				_ = GameAIManagerAPI.Instance;
				GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
				if (instance.ForceHopsFarmFix.Value)
				{
					return true;
				}
				AILords aILord = instance2.GetAILord((int)playerId);
				bool value = default(bool);
				if ((instance.HopsFarmWhitelist?.TryGetValue(aILord, out value) ?? false) && value)
				{
					return true;
				}
				CustomLordPreferencesEntry preferences;
				return Preferences.TryGet((int)playerId, out preferences) && preferences.EnableHopsFarmFix == true;
			}), 1, (Action<Assembler>)null, true, false, Array.Empty<X64StackArgument>());
			asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rcx);
			asm.jnz(val);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(2));
		}

		internal bool <.ctor>b__7_36(ulong playerId)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			Plugin instance = Plugin.Instance;
			_ = GameAIManagerAPI.Instance;
			GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
			if (instance.ForceHopsFarmFix.Value)
			{
				return true;
			}
			AILords aILord = instance2.GetAILord((int)playerId);
			bool value = default(bool);
			if ((instance.HopsFarmWhitelist?.TryGetValue(aILord, out value) ?? false) && value)
			{
				return true;
			}
			if (Preferences.TryGet((int)playerId, out CustomLordPreferencesEntry preferences))
			{
				return preferences.EnableHopsFarmFix == true;
			}
			return false;
		}

		internal void <.ctor>b__7_3(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).Skip(2).ToArray();
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal unsafe void <.ctor>b__7_37(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal unsafe void <.ctor>b__7_38(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal unsafe void <.ctor>b__7_39(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
			asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
			asm.sub(AssemblerRegisters.ecx, 1);
			asm.cmp(AssemblerRegisters.ecx, 23);
			asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal unsafe void <.ctor>b__7_40(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal bool <.ctor>b__7_44(Instruction x)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			return (int)((Instruction)(ref x)).Mnemonic == 751;
		}

		internal unsafe void <.ctor>b__7_41(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
			asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
			asm.sub(AssemblerRegisters.ecx, 1);
			asm.cmp(AssemblerRegisters.ecx, 23);
			asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal unsafe void <.ctor>b__7_42(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal bool <.ctor>b__7_45(Instruction x)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			return (int)((Instruction)(ref x)).Mnemonic == 751;
		}

		internal unsafe void <.ctor>b__7_43(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
			asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
			asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
			asm.sub(AssemblerRegisters.ecx, 1);
			asm.cmp(AssemblerRegisters.ecx, 23);
			asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal void <.ctor>b__7_4(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0212: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_0222: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_022e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0239: Unknown result type (might be due to invalid IL or missing references)
			//IL_0245: Unknown result type (might be due to invalid IL or missing references)
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0255: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_026b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0275: Unknown result type (might be due to invalid IL or missing references)
			//IL_027a: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Unknown result type (might be due to invalid IL or missing references)
			//IL_028a: Unknown result type (might be due to invalid IL or missing references)
			//IL_028f: Unknown result type (might be due to invalid IL or missing references)
			//IL_029a: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02af: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_030c: Unknown result type (might be due to invalid IL or missing references)
			//IL_031b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0326: Unknown result type (might be due to invalid IL or missing references)
			//IL_0336: Unknown result type (might be due to invalid IL or missing references)
			//IL_033d: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0352: Unknown result type (might be due to invalid IL or missing references)
			//IL_0365: Unknown result type (might be due to invalid IL or missing references)
			//IL_0370: Unknown result type (might be due to invalid IL or missing references)
			//IL_037b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0386: Unknown result type (might be due to invalid IL or missing references)
			//IL_0391: Unknown result type (might be due to invalid IL or missing references)
			//IL_039c: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			Label val3 = asm.CreateLabel((string)null);
			Label val4 = asm.CreateLabel((string)null);
			Label val5 = asm.CreateLabel((string)null);
			Label val6 = asm.CreateLabel((string)null);
			Label val7 = asm.CreateLabel((string)null);
			AssemblerExtensions.AddInstructions(asm, overwritten);
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r8);
			asm.push(AssemblerRegisters.r10);
			asm.push(AssemblerRegisters.r11);
			asm.push(AssemblerRegisters.rsi);
			asm.push(AssemblerRegisters.rdi);
			asm.push(AssemblerRegisters.r12);
			asm.mov(AssemblerRegisters.r12d, -1);
			asm.cmp(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rcx + 1240156L]);
			asm.jbe(val7);
			asm.mov(AssemblerRegisters.r12d, 1);
			asm.Label(ref val7);
			asm.mov(AssemblerRegisters.r10, GameGlobalsManager.Instance.GameTribeManagerVA);
			asm.imul(AssemblerRegisters.r11, AssemblerRegisters.rbx, System.Runtime.CompilerServices.Unsafe.SizeOf<GameTribe>());
			asm.add(AssemblerRegisters.r10, AssemblerRegisters.r11);
			asm.add(AssemblerRegisters.r10, 42);
			asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r10 + 50L]);
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jz(val6);
			asm.add(AssemblerRegisters.r10, 54);
			asm.mov(AssemblerRegisters.r8, GameGlobalsManager.Instance.GameUnitManagerVA);
			asm.xor(AssemblerRegisters.esi, AssemblerRegisters.esi);
			asm.Label(ref val);
			asm.movzx(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rsi * 2]);
			asm.test(AssemblerRegisters.r11d, AssemblerRegisters.r11d);
			asm.jz(val4);
			asm.xor(AssemblerRegisters.edi, AssemblerRegisters.edi);
			asm.Label(ref val2);
			asm.bt(AssemblerRegisters.r11d, AssemblerRegisters.edi);
			asm.jnc(val3);
			asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.esi);
			asm.shl(AssemblerRegisters.ecx, (sbyte)4);
			asm.add(AssemblerRegisters.ecx, AssemblerRegisters.edi);
			asm.imul(AssemblerRegisters.rcx, AssemblerRegisters.rcx, System.Runtime.CompilerServices.Unsafe.SizeOf<GameUnit>());
			asm.movzx(AssemblerRegisters.edx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rcx + 1766L]);
			asm.cmp(AssemblerRegisters.edx, 74);
			asm.je(val5);
			asm.cmp(AssemblerRegisters.edx, 78);
			asm.je(val5);
			asm.cmp(AssemblerRegisters.edx, 83);
			asm.je(val5);
			asm.dec(AssemblerRegisters.eax);
			asm.jz(val6);
			asm.Label(ref val3);
			asm.inc(AssemblerRegisters.edi);
			asm.cmp(AssemblerRegisters.edi, 16);
			asm.jb(val2);
			asm.Label(ref val4);
			asm.inc(AssemblerRegisters.esi);
			asm.cmp(AssemblerRegisters.esi, 625);
			asm.jb(val);
			asm.jmp(val6);
			asm.Label(ref val5);
			asm.add(AssemblerRegisters.r9d, AssemblerRegisters.r12d);
			asm.Label(ref val6);
			asm.pop(AssemblerRegisters.r12);
			asm.pop(AssemblerRegisters.rdi);
			asm.pop(AssemblerRegisters.rsi);
			asm.pop(AssemblerRegisters.r11);
			asm.pop(AssemblerRegisters.r10);
			asm.pop(AssemblerRegisters.r8);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
		}

		internal void <.ctor>b__7_5(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
			AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<int, int>)delegate(int playerId)
			{
				_ = GameAIManagerAPI.Instance;
				_ = Plugin.Instance;
				CustomLordPreferencesEntry preferences;
				return Preferences.TryGet(playerId, out preferences) ? ((!preferences.EarlyDefensiveTimeDurationTicks.HasValue) ? 4800 : preferences.EarlyDefensiveTimeDurationTicks.Value) : 4800;
			}), 1, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
			asm.mov(AssemblerRegisters.rcx, GameGlobalsManager.Instance.CurrentGameTickVA);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx], AssemblerRegisters.eax);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
		}

		internal int <.ctor>b__7_46(int playerId)
		{
			_ = GameAIManagerAPI.Instance;
			_ = Plugin.Instance;
			if (Preferences.TryGet(playerId, out CustomLordPreferencesEntry preferences))
			{
				if (!preferences.EarlyDefensiveTimeDurationTicks.HasValue)
				{
					return 4800;
				}
				return preferences.EarlyDefensiveTimeDurationTicks.Value;
			}
			return 4800;
		}

		internal void <.ctor>b__7_6(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0189: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_019d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01be: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_0222: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_0236: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0265: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_0291: Unknown result type (might be due to invalid IL or missing references)
			//IL_029c: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			ulong num = ((Instruction)overwritten[1]).NearBranchTarget + 5;
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r8);
			asm.push(AssemblerRegisters.r9);
			asm.push(AssemblerRegisters.r10);
			asm.push(AssemblerRegisters.r11);
			asm.mov(AssemblerRegisters.rdx, GameGlobalsManager.Instance.GameUnitManagerVA + 1628);
			asm.add(AssemblerRegisters.rdx, AssemblerRegisters.rcx);
			asm.mov(AssemblerRegisters.r8b, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__byte_ptr))[AssemblerRegisters.rdx + 146L]);
			asm.mov(AssemblerRegisters.r9, GameGlobalsManager.Instance.GamePlayerManagerVA);
			asm.add(AssemblerRegisters.r9, 1580332);
			asm.imul(AssemblerRegisters.r11, AssemblerRegisters.r8, 4);
			asm.mov(AssemblerRegisters.r10, AssemblerRegisters.r9);
			asm.add(AssemblerRegisters.r10, AssemblerRegisters.r11);
			asm.mov(AssemblerRegisters.r10d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10]);
			asm.imul(AssemblerRegisters.r11, AssemblerRegisters.rbp, 4);
			asm.add(AssemblerRegisters.r9, AssemblerRegisters.r11);
			asm.mov(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r9]);
			asm.cmp(AssemblerRegisters.r9d, AssemblerRegisters.r10d);
			asm.jne(val);
			asm.mov(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdx + 138L]);
			asm.cmp(AssemblerRegisters.r8, 28);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 74);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 78);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 83);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 29);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 39);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 77);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 40);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 59);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 60);
			asm.je(val);
			asm.cmp(AssemblerRegisters.r8, 58);
			asm.je(val);
			asm.pop(AssemblerRegisters.r11);
			asm.pop(AssemblerRegisters.r10);
			asm.pop(AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.r8);
			asm.pop(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.eax, 0);
			asm.jmp(num);
			asm.Label(ref val);
			asm.pop(AssemblerRegisters.r11);
			asm.pop(AssemblerRegisters.r10);
			asm.pop(AssemblerRegisters.r9);
			asm.pop(AssemblerRegisters.r8);
			asm.pop(AssemblerRegisters.rdx);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal unsafe void <.ctor>b__7_47(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			System.Span<Instruction> span = System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
			Label val = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.rdx, AssemblerRegisters.r12);
			AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<int, int, bool>)delegate(int targetPlayerLordId, int attackerPlayerId)
			{
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Invalid comparison between Unknown and I4
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Invalid comparison between Unknown and I4
				GameUnitManagerAPI instance = GameUnitManagerAPI.Instance;
				_ = GameAIManagerAPI.Instance;
				LobbySettingsViewModel lobbySettingsViewModel = Plugin.Instance.LobbySettingsViewModel;
				GameUnit* ptr = default(GameUnit*);
				if (!instance.TryGetUnitById(targetPlayerLordId, ref ptr) || (int)((GameUnit)ptr).r_UnitChimp != 55 || (int)((GameUnit)ptr).r_AliveState != 2)
				{
					return false;
				}
				if (Preferences.TryGet(attackerPlayerId, out CustomLordPreferencesEntry preferences))
				{
					if (!preferences.LethalLordTargeting.HasValue)
					{
						return false;
					}
					return preferences.LethalLordTargeting.Value;
				}
				return (lobbySettingsViewModel != null && lobbySettingsViewModel.ForceLethalAILordTargeting) ? true : false;
			}), 1, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
			asm.pop(AssemblerRegisters.rdx);
			asm.cmp(AssemblerRegisters.rax, 1);
			asm.pop(AssemblerRegisters.rax);
			asm.jnz(val);
			AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
			AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress + 40);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
		}

		internal unsafe bool <.ctor>b__7_49(int targetPlayerLordId, int attackerPlayerId)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Invalid comparison between Unknown and I4
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Invalid comparison between Unknown and I4
			GameUnitManagerAPI instance = GameUnitManagerAPI.Instance;
			_ = GameAIManagerAPI.Instance;
			LobbySettingsViewModel lobbySettingsViewModel = Plugin.Instance.LobbySettingsViewModel;
			GameUnit* ptr = default(GameUnit*);
			if (!instance.TryGetUnitById(targetPlayerLordId, ref ptr) || (int)((GameUnit)ptr).r_UnitChimp != 55 || (int)((GameUnit)ptr).r_AliveState != 2)
			{
				return false;
			}
			if (Preferences.TryGet(attackerPlayerId, out CustomLordPreferencesEntry preferences))
			{
				if (!preferences.LethalLordTargeting.HasValue)
				{
					return false;
				}
				return preferences.LethalLordTargeting.Value;
			}
			if (lobbySettingsViewModel != null && lobbySettingsViewModel.ForceLethalAILordTargeting)
			{
				return true;
			}
			return false;
		}

		internal void <.ctor>b__7_48(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Unknown result type (might be due to invalid IL or missing references)
			//IL_018e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Unknown result type (might be due to invalid IL or missing references)
			//IL_019e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01be: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0203: Unknown result type (might be due to invalid IL or missing references)
			//IL_020d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0225: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0237: Unknown result type (might be due to invalid IL or missing references)
			//IL_023c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0247: Unknown result type (might be due to invalid IL or missing references)
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0269: Unknown result type (might be due to invalid IL or missing references)
			System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
			System.Span<Instruction> span = System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
			asm.mov(AssemblerRegisters.r10d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 88L]);
			asm.sub(AssemblerRegisters.rsp, 96);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 56L], AssemblerRegisters.rcx);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 64L], AssemblerRegisters.rdx);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L], AssemblerRegisters.r8);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 80L], AssemblerRegisters.r9);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 32L], 0);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 40L], 0);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 48L], 0);
			asm.mov(AssemblerRegisters.r9d, AssemblerRegisters.r10d);
			asm.imul(AssemblerRegisters.r9, AssemblerRegisters.r9, 22588);
			asm.mov(AssemblerRegisters.r8, GameGlobalsManager.Instance.GamePlayerManagerVA);
			asm.add(AssemblerRegisters.r8, 1240000);
			asm.add(AssemblerRegisters.r8, AssemblerRegisters.r9);
			asm.mov(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + 156L]);
			asm.mov(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + 152L]);
			asm.AddInstruction(span[0]);
			asm.mov(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 56L]);
			asm.mov(AssemblerRegisters.rdx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 64L]);
			asm.mov(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L]);
			asm.mov(AssemblerRegisters.r9, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 80L]);
			asm.add(AssemblerRegisters.rsp, 96);
			AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
		}

		internal void <.ctor>b__7_9(Assembler asm, ulong va)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			asm.imul(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L], 40000);
		}

		internal void <.ctor>b__7_10(Assembler asm, ulong va)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			asm.imul(AssemblerRegisters.rdx, AssemblerRegisters.rdi, 40000);
		}

		internal void <.ctor>b__7_11(Assembler asm, ulong va)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			asm.imul(AssemblerRegisters.r9, AssemblerRegisters.rdi, 40000);
		}

		internal void <.ctor>b__7_12(Assembler asm, ulong va)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			asm.imul(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 32L], 40000);
		}

		internal void <.ctor>b__7_13(Assembler asm, ulong va)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			asm.imul(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 32L], 40000);
		}

		internal void <.ctor>b__7_20(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r8);
			asm.cmp(AssemblerRegisters.rdi, 9);
			asm.jne(val);
			AssemblerExtensions.TestNativeFlag(asm, wheatSaleCategoryFlagArray, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 80L], AssemblerRegisters.edx, AssemblerRegisters.r8, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.ecx, 6);
			asm.Label(ref val);
			asm.pop(AssemblerRegisters.r8);
			asm.pop(AssemblerRegisters.rdx);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_21(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.r8);
			asm.pushfq();
			AssemblerExtensions.TestNativeFlag(asm, minGoldForHarassmentSiegeEnginesFlagArray, AssemblerRegisters.edx, AssemblerRegisters.r8, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.r8, minGoldForHarassmentSiegeEnginesCostsArray.Address);
			asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rdx * 2]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rbx + AssemblerRegisters.r13 + 58307292L], AssemblerRegisters.r8d);
			asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
			asm.jmp(val2);
			asm.Label(ref val);
			asm.popfq();
			asm.Label(ref val2);
			asm.pop(AssemblerRegisters.r8);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_52(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			asm.mov(AssemblerRegisters.eax, 186);
			asm.cmp(AssemblerRegisters.bx, AssemblerRegisters.ax);
			asm.jnz(val);
			asm.or(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdi + AssemblerRegisters.rdx * 4 + 9012224L], 2048);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_22(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			AssemblerExtensions.AddInstructions(asm, overwritten.Slice(0, 6));
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.r8);
			asm.pushfq();
			AssemblerExtensions.TestNativeFlag(asm, stoneToOxenRatioFlagArray, AssemblerRegisters.ebp, AssemblerRegisters.r8, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.r8, stoneToOxenRatioArray.Address);
			asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rbp * 2]);
			asm.cmp(AssemblerRegisters.edi, AssemblerRegisters.r8d);
			asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
			asm.jmp(val2);
			asm.Label(ref val);
			asm.popfq();
			asm.Label(ref val2);
			asm.pop(AssemblerRegisters.r8);
		}

		internal void <.ctor>b__7_23(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.r8);
			asm.pushfq();
			AssemblerExtensions.TestNativeFlag(asm, maxOxenFlagArray, AssemblerRegisters.ebp, AssemblerRegisters.r8, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.r8, maxOxenArray.Address);
			asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rbp * 2]);
			asm.cmp(AssemblerRegisters.eax, AssemblerRegisters.r8d);
			asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
			asm.jmp(val2);
			asm.Label(ref val);
			asm.popfq();
			asm.Label(ref val2);
			asm.pop(AssemblerRegisters.r8);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_24(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01da: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_020b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0220: Unknown result type (might be due to invalid IL or missing references)
			//IL_022a: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_023a: Unknown result type (might be due to invalid IL or missing references)
			//IL_023f: Unknown result type (might be due to invalid IL or missing references)
			//IL_024f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0254: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_0264: Unknown result type (might be due to invalid IL or missing references)
			//IL_0269: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_028b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0297: Unknown result type (might be due to invalid IL or missing references)
			//IL_029e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0301: Unknown result type (might be due to invalid IL or missing references)
			//IL_030c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_0328: Unknown result type (might be due to invalid IL or missing references)
			//IL_0332: Unknown result type (might be due to invalid IL or missing references)
			//IL_0337: Unknown result type (might be due to invalid IL or missing references)
			//IL_033d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0342: Unknown result type (might be due to invalid IL or missing references)
			//IL_0347: Unknown result type (might be due to invalid IL or missing references)
			//IL_0357: Unknown result type (might be due to invalid IL or missing references)
			//IL_035c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0361: Unknown result type (might be due to invalid IL or missing references)
			//IL_036c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0371: Unknown result type (might be due to invalid IL or missing references)
			//IL_0376: Unknown result type (might be due to invalid IL or missing references)
			//IL_0389: Unknown result type (might be due to invalid IL or missing references)
			//IL_0394: Unknown result type (might be due to invalid IL or missing references)
			//IL_039f: Unknown result type (might be due to invalid IL or missing references)
			//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			Label val3 = asm.CreateLabel((string)null);
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rdx);
			asm.push(AssemblerRegisters.r10);
			asm.push(AssemblerRegisters.r11);
			asm.mov(AssemblerRegisters.rax, 6690088872491085835L);
			asm.mul(AssemblerRegisters.r8);
			asm.shr(AssemblerRegisters.rdx, (sbyte)13);
			AssemblerExtensions.TestNativeFlag(asm, buyWoodAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.r10, buyWoodAtArray.Address);
			asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241240L], AssemblerRegisters.r11d);
			asm.jge(val);
			asm.mov(AssemblerRegisters.r10, buyWoodAmountArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250872L], AssemblerRegisters.eax);
			asm.Label(ref val);
			AssemblerExtensions.TestNativeFlag(asm, buyIronAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
			asm.jz(val2);
			asm.mov(AssemblerRegisters.r10, buyIronAtArray.Address);
			asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241256L], AssemblerRegisters.r11d);
			asm.jge(val2);
			asm.mov(AssemblerRegisters.r10, buyIronAmountArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250888L], AssemblerRegisters.eax);
			asm.Label(ref val2);
			AssemblerExtensions.TestNativeFlag(asm, buyAleAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
			asm.jz(val3);
			asm.mov(AssemblerRegisters.r10, buyAleAtArray.Address);
			asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241288L], AssemblerRegisters.r11d);
			asm.jge(val3);
			asm.mov(AssemblerRegisters.r10, buyAleAmountArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250920L], AssemblerRegisters.eax);
			asm.Label(ref val3);
			asm.pop(AssemblerRegisters.r11);
			asm.pop(AssemblerRegisters.r10);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rax);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal unsafe void <.ctor>b__7_25(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			AssemblerExtensions.TestNativeFlag(asm, customGoodThingRequiredAttemptsFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.rax, customGoodThingRequiredAttemptsArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317084L], AssemblerRegisters.eax);
			asm.jmp(val2);
			asm.Label(ref val);
			asm.AddInstruction(System.Runtime.CompilerServices.Unsafe.Read<Instruction>((void*)overwritten[0]));
			asm.Label(ref val2);
			AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
		}

		internal void <.ctor>b__7_26(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			AssemblerExtensions.TestNativeFlag(asm, customBadThingLogicFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
			asm.jz(val);
			asm.mov(AssemblerRegisters.rax, customBadThingRequiredMinPopularityArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58306096L], AssemblerRegisters.eax);
			asm.jl(val2);
			asm.inc(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L]);
			asm.mov(AssemblerRegisters.rax, customBadThingRequiredAttemptsArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L], AssemblerRegisters.eax);
			asm.jl(val2);
			asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L], 0);
			asm.xor(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.ret();
			asm.Label(ref val2);
			asm.mov(AssemblerRegisters.eax, 1);
			asm.ret();
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_27(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Unknown result type (might be due to invalid IL or missing references)
			//IL_017c: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			Label val3 = asm.CreateLabel((string)null);
			Label val4 = asm.CreateLabel((string)null);
			Label val5 = asm.CreateLabel((string)null);
			asm.jz(val);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r14], 9);
			asm.jnz(val4);
			asm.Label(ref val);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254576L], AssemblerRegisters.r13d);
			asm.jle(val2);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r14], 5);
			asm.jle(val4);
			asm.Label(ref val2);
			AssemblerExtensions.TestNativeFlag(asm, customSiegeLifetimeFlagArray, AssemblerRegisters.rbx, AssemblerRegisters.rax, true);
			asm.jz(val3);
			asm.mov(AssemblerRegisters.rax, customSiegeLifetimeArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rbx * 4]);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254704L], AssemblerRegisters.eax);
			asm.jmp(val5);
			asm.Label(ref val3);
			asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254704L], 200);
			asm.jmp(val5);
			asm.Label(ref val4);
			asm.cmp(AssemblerRegisters.r13d, AssemblerRegisters.r13d);
			asm.Label(ref val5);
		}

		internal void <.ctor>b__7_28(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			AssemblerExtensions.TestNativeFlag(asm, disableSiegeAIFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
			asm.jz(val);
			AssemblerExtensions.AddUnrestrictedJmp(asm, ((Instruction)overwritten[1]).NearBranch64);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_29(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			AssemblerExtensions.TestNativeFlag(asm, disableGeneralAIFlagArray, AssemblerRegisters.rdi, AssemblerRegisters.rax, true);
			asm.jz(val);
			AssemblerExtensions.AddUnrestrictedJmp(asm, ((Instruction)overwritten[1]).NearBranch64);
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal void <.ctor>b__7_30(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0154: Unknown result type (might be due to invalid IL or missing references)
			ulong nearBranch = ((Instruction)overwritten[3]).NearBranch64;
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			asm.pushfq();
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			AssemblerExtensions.TestNativeFlag(asm, smarterSiegeAssassinsFlagArray, AssemblerRegisters.rbp, AssemblerRegisters.rax, true);
			asm.jz(val);
			asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r15 + 105696978L]);
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jz(val);
			asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax * 4 + 58646332L]);
			asm.cmp(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rbp * 4 + 58646332L]);
			asm.je(val2);
			asm.Label(ref val);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
			AssemblerExtensions.AddInstructions(asm, overwritten);
			AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress);
			asm.Label(ref val2);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
			AssemblerExtensions.AddUnrestrictedJmp(asm, nearBranch);
		}

		internal void <.ctor>b__7_31(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			//IL_016a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Unknown result type (might be due to invalid IL or missing references)
			//IL_018a: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Unknown result type (might be due to invalid IL or missing references)
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			Label val2 = asm.CreateLabel((string)null);
			asm.pushfq();
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			AssemblerExtensions.TestNativeFlag(asm, smarterSiegeAssassinsFlagArray, AssemblerRegisters.rbp, AssemblerRegisters.rax, true);
			asm.jz(val);
			asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.r11 * 2 + 79080016L]);
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jz(val);
			asm.imul(AssemblerRegisters.rax, AssemblerRegisters.rax, 812);
			asm.movzx(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax + 105696478L]);
			asm.sub(AssemblerRegisters.ecx, 45);
			asm.cmp(AssemblerRegisters.ecx, 1);
			asm.ja(val);
			asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax + 105696978L]);
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jz(val);
			asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax * 4 + 58646332L]);
			asm.cmp(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rbp * 4 + 58646332L]);
			asm.je(val2);
			asm.Label(ref val);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
			AssemblerExtensions.AddInstructions(asm, overwritten);
			AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress);
			asm.Label(ref val2);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
		}

		internal void <.ctor>b__7_32(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel((string)null);
			asm.pushfq();
			asm.push(AssemblerRegisters.rcx);
			asm.movsx(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.rbx + AssemblerRegisters.rdi + 306L]);
			asm.cmp(AssemblerRegisters.ecx, 1);
			asm.jl(val);
			asm.cmp(AssemblerRegisters.ecx, 8);
			asm.jg(val);
			asm.mov(AssemblerRegisters.rax, customTunnelersDigRangeArray.Address);
			asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rcx * 4]);
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jle(val);
			asm.mov(AssemblerRegisters.r9d, AssemblerRegisters.eax);
			asm.Label(ref val);
			asm.pop(AssemblerRegisters.rcx);
			asm.popfq();
			AssemblerExtensions.AddInstructions(asm, overwritten);
		}

		internal unsafe int <.ctor>b__7_57(int coarseX, int coarseY)
		{
			if ((uint)coarseX >= 160u || (uint)coarseY >= 160u)
			{
				return 0;
			}
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			int num = coarseX * 5;
			int num2 = coarseY * 5;
			int num3 = Math.Max(0, num - 2);
			int num4 = Math.Max(0, num2 - 2);
			int num5 = Math.Min(799, num + 4 + 2);
			int num6 = Math.Min(799, num2 + 4 + 2);
			for (int i = num4; i <= num6; i++)
			{
				int num7 = instance.MapRowLookupTable[3 * i];
				for (int j = num3; j <= num5; j++)
				{
					int num8 = num7 + j;
					if (!instance.IsValidTileId(num8))
					{
						return 0;
					}
					if ((instance.TileManager.LogicGrid[num8] & 0xF000004) != 0)
					{
						return 0;
					}
				}
			}
			return 1;
		}

		internal ModLogHelper <.cctor>b__102_0()
		{
			return ModLoggerFactory.CreateHelper("Fixes");
		}
	}

	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	internal IntPtr _strongestRunningScoreAddress;

	internal IntPtr _newOrderedMapTileIdsAddress;

	internal const int ExtendedOrdereMapTileIdsCapacity = 40000;

	internal const int ExtendedOrdereMapTileIdsCapacityByteSize = 1440000;

	private static LadderSelectDelegate? _ladderSelect;

	private static LadderAttachDelegate? _ladderAttach;

	private static readonly int[] _ladderDistances = new int[320800];

	private static readonly PackedPathDirection[] _ladderDirections;

	private static ulong _ladderBoundSearchAddress;

	private static int _ladderRoot;

	private static ulong _ladderCacheTick;

	internal static HookHandle<X64InlineHook> c_game_ai_ladder_objective_rank_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_ladder_attachment_rank_hook;

	internal static HookHandle<X64InlineHook> c_game_aiv_find_open_coarse_cell_hook;

	private static Func<int, int, int>? orchardSiteFilter;

	internal static NativeStateArray<int>? customTunnelersDigRangeArray;

	internal static HookHandle<X64InlineHook> c_game_tunnel_building_try_find_attack_target_hook;

	internal static NativeFlagArray? siegeAssassinsIgnoreNonSiegeTargetsFlagArray;

	internal static HookHandle<X64InlineHook> c_game_pathfinding_find_nearest_enemy_gatehouse_hook2;

	internal static NativeFlagArray? smarterSiegeAssassinsFlagArray;

	internal static NativeFlagArray? smarterSiegeAssassinsOverrideFlagArray;

	internal static HookHandle<X64InlineHook> c_game_gatehouse_search_departure_hook;

	internal static HookHandle<X64InlineHook> c_game_pathfinding_find_nearest_enemy_gatehouse_hook;

	internal static NativeFlagArray? disableGeneralAIFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_update_all_players_hook;

	internal static NativeFlagArray? disableSiegeAIFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_siege_processing_handler_hook2;

	internal static NativeStateArray<int>? customSiegeLifetimeArray;

	internal static NativeFlagArray? customSiegeLifetimeFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_siege_processing_handler_hook;

	internal static NativeStateArray<int>? customBadThingRequiredMinPopularityArray;

	internal static NativeStateArray<int>? customBadThingRequiredAttemptsArray;

	internal static NativeFlagArray? customBadThingLogicFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_should_defer_bad_thing_hook;

	internal static NativeStateArray<int>? customGoodThingRequiredAttemptsArray;

	internal static NativeFlagArray? customGoodThingRequiredAttemptsFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_should_defer_good_thing_hook;

	internal static NativeStateArray<int>? buyAleAmountArray;

	internal static NativeStateArray<int>? buyAleAtArray;

	internal static NativeFlagArray? buyAleAtFlagArray;

	internal static NativeStateArray<int>? buyIronAmountArray;

	internal static NativeStateArray<int>? buyIronAtArray;

	internal static NativeFlagArray? buyIronAtFlagArray;

	internal static NativeStateArray<int>? buyWoodAmountArray;

	internal static NativeStateArray<int>? buyWoodAtArray;

	internal static NativeFlagArray? buyWoodAtFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_food_buy_handler_hook;

	internal static NativeStateArray<short>? maxOxenArray;

	internal static NativeFlagArray? maxOxenFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_need_more_ox_bases_hook2;

	internal static NativeStateArray<short>? stoneToOxenRatioArray;

	internal static NativeFlagArray? stoneToOxenRatioFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_need_more_ox_bases_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_build_wall_internal_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_build_wall_hook;

	internal static NativeStateArray<short>? minGoldForHarassmentSiegeEnginesCostsArray;

	internal static NativeFlagArray? minGoldForHarassmentSiegeEnginesFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_deploy_harassing_siege_engine_tents_hook2;

	internal static NativeFlagArray? wheatSaleCategoryFlagArray;

	internal static HookHandle<X64InlineHook> c_game_ai_sell_item_handler_hook;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook11;

	internal static HookHandle<X64AssemblyPatch> c_game_aiv_orderedmaptileids_hook10;

	internal static HookHandle<X64AssemblyPatch> c_game_aiv_orderedmaptileids_hook9;

	internal static HookHandle<X64AssemblyPatch> c_game_aiv_orderedmaptileids_hook8;

	internal static HookHandle<X64AssemblyPatch> c_game_aiv_orderedmaptileids_hook7;

	internal static HookHandle<X64AssemblyPatch> c_game_aiv_orderedmaptileids_hook6;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook5;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook4;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook3;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook2;

	internal static HookHandle<X64InlineHook> c_game_aiv_orderedmaptileids_hook1;

	internal static HookHandle<X64InlineHook> c_game_ai_tribe_siege_behaviour_handler_hook2;

	internal static HookHandle<X64InlineHook> c_game_ai_tribe_siege_behaviour_handler_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_decide_target_priority_hook2;

	internal static HookHandle<X64InlineHook> c_game_ai_decide_target_priority_hook;

	internal static HookHandle<X64InlineHook> c_game_aiv_validate_building_tile_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_recruitmentmode_handler_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_move_tribe_to_keep_entrance_hook;

	internal static HookHandle<X64InlineHook> c_game_attempt_recruit_required_troops_hook;

	internal static HookHandle<X64InlineHook> c_game_attempt_recruit_required_troops_hook2;

	internal static HookHandle<X64InlineHook> c_game_attempt_recruit_required_troops_hook3;

	internal static HookHandle<X64InlineHook> c_game_ai_train_economy_protection_troops_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_train_economy_protection_troops_hook2;

	internal static HookHandle<X64InlineHook> c_game_ai_train_bodyguard_troops_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_train_bodyguard_troops_hook2;

	internal static DetourHandle<c_game_ai_siege_master_handler_delegate> c_game_ai_siege_master_handler_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_deploy_harassing_siege_engine_tents_hook;

	internal static HookHandle<X64InlineHook> c_game_ai_find_valid_siege_tent_site_hook;

	internal static HookHandle<X64InlineHook> c_game_aiv_prepare_layout_hook;

	public static HookHandle<X64InlineHook> c_game_ai_count_active_farms_hook;

	private static ModLogHelper Log => _log.Value;

	public unsafe FixesAIDetours(CrusaderLibraryLoadContext context, HookTransaction tx, DataScanner scanner, NativeStateBlock nativeStateBlock)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Expected O, but got Unknown
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Expected O, but got Unknown
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Expected O, but got Unknown
		//IL_056f: Unknown result type (might be due to invalid IL or missing references)
		//IL_053b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0540: Unknown result type (might be due to invalid IL or missing references)
		//IL_0546: Expected O, but got Unknown
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Expected O, but got Unknown
		//IL_05bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_058d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0593: Expected O, but got Unknown
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Expected O, but got Unknown
		//IL_05d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05da: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e0: Expected O, but got Unknown
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Expected O, but got Unknown
		//IL_0625: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Expected O, but got Unknown
		//IL_07b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ff: Expected O, but got Unknown
		//IL_070b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Unknown result type (might be due to invalid IL or missing references)
		//IL_0728: Expected O, but got Unknown
		//IL_0661: Unknown result type (might be due to invalid IL or missing references)
		//IL_063e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0643: Unknown result type (might be due to invalid IL or missing references)
		//IL_0649: Expected O, but got Unknown
		//IL_03ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_031f: Expected O, but got Unknown
		//IL_09f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_07d4: Expected O, but got Unknown
		//IL_067a: Unknown result type (might be due to invalid IL or missing references)
		//IL_067f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0685: Expected O, but got Unknown
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f2: Expected O, but got Unknown
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_033a: Expected O, but got Unknown
		//IL_0a5f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a09: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a14: Expected O, but got Unknown
		//IL_0827: Unknown result type (might be due to invalid IL or missing references)
		//IL_0805: Unknown result type (might be due to invalid IL or missing references)
		//IL_080a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0810: Expected O, but got Unknown
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Expected O, but got Unknown
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aaf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a78: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a83: Expected O, but got Unknown
		//IL_0862: Unknown result type (might be due to invalid IL or missing references)
		//IL_0840: Unknown result type (might be due to invalid IL or missing references)
		//IL_0845: Unknown result type (might be due to invalid IL or missing references)
		//IL_084b: Expected O, but got Unknown
		//IL_0456: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Expected O, but got Unknown
		//IL_0399: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Expected O, but got Unknown
		//IL_0ba2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ac8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0acd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ad3: Expected O, but got Unknown
		//IL_089e: Unknown result type (might be due to invalid IL or missing references)
		//IL_087b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0880: Unknown result type (might be due to invalid IL or missing references)
		//IL_0886: Expected O, but got Unknown
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_047c: Expected O, but got Unknown
		//IL_0c0e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bbb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc6: Expected O, but got Unknown
		//IL_0b48: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b55: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b66: Expected O, but got Unknown
		//IL_08da: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08f7: Expected O, but got Unknown
		//IL_0903: Unknown result type (might be due to invalid IL or missing references)
		//IL_090f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0920: Expected O, but got Unknown
		//IL_092c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0938: Unknown result type (might be due to invalid IL or missing references)
		//IL_0949: Expected O, but got Unknown
		//IL_0955: Unknown result type (might be due to invalid IL or missing references)
		//IL_0961: Unknown result type (might be due to invalid IL or missing references)
		//IL_0972: Expected O, but got Unknown
		//IL_097e: Unknown result type (might be due to invalid IL or missing references)
		//IL_098a: Unknown result type (might be due to invalid IL or missing references)
		//IL_099b: Expected O, but got Unknown
		//IL_09a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_09c4: Expected O, but got Unknown
		//IL_08b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c2: Expected O, but got Unknown
		//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c27: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c2c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c32: Expected O, but got Unknown
		//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e7: Expected O, but got Unknown
		//IL_0d8c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d01: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d06: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d0c: Expected O, but got Unknown
		//IL_0dc8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0daa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0db0: Expected O, but got Unknown
		//IL_0f12: Unknown result type (might be due to invalid IL or missing references)
		//IL_0de1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0de6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dec: Expected O, but got Unknown
		//IL_0f80: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f2b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f30: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f36: Expected O, but got Unknown
		//IL_0e8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e98: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ea9: Expected O, but got Unknown
		//IL_0eb5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ec2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ed3: Expected O, but got Unknown
		//IL_1035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fbc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f99: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f9e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fa4: Expected O, but got Unknown
		//IL_10cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1071: Unknown result type (might be due to invalid IL or missing references)
		//IL_104e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1053: Unknown result type (might be due to invalid IL or missing references)
		//IL_1059: Expected O, but got Unknown
		//IL_0fd5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fda: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fe0: Expected O, but got Unknown
		//IL_10e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_10eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_10f1: Expected O, but got Unknown
		//IL_108a: Unknown result type (might be due to invalid IL or missing references)
		//IL_108f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1095: Expected O, but got Unknown
		//IL_1157: Unknown result type (might be due to invalid IL or missing references)
		//IL_1164: Unknown result type (might be due to invalid IL or missing references)
		//IL_1175: Expected O, but got Unknown
		//IL_11d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_11e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_11f1: Expected O, but got Unknown
		ILogger val = Plugin.Instance.LoggerFactory.CreateLogger("Fixes-AIDetours");
		Log.Information("Applying", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
		ulong currentImageBase = (ulong)(long)context.ModuleHandle;
		tx.AddDetour<c_game_ai_siege_master_handler_delegate>(c_game_ai_siege_master_handler_hook, HookTarget.op_Implicit("48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC ? 83 3D ? ? ? ? ? 48 8B F1"), (c_game_ai_siege_master_handler_delegate)c_game_ai_siege_master_handler_hook_impl, "c_game_ai_siege_master_handler_hook");
		if (Plugin.Instance.EnableHarassingTentIdleEngineersFix.Value)
		{
			HookHandle<X64InlineHook> obj = c_game_ai_deploy_harassing_siege_engine_tents_hook;
			HookTarget val2 = HookTarget.op_Implicit("42 89 BC 29 ? ? ? ? 48 8D 0D");
			object obj2 = <>c.<>9__7_0;
			if (obj2 == null)
			{
				InlineHookGenerator val3 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0048: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Expected O, but got Unknown
					//IL_0053: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Expected O, but got Unknown
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
					AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action<ulong, ulong, ulong, int, int, int>>(delegate(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
					{
						GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(Plugin.Instance.HarassingTentIdleEngineersFixDelayAmount.Value, (Action)delegate
						{
							GameTribeManagerAPI.Instance.IssueTargettedCommand((int)tribeId, (TribeAICommand)aiCommandType, targetValue1, targetValue2, a6);
						}, string.Empty);
					}), 6, (Action<Assembler>)null, true, false, (X64StackArgument[])(object)new X64StackArgument[2]
					{
						new X64StackArgument((X64ArgumentSourceType)0, 0uL),
						new X64StackArgument((X64ArgumentSourceType)0, 1uL)
					});
				};
				<>c.<>9__7_0 = val3;
				obj2 = (object)val3;
			}
			tx.AddInline(obj, val2, (InlineHookGenerator)obj2, 23, "c_game_ai_deploy_harassing_siege_engine_tents_hook");
		}
		if (Plugin.Instance.EnableAIDistancedSiegeTents.Value)
		{
			HookHandle<X64InlineHook> obj3 = c_game_ai_find_valid_siege_tent_site_hook;
			HookTarget val4 = HookTarget.op_Implicit("41 F6 84 B9");
			object obj4 = <>c.<>9__7_1;
			if (obj4 == null)
			{
				InlineHookGenerator val5 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0026: Unknown result type (might be due to invalid IL or missing references)
					//IL_0031: Unknown result type (might be due to invalid IL or missing references)
					//IL_003c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0047: Unknown result type (might be due to invalid IL or missing references)
					//IL_004c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0057: Unknown result type (might be due to invalid IL or missing references)
					//IL_0061: Unknown result type (might be due to invalid IL or missing references)
					//IL_0069: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0079: Unknown result type (might be due to invalid IL or missing references)
					//IL_0083: Unknown result type (might be due to invalid IL or missing references)
					//IL_008e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0093: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
					//IL_0102: Unknown result type (might be due to invalid IL or missing references)
					AssemblerExtensions.AddInstructions(asm, instrs);
					asm.jnz(((Instruction)instrs[1]).IPRelativeMemoryAddress);
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r8);
					asm.push(AssemblerRegisters.r9);
					asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.edi);
					asm.mov(AssemblerRegisters.edx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 64L]);
					asm.mov(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 208L]);
					AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<int, int, int, int>>(delegate(int footprintTileId, int centerTileId, int playerId)
					{
						//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
						//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
						//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
						//IL_01e7: Invalid comparison between Unknown and I4
						//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
						//IL_01ed: Invalid comparison between Unknown and I4
						//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
						//IL_01f3: Invalid comparison between Unknown and I4
						//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
						//IL_01f9: Invalid comparison between Unknown and I4
						GameTileManagerAPI instance = GameTileManagerAPI.Instance;
						GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
						int num3 = instance.MapColumnLookupTable[footprintTileId];
						int num4 = instance.MapColumnLookupTable[centerTileId];
						if ((uint)num3 >= 800u || (uint)num4 >= 800u)
						{
							return 0;
						}
						int num5 = footprintTileId - instance.MapRowLookupTable[3 * num3];
						int num6 = centerTileId - instance.MapRowLookupTable[3 * num4];
						if ((uint)num5 >= 800u || (uint)num6 >= 800u)
						{
							return 0;
						}
						for (int i = Math.Max(0, num3 - 2); i <= Math.Min(799, num3 + 2); i++)
						{
							int num7 = instance.MapRowLookupTable[3 * i];
							for (int j = Math.Max(0, num5 - 2); j <= Math.Min(799, num5 + 2); j++)
							{
								if (instance.TileManager.StructureGrid[num7 + j] != 0)
								{
									return 0;
								}
							}
						}
						if (!instance2.IsPlayerIdValid(playerId))
						{
							return 1;
						}
						GamePlayerResources* ptr2 = default(GamePlayerResources*);
						if (!instance2.TryGetPlayerResourcesById(playerId, ref ptr2))
						{
							return 1;
						}
						int r_SiegeAttackTargetPlayerId = (int)((GamePlayerResources)ptr2).r_SiegeAttackTargetPlayerId;
						GamePlayerResources* ptr3 = default(GamePlayerResources*);
						if (instance2.IsPlayerIdValid(r_SiegeAttackTargetPlayerId) || r_SiegeAttackTargetPlayerId == playerId || !instance2.TryGetPlayerResourcesById(r_SiegeAttackTargetPlayerId, ref ptr3))
						{
							return 1;
						}
						int r_KeepDoorTilePositionX = (int)((GamePlayerResources)ptr3).r_KeepDoorTilePositionX;
						int r_KeepDoorTilePositionY = (int)((GamePlayerResources)ptr3).r_KeepDoorTilePositionY;
						if ((uint)r_KeepDoorTilePositionX >= 800u || (uint)r_KeepDoorTilePositionY >= 800u || (r_KeepDoorTilePositionX | r_KeepDoorTilePositionY) == 0)
						{
							return 1;
						}
						System.Span<GameBuilding> buildingsAsSpan = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
						for (int k = Math.Max(0, num4 - 8); k <= Math.Min(799, num4 + 8); k++)
						{
							int num8 = instance.MapRowLookupTable[3 * k];
							for (int l = Math.Max(0, num6 - 8); l <= Math.Min(799, num6 + 8); l++)
							{
								int num9 = instance.TileManager.StructureGrid[num8 + l];
								if (num9 != 0 && num9 <= buildingsAsSpan.Length)
								{
									ref GameBuilding reference = ref buildingsAsSpan[num9 - 1];
									if (reference.r_PlayerIdOwner == playerId)
									{
										eStructs r_BuildingType = reference.r_BuildingType;
										if ((int)r_BuildingType == 29 || (int)r_BuildingType == 54 || ((int)r_BuildingType >= 80 && (int)r_BuildingType <= 84))
										{
											int num10 = reference.r_TilePositionXBegin + 1;
											int num11 = reference.r_TilePositionYBegin + 1;
											int num12 = r_KeepDoorTilePositionX - num10;
											int num13 = r_KeepDoorTilePositionY - num11;
											int num14 = Math.Max(Math.Abs(num12), Math.Abs(num13));
											if (num14 >= 4 && (num6 - num10) * num12 + (num4 - num11) * num13 > num14)
											{
												return 0;
											}
										}
									}
								}
							}
						}
						return 1;
					}), 3, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
					asm.pop(AssemblerRegisters.r9);
					asm.pop(AssemblerRegisters.r8);
					asm.pop(AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rcx);
					asm.cmp(AssemblerRegisters.rax, 1);
				};
				<>c.<>9__7_1 = val5;
				obj4 = (object)val5;
			}
			tx.AddInline(obj3, val4, (InlineHookGenerator)obj4, 14, "c_game_ai_find_valid_siege_tent_site_hook");
		}
		if (Plugin.Instance.EnableHopsFarmFix.Value)
		{
			HookHandle<X64InlineHook> obj5 = c_game_ai_count_active_farms_hook;
			HookTarget val6 = HookTarget.op_Implicit("66 83 FA ? 74 ? 45 85 C0 74 ? 66 83 B9");
			object obj6 = <>c.<>9__7_2;
			if (obj6 == null)
			{
				InlineHookGenerator val7 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0023: Unknown result type (might be due to invalid IL or missing references)
					//IL_0028: Unknown result type (might be due to invalid IL or missing references)
					//IL_006b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0070: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0086: Unknown result type (might be due to invalid IL or missing references)
					//IL_0091: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel("lblSkip");
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
					AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<ulong, bool>)delegate(ulong playerId)
					{
						//IL_0024: Unknown result type (might be due to invalid IL or missing references)
						//IL_0029: Unknown result type (might be due to invalid IL or missing references)
						//IL_0035: Unknown result type (might be due to invalid IL or missing references)
						Plugin instance = Plugin.Instance;
						_ = GameAIManagerAPI.Instance;
						GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
						if (instance.ForceHopsFarmFix.Value)
						{
							return true;
						}
						AILords aILord = instance2.GetAILord((int)playerId);
						bool value = default(bool);
						if ((instance.HopsFarmWhitelist?.TryGetValue(aILord, out value) ?? false) && value)
						{
							return true;
						}
						CustomLordPreferencesEntry preferences;
						return Preferences.TryGet((int)playerId, out preferences) && preferences.EnableHopsFarmFix == true;
					}), 1, (Action<Assembler>)null, true, false, Array.Empty<X64StackArgument>());
					asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
					asm.pop(AssemblerRegisters.rax);
					asm.pop(AssemblerRegisters.rcx);
					asm.jnz(val82);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(2));
				};
				<>c.<>9__7_2 = val7;
				obj6 = (object)val7;
			}
			tx.AddInline(obj5, val6, (InlineHookGenerator)obj6, 14, "c_game_ai_count_active_farms_hook");
		}
		if (Plugin.Instance.AllowKeep3ForAIVs.Value)
		{
			HookHandle<X64InlineHook> obj7 = c_game_aiv_prepare_layout_hook;
			HookTarget val8 = HookTarget.op_Implicit("66 83 F8 ? 74 ? 66 41 2B C0");
			object obj8 = <>c.<>9__7_3;
			if (obj8 == null)
			{
				InlineHookGenerator val9 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).Skip(2).ToArray();
					AssemblerExtensions.AddInstructions(asm, array3);
				};
				<>c.<>9__7_3 = val9;
				obj8 = (object)val9;
			}
			tx.AddInline(obj7, val8, (InlineHookGenerator)obj8, 14, "c_game_aiv_prepare_layout_hook");
		}
		if (Plugin.Instance.EnableMissingHorsePurchasingFix.Value)
		{
			HookHandle<X64InlineHook> obj9 = c_game_attempt_recruit_required_troops_hook;
			HookTarget val10 = HookTarget.op_Implicit("45 8B CE 89 5C 24 ? 44 8B C0 48 8D 0D ? ? ? ? E8 ? ? ? ? 8B D8");
			object obj10 = <>c.<>9__7_37;
			if (obj10 == null)
			{
				InlineHookGenerator val11 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004f: Unknown result type (might be due to invalid IL or missing references)
					Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
					asm.pop(AssemblerRegisters.rax);
					AssemblerExtensions.AddInstructions(asm, array3);
				};
				<>c.<>9__7_37 = val11;
				obj10 = (object)val11;
			}
			tx.AddInline(obj9, val10, (InlineHookGenerator)obj10, 14, "c_game_attempt_recruit_required_troops_hook");
			HookHandle<X64InlineHook> obj11 = c_game_attempt_recruit_required_troops_hook2;
			HookTarget val12 = HookTarget.op_Implicit("8B D7 83 FF");
			object obj12 = <>c.<>9__7_38;
			if (obj12 == null)
			{
				InlineHookGenerator val13 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004f: Unknown result type (might be due to invalid IL or missing references)
					Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
					asm.pop(AssemblerRegisters.rax);
					AssemblerExtensions.AddInstructions(asm, array3);
				};
				<>c.<>9__7_38 = val13;
				obj12 = (object)val13;
			}
			tx.AddInline(obj11, val12, (InlineHookGenerator)obj12, 14, "c_game_attempt_recruit_required_troops_hook2");
			HookHandle<X64InlineHook> obj13 = c_game_attempt_recruit_required_troops_hook3;
			HookTarget val14 = HookTarget.op_Implicit("85 C0 7E ? 83 3D ? ? ? ? ? 75 ? 48 8B 44 24");
			object obj14 = <>c.<>9__7_39;
			if (obj14 == null)
			{
				InlineHookGenerator val15 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0023: Unknown result type (might be due to invalid IL or missing references)
					//IL_002d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0044: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0053: Unknown result type (might be due to invalid IL or missing references)
					//IL_005e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Unknown result type (might be due to invalid IL or missing references)
					//IL_007a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_008c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0097: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
					Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
					asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
					asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
					asm.sub(AssemblerRegisters.ecx, 1);
					asm.cmp(AssemblerRegisters.ecx, 23);
					asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rcx);
					AssemblerExtensions.AddInstructions(asm, array3);
				};
				<>c.<>9__7_39 = val15;
				obj14 = (object)val15;
			}
			tx.AddInline(obj13, val14, (InlineHookGenerator)obj14, 14, "c_game_attempt_recruit_required_troops_hook3");
			HookHandle<X64InlineHook> obj15 = c_game_ai_train_economy_protection_troops_hook;
			HookTarget val16 = HookTarget.op_Implicit("48 89 74 24 ? 44 8B CD 4C 89 74 24");
			object obj16 = <>c.<>9__7_40;
			if (obj16 == null)
			{
				InlineHookGenerator val17 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004f: Unknown result type (might be due to invalid IL or missing references)
					Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
					asm.pop(AssemblerRegisters.rax);
					AssemblerExtensions.AddInstructions(asm, array3);
				};
				<>c.<>9__7_40 = val17;
				obj16 = (object)val17;
			}
			tx.AddInline(obj15, val16, (InlineHookGenerator)obj16, 14, "c_game_ai_train_economy_protection_troops_hook");
			DataScanner val18 = scanner.Scan(CompiledPattern.Parse("40 55 41 54 41 55 41 57 48 83 EC ? 48 63 EA 4C 8D 2D ? ? ? ? 4C 69 CD ? ? ? ? 4C 8B E1 4C 8B FD 4B 63 84 29 ? ? ? ? 85 C0 0F 84 ? ? ? ? 48 89 7C 24 ? 48 69 F8 ? ? ? ? 48 03 F9 8B 8F"), 0);
			if (val18.Found)
			{
				InstructionWalkerConfig val19 = new InstructionWalkerConfig();
				val19.Bitness = 64;
				val19.EndDetection = (FunctionEndDetection)0;
				InstructionWalkerConfig obj17 = val19;
				InstructionWalkRule[] array = new InstructionWalkRule[1];
				object obj18 = <>c.<>9__7_44;
				if (obj18 == null)
				{
					InstructionPredicate val20 = (Instruction x) => (int)((Instruction)(ref x)).Mnemonic == 751;
					<>c.<>9__7_44 = val20;
					obj18 = (object)val20;
				}
				object obj19 = <>O.<1>__ExtractInstructionAddress;
				if (obj19 == null)
				{
					InstructionExtractor val21 = InstructionWalkerHelpers.ExtractInstructionAddress;
					<>O.<1>__ExtractInstructionAddress = val21;
					obj19 = (object)val21;
				}
				array[0] = InstructionWalkRule.FindAndExtract((InstructionPredicate)obj18, (InstructionExtractor)obj19, 0, (InstructionWalkerStartMode)1);
				obj17.WalkRules = (InstructionWalkRule[])(object)array;
				ulong num = default(ulong);
				if (DataScannerInstructionWalkerExtensions.CreateWalker(val18, val19, (ILogger)null).TryWalk<ulong>(ref num))
				{
					LoggerExtensions.LogInformation(val, "Found anchor c_game_ai_train_economy_protection_troops_hook2 at " + num.ToString("X16"), Array.Empty<object>());
					HookHandle<X64InlineHook> obj20 = c_game_ai_train_economy_protection_troops_hook2;
					HookTarget val22 = HookTarget.op_Implicit(num);
					object obj21 = <>c.<>9__7_41;
					if (obj21 == null)
					{
						InlineHookGenerator val23 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
						{
							//IL_000d: Unknown result type (might be due to invalid IL or missing references)
							//IL_0018: Unknown result type (might be due to invalid IL or missing references)
							//IL_0023: Unknown result type (might be due to invalid IL or missing references)
							//IL_002d: Unknown result type (might be due to invalid IL or missing references)
							//IL_0044: Unknown result type (might be due to invalid IL or missing references)
							//IL_004e: Unknown result type (might be due to invalid IL or missing references)
							//IL_0053: Unknown result type (might be due to invalid IL or missing references)
							//IL_005e: Unknown result type (might be due to invalid IL or missing references)
							//IL_0063: Unknown result type (might be due to invalid IL or missing references)
							//IL_006e: Unknown result type (might be due to invalid IL or missing references)
							//IL_007a: Unknown result type (might be due to invalid IL or missing references)
							//IL_0087: Unknown result type (might be due to invalid IL or missing references)
							//IL_008c: Unknown result type (might be due to invalid IL or missing references)
							//IL_0097: Unknown result type (might be due to invalid IL or missing references)
							//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
							Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
							asm.push(AssemblerRegisters.rcx);
							asm.push(AssemblerRegisters.rdx);
							asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
							asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
							asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
							asm.sub(AssemblerRegisters.ecx, 1);
							asm.cmp(AssemblerRegisters.ecx, 23);
							asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
							asm.pop(AssemblerRegisters.rdx);
							asm.pop(AssemblerRegisters.rcx);
							AssemblerExtensions.AddInstructions(asm, array3);
						};
						<>c.<>9__7_41 = val23;
						obj21 = (object)val23;
					}
					tx.AddInline(obj20, val22, (InlineHookGenerator)obj21, 14, "c_game_ai_train_economy_protection_troops_hook2");
					goto IL_03c3;
				}
			}
			LoggerExtensions.LogError(val, "Could not find anchor for c_game_ai_train_economy_protection_troops_hook2", Array.Empty<object>());
			goto IL_03c3;
		}
		goto IL_0506;
		IL_0506:
		if (Plugin.Instance.EnableMountedUnitsRetreatFix.Value)
		{
			HookHandle<X64InlineHook> obj22 = c_game_ai_move_tribe_to_keep_entrance_hook;
			HookTarget val24 = HookTarget.op_Implicit("83 BC 01 ? ? ? ? 00 7E ? 44 8B 8C 01");
			object obj23 = <>c.<>9__7_4;
			if (obj23 == null)
			{
				InlineHookGenerator val25 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0012: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_001a: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_0034: Unknown result type (might be due to invalid IL or missing references)
					//IL_0039: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Unknown result type (might be due to invalid IL or missing references)
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					//IL_006f: Unknown result type (might be due to invalid IL or missing references)
					//IL_007a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0085: Unknown result type (might be due to invalid IL or missing references)
					//IL_0090: Unknown result type (might be due to invalid IL or missing references)
					//IL_009b: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
					//IL_0112: Unknown result type (might be due to invalid IL or missing references)
					//IL_0117: Unknown result type (might be due to invalid IL or missing references)
					//IL_0128: Unknown result type (might be due to invalid IL or missing references)
					//IL_012d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0138: Unknown result type (might be due to invalid IL or missing references)
					//IL_0145: Unknown result type (might be due to invalid IL or missing references)
					//IL_014f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0157: Unknown result type (might be due to invalid IL or missing references)
					//IL_015c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0167: Unknown result type (might be due to invalid IL or missing references)
					//IL_016c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0177: Unknown result type (might be due to invalid IL or missing references)
					//IL_017f: Unknown result type (might be due to invalid IL or missing references)
					//IL_018c: Unknown result type (might be due to invalid IL or missing references)
					//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
					//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
					//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
					//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
					//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
					//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
					//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
					//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
					//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
					//IL_0212: Unknown result type (might be due to invalid IL or missing references)
					//IL_0217: Unknown result type (might be due to invalid IL or missing references)
					//IL_0222: Unknown result type (might be due to invalid IL or missing references)
					//IL_0229: Unknown result type (might be due to invalid IL or missing references)
					//IL_022e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0239: Unknown result type (might be due to invalid IL or missing references)
					//IL_0245: Unknown result type (might be due to invalid IL or missing references)
					//IL_024a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0255: Unknown result type (might be due to invalid IL or missing references)
					//IL_025a: Unknown result type (might be due to invalid IL or missing references)
					//IL_026b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0275: Unknown result type (might be due to invalid IL or missing references)
					//IL_027a: Unknown result type (might be due to invalid IL or missing references)
					//IL_027f: Unknown result type (might be due to invalid IL or missing references)
					//IL_028a: Unknown result type (might be due to invalid IL or missing references)
					//IL_028f: Unknown result type (might be due to invalid IL or missing references)
					//IL_029a: Unknown result type (might be due to invalid IL or missing references)
					//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
					//IL_02af: Unknown result type (might be due to invalid IL or missing references)
					//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
					//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
					//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
					//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
					//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
					//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
					//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
					//IL_030c: Unknown result type (might be due to invalid IL or missing references)
					//IL_031b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0326: Unknown result type (might be due to invalid IL or missing references)
					//IL_0336: Unknown result type (might be due to invalid IL or missing references)
					//IL_033d: Unknown result type (might be due to invalid IL or missing references)
					//IL_034d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0352: Unknown result type (might be due to invalid IL or missing references)
					//IL_0365: Unknown result type (might be due to invalid IL or missing references)
					//IL_0370: Unknown result type (might be due to invalid IL or missing references)
					//IL_037b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0386: Unknown result type (might be due to invalid IL or missing references)
					//IL_0391: Unknown result type (might be due to invalid IL or missing references)
					//IL_039c: Unknown result type (might be due to invalid IL or missing references)
					//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
					//IL_03b2: Unknown result type (might be due to invalid IL or missing references)
					//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					Label val84 = asm.CreateLabel((string)null);
					Label val85 = asm.CreateLabel((string)null);
					Label val86 = asm.CreateLabel((string)null);
					Label val87 = asm.CreateLabel((string)null);
					Label val88 = asm.CreateLabel((string)null);
					AssemblerExtensions.AddInstructions(asm, overwritten);
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r8);
					asm.push(AssemblerRegisters.r10);
					asm.push(AssemblerRegisters.r11);
					asm.push(AssemblerRegisters.rsi);
					asm.push(AssemblerRegisters.rdi);
					asm.push(AssemblerRegisters.r12);
					asm.mov(AssemblerRegisters.r12d, -1);
					asm.cmp(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rcx + 1240156L]);
					asm.jbe(val88);
					asm.mov(AssemblerRegisters.r12d, 1);
					asm.Label(ref val88);
					asm.mov(AssemblerRegisters.r10, GameGlobalsManager.Instance.GameTribeManagerVA);
					asm.imul(AssemblerRegisters.r11, AssemblerRegisters.rbx, System.Runtime.CompilerServices.Unsafe.SizeOf<GameTribe>());
					asm.add(AssemblerRegisters.r10, AssemblerRegisters.r11);
					asm.add(AssemblerRegisters.r10, 42);
					asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r10 + 50L]);
					asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.jz(val87);
					asm.add(AssemblerRegisters.r10, 54);
					asm.mov(AssemblerRegisters.r8, GameGlobalsManager.Instance.GameUnitManagerVA);
					asm.xor(AssemblerRegisters.esi, AssemblerRegisters.esi);
					asm.Label(ref val82);
					asm.movzx(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rsi * 2]);
					asm.test(AssemblerRegisters.r11d, AssemblerRegisters.r11d);
					asm.jz(val85);
					asm.xor(AssemblerRegisters.edi, AssemblerRegisters.edi);
					asm.Label(ref val83);
					asm.bt(AssemblerRegisters.r11d, AssemblerRegisters.edi);
					asm.jnc(val84);
					asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.esi);
					asm.shl(AssemblerRegisters.ecx, (sbyte)4);
					asm.add(AssemblerRegisters.ecx, AssemblerRegisters.edi);
					asm.imul(AssemblerRegisters.rcx, AssemblerRegisters.rcx, System.Runtime.CompilerServices.Unsafe.SizeOf<GameUnit>());
					asm.movzx(AssemblerRegisters.edx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rcx + 1766L]);
					asm.cmp(AssemblerRegisters.edx, 74);
					asm.je(val86);
					asm.cmp(AssemblerRegisters.edx, 78);
					asm.je(val86);
					asm.cmp(AssemblerRegisters.edx, 83);
					asm.je(val86);
					asm.dec(AssemblerRegisters.eax);
					asm.jz(val87);
					asm.Label(ref val84);
					asm.inc(AssemblerRegisters.edi);
					asm.cmp(AssemblerRegisters.edi, 16);
					asm.jb(val83);
					asm.Label(ref val85);
					asm.inc(AssemblerRegisters.esi);
					asm.cmp(AssemblerRegisters.esi, 625);
					asm.jb(val82);
					asm.jmp(val87);
					asm.Label(ref val86);
					asm.add(AssemblerRegisters.r9d, AssemblerRegisters.r12d);
					asm.Label(ref val87);
					asm.pop(AssemblerRegisters.r12);
					asm.pop(AssemblerRegisters.rdi);
					asm.pop(AssemblerRegisters.rsi);
					asm.pop(AssemblerRegisters.r11);
					asm.pop(AssemblerRegisters.r10);
					asm.pop(AssemblerRegisters.r8);
					asm.pop(AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
				};
				<>c.<>9__7_4 = val25;
				obj23 = (object)val25;
			}
			tx.AddInline(obj22, val24, (InlineHookGenerator)obj23, 14, "c_game_ai_move_tribe_to_keep_entrance_hook");
		}
		if (Plugin.Instance.EnablePerLordCustomRequiredDefensiveRecruitmentTickTime.Value)
		{
			HookHandle<X64InlineHook> obj24 = c_game_ai_recruitmentmode_handler_hook;
			HookTarget val26 = HookTarget.op_Implicit("81 3D ? ? ? ? ? ? ? ? 7C ");
			object obj25 = <>c.<>9__7_5;
			if (obj25 == null)
			{
				InlineHookGenerator val27 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_001c: Unknown result type (might be due to invalid IL or missing references)
					//IL_005f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0079: Unknown result type (might be due to invalid IL or missing references)
					//IL_007e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0083: Unknown result type (might be due to invalid IL or missing references)
					//IL_008e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0099: Unknown result type (might be due to invalid IL or missing references)
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rcx);
					asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
					AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<int, int>)delegate(int playerId)
					{
						_ = GameAIManagerAPI.Instance;
						_ = Plugin.Instance;
						CustomLordPreferencesEntry preferences;
						return Preferences.TryGet(playerId, out preferences) ? ((!preferences.EarlyDefensiveTimeDurationTicks.HasValue) ? 4800 : preferences.EarlyDefensiveTimeDurationTicks.Value) : 4800;
					}), 1, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
					asm.mov(AssemblerRegisters.rcx, GameGlobalsManager.Instance.CurrentGameTickVA);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx], AssemblerRegisters.eax);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
				};
				<>c.<>9__7_5 = val27;
				obj25 = (object)val27;
			}
			tx.AddInline(obj24, val26, (InlineHookGenerator)obj25, 14, "c_game_ai_recruitmentmode_handler_hook");
		}
		if (Plugin.Instance.AllowAIToBuiltTowersAndSimiliarOverAlliedUnits.Value)
		{
			HookHandle<X64InlineHook> obj26 = c_game_aiv_validate_building_tile_hook;
			HookTarget val28 = HookTarget.op_Implicit("66 42 83 BC 09 ? ? ? ? ? 0F 84 ? ? ? ? 66 42 83 BC 09 ? ? ? ? 00 0F 84 ? ? ? ? 42 0F BF 84 09 ");
			object obj27 = <>c.<>9__7_6;
			if (obj27 == null)
			{
				InlineHookGenerator val29 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_001a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_003b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0046: Unknown result type (might be due to invalid IL or missing references)
					//IL_0051: Unknown result type (might be due to invalid IL or missing references)
					//IL_006d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0072: Unknown result type (might be due to invalid IL or missing references)
					//IL_007d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_0092: Unknown result type (might be due to invalid IL or missing references)
					//IL_0097: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
					//IL_0102: Unknown result type (might be due to invalid IL or missing references)
					//IL_0107: Unknown result type (might be due to invalid IL or missing references)
					//IL_0112: Unknown result type (might be due to invalid IL or missing references)
					//IL_0117: Unknown result type (might be due to invalid IL or missing references)
					//IL_0123: Unknown result type (might be due to invalid IL or missing references)
					//IL_0128: Unknown result type (might be due to invalid IL or missing references)
					//IL_0133: Unknown result type (might be due to invalid IL or missing references)
					//IL_013d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0142: Unknown result type (might be due to invalid IL or missing references)
					//IL_014d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0152: Unknown result type (might be due to invalid IL or missing references)
					//IL_015d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0164: Unknown result type (might be due to invalid IL or missing references)
					//IL_016e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0179: Unknown result type (might be due to invalid IL or missing references)
					//IL_017e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0189: Unknown result type (might be due to invalid IL or missing references)
					//IL_0196: Unknown result type (might be due to invalid IL or missing references)
					//IL_019d: Unknown result type (might be due to invalid IL or missing references)
					//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
					//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
					//IL_01be: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
					//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
					//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
					//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
					//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
					//IL_0201: Unknown result type (might be due to invalid IL or missing references)
					//IL_020e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0215: Unknown result type (might be due to invalid IL or missing references)
					//IL_0222: Unknown result type (might be due to invalid IL or missing references)
					//IL_0229: Unknown result type (might be due to invalid IL or missing references)
					//IL_0236: Unknown result type (might be due to invalid IL or missing references)
					//IL_023d: Unknown result type (might be due to invalid IL or missing references)
					//IL_024a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0251: Unknown result type (might be due to invalid IL or missing references)
					//IL_025e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0265: Unknown result type (might be due to invalid IL or missing references)
					//IL_0270: Unknown result type (might be due to invalid IL or missing references)
					//IL_027b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0286: Unknown result type (might be due to invalid IL or missing references)
					//IL_0291: Unknown result type (might be due to invalid IL or missing references)
					//IL_029c: Unknown result type (might be due to invalid IL or missing references)
					//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
					//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
					//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
					//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
					//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					ulong num3 = ((Instruction)overwritten[1]).NearBranchTarget + 5;
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r8);
					asm.push(AssemblerRegisters.r9);
					asm.push(AssemblerRegisters.r10);
					asm.push(AssemblerRegisters.r11);
					asm.mov(AssemblerRegisters.rdx, GameGlobalsManager.Instance.GameUnitManagerVA + 1628);
					asm.add(AssemblerRegisters.rdx, AssemblerRegisters.rcx);
					asm.mov(AssemblerRegisters.r8b, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__byte_ptr))[AssemblerRegisters.rdx + 146L]);
					asm.mov(AssemblerRegisters.r9, GameGlobalsManager.Instance.GamePlayerManagerVA);
					asm.add(AssemblerRegisters.r9, 1580332);
					asm.imul(AssemblerRegisters.r11, AssemblerRegisters.r8, 4);
					asm.mov(AssemblerRegisters.r10, AssemblerRegisters.r9);
					asm.add(AssemblerRegisters.r10, AssemblerRegisters.r11);
					asm.mov(AssemblerRegisters.r10d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10]);
					asm.imul(AssemblerRegisters.r11, AssemblerRegisters.rbp, 4);
					asm.add(AssemblerRegisters.r9, AssemblerRegisters.r11);
					asm.mov(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r9]);
					asm.cmp(AssemblerRegisters.r9d, AssemblerRegisters.r10d);
					asm.jne(val82);
					asm.mov(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdx + 138L]);
					asm.cmp(AssemblerRegisters.r8, 28);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 74);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 78);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 83);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 29);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 39);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 77);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 40);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 59);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 60);
					asm.je(val82);
					asm.cmp(AssemblerRegisters.r8, 58);
					asm.je(val82);
					asm.pop(AssemblerRegisters.r11);
					asm.pop(AssemblerRegisters.r10);
					asm.pop(AssemblerRegisters.r9);
					asm.pop(AssemblerRegisters.r8);
					asm.pop(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.eax, 0);
					asm.jmp(num3);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.r11);
					asm.pop(AssemblerRegisters.r10);
					asm.pop(AssemblerRegisters.r9);
					asm.pop(AssemblerRegisters.r8);
					asm.pop(AssemblerRegisters.rdx);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_6 = val29;
				obj27 = (object)val29;
			}
			tx.AddInline(obj26, val28, (InlineHookGenerator)obj27, 14, "c_game_aiv_validate_building_tile_hook");
		}
		if (Plugin.Instance.AllowLethalAILordTargeting.Value)
		{
			DataScanner val30 = scanner.Scan("4E 0F BF 64 1E", 0);
			if (val30.Found)
			{
				HookHandle<X64InlineHook> obj28 = c_game_ai_tribe_siege_behaviour_handler_hook;
				HookTarget val31 = HookTarget.op_Implicit(val30.CurrentAddress);
				object obj29 = <>c.<>9__7_47;
				if (obj29 == null)
				{
					InlineHookGenerator val32 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
					{
						//IL_000e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0013: Unknown result type (might be due to invalid IL or missing references)
						//IL_0015: Unknown result type (might be due to invalid IL or missing references)
						//IL_0020: Unknown result type (might be due to invalid IL or missing references)
						//IL_002b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0030: Unknown result type (might be due to invalid IL or missing references)
						//IL_0073: Unknown result type (might be due to invalid IL or missing references)
						//IL_007e: Unknown result type (might be due to invalid IL or missing references)
						//IL_008a: Unknown result type (might be due to invalid IL or missing references)
						//IL_0095: Unknown result type (might be due to invalid IL or missing references)
						System.Span<Instruction> span = System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
						Label val82 = asm.CreateLabel((string)null);
						asm.push(AssemblerRegisters.rax);
						asm.push(AssemblerRegisters.rdx);
						asm.mov(AssemblerRegisters.rdx, AssemblerRegisters.r12);
						AssemblerExtensions.X64FastcallSafe(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate((Delegate)(Func<int, int, bool>)delegate(int targetPlayerLordId, int attackerPlayerId)
						{
							//IL_0021: Unknown result type (might be due to invalid IL or missing references)
							//IL_0028: Invalid comparison between Unknown and I4
							//IL_002b: Unknown result type (might be due to invalid IL or missing references)
							//IL_0031: Invalid comparison between Unknown and I4
							GameUnitManagerAPI instance = GameUnitManagerAPI.Instance;
							_ = GameAIManagerAPI.Instance;
							LobbySettingsViewModel lobbySettingsViewModel = Plugin.Instance.LobbySettingsViewModel;
							GameUnit* ptr2 = default(GameUnit*);
							if (!instance.TryGetUnitById(targetPlayerLordId, ref ptr2) || (int)((GameUnit)ptr2).r_UnitChimp != 55 || (int)((GameUnit)ptr2).r_AliveState != 2)
							{
								return false;
							}
							if (Preferences.TryGet(attackerPlayerId, out CustomLordPreferencesEntry preferences))
							{
								if (!preferences.LethalLordTargeting.HasValue)
								{
									return false;
								}
								return preferences.LethalLordTargeting.Value;
							}
							return (lobbySettingsViewModel != null && lobbySettingsViewModel.ForceLethalAILordTargeting) ? true : false;
						}), 1, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
						asm.pop(AssemblerRegisters.rdx);
						asm.cmp(AssemblerRegisters.rax, 1);
						asm.pop(AssemblerRegisters.rax);
						asm.jnz(val82);
						AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
						AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress + 40);
						asm.Label(ref val82);
						AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
					};
					<>c.<>9__7_47 = val32;
					obj29 = (object)val32;
				}
				tx.AddInline(obj28, val31, (InlineHookGenerator)obj29, 14, "c_game_ai_tribe_siege_behaviour_handler_hook");
				HookHandle<X64InlineHook> obj30 = c_game_ai_tribe_siege_behaviour_handler_hook2;
				HookTarget val33 = HookTarget.op_Implicit("E8 ? ? ? ? 85 C0 0F 84 ? ? ? ? B8 ? ? ? ? 8B E8");
				object obj31 = <>c.<>9__7_48;
				if (obj31 == null)
				{
					InlineHookGenerator val34 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
					{
						//IL_0019: Unknown result type (might be due to invalid IL or missing references)
						//IL_0023: Unknown result type (might be due to invalid IL or missing references)
						//IL_002b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0030: Unknown result type (might be due to invalid IL or missing references)
						//IL_003b: Unknown result type (might be due to invalid IL or missing references)
						//IL_004d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0055: Unknown result type (might be due to invalid IL or missing references)
						//IL_005a: Unknown result type (might be due to invalid IL or missing references)
						//IL_005f: Unknown result type (might be due to invalid IL or missing references)
						//IL_006f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0077: Unknown result type (might be due to invalid IL or missing references)
						//IL_007c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0081: Unknown result type (might be due to invalid IL or missing references)
						//IL_0091: Unknown result type (might be due to invalid IL or missing references)
						//IL_0099: Unknown result type (might be due to invalid IL or missing references)
						//IL_009e: Unknown result type (might be due to invalid IL or missing references)
						//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
						//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
						//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
						//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
						//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
						//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
						//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
						//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
						//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
						//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
						//IL_0100: Unknown result type (might be due to invalid IL or missing references)
						//IL_0111: Unknown result type (might be due to invalid IL or missing references)
						//IL_0119: Unknown result type (might be due to invalid IL or missing references)
						//IL_011e: Unknown result type (might be due to invalid IL or missing references)
						//IL_012a: Unknown result type (might be due to invalid IL or missing references)
						//IL_012f: Unknown result type (might be due to invalid IL or missing references)
						//IL_013a: Unknown result type (might be due to invalid IL or missing references)
						//IL_013f: Unknown result type (might be due to invalid IL or missing references)
						//IL_014f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0164: Unknown result type (might be due to invalid IL or missing references)
						//IL_0174: Unknown result type (might be due to invalid IL or missing references)
						//IL_0179: Unknown result type (might be due to invalid IL or missing references)
						//IL_0184: Unknown result type (might be due to invalid IL or missing references)
						//IL_018e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0199: Unknown result type (might be due to invalid IL or missing references)
						//IL_019e: Unknown result type (might be due to invalid IL or missing references)
						//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
						//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
						//IL_01be: Unknown result type (might be due to invalid IL or missing references)
						//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
						//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
						//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
						//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
						//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
						//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
						//IL_0203: Unknown result type (might be due to invalid IL or missing references)
						//IL_020d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0215: Unknown result type (might be due to invalid IL or missing references)
						//IL_021a: Unknown result type (might be due to invalid IL or missing references)
						//IL_0225: Unknown result type (might be due to invalid IL or missing references)
						//IL_022f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0237: Unknown result type (might be due to invalid IL or missing references)
						//IL_023c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0247: Unknown result type (might be due to invalid IL or missing references)
						//IL_0251: Unknown result type (might be due to invalid IL or missing references)
						//IL_0259: Unknown result type (might be due to invalid IL or missing references)
						//IL_025e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0269: Unknown result type (might be due to invalid IL or missing references)
						System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
						System.Span<Instruction> span = System.Span<Instruction>.op_Implicit(IcedExtensions.CloneInstructionsWithoutIP(overwritten));
						asm.mov(AssemblerRegisters.r10d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 88L]);
						asm.sub(AssemblerRegisters.rsp, 96);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 56L], AssemblerRegisters.rcx);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 64L], AssemblerRegisters.rdx);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L], AssemblerRegisters.r8);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 80L], AssemblerRegisters.r9);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 32L], 0);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 40L], 0);
						asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 48L], 0);
						asm.mov(AssemblerRegisters.r9d, AssemblerRegisters.r10d);
						asm.imul(AssemblerRegisters.r9, AssemblerRegisters.r9, 22588);
						asm.mov(AssemblerRegisters.r8, GameGlobalsManager.Instance.GamePlayerManagerVA);
						asm.add(AssemblerRegisters.r8, 1240000);
						asm.add(AssemblerRegisters.r8, AssemblerRegisters.r9);
						asm.mov(AssemblerRegisters.r9d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + 156L]);
						asm.mov(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + 152L]);
						asm.AddInstruction(span[0]);
						asm.mov(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 56L]);
						asm.mov(AssemblerRegisters.rdx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 64L]);
						asm.mov(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L]);
						asm.mov(AssemblerRegisters.r9, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 80L]);
						asm.add(AssemblerRegisters.rsp, 96);
						AssemblerExtensions.AddInstructions(asm, System.Span<Instruction>.op_Implicit(span));
					};
					<>c.<>9__7_48 = val34;
					obj31 = (object)val34;
				}
				tx.AddInline(obj30, val33, (InlineHookGenerator)obj31, 14, "c_game_ai_tribe_siege_behaviour_handler_hook2");
			}
		}
		if (Plugin.Instance.ImplementStrongestAttackPrioritySupport.Value)
		{
			_strongestRunningScoreAddress = NativeMemoryManager.Alloc(4, (MemoryProtection)3);
			DataScanner val35 = scanner.Scan("FF C3 48 83 C5 ? 49 81 C7", 0);
			if (val35.Found)
			{
				ulong lblSmthAddress = val35.CurrentAddress;
				tx.AddInline(c_game_ai_decide_target_priority_hook, HookTarget.op_Implicit("83 F8 ? 75 ? 41 8B 87"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0015: Unknown result type (might be due to invalid IL or missing references)
					//IL_001c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0026: Unknown result type (might be due to invalid IL or missing references)
					//IL_0031: Unknown result type (might be due to invalid IL or missing references)
					//IL_0036: Unknown result type (might be due to invalid IL or missing references)
					//IL_0041: Unknown result type (might be due to invalid IL or missing references)
					//IL_004b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0050: Unknown result type (might be due to invalid IL or missing references)
					//IL_0056: Unknown result type (might be due to invalid IL or missing references)
					//IL_005b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0060: Unknown result type (might be due to invalid IL or missing references)
					//IL_006b: Unknown result type (might be due to invalid IL or missing references)
					//IL_007f: Unknown result type (might be due to invalid IL or missing references)
					//IL_008a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0094: Unknown result type (might be due to invalid IL or missing references)
					//IL_009f: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
					//IL_00af: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00be: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
					//IL_0113: Unknown result type (might be due to invalid IL or missing references)
					//IL_0118: Unknown result type (might be due to invalid IL or missing references)
					//IL_0123: Unknown result type (might be due to invalid IL or missing references)
					//IL_012f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0134: Unknown result type (might be due to invalid IL or missing references)
					//IL_013f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0144: Unknown result type (might be due to invalid IL or missing references)
					//IL_014f: Unknown result type (might be due to invalid IL or missing references)
					//IL_015c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0161: Unknown result type (might be due to invalid IL or missing references)
					//IL_016c: Unknown result type (might be due to invalid IL or missing references)
					//IL_017d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0188: Unknown result type (might be due to invalid IL or missing references)
					//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
					//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
					//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
					//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
					//IL_01df: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
					//IL_0204: Unknown result type (might be due to invalid IL or missing references)
					//IL_0209: Unknown result type (might be due to invalid IL or missing references)
					//IL_0214: Unknown result type (might be due to invalid IL or missing references)
					//IL_021f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0224: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					asm.cmp(AssemblerRegisters.eax, 5);
					asm.jnz(val82);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + 7284L]);
					asm.lea(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rax * 4]);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[GameGlobalsManager.Instance.AxisDistanceMetricsVA]);
					asm.add(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + 13276L]);
					asm.lea(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.rax * 2]);
					asm.mov(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 152L]);
					asm.mov(AssemblerRegisters.eax, 1374389535);
					asm.imul(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15]);
					asm.sar(AssemblerRegisters.edx, (sbyte)5);
					asm.mov(AssemblerRegisters.eax, AssemblerRegisters.edx);
					asm.add(AssemblerRegisters.edx, AssemblerRegisters.r8d);
					asm.shr(AssemblerRegisters.eax, (sbyte)31);
					asm.add(AssemblerRegisters.eax, AssemblerRegisters.edx);
					asm.mov(AssemblerRegisters.rdx, currentImageBase);
					asm.push(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.rdx, (ulong)(long)_strongestRunningScoreAddress);
					asm.cmp(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdx]);
					asm.pop(AssemblerRegisters.rdx);
					asm.jl(lblSmthAddress);
					asm.push(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.rdx, (ulong)(long)_strongestRunningScoreAddress);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdx], AssemblerRegisters.eax);
					asm.pop(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.edi, AssemblerRegisters.ebx);
					asm.jmp(lblSmthAddress);
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				}, 14, "c_game_ai_decide_target_priority_hook");
				tx.AddInline(c_game_ai_decide_target_priority_hook2, HookTarget.op_Implicit("48 8D 15 ? ? ? ? 41 BD"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0008: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_002e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0033: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					AssemblerExtensions.AddInstructions(asm, overwritten);
					asm.push(AssemblerRegisters.rdx);
					asm.mov(AssemblerRegisters.rdx, (ulong)(long)_strongestRunningScoreAddress);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdx], 2147483648u);
					asm.pop(AssemblerRegisters.rdx);
				}, 14, "c_game_ai_decide_target_priority_hook2");
			}
		}
		if (Plugin.Instance.RelocateAIVOrderedMapTileIds.Value)
		{
			_newOrderedMapTileIdsAddress = NativeMemoryManager.Alloc(1440000, (MemoryProtection)3);
			Log.Information("_newOrderedMapTileIdsAddress: 0x" + _newOrderedMapTileIdsAddress.ToString("X16"), ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
			ModSaveDataAPI.Instance.RegisterModDataHandler("extendedaivbuffer-savedata", (Func<SaveContext, byte[]>)delegate(SaveContext val82)
			{
				if (!val82.IsSaveFile)
				{
					return (byte[]?)null;
				}
				ExtendedAIVBufferContainer extendedAIVBufferContainer = new ExtendedAIVBufferContainer
				{
					buffers = new Dictionary<int, ExtendedAIVBuffer>()
				};
				GameAIVManagerAPI instance = GameAIVManagerAPI.Instance;
				uint* ptr2 = (uint*)(void*)_newOrderedMapTileIdsAddress;
				int num3 = default(int);
				for (int i = 1; i <= 8; i++)
				{
					if (instance.TryGetVillageSlotByPlayerId(i, ref num3))
					{
						uint* num4 = ptr2 + (long)num3 * 40000L;
						ExtendedAIVBuffer extendedAIVBuffer = new ExtendedAIVBuffer
						{
							buffer = new uint[40000]
						};
						new System.ReadOnlySpan<uint>((void*)num4, 40000).CopyTo(System.Span<uint>.op_Implicit(extendedAIVBuffer.buffer));
						extendedAIVBufferContainer.buffers.Add(i, extendedAIVBuffer);
						Log.Information($"Saved extended aiv buffer for player {i} (slot {num3})", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
					}
				}
				return MessagePackSerializer.Serialize<ExtendedAIVBufferContainer>(extendedAIVBufferContainer, (MessagePackSerializerOptions)null, default(CancellationToken));
			}, (Action<byte[], LoadContext>)delegate(byte[] bytes, LoadContext val82)
			{
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				if (bytes != null && bytes.Length != 0)
				{
					ExtendedAIVBufferContainer extendedAIVBufferContainer = MessagePackSerializer.Deserialize<ExtendedAIVBufferContainer>(ReadOnlyMemory<byte>.op_Implicit(bytes), (MessagePackSerializerOptions)null, default(CancellationToken));
					if (extendedAIVBufferContainer?.buffers != null)
					{
						GameAIVManagerAPI instance = GameAIVManagerAPI.Instance;
						uint* ptr2 = (uint*)(void*)_newOrderedMapTileIdsAddress;
						int num3 = default(int);
						for (int i = 1; i <= 8; i++)
						{
							if (extendedAIVBufferContainer.buffers.TryGetValue(i, out ExtendedAIVBuffer value))
							{
								if (value.buffer == null || value.buffer.Length != 40000)
								{
									Log.Warning($"Saved buffer for player {i} has unexpected length; skipping.", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
								}
								else if (instance.TryGetVillageSlotByPlayerId(i, ref num3))
								{
									uint* ptr3 = ptr2 + (long)num3 * 40000L;
									MemoryExtensions.AsSpan<uint>(value.buffer).CopyTo(new System.Span<uint>((void*)ptr3, 40000));
									Log.Information($"Loaded extended aiv buffer for player {i} (slot {num3})", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
								}
							}
						}
					}
				}
			}, (Action)null);
			HookHandle<X64AssemblyPatch> obj32 = c_game_aiv_orderedmaptileids_hook6;
			HookTarget val36 = HookTarget.op_Implicit("4C 69 44 24");
			object obj33 = <>c.<>9__7_9;
			if (obj33 == null)
			{
				AssemblyPatchGenerator val37 = delegate(Assembler asm, ulong va)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					asm.imul(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 72L], 40000);
				};
				<>c.<>9__7_9 = val37;
				obj33 = (object)val37;
			}
			tx.AddAssemblyPatch(obj32, val36, (AssemblyPatchGenerator)obj33, 9, "c_game_aiv_orderedmaptileids_hook6");
			HookHandle<X64AssemblyPatch> obj34 = c_game_aiv_orderedmaptileids_hook7;
			HookTarget val38 = HookTarget.op_Implicit("48 69 D7 ? ? ? ? 4C 8D 25");
			object obj35 = <>c.<>9__7_10;
			if (obj35 == null)
			{
				AssemblyPatchGenerator val39 = delegate(Assembler asm, ulong va)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					asm.imul(AssemblerRegisters.rdx, AssemblerRegisters.rdi, 40000);
				};
				<>c.<>9__7_10 = val39;
				obj35 = (object)val39;
			}
			tx.AddAssemblyPatch(obj34, val38, (AssemblyPatchGenerator)obj35, 7, "c_game_aiv_orderedmaptileids_hook7");
			HookHandle<X64AssemblyPatch> obj36 = c_game_aiv_orderedmaptileids_hook8;
			HookTarget val40 = HookTarget.op_Implicit("4C 69 CF ? ? ? ? 4C 8D 25");
			object obj37 = <>c.<>9__7_11;
			if (obj37 == null)
			{
				AssemblyPatchGenerator val41 = delegate(Assembler asm, ulong va)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					asm.imul(AssemblerRegisters.r9, AssemblerRegisters.rdi, 40000);
				};
				<>c.<>9__7_11 = val41;
				obj37 = (object)val41;
			}
			tx.AddAssemblyPatch(obj36, val40, (AssemblyPatchGenerator)obj37, 7, "c_game_aiv_orderedmaptileids_hook8");
			HookHandle<X64AssemblyPatch> obj38 = c_game_aiv_orderedmaptileids_hook9;
			HookTarget val42 = HookTarget.op_Implicit("48 69 4C 24 ? ? ? ? ? 41 C6 44 87");
			object obj39 = <>c.<>9__7_12;
			if (obj39 == null)
			{
				AssemblyPatchGenerator val43 = delegate(Assembler asm, ulong va)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					asm.imul(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 32L], 40000);
				};
				<>c.<>9__7_12 = val43;
				obj39 = (object)val43;
			}
			tx.AddAssemblyPatch(obj38, val42, (AssemblyPatchGenerator)obj39, 9, "c_game_aiv_orderedmaptileids_hook9");
			HookHandle<X64AssemblyPatch> obj40 = c_game_aiv_orderedmaptileids_hook10;
			HookTarget val44 = HookTarget.op_Implicit("48 69 4C 24 ? ? ? ? ? 48 03 C8");
			object obj41 = <>c.<>9__7_13;
			if (obj41 == null)
			{
				AssemblyPatchGenerator val45 = delegate(Assembler asm, ulong va)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					asm.imul(AssemblerRegisters.rcx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__qword_ptr))[AssemblerRegisters.rsp + 32L], 40000);
				};
				<>c.<>9__7_13 = val45;
				obj41 = (object)val45;
			}
			tx.AddAssemblyPatch(obj40, val44, (AssemblyPatchGenerator)obj41, 9, "c_game_aiv_orderedmaptileids_hook10");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook1, HookTarget.op_Implicit("49 63 B4 8D ? ? ? ? C7 05"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.r13);
				asm.mov(AssemblerRegisters.r13, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.movsxd(AssemblerRegisters.rsi, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r13 + AssemblerRegisters.rcx * 4]);
				asm.pop(AssemblerRegisters.r13);
				AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
			}, 14, "c_game_aiv_orderedmaptileids_hook1");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook2, HookTarget.op_Implicit("49 63 B4 8D ? ? ? ? 49 0F BF BC 74"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.r13);
				asm.mov(AssemblerRegisters.r13, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.movsxd(AssemblerRegisters.rsi, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r13 + AssemblerRegisters.rcx * 4]);
				asm.pop(AssemblerRegisters.r13);
				AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
			}, 14, "c_game_aiv_orderedmaptileids_hook2");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook3, HookTarget.op_Implicit("4D 63 84 8D"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.r13);
				asm.mov(AssemblerRegisters.r13, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.movsxd(AssemblerRegisters.r8, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r13 + AssemblerRegisters.rcx * 4]);
				asm.pop(AssemblerRegisters.r13);
				AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
			}, 14, "c_game_aiv_orderedmaptileids_hook3");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook4, HookTarget.op_Implicit("45 89 84 8F ? ? ? ? 41 FF 83 ? ? ? ? 45 3B 73 ? 7E ? 45 89 73 ? 41 C6 42"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.r15);
				asm.mov(AssemblerRegisters.r15, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rcx * 4], AssemblerRegisters.r8d);
				asm.pop(AssemblerRegisters.r15);
				AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
			}, 14, "c_game_aiv_orderedmaptileids_hook4");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook5, HookTarget.op_Implicit("45 89 84 8F ? ? ? ? 41 FF 83 ? ? ? ? 45 3B 73 ? 7E ? 45 89 73 ? ? ? ? ? 4C 8B 44 24"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.r15);
				asm.mov(AssemblerRegisters.r15, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rcx * 4], AssemblerRegisters.r8d);
				asm.pop(AssemblerRegisters.r15);
				AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
			}, 14, "c_game_aiv_orderedmaptileids_hook5");
			tx.AddInline(c_game_aiv_orderedmaptileids_hook11, HookTarget.op_Implicit("48 81 C3 ? ? ? ? 48 83 EF ? 75 ? 48 8B CE"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0038: Unknown result type (might be due to invalid IL or missing references)
				//IL_0044: Unknown result type (might be due to invalid IL or missing references)
				//IL_0060: Unknown result type (might be due to invalid IL or missing references)
				//IL_006b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0076: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.rdi);
				asm.push(AssemblerRegisters.rcx);
				asm.push(AssemblerRegisters.rax);
				asm.mov(AssemblerRegisters.rdi, (ulong)(long)_newOrderedMapTileIdsAddress);
				asm.mov(AssemblerRegisters.eax, 0);
				asm.mov(AssemblerRegisters.rcx, 180000uL);
				asm.rep.stosq();
				asm.pop(AssemblerRegisters.rax);
				asm.pop(AssemblerRegisters.rcx);
				asm.pop(AssemblerRegisters.rdi);
				AssemblerExtensions.AddInstructions(asm, overwritten);
			}, 14, "c_game_aiv_orderedmaptileids_hook11");
		}
		if (Plugin.Instance.EnableWheatSaleCategoryFix.Value)
		{
			wheatSaleCategoryFlagArray = nativeStateBlock.AllocateFlags(9, false);
			HookHandle<X64InlineHook> obj42 = c_game_ai_sell_item_handler_hook;
			HookTarget val46 = HookTarget.op_Implicit("45 8B 9A");
			object obj43 = <>c.<>9__7_20;
			if (obj43 == null)
			{
				InlineHookGenerator val47 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					//IL_003d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					//IL_004a: Unknown result type (might be due to invalid IL or missing references)
					//IL_004f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0054: Unknown result type (might be due to invalid IL or missing references)
					//IL_0060: Unknown result type (might be due to invalid IL or missing references)
					//IL_0067: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0086: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r8);
					asm.cmp(AssemblerRegisters.rdi, 9);
					asm.jne(val82);
					AssemblerExtensions.TestNativeFlag(asm, wheatSaleCategoryFlagArray, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsp + 80L], AssemblerRegisters.edx, AssemblerRegisters.r8, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.ecx, 6);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.r8);
					asm.pop(AssemblerRegisters.rdx);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_20 = val47;
				obj43 = (object)val47;
			}
			tx.AddInline(obj42, val46, (InlineHookGenerator)obj43, 14, "c_game_ai_sell_item_handler_hook");
		}
		if (Plugin.Instance.AllowCustomMinGoldForHarassmentSiegeEngines.Value)
		{
			minGoldForHarassmentSiegeEnginesFlagArray = nativeStateBlock.AllocateFlags(9, false);
			minGoldForHarassmentSiegeEnginesCostsArray = nativeStateBlock.AllocateArray<short>(9, (short)500);
			HookHandle<X64InlineHook> obj44 = c_game_ai_deploy_harassing_siege_engine_tents_hook2;
			HookTarget val48 = HookTarget.op_Implicit("0F 8C ? ? ? ? 4C 8D 50");
			object obj45 = <>c.<>9__7_21;
			if (obj45 == null)
			{
				InlineHookGenerator val49 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0038: Unknown result type (might be due to invalid IL or missing references)
					//IL_003f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0054: Unknown result type (might be due to invalid IL or missing references)
					//IL_005e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_0069: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0073: Unknown result type (might be due to invalid IL or missing references)
					//IL_0083: Unknown result type (might be due to invalid IL or missing references)
					//IL_0088: Unknown result type (might be due to invalid IL or missing references)
					//IL_008d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0098: Unknown result type (might be due to invalid IL or missing references)
					//IL_009d: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00be: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
					//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.push(AssemblerRegisters.r8);
					asm.pushfq();
					AssemblerExtensions.TestNativeFlag(asm, minGoldForHarassmentSiegeEnginesFlagArray, AssemblerRegisters.edx, AssemblerRegisters.r8, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.r8, minGoldForHarassmentSiegeEnginesCostsArray.Address);
					asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rdx * 2]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rbx + AssemblerRegisters.r13 + 58307292L], AssemblerRegisters.r8d);
					asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
					asm.jmp(val83);
					asm.Label(ref val82);
					asm.popfq();
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.r8);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_21 = val49;
				obj45 = (object)val49;
			}
			tx.AddInline(obj44, val48, (InlineHookGenerator)obj45, 14, "c_game_ai_deploy_harassing_siege_engine_tents_hook2");
		}
		if (Plugin.Instance.EnableStair6SlowdownFix.Value)
		{
			HookHandle<X64InlineHook> obj46 = c_game_ai_build_wall_hook;
			HookTarget val50 = HookTarget.op_Implicit("8B 84 97 ? ? ? ? 25");
			object obj47 = <>c.<>9__7_52;
			if (obj47 == null)
			{
				InlineHookGenerator val51 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0019: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0029: Unknown result type (might be due to invalid IL or missing references)
					//IL_0035: Unknown result type (might be due to invalid IL or missing references)
					//IL_003a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0040: Unknown result type (might be due to invalid IL or missing references)
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					//IL_0050: Unknown result type (might be due to invalid IL or missing references)
					//IL_0055: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					asm.mov(AssemblerRegisters.eax, 186);
					asm.cmp(AssemblerRegisters.bx, AssemblerRegisters.ax);
					asm.jnz(val82);
					asm.or(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rdi + AssemblerRegisters.rdx * 4 + 9012224L], 2048);
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_52 = val51;
				obj47 = (object)val51;
			}
			tx.AddInline(obj46, val50, (InlineHookGenerator)obj47, 14, "c_game_ai_build_wall_hook");
			DataScanner val52 = scanner.Scan("73 ? 80 BC 0F", 0);
			if (val52.Found)
			{
				ulong c_game_ai_build_wall_internal_va = val52.CurrentAddress;
				Log.Information("c_game_ai_build_wall_internal_va=" + c_game_ai_build_wall_internal_va.ToString("X16"), ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
				tx.AddInline(c_game_ai_build_wall_internal_hook, HookTarget.op_Implicit(c_game_ai_build_wall_internal_va), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_002b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0032: Unknown result type (might be due to invalid IL or missing references)
					//IL_0042: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0053: Unknown result type (might be due to invalid IL or missing references)
					//IL_0058: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_0068: Unknown result type (might be due to invalid IL or missing references)
					//IL_0075: Unknown result type (might be due to invalid IL or missing references)
					//IL_0097: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.rsp + 112L], 186);
					asm.jne(val82);
					asm.test(AssemblerRegisters.r8d, 2048);
					asm.jz(val82);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__byte_ptr))[AssemblerRegisters.rdi + AssemblerRegisters.rcx + 16394624L], 12);
					asm.jge(val82);
					asm.jmp(c_game_ai_build_wall_internal_va + 259);
					asm.Label(ref val82);
					asm.bt(AssemblerRegisters.r8d, (sbyte)8);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				}, 14, "c_game_ai_build_wall_internal_hook");
			}
		}
		if (Plugin.Instance.AllowCustomStoneToOxenRatio.Value)
		{
			stoneToOxenRatioFlagArray = nativeStateBlock.AllocateFlags(9, false);
			stoneToOxenRatioArray = nativeStateBlock.AllocateArray<short>(9, (short)20);
			HookHandle<X64InlineHook> obj48 = c_game_ai_need_more_ox_bases_hook;
			HookTarget val53 = HookTarget.op_Implicit("E8 ? ? ? ? 8B D8 44 8B FF");
			object obj49 = <>c.<>9__7_22;
			if (obj49 == null)
			{
				InlineHookGenerator val54 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_0019: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0020: Unknown result type (might be due to invalid IL or missing references)
					//IL_0036: Unknown result type (might be due to invalid IL or missing references)
					//IL_003b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0047: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_006d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0072: Unknown result type (might be due to invalid IL or missing references)
					//IL_0078: Unknown result type (might be due to invalid IL or missing references)
					//IL_007d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0082: Unknown result type (might be due to invalid IL or missing references)
					//IL_008d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0092: Unknown result type (might be due to invalid IL or missing references)
					//IL_009d: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00be: Unknown result type (might be due to invalid IL or missing references)
					//IL_00db: Unknown result type (might be due to invalid IL or missing references)
					AssemblerExtensions.AddInstructions(asm, overwritten.Slice(0, 6));
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.push(AssemblerRegisters.r8);
					asm.pushfq();
					AssemblerExtensions.TestNativeFlag(asm, stoneToOxenRatioFlagArray, AssemblerRegisters.ebp, AssemblerRegisters.r8, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.r8, stoneToOxenRatioArray.Address);
					asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rbp * 2]);
					asm.cmp(AssemblerRegisters.edi, AssemblerRegisters.r8d);
					asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
					asm.jmp(val83);
					asm.Label(ref val82);
					asm.popfq();
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.r8);
				};
				<>c.<>9__7_22 = val54;
				obj49 = (object)val54;
			}
			tx.AddInline(obj48, val53, (InlineHookGenerator)obj49, 21, "c_game_ai_need_more_ox_bases_hook");
		}
		if (Plugin.Instance.AllowCustomMaxOxenAmount.Value)
		{
			maxOxenFlagArray = nativeStateBlock.AllocateFlags(9, false);
			maxOxenArray = nativeStateBlock.AllocateArray<short>(9, (short)10);
			HookHandle<X64InlineHook> obj50 = c_game_ai_need_more_ox_bases_hook2;
			HookTarget val55 = HookTarget.op_Implicit("0F 8D ? ? ? ? 48 69 F5");
			object obj51 = <>c.<>9__7_23;
			if (obj51 == null)
			{
				InlineHookGenerator val56 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0038: Unknown result type (might be due to invalid IL or missing references)
					//IL_003f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0054: Unknown result type (might be due to invalid IL or missing references)
					//IL_005e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_0069: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0073: Unknown result type (might be due to invalid IL or missing references)
					//IL_007e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0083: Unknown result type (might be due to invalid IL or missing references)
					//IL_008e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0098: Unknown result type (might be due to invalid IL or missing references)
					//IL_009f: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
					//IL_00af: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.push(AssemblerRegisters.r8);
					asm.pushfq();
					AssemblerExtensions.TestNativeFlag(asm, maxOxenFlagArray, AssemblerRegisters.ebp, AssemblerRegisters.r8, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.r8, maxOxenArray.Address);
					asm.movsx(AssemblerRegisters.r8d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.rbp * 2]);
					asm.cmp(AssemblerRegisters.eax, AssemblerRegisters.r8d);
					asm.lea(AssemblerRegisters.rsp, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__))[AssemblerRegisters.rsp + 8L]);
					asm.jmp(val83);
					asm.Label(ref val82);
					asm.popfq();
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.r8);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_23 = val56;
				obj51 = (object)val56;
			}
			tx.AddInline(obj50, val55, (InlineHookGenerator)obj51, 14, "c_game_ai_need_more_ox_bases_hook2");
		}
		if (Plugin.Instance.AllowCustomAIPurchasingRoutines.Value)
		{
			buyWoodAtFlagArray = nativeStateBlock.AllocateFlags(9, false);
			buyWoodAtArray = nativeStateBlock.AllocateArray<int>(9, 5);
			buyWoodAmountArray = nativeStateBlock.AllocateArray<int>(9, 20);
			buyIronAtFlagArray = nativeStateBlock.AllocateFlags(9, false);
			buyIronAtArray = nativeStateBlock.AllocateArray<int>(9, 5);
			buyIronAmountArray = nativeStateBlock.AllocateArray<int>(9, 20);
			buyAleAtFlagArray = nativeStateBlock.AllocateFlags(9, false);
			buyAleAtArray = nativeStateBlock.AllocateArray<int>(9, 5);
			buyAleAmountArray = nativeStateBlock.AllocateArray<int>(9, 20);
			HookHandle<X64InlineHook> obj52 = c_game_ai_food_buy_handler_hook;
			HookTarget val57 = HookTarget.op_Implicit("8B 84 0A ? ? ? ? 85 C0 7E ? 43 39 84 08 ? ? ? ? 7D ? 8B 84 0A ? ? ? ? 43 89 84 08 ? ? ? ? C3");
			object obj53 = <>c.<>9__7_24;
			if (obj53 == null)
			{
				InlineHookGenerator val58 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0012: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_0019: Unknown result type (might be due to invalid IL or missing references)
					//IL_0024: Unknown result type (might be due to invalid IL or missing references)
					//IL_002f: Unknown result type (might be due to invalid IL or missing references)
					//IL_003a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Unknown result type (might be due to invalid IL or missing references)
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					//IL_0076: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_008e: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
					//IL_0103: Unknown result type (might be due to invalid IL or missing references)
					//IL_0118: Unknown result type (might be due to invalid IL or missing references)
					//IL_0122: Unknown result type (might be due to invalid IL or missing references)
					//IL_0127: Unknown result type (might be due to invalid IL or missing references)
					//IL_012d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0132: Unknown result type (might be due to invalid IL or missing references)
					//IL_0137: Unknown result type (might be due to invalid IL or missing references)
					//IL_0147: Unknown result type (might be due to invalid IL or missing references)
					//IL_014c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0151: Unknown result type (might be due to invalid IL or missing references)
					//IL_015c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0161: Unknown result type (might be due to invalid IL or missing references)
					//IL_0166: Unknown result type (might be due to invalid IL or missing references)
					//IL_017e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0183: Unknown result type (might be due to invalid IL or missing references)
					//IL_018f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0196: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
					//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
					//IL_01da: Unknown result type (might be due to invalid IL or missing references)
					//IL_01df: Unknown result type (might be due to invalid IL or missing references)
					//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
					//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
					//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
					//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
					//IL_0204: Unknown result type (might be due to invalid IL or missing references)
					//IL_020b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0220: Unknown result type (might be due to invalid IL or missing references)
					//IL_022a: Unknown result type (might be due to invalid IL or missing references)
					//IL_022f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0235: Unknown result type (might be due to invalid IL or missing references)
					//IL_023a: Unknown result type (might be due to invalid IL or missing references)
					//IL_023f: Unknown result type (might be due to invalid IL or missing references)
					//IL_024f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0254: Unknown result type (might be due to invalid IL or missing references)
					//IL_0259: Unknown result type (might be due to invalid IL or missing references)
					//IL_0264: Unknown result type (might be due to invalid IL or missing references)
					//IL_0269: Unknown result type (might be due to invalid IL or missing references)
					//IL_026e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0286: Unknown result type (might be due to invalid IL or missing references)
					//IL_028b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0297: Unknown result type (might be due to invalid IL or missing references)
					//IL_029e: Unknown result type (might be due to invalid IL or missing references)
					//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
					//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
					//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
					//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
					//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
					//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
					//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
					//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
					//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
					//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
					//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
					//IL_0301: Unknown result type (might be due to invalid IL or missing references)
					//IL_030c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0313: Unknown result type (might be due to invalid IL or missing references)
					//IL_0328: Unknown result type (might be due to invalid IL or missing references)
					//IL_0332: Unknown result type (might be due to invalid IL or missing references)
					//IL_0337: Unknown result type (might be due to invalid IL or missing references)
					//IL_033d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0342: Unknown result type (might be due to invalid IL or missing references)
					//IL_0347: Unknown result type (might be due to invalid IL or missing references)
					//IL_0357: Unknown result type (might be due to invalid IL or missing references)
					//IL_035c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0361: Unknown result type (might be due to invalid IL or missing references)
					//IL_036c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0371: Unknown result type (might be due to invalid IL or missing references)
					//IL_0376: Unknown result type (might be due to invalid IL or missing references)
					//IL_0389: Unknown result type (might be due to invalid IL or missing references)
					//IL_0394: Unknown result type (might be due to invalid IL or missing references)
					//IL_039f: Unknown result type (might be due to invalid IL or missing references)
					//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					Label val84 = asm.CreateLabel((string)null);
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rdx);
					asm.push(AssemblerRegisters.r10);
					asm.push(AssemblerRegisters.r11);
					asm.mov(AssemblerRegisters.rax, 6690088872491085835L);
					asm.mul(AssemblerRegisters.r8);
					asm.shr(AssemblerRegisters.rdx, (sbyte)13);
					AssemblerExtensions.TestNativeFlag(asm, buyWoodAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.r10, buyWoodAtArray.Address);
					asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241240L], AssemblerRegisters.r11d);
					asm.jge(val82);
					asm.mov(AssemblerRegisters.r10, buyWoodAmountArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250872L], AssemblerRegisters.eax);
					asm.Label(ref val82);
					AssemblerExtensions.TestNativeFlag(asm, buyIronAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
					asm.jz(val83);
					asm.mov(AssemblerRegisters.r10, buyIronAtArray.Address);
					asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241256L], AssemblerRegisters.r11d);
					asm.jge(val83);
					asm.mov(AssemblerRegisters.r10, buyIronAmountArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250888L], AssemblerRegisters.eax);
					asm.Label(ref val83);
					AssemblerExtensions.TestNativeFlag(asm, buyAleAtFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.r10, true);
					asm.jz(val84);
					asm.mov(AssemblerRegisters.r10, buyAleAtArray.Address);
					asm.mov(AssemblerRegisters.r11d, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1241288L], AssemblerRegisters.r11d);
					asm.jge(val84);
					asm.mov(AssemblerRegisters.r10, buyAleAmountArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r10 + AssemblerRegisters.rdx * 4]);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r9 + 1250920L], AssemblerRegisters.eax);
					asm.Label(ref val84);
					asm.pop(AssemblerRegisters.r11);
					asm.pop(AssemblerRegisters.r10);
					asm.pop(AssemblerRegisters.rdx);
					asm.pop(AssemblerRegisters.rax);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_24 = val58;
				obj53 = (object)val58;
			}
			tx.AddInline(obj52, val57, (InlineHookGenerator)obj53, 14, "c_game_ai_food_buy_handler_hook");
		}
		if (Plugin.Instance.AllowCustomGoodBadRequirements.Value)
		{
			customGoodThingRequiredAttemptsFlagArray = nativeStateBlock.AllocateFlags(9, false);
			customGoodThingRequiredAttemptsArray = nativeStateBlock.AllocateArray<int>(9, 200);
			customBadThingLogicFlagArray = nativeStateBlock.AllocateFlags(9, false);
			customBadThingRequiredAttemptsArray = nativeStateBlock.AllocateArray<int>(9, 100);
			customBadThingRequiredMinPopularityArray = nativeStateBlock.AllocateArray<int>(9, 5000);
			HookHandle<X64InlineHook> obj54 = c_game_ai_should_defer_good_thing_hook;
			HookTarget val59 = HookTarget.op_Implicit("42 81 BC 01 ? ? ? ? ? ? ? ? 7D");
			object obj55 = <>c.<>9__7_25;
			if (obj55 == null)
			{
				InlineHookGenerator val60 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0052: Unknown result type (might be due to invalid IL or missing references)
					//IL_0058: Unknown result type (might be due to invalid IL or missing references)
					//IL_005d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0062: Unknown result type (might be due to invalid IL or missing references)
					//IL_0072: Unknown result type (might be due to invalid IL or missing references)
					//IL_0077: Unknown result type (might be due to invalid IL or missing references)
					//IL_007c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_008c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0091: Unknown result type (might be due to invalid IL or missing references)
					//IL_009c: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					AssemblerExtensions.TestNativeFlag(asm, customGoodThingRequiredAttemptsFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.rax, customGoodThingRequiredAttemptsArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317084L], AssemblerRegisters.eax);
					asm.jmp(val83);
					asm.Label(ref val82);
					asm.AddInstruction(System.Runtime.CompilerServices.Unsafe.Read<Instruction>((void*)overwritten[0]));
					asm.Label(ref val83);
					AssemblerExtensions.AddInstructions(asm, overwritten.Slice(1));
				};
				<>c.<>9__7_25 = val60;
				obj55 = (object)val60;
			}
			tx.AddInline(obj54, val59, (InlineHookGenerator)obj55, 14, "c_game_ai_should_defer_good_thing_hook");
			HookHandle<X64InlineHook> obj56 = c_game_ai_should_defer_bad_thing_hook;
			HookTarget val61 = HookTarget.op_Implicit("42 81 BC 01 ? ? ? ? ? ? ? ? 7C");
			object obj57 = <>c.<>9__7_26;
			if (obj57 == null)
			{
				InlineHookGenerator val62 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0052: Unknown result type (might be due to invalid IL or missing references)
					//IL_0058: Unknown result type (might be due to invalid IL or missing references)
					//IL_005d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0062: Unknown result type (might be due to invalid IL or missing references)
					//IL_0072: Unknown result type (might be due to invalid IL or missing references)
					//IL_0077: Unknown result type (might be due to invalid IL or missing references)
					//IL_007c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_008c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0091: Unknown result type (might be due to invalid IL or missing references)
					//IL_009c: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
					//IL_0101: Unknown result type (might be due to invalid IL or missing references)
					//IL_0111: Unknown result type (might be due to invalid IL or missing references)
					//IL_0116: Unknown result type (might be due to invalid IL or missing references)
					//IL_011b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0126: Unknown result type (might be due to invalid IL or missing references)
					//IL_012b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0130: Unknown result type (might be due to invalid IL or missing references)
					//IL_013b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0147: Unknown result type (might be due to invalid IL or missing references)
					//IL_014c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0151: Unknown result type (might be due to invalid IL or missing references)
					//IL_015c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0161: Unknown result type (might be due to invalid IL or missing references)
					//IL_016d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0172: Unknown result type (might be due to invalid IL or missing references)
					//IL_018b: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					AssemblerExtensions.TestNativeFlag(asm, customBadThingLogicFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.rax, customBadThingRequiredMinPopularityArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58306096L], AssemblerRegisters.eax);
					asm.jl(val83);
					asm.inc(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L]);
					asm.mov(AssemblerRegisters.rax, customBadThingRequiredAttemptsArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rdx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L], AssemblerRegisters.eax);
					asm.jl(val83);
					asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx + AssemblerRegisters.r8 + 58317080L], 0);
					asm.xor(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.ret();
					asm.Label(ref val83);
					asm.mov(AssemblerRegisters.eax, 1);
					asm.ret();
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_26 = val62;
				obj57 = (object)val62;
			}
			tx.AddInline(obj56, val61, (InlineHookGenerator)obj57, 14, "c_game_ai_should_defer_bad_thing_hook");
		}
		if (Plugin.Instance.SmarterSiegeLaddermen.Value)
		{
			_ladderSelect = SelectLadderObjective;
			_ladderAttach = SelectLadderAttachment;
			ulong selectAddress = (ulong)(long)Marshal.GetFunctionPointerForDelegate(_ladderSelect);
			ulong attachAddress = (ulong)(long)Marshal.GetFunctionPointerForDelegate(_ladderAttach);
			tx.AddInline(c_game_ai_ladder_objective_rank_hook, HookTarget.op_Implicit("89 54 24 ? 56 57 41 54 41 57"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0010: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0047: Unknown result type (might be due to invalid IL or missing references)
				//IL_0067: Unknown result type (might be due to invalid IL or missing references)
				//IL_0072: Unknown result type (might be due to invalid IL or missing references)
				//IL_007d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0088: Unknown result type (might be due to invalid IL or missing references)
				//IL_0093: Unknown result type (might be due to invalid IL or missing references)
				//IL_009e: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
				Label val82 = asm.CreateLabel((string)null);
				AssemblerExtensions.AddInstructions(asm, overwritten);
				asm.push(AssemblerRegisters.rcx);
				asm.push(AssemblerRegisters.rdx);
				asm.push(AssemblerRegisters.r8);
				asm.push(AssemblerRegisters.r9);
				asm.push(AssemblerRegisters.r10);
				asm.push(AssemblerRegisters.r11);
				AssemblerExtensions.X64FastcallSafe(asm, selectAddress, 2, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
				asm.pop(AssemblerRegisters.r11);
				asm.pop(AssemblerRegisters.r10);
				asm.pop(AssemblerRegisters.r9);
				asm.pop(AssemblerRegisters.r8);
				asm.pop(AssemblerRegisters.rdx);
				asm.pop(AssemblerRegisters.rcx);
				asm.cmp(AssemblerRegisters.eax, -1);
				asm.je(val82);
				AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress + 352);
				asm.Label(ref val82);
				asm.nop();
			}, 14, "c_game_ai_ladder_objective_rank_hook");
			tx.AddInline(c_game_ai_ladder_attachment_rank_hook, HookTarget.op_Implicit("? ? ? ? ? ? ? 48 63 D1 ? ? ? ? 41 03 C9 48 63 C9"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_002d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0038: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_0048: Unknown result type (might be due to invalid IL or missing references)
				//IL_0053: Unknown result type (might be due to invalid IL or missing references)
				//IL_0058: Unknown result type (might be due to invalid IL or missing references)
				//IL_0078: Unknown result type (might be due to invalid IL or missing references)
				//IL_007d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0088: Unknown result type (might be due to invalid IL or missing references)
				//IL_0093: Unknown result type (might be due to invalid IL or missing references)
				//IL_009e: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
				//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00da: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
				asm.push(AssemblerRegisters.rax);
				asm.push(AssemblerRegisters.rcx);
				asm.push(AssemblerRegisters.rdx);
				asm.push(AssemblerRegisters.r9);
				asm.push(AssemblerRegisters.r10);
				asm.push(AssemblerRegisters.r11);
				asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.r9d);
				asm.mov(AssemblerRegisters.edx, AssemblerRegisters.r11d);
				AssemblerExtensions.X64FastcallSafe(asm, attachAddress, 2, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
				asm.mov(AssemblerRegisters.r8d, AssemblerRegisters.eax);
				asm.pop(AssemblerRegisters.r11);
				asm.pop(AssemblerRegisters.r10);
				asm.pop(AssemblerRegisters.r9);
				asm.pop(AssemblerRegisters.rdx);
				asm.pop(AssemblerRegisters.rcx);
				asm.pop(AssemblerRegisters.rax);
				Label val82 = asm.CreateLabel((string)null);
				Label val83 = asm.CreateLabel((string)null);
				asm.cmp(AssemblerRegisters.r8d, -1);
				asm.je(val82);
				asm.cmp(AssemblerRegisters.r8d, 15);
				asm.je(val83);
				AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress + 48);
				asm.Label(ref val83);
				AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress + 37);
				asm.Label(ref val82);
				AssemblerExtensions.AddInstructions(asm, overwritten);
			}, 14, "c_game_ai_ladder_attachment_rank_hook");
		}
		if (Plugin.Instance.AllowCustomSiegeLifetime.Value)
		{
			customSiegeLifetimeFlagArray = nativeStateBlock.AllocateFlags(9, false);
			customSiegeLifetimeArray = nativeStateBlock.AllocateArray<int>(9, 200);
			HookHandle<X64InlineHook> obj58 = c_game_ai_siege_processing_handler_hook;
			HookTarget val63 = HookTarget.op_Implicit("74 ? ? ? ? ? 75 ? 46 39 AC 26");
			object obj59 = <>c.<>9__7_27;
			if (obj59 == null)
			{
				InlineHookGenerator val64 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0012: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_001a: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0036: Unknown result type (might be due to invalid IL or missing references)
					//IL_003b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0048: Unknown result type (might be due to invalid IL or missing references)
					//IL_005c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0061: Unknown result type (might be due to invalid IL or missing references)
					//IL_0066: Unknown result type (might be due to invalid IL or missing references)
					//IL_0071: Unknown result type (might be due to invalid IL or missing references)
					//IL_0076: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0086: Unknown result type (might be due to invalid IL or missing references)
					//IL_0092: Unknown result type (might be due to invalid IL or missing references)
					//IL_0097: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
					//IL_0103: Unknown result type (might be due to invalid IL or missing references)
					//IL_0113: Unknown result type (might be due to invalid IL or missing references)
					//IL_0118: Unknown result type (might be due to invalid IL or missing references)
					//IL_011d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0128: Unknown result type (might be due to invalid IL or missing references)
					//IL_012d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0132: Unknown result type (might be due to invalid IL or missing references)
					//IL_013d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0152: Unknown result type (might be due to invalid IL or missing references)
					//IL_0157: Unknown result type (might be due to invalid IL or missing references)
					//IL_015c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0167: Unknown result type (might be due to invalid IL or missing references)
					//IL_016c: Unknown result type (might be due to invalid IL or missing references)
					//IL_017c: Unknown result type (might be due to invalid IL or missing references)
					//IL_018c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0191: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					Label val84 = asm.CreateLabel((string)null);
					Label val85 = asm.CreateLabel((string)null);
					Label val86 = asm.CreateLabel((string)null);
					asm.jz(val82);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r14], 9);
					asm.jnz(val85);
					asm.Label(ref val82);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254576L], AssemblerRegisters.r13d);
					asm.jle(val83);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r14], 5);
					asm.jle(val85);
					asm.Label(ref val83);
					AssemblerExtensions.TestNativeFlag(asm, customSiegeLifetimeFlagArray, AssemblerRegisters.rbx, AssemblerRegisters.rax, true);
					asm.jz(val84);
					asm.mov(AssemblerRegisters.rax, customSiegeLifetimeArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rbx * 4]);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254704L], AssemblerRegisters.eax);
					asm.jmp(val86);
					asm.Label(ref val84);
					asm.cmp(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rsi + AssemblerRegisters.r12 + 1254704L], 200);
					asm.jmp(val86);
					asm.Label(ref val85);
					asm.cmp(AssemblerRegisters.r13d, AssemblerRegisters.r13d);
					asm.Label(ref val86);
				};
				<>c.<>9__7_27 = val64;
				obj59 = (object)val64;
			}
			tx.AddInline(obj58, val63, (InlineHookGenerator)obj59, 36, "c_game_ai_siege_processing_handler_hook");
		}
		if (Plugin.Instance.AllowCustomAIControl.Value)
		{
			disableSiegeAIFlagArray = nativeStateBlock.AllocateFlags(9, false);
			disableGeneralAIFlagArray = nativeStateBlock.AllocateFlags(9, false);
			HookHandle<X64InlineHook> obj60 = c_game_ai_siege_processing_handler_hook2;
			HookTarget val65 = HookTarget.op_Implicit("45 85 FF 0F 84 ? ? ? ? 42 83 BC 26");
			object obj61 = <>c.<>9__7_28;
			if (obj61 == null)
			{
				InlineHookGenerator val66 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					AssemblerExtensions.TestNativeFlag(asm, disableSiegeAIFlagArray, AssemblerRegisters.rdx, AssemblerRegisters.rax, true);
					asm.jz(val82);
					AssemblerExtensions.AddUnrestrictedJmp(asm, ((Instruction)overwritten[1]).NearBranch64);
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_28 = val66;
				obj61 = (object)val66;
			}
			tx.AddInline(obj60, val65, (InlineHookGenerator)obj61, 14, "c_game_ai_siege_processing_handler_hook2");
			HookHandle<X64InlineHook> obj62 = c_game_ai_update_all_players_hook;
			HookTarget val67 = HookTarget.op_Implicit("41 83 7F ? ? 75 ? ? ? ? 00 74 ? 83 7E ? 00");
			object obj63 = <>c.<>9__7_29;
			if (obj63 == null)
			{
				InlineHookGenerator val68 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					AssemblerExtensions.TestNativeFlag(asm, disableGeneralAIFlagArray, AssemblerRegisters.rdi, AssemblerRegisters.rax, true);
					asm.jz(val82);
					AssemblerExtensions.AddUnrestrictedJmp(asm, ((Instruction)overwritten[1]).NearBranch64);
					asm.Label(ref val82);
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_29 = val68;
				obj63 = (object)val68;
			}
			tx.AddInline(obj62, val67, (InlineHookGenerator)obj63, 14, "c_game_ai_update_all_players_hook");
		}
		if (Plugin.Instance.SmarterSiegeAssassins.Value)
		{
			smarterSiegeAssassinsFlagArray = nativeStateBlock.AllocateFlags(9, false);
			smarterSiegeAssassinsOverrideFlagArray = nativeStateBlock.AllocateFlags(9, false);
			smarterSiegeAssassinsFlagArray.Fill(true);
			HookHandle<X64InlineHook> obj64 = c_game_pathfinding_find_nearest_enemy_gatehouse_hook;
			HookTarget val69 = HookTarget.op_Implicit("43 0F B7 84 38 ? ? ? ? 66 83 E8");
			object obj65 = <>c.<>9__7_30;
			if (obj65 == null)
			{
				InlineHookGenerator val70 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0010: Unknown result type (might be due to invalid IL or missing references)
					//IL_0015: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_001d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_0040: Unknown result type (might be due to invalid IL or missing references)
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					//IL_0051: Unknown result type (might be due to invalid IL or missing references)
					//IL_0058: Unknown result type (might be due to invalid IL or missing references)
					//IL_0062: Unknown result type (might be due to invalid IL or missing references)
					//IL_0067: Unknown result type (might be due to invalid IL or missing references)
					//IL_006c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0077: Unknown result type (might be due to invalid IL or missing references)
					//IL_007c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0087: Unknown result type (might be due to invalid IL or missing references)
					//IL_008c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0097: Unknown result type (might be due to invalid IL or missing references)
					//IL_009e: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
					//IL_0108: Unknown result type (might be due to invalid IL or missing references)
					//IL_0117: Unknown result type (might be due to invalid IL or missing references)
					//IL_0122: Unknown result type (might be due to invalid IL or missing references)
					//IL_0149: Unknown result type (might be due to invalid IL or missing references)
					//IL_0154: Unknown result type (might be due to invalid IL or missing references)
					ulong nearBranch = ((Instruction)overwritten[3]).NearBranch64;
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.pushfq();
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rcx);
					AssemblerExtensions.TestNativeFlag(asm, smarterSiegeAssassinsFlagArray, AssemblerRegisters.rbp, AssemblerRegisters.rax, true);
					asm.jz(val82);
					asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r15 + 105696978L]);
					asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax * 4 + 58646332L]);
					asm.cmp(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rbp * 4 + 58646332L]);
					asm.je(val83);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
					AssemblerExtensions.AddInstructions(asm, overwritten);
					AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress);
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
					AssemblerExtensions.AddUnrestrictedJmp(asm, nearBranch);
				};
				<>c.<>9__7_30 = val70;
				obj65 = (object)val70;
			}
			tx.AddInline(obj64, val69, (InlineHookGenerator)obj65, 30, "c_game_pathfinding_find_nearest_enemy_gatehouse_hook");
			HookHandle<X64InlineHook> obj66 = c_game_gatehouse_search_departure_hook;
			HookTarget val71 = HookTarget.op_Implicit("43 0F B6 84 3B ? ? ? ? 42 84 84 3A");
			object obj67 = <>c.<>9__7_31;
			if (obj67 == null)
			{
				InlineHookGenerator val72 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000a: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0017: Unknown result type (might be due to invalid IL or missing references)
					//IL_0022: Unknown result type (might be due to invalid IL or missing references)
					//IL_0032: Unknown result type (might be due to invalid IL or missing references)
					//IL_0037: Unknown result type (might be due to invalid IL or missing references)
					//IL_0043: Unknown result type (might be due to invalid IL or missing references)
					//IL_004a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0054: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Unknown result type (might be due to invalid IL or missing references)
					//IL_005f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					//IL_006f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0074: Unknown result type (might be due to invalid IL or missing references)
					//IL_007f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0084: Unknown result type (might be due to invalid IL or missing references)
					//IL_008f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0096: Unknown result type (might be due to invalid IL or missing references)
					//IL_009b: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
					//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
					//IL_00da: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
					//IL_0104: Unknown result type (might be due to invalid IL or missing references)
					//IL_0109: Unknown result type (might be due to invalid IL or missing references)
					//IL_010e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0119: Unknown result type (might be due to invalid IL or missing references)
					//IL_011e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0129: Unknown result type (might be due to invalid IL or missing references)
					//IL_012e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0139: Unknown result type (might be due to invalid IL or missing references)
					//IL_0140: Unknown result type (might be due to invalid IL or missing references)
					//IL_014a: Unknown result type (might be due to invalid IL or missing references)
					//IL_014f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0155: Unknown result type (might be due to invalid IL or missing references)
					//IL_015a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0165: Unknown result type (might be due to invalid IL or missing references)
					//IL_016a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0175: Unknown result type (might be due to invalid IL or missing references)
					//IL_017f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0184: Unknown result type (might be due to invalid IL or missing references)
					//IL_018a: Unknown result type (might be due to invalid IL or missing references)
					//IL_018f: Unknown result type (might be due to invalid IL or missing references)
					//IL_019a: Unknown result type (might be due to invalid IL or missing references)
					//IL_019f: Unknown result type (might be due to invalid IL or missing references)
					//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
					//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
					//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
					//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
					//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.pushfq();
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rcx);
					AssemblerExtensions.TestNativeFlag(asm, smarterSiegeAssassinsFlagArray, AssemblerRegisters.rbp, AssemblerRegisters.rax, true);
					asm.jz(val82);
					asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.r11 * 2 + 79080016L]);
					asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.jz(val82);
					asm.imul(AssemblerRegisters.rax, AssemblerRegisters.rax, 812);
					asm.movzx(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax + 105696478L]);
					asm.sub(AssemblerRegisters.ecx, 45);
					asm.cmp(AssemblerRegisters.ecx, 1);
					asm.ja(val82);
					asm.movzx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax + 105696978L]);
					asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.jz(val82);
					asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rax * 4 + 58646332L]);
					asm.cmp(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.r15 + AssemblerRegisters.rbp * 4 + 58646332L]);
					asm.je(val83);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
					AssemblerExtensions.AddInstructions(asm, overwritten);
					AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress);
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
				};
				<>c.<>9__7_31 = val72;
				obj67 = (object)val72;
			}
			tx.AddInline(obj66, val71, (InlineHookGenerator)obj67, 23, "c_game_gatehouse_search_departure_hook");
		}
		if (Plugin.Instance.AllowCustomTunnelerDigRange.Value)
		{
			customTunnelersDigRangeArray = nativeStateBlock.AllocateArray<int>(9, 0);
			HookHandle<X64InlineHook> obj68 = c_game_tunnel_building_try_find_attack_target_hook;
			HookTarget val73 = HookTarget.op_Implicit("0F B7 84 3B ? ? ? ? 41 8B D2");
			object obj69 = <>c.<>9__7_32;
			if (obj69 == null)
			{
				InlineHookGenerator val74 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_001a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0024: Unknown result type (might be due to invalid IL or missing references)
					//IL_0029: Unknown result type (might be due to invalid IL or missing references)
					//IL_002e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0039: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0049: Unknown result type (might be due to invalid IL or missing references)
					//IL_0055: Unknown result type (might be due to invalid IL or missing references)
					//IL_005c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0068: Unknown result type (might be due to invalid IL or missing references)
					//IL_006f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0084: Unknown result type (might be due to invalid IL or missing references)
					//IL_008e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0093: Unknown result type (might be due to invalid IL or missing references)
					//IL_0099: Unknown result type (might be due to invalid IL or missing references)
					//IL_009e: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00be: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
					//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
					Label val82 = asm.CreateLabel((string)null);
					asm.pushfq();
					asm.push(AssemblerRegisters.rcx);
					asm.movsx(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.rbx + AssemblerRegisters.rdi + 306L]);
					asm.cmp(AssemblerRegisters.ecx, 1);
					asm.jl(val82);
					asm.cmp(AssemblerRegisters.ecx, 8);
					asm.jg(val82);
					asm.mov(AssemblerRegisters.rax, customTunnelersDigRangeArray.Address);
					asm.mov(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rcx * 4]);
					asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
					asm.jle(val82);
					asm.mov(AssemblerRegisters.r9d, AssemblerRegisters.eax);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.rcx);
					asm.popfq();
					AssemblerExtensions.AddInstructions(asm, overwritten);
				};
				<>c.<>9__7_32 = val74;
				obj69 = (object)val74;
			}
			tx.AddInline(obj68, val73, (InlineHookGenerator)obj69, 14, "c_game_tunnel_building_try_find_attack_target_hook");
		}
		if (Plugin.Instance.AllowSiegeAssassinsIgnoreNonSiegeTargets.Value)
		{
			siegeAssassinsIgnoreNonSiegeTargetsFlagArray = nativeStateBlock.AllocateFlags(9, false);
			GamePlayerResources* ptr = default(GamePlayerResources*);
			if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(1, ref ptr))
			{
				ulong siegeTargetAddress = (ulong)(&((GamePlayerResources)ptr).r_SiegeAttackTargetPlayerId) - (ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<GamePlayerResources>();
				tx.AddInline(c_game_pathfinding_find_nearest_enemy_gatehouse_hook2, HookTarget.op_Implicit("4B 0F BF 84 38"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					//IL_0010: Unknown result type (might be due to invalid IL or missing references)
					//IL_0015: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_001d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					//IL_003b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0047: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					//IL_005a: Unknown result type (might be due to invalid IL or missing references)
					//IL_0066: Unknown result type (might be due to invalid IL or missing references)
					//IL_006b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0077: Unknown result type (might be due to invalid IL or missing references)
					//IL_007e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0083: Unknown result type (might be due to invalid IL or missing references)
					//IL_0094: Unknown result type (might be due to invalid IL or missing references)
					//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00af: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
					//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00be: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
					//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
					//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
					//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
					//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
					//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
					//IL_0103: Unknown result type (might be due to invalid IL or missing references)
					//IL_010e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0113: Unknown result type (might be due to invalid IL or missing references)
					//IL_011e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0123: Unknown result type (might be due to invalid IL or missing references)
					//IL_012e: Unknown result type (might be due to invalid IL or missing references)
					//IL_013d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0148: Unknown result type (might be due to invalid IL or missing references)
					//IL_016f: Unknown result type (might be due to invalid IL or missing references)
					//IL_017a: Unknown result type (might be due to invalid IL or missing references)
					ulong nearBranch = ((Instruction)overwritten[3]).NearBranch64;
					Label val82 = asm.CreateLabel((string)null);
					Label val83 = asm.CreateLabel((string)null);
					asm.pushfq();
					asm.push(AssemblerRegisters.rax);
					asm.push(AssemblerRegisters.rcx);
					asm.cmp(AssemblerRegisters.ebp, 1);
					asm.jl(val82);
					asm.cmp(AssemblerRegisters.ebp, 8);
					asm.jg(val82);
					AssemblerExtensions.TestNativeFlag(asm, siegeAssassinsIgnoreNonSiegeTargetsFlagArray, AssemblerRegisters.rbp, AssemblerRegisters.rax, true);
					asm.jz(val82);
					asm.imul(AssemblerRegisters.rcx, AssemblerRegisters.rbp, System.Runtime.CompilerServices.Unsafe.SizeOf<GamePlayerResources>());
					asm.mov(AssemblerRegisters.rax, siegeTargetAddress);
					asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax + AssemblerRegisters.rcx]);
					asm.cmp(AssemblerRegisters.ecx, 1);
					asm.jl(val83);
					asm.cmp(AssemblerRegisters.ecx, 8);
					asm.jg(val83);
					asm.movsx(AssemblerRegisters.eax, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__word_ptr))[AssemblerRegisters.r8 + AssemblerRegisters.r15 + 105696482L]);
					asm.cmp(AssemblerRegisters.eax, AssemblerRegisters.ecx);
					asm.jne(val83);
					asm.Label(ref val82);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
					AssemblerExtensions.AddInstructions(asm, overwritten);
					AssemblerExtensions.AddUnrestrictedJmp(asm, returnAddress);
					asm.Label(ref val83);
					asm.pop(AssemblerRegisters.rcx);
					asm.pop(AssemblerRegisters.rax);
					asm.popfq();
					AssemblerExtensions.AddUnrestrictedJmp(asm, nearBranch);
				}, 27, "c_game_pathfinding_find_nearest_enemy_gatehouse_hook2");
			}
		}
		if (!Plugin.Instance.FixPlacementSelectionNotAccountingForFarmland.Value)
		{
			return;
		}
		orchardSiteFilter = delegate(int coarseX, int coarseY)
		{
			if ((uint)coarseX >= 160u || (uint)coarseY >= 160u)
			{
				return 0;
			}
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			int num3 = coarseX * 5;
			int num4 = coarseY * 5;
			int num5 = Math.Max(0, num3 - 2);
			int num6 = Math.Max(0, num4 - 2);
			int num7 = Math.Min(799, num3 + 4 + 2);
			int num8 = Math.Min(799, num4 + 4 + 2);
			for (int i = num6; i <= num8; i++)
			{
				int num9 = instance.MapRowLookupTable[3 * i];
				for (int j = num5; j <= num7; j++)
				{
					int num10 = num9 + j;
					if (!instance.IsValidTileId(num10))
					{
						return 0;
					}
					if ((instance.TileManager.LogicGrid[num10] & 0xF000004) != 0)
					{
						return 0;
					}
				}
			}
			return 1;
		};
		ulong orchardSiteFilterAddress = (ulong)(long)Marshal.GetFunctionPointerForDelegate(orchardSiteFilter);
		tx.AddInline(c_game_aiv_find_open_coarse_cell_hook, HookTarget.op_Implicit("75 ? 44 38 B2 ? ? ? ? 75 ? 44 38 B2 ? ? ? ? 7F"), (InlineHookGenerator)delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			ulong nearBranch = ((Instruction)overwritten[0]).NearBranch64;
			ulong nearBranch2 = ((Instruction)overwritten[6]).NearBranch64;
			Label val82 = asm.CreateLabel((string)null);
			Label val83 = asm.CreateLabel((string)null);
			AssemblerExtensions.AddInstructions(asm, overwritten.Slice(0, 5));
			asm.test(AssemblerRegisters.bl, AssemblerRegisters.bl);
			asm.jnz(val82);
			asm.pushfq();
			asm.push(AssemblerRegisters.rax);
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rdx);
			asm.mov(AssemblerRegisters.ecx, AssemblerRegisters.r11d);
			asm.mov(AssemblerRegisters.edx, AssemblerRegisters.r10d);
			AssemblerExtensions.X64FastcallSafe(asm, orchardSiteFilterAddress, 2, (Action<Assembler>)null, false, false, Array.Empty<X64StackArgument>());
			asm.test(AssemblerRegisters.eax, AssemblerRegisters.eax);
			asm.jz(val83);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
			AssemblerExtensions.AddUnrestrictedJmp(asm, nearBranch2);
			asm.Label(ref val83);
			asm.pop(AssemblerRegisters.rdx);
			asm.pop(AssemblerRegisters.rcx);
			asm.pop(AssemblerRegisters.rax);
			asm.popfq();
			asm.Label(ref val82);
			AssemblerExtensions.AddUnrestrictedJmp(asm, nearBranch);
		}, 26, "c_game_aiv_find_open_coarse_cell_hook");
		return;
		IL_03c3:
		HookHandle<X64InlineHook> obj70 = c_game_ai_train_bodyguard_troops_hook;
		HookTarget val75 = HookTarget.op_Implicit("48 89 74 24 ? 4C 89 74 24 ? 83 FB");
		object obj71 = <>c.<>9__7_42;
		if (obj71 == null)
		{
			InlineHookGenerator val76 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0018: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_004f: Unknown result type (might be due to invalid IL or missing references)
				Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
				asm.push(AssemblerRegisters.rax);
				asm.mov(AssemblerRegisters.rax, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
				asm.mov(((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rax], -1);
				asm.pop(AssemblerRegisters.rax);
				AssemblerExtensions.AddInstructions(asm, array3);
			};
			<>c.<>9__7_42 = val76;
			obj71 = (object)val76;
		}
		tx.AddInline(obj70, val75, (InlineHookGenerator)obj71, 14, "c_game_ai_train_bodyguard_troops_hook");
		DataScanner val77 = scanner.Scan(CompiledPattern.Parse("40 55 41 54 41 55 41 57 48 83 EC ? 48 63 EA 4C 8D 2D ? ? ? ? 4C 69 CD ? ? ? ? 4C 8B E1 4C 8B FD 4B 63 84 29 ? ? ? ? 85 C0 0F 84 ? ? ? ? 48 89 7C 24 ? 48 69 F8 ? ? ? ? 48 03 F9 8B 87"), 0);
		if (val77.Found)
		{
			InstructionWalkerConfig val19 = new InstructionWalkerConfig();
			val19.Bitness = 64;
			val19.EndDetection = (FunctionEndDetection)0;
			InstructionWalkerConfig obj72 = val19;
			InstructionWalkRule[] array2 = new InstructionWalkRule[1];
			object obj73 = <>c.<>9__7_45;
			if (obj73 == null)
			{
				InstructionPredicate val78 = (Instruction x) => (int)((Instruction)(ref x)).Mnemonic == 751;
				<>c.<>9__7_45 = val78;
				obj73 = (object)val78;
			}
			object obj74 = <>O.<1>__ExtractInstructionAddress;
			if (obj74 == null)
			{
				InstructionExtractor val79 = InstructionWalkerHelpers.ExtractInstructionAddress;
				<>O.<1>__ExtractInstructionAddress = val79;
				obj74 = (object)val79;
			}
			array2[0] = InstructionWalkRule.FindAndExtract((InstructionPredicate)obj73, (InstructionExtractor)obj74, 0, (InstructionWalkerStartMode)1);
			obj72.WalkRules = (InstructionWalkRule[])(object)array2;
			ulong num2 = default(ulong);
			if (DataScannerInstructionWalkerExtensions.CreateWalker(val77, val19, (ILogger)null).TryWalk<ulong>(ref num2))
			{
				LoggerExtensions.LogInformation(val, "Found anchor c_game_ai_train_bodyguard_troops_hook2 at " + num2.ToString("X16"), Array.Empty<object>());
				HookHandle<X64InlineHook> obj75 = c_game_ai_train_bodyguard_troops_hook2;
				HookTarget val80 = HookTarget.op_Implicit(num2);
				object obj76 = <>c.<>9__7_43;
				if (obj76 == null)
				{
					InlineHookGenerator val81 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
					{
						//IL_000d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0018: Unknown result type (might be due to invalid IL or missing references)
						//IL_0023: Unknown result type (might be due to invalid IL or missing references)
						//IL_002d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0044: Unknown result type (might be due to invalid IL or missing references)
						//IL_004e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0053: Unknown result type (might be due to invalid IL or missing references)
						//IL_005e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0063: Unknown result type (might be due to invalid IL or missing references)
						//IL_006e: Unknown result type (might be due to invalid IL or missing references)
						//IL_007a: Unknown result type (might be due to invalid IL or missing references)
						//IL_0087: Unknown result type (might be due to invalid IL or missing references)
						//IL_008c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0097: Unknown result type (might be due to invalid IL or missing references)
						//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
						Instruction[] array3 = IcedExtensions.CloneInstructionsWithoutIP(overwritten).ToArray();
						asm.push(AssemblerRegisters.rcx);
						asm.push(AssemblerRegisters.rdx);
						asm.mov(AssemblerRegisters.rcx, (ulong)(&((GameUnitManager)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).r_RecruitmentResultMissingGoodId));
						asm.mov(AssemblerRegisters.ecx, ((AssemblerMemoryOperandFactory)(ref AssemblerRegisters.__dword_ptr))[AssemblerRegisters.rcx]);
						asm.xor(AssemblerRegisters.edx, AssemblerRegisters.edx);
						asm.sub(AssemblerRegisters.ecx, 1);
						asm.cmp(AssemblerRegisters.ecx, 23);
						asm.cmova(AssemblerRegisters.rax, AssemblerRegisters.rdx);
						asm.pop(AssemblerRegisters.rdx);
						asm.pop(AssemblerRegisters.rcx);
						AssemblerExtensions.AddInstructions(asm, array3);
					};
					<>c.<>9__7_43 = val81;
					obj76 = (object)val81;
				}
				tx.AddInline(obj75, val80, (InlineHookGenerator)obj76, 14, "c_game_ai_train_bodyguard_troops_hook2");
				goto IL_0506;
			}
		}
		LoggerExtensions.LogError(val, "Could not find anchor for c_game_ai_train_bodyguard_troops_hook2", Array.Empty<object>());
		goto IL_0506;
	}

	internal static void ResetLadderDistanceMap()
	{
		_ladderRoot = -1;
	}

	private unsafe static bool BuildLadderDistanceMap(GameUnit* unit)
	{
		int r_ControllableForPlayerId = ((GameUnit)unit).r_ControllableForPlayerId;
		GamePlayerManagerAPI instance = GamePlayerManagerAPI.Instance;
		GamePlayerResources* ptr = default(GamePlayerResources*);
		if (r_ControllableForPlayerId < 1 || r_ControllableForPlayerId > 8 || !instance.TryGetPlayerResourcesById(r_ControllableForPlayerId, ref ptr))
		{
			return false;
		}
		int r_SiegeAttackTargetPlayerId = (int)((GamePlayerResources)ptr).r_SiegeAttackTargetPlayerId;
		GamePlayerResources* ptr2 = default(GamePlayerResources*);
		if (r_SiegeAttackTargetPlayerId < 1 || r_SiegeAttackTargetPlayerId > 8 || r_SiegeAttackTargetPlayerId == r_ControllableForPlayerId || !instance.TryGetPlayerResourcesById(r_SiegeAttackTargetPlayerId, ref ptr2))
		{
			return false;
		}
		GameTileManagerAPI instance2 = GameTileManagerAPI.Instance;
		int num = (int)((GamePlayerResources)ptr2).r_KeepDoorTileId;
		if (num <= 0 || !instance2.IsValidTileId(num) || instance2.TileManager.PathEdgeMaskGrid[num] == 0)
		{
			num = (int)((GamePlayerResources)ptr2).r_KeepTileId;
		}
		if (num <= 0 || !instance2.IsValidTileId(num))
		{
			return false;
		}
		GameGlobalsManager instance3 = GameGlobalsManager.Instance;
		ulong num2 = *(ulong*)instance3.CurrentGameTickVA;
		if (_ladderRoot == num && _ladderCacheTick == num2)
		{
			return true;
		}
		int num3 = instance2.MapColumnLookupTable[num];
		int num4 = num - instance2.MapRowLookupTable[3 * num3];
		GamePathfindingContextView pathfindingContextView = GamePathingManagerAPI.Instance.GetPathfindingContextView();
		int walkGeneration = pathfindingContextView.WalkGeneration;
		BulkPathingDetours.c_game_pathsearch_bfs_ignoring_dynamic_occupancy.Invoke((IntPtr)(long)instance3.PathfindingContextVA, num4, num3, -1, 0, 400000, 0);
		if (pathfindingContextView.WalkGeneration == walkGeneration)
		{
			return false;
		}
		ushort num5 = (ushort)pathfindingContextView.WalkGeneration;
		System.Span<ushort> walkGrid = instance2.TileManager.WalkGrid;
		System.Span<ushort> certainPathGrid = instance2.TileManager.CertainPathGrid;
		for (int i = 0; i < _ladderDistances.Length; i++)
		{
			_ladderDistances[i] = ((walkGrid[i] == num5) ? (certainPathGrid[i] - 1) : int.MaxValue);
		}
		_ladderRoot = num;
		_ladderCacheTick = num2;
		return true;
	}

	private unsafe static int SelectLadderObjective(IntPtr selection, int unitId)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		try
		{
			GameUnit* ptr = default(GameUnit*);
			if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, ref ptr) || (int)((GameUnit)ptr).r_UnitChimp != 29)
			{
				return -1;
			}
			ulong aIPlayerObjectiveStorageVA = GameGlobalsManager.Instance.AIPlayerObjectiveStorageVA;
			if (aIPlayerObjectiveStorageVA == 0L || !BuildLadderDistanceMap(ptr))
			{
				return -1;
			}
			AIPlayerObjectiveStorage* ptr2 = (AIPlayerObjectiveStorage*)((nint)aIPlayerObjectiveStorageVA + (nint)(int)((GameUnit)ptr).r_ControllableForPlayerId * (nint)System.Runtime.CompilerServices.Unsafe.SizeOf<AIPlayerObjectiveStorage>());
			int siegeObjectiveCount = ((AIPlayerObjectiveStorage)ptr2).siegeObjectiveCount;
			if (siegeObjectiveCount < 0 || siegeObjectiveCount > 6011)
			{
				return -1;
			}
			AISiegeObjective* ptr3 = &((AIPlayerObjectiveStorage)ptr2).siegeObjectives;
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			int num = -1;
			int num2 = int.MaxValue;
			int num3 = 1000;
			for (int i = 0; i < siegeObjectiveCount; i++)
			{
				ref AISiegeObjective reference = ref *(AISiegeObjective*)((byte*)ptr3 + (nint)i * (nint)System.Runtime.CompilerServices.Unsafe.SizeOf<AISiegeObjective>());
				if (reference.eligibilityValue >= 5999 || reference.assignedUnitId != 0 || !instance.IsValidTileId(reference.wallTileId) || !instance.IsValidTileId(reference.approachTileId))
				{
					continue;
				}
				int num4 = instance.MapColumnLookupTable[reference.approachTileId];
				int num5 = Math.Max(Math.Abs(reference.approachTileId - instance.MapRowLookupTable[3 * num4] - ((GameUnit)ptr).r_CurrentTilePositionX), Math.Abs(num4 - ((GameUnit)ptr).r_CurrentTilePositionY));
				if (num5 < 1000)
				{
					int num6 = _ladderDistances[reference.wallTileId];
					if (num < 0 || (num6 <= num2 && (num6 != num2 || num5 < num3)))
					{
						num = i;
						num2 = num6;
						num3 = num5;
					}
				}
			}
			if (num < 0)
			{
				return 0;
			}
			ref AISiegeObjective reference2 = ref *(AISiegeObjective*)((byte*)ptr3 + (nint)num * (nint)System.Runtime.CompilerServices.Unsafe.SizeOf<AISiegeObjective>());
			int num7 = instance.MapColumnLookupTable[reference2.approachTileId];
			AITargetSelectionResult* ptr4 = (AITargetSelectionResult*)(void*)selection;
			((AITargetSelectionResult)ptr4).MovementTileX = reference2.approachTileId - instance.MapRowLookupTable[3 * num7];
			((AITargetSelectionResult)ptr4).MovementTileY = num7;
			((AITargetSelectionResult)ptr4).ContextTargetTileId = reference2.wallTileId;
			reference2.assignedUnitId = unitId;
			return reference2.approachTileId;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ladder objective selection failed; native fallback", "SelectLadderObjective", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
			return -1;
		}
	}

	private unsafe static int SelectLadderAttachment(int approachTileId, int rowDirectionBase)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		try
		{
			int num = *(int*)GameGlobalsManager.Instance.CurrentContextUnitIdVA;
			GameUnit* ptr = default(GameUnit*);
			if (!GameUnitManagerAPI.Instance.TryGetUnitById(num, ref ptr) || (int)((GameUnit)ptr).r_UnitChimp != 29 || (int)((GameUnit)ptr).r_AIObjectiveRole2 != 1012)
			{
				return -1;
			}
			if (!BuildLadderDistanceMap(ptr))
			{
				return -1;
			}
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			int num2 = 15;
			int num3 = int.MaxValue;
			PackedPathDirection[] ladderDirections = _ladderDirections;
			for (int i = 0; i < ladderDirections.Length; i++)
			{
				int num4 = (int)ladderDirections[i];
				int num5 = approachTileId + instance.TileManager.PackedNeighborTileDeltas[rowDirectionBase + num4];
				if (instance.IsValidTileId(num5) && (instance.TileManager.LogicGrid[num5] & 0x400102) == 256)
				{
					int num6 = _ladderDistances[num5];
					if (num2 == 15 || num6 < num3)
					{
						num2 = num4;
						num3 = num6;
					}
				}
			}
			return num2;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ladder attachment selection failed; native fallback", "SelectLadderAttachment", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
			return -1;
		}
	}

	internal static void c_game_ai_siege_master_handler_hook_impl(IntPtr pLordManager)
	{
		try
		{
			if (!int.TryParse(Plugin.Instance.LobbySettingsViewModel.AIPeaceTime, out var result))
			{
				c_game_ai_siege_master_handler_hook.Original(pLordManager);
				return;
			}
			if (GameTimeManagerAPI.Instance.GetElapsedMapTicks() >= result)
			{
				c_game_ai_siege_master_handler_hook.Original(pLordManager);
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Exception during siege master handler", "c_game_ai_siege_master_handler_hook_impl", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
		}
		c_game_ai_siege_master_handler_hook.Original(pLordManager);
	}

	static FixesAIDetours()
	{
		PackedPathDirection[] array = new PackedPathDirection[8];
		RuntimeHelpers.InitializeArray(array, (RuntimeFieldHandle)/*OpCode not supported: LdMemberToken*/);
		_ladderDirections = (PackedPathDirection[])(object)array;
		_ladderRoot = -1;
		c_game_ai_ladder_objective_rank_hook = new HookHandle<X64InlineHook>();
		c_game_ai_ladder_attachment_rank_hook = new HookHandle<X64InlineHook>();
		c_game_aiv_find_open_coarse_cell_hook = new HookHandle<X64InlineHook>();
		c_game_tunnel_building_try_find_attack_target_hook = new HookHandle<X64InlineHook>();
		c_game_pathfinding_find_nearest_enemy_gatehouse_hook2 = new HookHandle<X64InlineHook>();
		c_game_gatehouse_search_departure_hook = new HookHandle<X64InlineHook>();
		c_game_pathfinding_find_nearest_enemy_gatehouse_hook = new HookHandle<X64InlineHook>();
		c_game_ai_update_all_players_hook = new HookHandle<X64InlineHook>();
		c_game_ai_siege_processing_handler_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_siege_processing_handler_hook = new HookHandle<X64InlineHook>();
		c_game_ai_should_defer_bad_thing_hook = new HookHandle<X64InlineHook>();
		c_game_ai_should_defer_good_thing_hook = new HookHandle<X64InlineHook>();
		c_game_ai_food_buy_handler_hook = new HookHandle<X64InlineHook>();
		c_game_ai_need_more_ox_bases_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_need_more_ox_bases_hook = new HookHandle<X64InlineHook>();
		c_game_ai_build_wall_internal_hook = new HookHandle<X64InlineHook>();
		c_game_ai_build_wall_hook = new HookHandle<X64InlineHook>();
		c_game_ai_deploy_harassing_siege_engine_tents_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_sell_item_handler_hook = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook11 = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook10 = new HookHandle<X64AssemblyPatch>();
		c_game_aiv_orderedmaptileids_hook9 = new HookHandle<X64AssemblyPatch>();
		c_game_aiv_orderedmaptileids_hook8 = new HookHandle<X64AssemblyPatch>();
		c_game_aiv_orderedmaptileids_hook7 = new HookHandle<X64AssemblyPatch>();
		c_game_aiv_orderedmaptileids_hook6 = new HookHandle<X64AssemblyPatch>();
		c_game_aiv_orderedmaptileids_hook5 = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook4 = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook3 = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook2 = new HookHandle<X64InlineHook>();
		c_game_aiv_orderedmaptileids_hook1 = new HookHandle<X64InlineHook>();
		c_game_ai_tribe_siege_behaviour_handler_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_tribe_siege_behaviour_handler_hook = new HookHandle<X64InlineHook>();
		c_game_ai_decide_target_priority_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_decide_target_priority_hook = new HookHandle<X64InlineHook>();
		c_game_aiv_validate_building_tile_hook = new HookHandle<X64InlineHook>();
		c_game_ai_recruitmentmode_handler_hook = new HookHandle<X64InlineHook>();
		c_game_ai_move_tribe_to_keep_entrance_hook = new HookHandle<X64InlineHook>();
		c_game_attempt_recruit_required_troops_hook = new HookHandle<X64InlineHook>();
		c_game_attempt_recruit_required_troops_hook2 = new HookHandle<X64InlineHook>();
		c_game_attempt_recruit_required_troops_hook3 = new HookHandle<X64InlineHook>();
		c_game_ai_train_economy_protection_troops_hook = new HookHandle<X64InlineHook>();
		c_game_ai_train_economy_protection_troops_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_train_bodyguard_troops_hook = new HookHandle<X64InlineHook>();
		c_game_ai_train_bodyguard_troops_hook2 = new HookHandle<X64InlineHook>();
		c_game_ai_siege_master_handler_hook = new DetourHandle<c_game_ai_siege_master_handler_delegate>();
		c_game_ai_deploy_harassing_siege_engine_tents_hook = new HookHandle<X64InlineHook>();
		c_game_ai_find_valid_siege_tent_site_hook = new HookHandle<X64InlineHook>();
		c_game_aiv_prepare_layout_hook = new HookHandle<X64InlineHook>();
		c_game_ai_count_active_farms_hook = new HookHandle<X64InlineHook>();
	}
}
