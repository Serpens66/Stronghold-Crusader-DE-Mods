using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Fixes.Boostrap;
using Fixes.Config;
using Iced.Intel;
using PolyHook2.Managed;
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.Interop.Enums;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace Fixes.Detours;

[SuppressUnmanagedCodeSecurity]
public class FixesAIDetours : IDisposable
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void c_game_ai_siege_master_handler_delegate(IntPtr pLordManager);

	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static Action<ulong, ulong, ulong, int, int, int> _003C_003E9__4_4;

		public static InlineHookGenerator _003C_003E9__4_0;

		public static Func<ulong, ulong, int> _003C_003E9__4_6;

		public static InlineHookGenerator _003C_003E9__4_1;

		public static Func<ulong, bool> _003C_003E9__4_7;

		public static InlineHookGenerator _003C_003E9__4_2;

		public static InlineHookGenerator _003C_003E9__4_3;

		internal void _003C_002Ector_003Eb__4_0(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
			AssemblerExtensions.X64FastcallSafeEx2(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action<ulong, ulong, ulong, int, int, int>>(delegate(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
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

		internal void _003C_002Ector_003Eb__4_4(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
		{
			GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(Plugin.Instance.HarassingTentIdleEngineersFixDelayAmount.Value, (Action)delegate
			{
				GameTribeManagerAPI.Instance.IssueTargettedCommand((int)tribeId, (TribeAICommand)aiCommandType, targetValue1, targetValue2, a6);
			}, string.Empty);
		}

		internal unsafe void _003C_002Ector_003Eb__4_1(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			AssemblerExtensions.AddInstructions(asm, instrs);
			asm.jnz(((Instruction)instrs[1]).IPRelativeMemoryAddress);
			AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, ulong, int>>(delegate(ulong _RDX, ulong currentTileId)
			{
				GameTileManagerAPI instance = GameTileManagerAPI.Instance;
				int num = instance.MapColumnLookupTable[currentTileId];
				int num2 = (int)currentTileId - instance.MapRowLookupTable[3 * num];
				for (int i = -2; i <= 2; i++)
				{
					int num3 = num + i;
					if (num3 >= 0 && num3 < 800)
					{
						int num4 = instance.MapRowLookupTable[3 * num3];
						for (int j = -2; j <= 2; j++)
						{
							int num5 = num2 + j;
							if (num5 >= 0 && num5 < 800)
							{
								int num6 = num4 + num5;
								if (instance.TileManager.StructureGrid[num6] != 0)
								{
									return 0;
								}
							}
						}
					}
				}
				return 1;
			}), 2, (Action<Assembler>)null, false, false);
			asm.cmp(AssemblerRegisters.rax, 1);
		}

		internal unsafe int _003C_002Ector_003Eb__4_6(ulong _RDX, ulong currentTileId)
		{
			GameTileManagerAPI instance = GameTileManagerAPI.Instance;
			int num = instance.MapColumnLookupTable[currentTileId];
			int num2 = (int)currentTileId - instance.MapRowLookupTable[3 * num];
			for (int i = -2; i <= 2; i++)
			{
				int num3 = num + i;
				if (num3 < 0 || num3 >= 800)
				{
					continue;
				}
				int num4 = instance.MapRowLookupTable[3 * num3];
				for (int j = -2; j <= 2; j++)
				{
					int num5 = num2 + j;
					if (num5 >= 0 && num5 < 800)
					{
						int num6 = num4 + num5;
						if (instance.TileManager.StructureGrid[num6] != 0)
						{
							return 0;
						}
					}
				}
			}
			return 1;
		}

		internal void _003C_002Ector_003Eb__4_2(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			Label val = asm.CreateLabel("lblSkip");
			asm.push(AssemblerRegisters.rcx);
			asm.push(AssemblerRegisters.rax);
			asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
			AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, bool>>(delegate(ulong playerId)
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0029: Unknown result type (might be due to invalid IL or missing references)
				//IL_004d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				Plugin instance = Plugin.Instance;
				GameAIManagerAPI instance2 = GameAIManagerAPI.Instance;
				GamePlayerManagerAPI instance3 = GamePlayerManagerAPI.Instance;
				if (instance.ForceHopsFarmFix.Value)
				{
					return true;
				}
				AILords aILord = instance3.GetAILord((int)playerId);
				Dictionary<AILords, bool> hopsFarmWhitelist = instance.HopsFarmWhitelist;
				if (hopsFarmWhitelist != null && hopsFarmWhitelist.Count > 0 && hopsFarmWhitelist.ContainsKey(aILord))
				{
					return true;
				}
				if (instance2.IsCustomLord(aILord))
				{
					string customAILordNameByPlayerId = instance2.GetCustomAILordNameByPlayerId((int)playerId);
					if (instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out CustomLordPreferencesEntry value))
					{
						return value.EnableHopsFarmFix == true;
					}
				}
				return false;
			}), 1, (Action<Assembler>)null, true, false);
			asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rax);
			asm.pop(AssemblerRegisters.rcx);
			asm.jnz(val);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
			asm.Label(ref val);
			AssemblerExtensions.AddInstructions(asm, instrs.Slice(2));
		}

		internal bool _003C_002Ector_003Eb__4_7(ulong playerId)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			Plugin instance = Plugin.Instance;
			GameAIManagerAPI instance2 = GameAIManagerAPI.Instance;
			GamePlayerManagerAPI instance3 = GamePlayerManagerAPI.Instance;
			if (instance.ForceHopsFarmFix.Value)
			{
				return true;
			}
			AILords aILord = instance3.GetAILord((int)playerId);
			Dictionary<AILords, bool> hopsFarmWhitelist = instance.HopsFarmWhitelist;
			if (hopsFarmWhitelist != null && hopsFarmWhitelist.Count > 0 && hopsFarmWhitelist.ContainsKey(aILord))
			{
				return true;
			}
			if (instance2.IsCustomLord(aILord))
			{
				string customAILordNameByPlayerId = instance2.GetCustomAILordNameByPlayerId((int)playerId);
				if (instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out CustomLordPreferencesEntry value))
				{
					return value.EnableHopsFarmFix == true;
				}
			}
			return false;
		}

		internal void _003C_002Ector_003Eb__4_3(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
		{
			Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).Skip(2).ToArray();
			AssemblerExtensions.AddInstructions(asm, array);
		}

		internal ModLogHelper _003C_002Ecctor_003Eb__13_0()
		{
			return ModLoggerFactory.CreateHelper("Fixes");
		}
	}

	private HookTransaction? tx;

	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	internal static HookRef<X64ManagedFunctionDetourAOB<c_game_ai_siege_master_handler_delegate>> c_game_ai_siege_master_handler_hook = new HookRef<X64ManagedFunctionDetourAOB<c_game_ai_siege_master_handler_delegate>>();

	internal static HookRef<X64InlineHook> c_game_ai_deploy_harassing_siege_engine_tents_hook = new HookRef<X64InlineHook>();

	internal static HookRef<X64InlineHook> c_game_ai_find_valid_siege_tent_site_hook = new HookRef<X64InlineHook>();

	internal static HookRef<X64InlineHook> c_game_aiv_prepare_layout_hook = new HookRef<X64InlineHook>();

	public static HookRef<X64InlineHook> c_game_ai_count_active_farms_hook;

	private static ModLogHelper Log => _log.Value;

	public unsafe FixesAIDetours(IntPtr libraryHandle, System.ReadOnlySpan<byte> memory)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected O, but got Unknown
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Expected O, but got Unknown
		Plugin.Instance.LoggerFactory.CreateLogger("Fixes-AIDetours");
		Log.Information("Applying", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
		ulong num = (ulong)(long)libraryHandle;
		if (tx == null)
		{
			tx = new HookTransaction(memory, num, Plugin.Instance.LoggerFactory, (TransactionFailureMode)1);
		}
		tx.AddDetour<c_game_ai_siege_master_handler_delegate>(ref c_game_ai_siege_master_handler_hook, "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC ? 83 3D ? ? ? ? ? 48 8B F1", (c_game_ai_siege_master_handler_delegate)c_game_ai_siege_master_handler_hook_impl, 0, "c_game_ai_siege_master_handler_hook");
		if (Plugin.Instance.EnableHarassingTentIdleEngineersFix.Value)
		{
			HookTransaction? obj = tx;
			object obj2 = _003C_003Ec._003C_003E9__4_0;
			if (obj2 == null)
			{
				InlineHookGenerator val = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0048: Unknown result type (might be due to invalid IL or missing references)
					//IL_004e: Expected O, but got Unknown
					//IL_0053: Unknown result type (might be due to invalid IL or missing references)
					//IL_0059: Expected O, but got Unknown
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
					AssemblerExtensions.X64FastcallSafeEx2(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Action<ulong, ulong, ulong, int, int, int>>(delegate(ulong pTribeManager, ulong tribeId, ulong aiCommandType, int targetValue1, int targetValue2, int a6)
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
				_003C_003Ec._003C_003E9__4_0 = val;
				obj2 = (object)val;
			}
			obj.AddInline(ref c_game_ai_deploy_harassing_siege_engine_tents_hook, "42 89 BC 29 ? ? ? ? 48 8D 0D", (InlineHookGenerator)obj2, 0, 23, "c_game_ai_deploy_harassing_siege_engine_tents_hook");
		}
		if (Plugin.Instance.EnableAIDistancedSiegeTents.Value)
		{
			HookTransaction? obj3 = tx;
			object obj4 = _003C_003Ec._003C_003E9__4_1;
			if (obj4 == null)
			{
				InlineHookGenerator val2 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_004e: Unknown result type (might be due to invalid IL or missing references)
					AssemblerExtensions.AddInstructions(asm, instrs);
					asm.jnz(((Instruction)instrs[1]).IPRelativeMemoryAddress);
					AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, ulong, int>>(delegate(ulong _RDX, ulong currentTileId)
					{
						GameTileManagerAPI instance = GameTileManagerAPI.Instance;
						int num2 = instance.MapColumnLookupTable[currentTileId];
						int num3 = (int)currentTileId - instance.MapRowLookupTable[3 * num2];
						for (int i = -2; i <= 2; i++)
						{
							int num4 = num2 + i;
							if (num4 >= 0 && num4 < 800)
							{
								int num5 = instance.MapRowLookupTable[3 * num4];
								for (int j = -2; j <= 2; j++)
								{
									int num6 = num3 + j;
									if (num6 >= 0 && num6 < 800)
									{
										int num7 = num5 + num6;
										if (instance.TileManager.StructureGrid[num7] != 0)
										{
											return 0;
										}
									}
								}
							}
						}
						return 1;
					}), 2, (Action<Assembler>)null, false, false);
					asm.cmp(AssemblerRegisters.rax, 1);
				};
				_003C_003Ec._003C_003E9__4_1 = val2;
				obj4 = (object)val2;
			}
			obj3.AddInline(ref c_game_ai_find_valid_siege_tent_site_hook, "41 F6 84 B9", (InlineHookGenerator)obj4, 0, 14, "c_game_ai_find_valid_siege_tent_site_hook");
		}
		if (Plugin.Instance.EnableHopsFarmFix.Value)
		{
			HookTransaction? obj5 = tx;
			object obj6 = _003C_003Ec._003C_003E9__4_2;
			if (obj6 == null)
			{
				InlineHookGenerator val3 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> instrs, ulong returnAddress)
				{
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_0023: Unknown result type (might be due to invalid IL or missing references)
					//IL_0028: Unknown result type (might be due to invalid IL or missing references)
					//IL_0066: Unknown result type (might be due to invalid IL or missing references)
					//IL_006b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0076: Unknown result type (might be due to invalid IL or missing references)
					//IL_0081: Unknown result type (might be due to invalid IL or missing references)
					//IL_008c: Unknown result type (might be due to invalid IL or missing references)
					Label val5 = asm.CreateLabel("lblSkip");
					asm.push(AssemblerRegisters.rcx);
					asm.push(AssemblerRegisters.rax);
					asm.mov(AssemblerRegisters.rcx, AssemblerRegisters.r11);
					AssemblerExtensions.X64FastcallSafeEx(asm, (ulong)(long)Marshal.GetFunctionPointerForDelegate<Func<ulong, bool>>(delegate(ulong playerId)
					{
						//IL_0024: Unknown result type (might be due to invalid IL or missing references)
						//IL_0029: Unknown result type (might be due to invalid IL or missing references)
						//IL_004d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0042: Unknown result type (might be due to invalid IL or missing references)
						Plugin instance = Plugin.Instance;
						GameAIManagerAPI instance2 = GameAIManagerAPI.Instance;
						GamePlayerManagerAPI instance3 = GamePlayerManagerAPI.Instance;
						if (instance.ForceHopsFarmFix.Value)
						{
							return true;
						}
						AILords aILord = instance3.GetAILord((int)playerId);
						Dictionary<AILords, bool> hopsFarmWhitelist = instance.HopsFarmWhitelist;
						if (hopsFarmWhitelist != null && hopsFarmWhitelist.Count > 0 && hopsFarmWhitelist.ContainsKey(aILord))
						{
							return true;
						}
						if (instance2.IsCustomLord(aILord))
						{
							string customAILordNameByPlayerId = instance2.GetCustomAILordNameByPlayerId((int)playerId);
							if (instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out CustomLordPreferencesEntry value))
							{
								return value.EnableHopsFarmFix == true;
							}
						}
						return false;
					}), 1, (Action<Assembler>)null, true, false);
					asm.test(AssemblerRegisters.rax, AssemblerRegisters.rax);
					asm.pop(AssemblerRegisters.rax);
					asm.pop(AssemblerRegisters.rcx);
					asm.jnz(val5);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(0, 2));
					asm.Label(ref val5);
					AssemblerExtensions.AddInstructions(asm, instrs.Slice(2));
				};
				_003C_003Ec._003C_003E9__4_2 = val3;
				obj6 = (object)val3;
			}
			obj5.AddInline(ref c_game_ai_count_active_farms_hook, "66 83 FA ? 74 ? 45 85 C0 74 ? 66 83 B9", (InlineHookGenerator)obj6, 0, 14, "c_game_ai_count_active_farms_hook");
		}
		if (Plugin.Instance.AllowKeep3ForAIVs.Value)
		{
			HookTransaction? obj7 = tx;
			object obj8 = _003C_003Ec._003C_003E9__4_3;
			if (obj8 == null)
			{
				InlineHookGenerator val4 = delegate(Assembler asm, System.ReadOnlySpan<Instruction> overwritten, ulong returnAddress)
				{
					Instruction[] array = IcedExtensions.CloneInstructionsWithoutIP(overwritten).Skip(2).ToArray();
					AssemblerExtensions.AddInstructions(asm, array);
				};
				_003C_003Ec._003C_003E9__4_3 = val4;
				obj8 = (object)val4;
			}
			obj7.AddInline(ref c_game_aiv_prepare_layout_hook, "66 83 F8 ? 74 ? 66 41 2B C0", (InlineHookGenerator)obj8, 0, 14, "c_game_aiv_prepare_layout_hook");
		}
		tx.Commit();
	}

	internal static void c_game_ai_siege_master_handler_hook_impl(IntPtr pLordManager)
	{
		try
		{
			if (!int.TryParse(Plugin.Instance.LobbySettingsViewModel.AIPeaceTime, out var result))
			{
				((ManagedFunctionDetour<c_game_ai_siege_master_handler_delegate>)(object)c_game_ai_siege_master_handler_hook.Value.Hook).Trampoline(pLordManager);
				return;
			}
			if (GameTimeManagerAPI.Instance.GetElapsedMapTicks() >= result)
			{
				((ManagedFunctionDetour<c_game_ai_siege_master_handler_delegate>)(object)c_game_ai_siege_master_handler_hook.Value.Hook).Trampoline(pLordManager);
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Exception during siege master handler", "c_game_ai_siege_master_handler_hook_impl", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Detours\\AIDetours.cs");
		}
		((ManagedFunctionDetour<c_game_ai_siege_master_handler_delegate>)(object)c_game_ai_siege_master_handler_hook.Value.Hook).Trampoline(pLordManager);
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
