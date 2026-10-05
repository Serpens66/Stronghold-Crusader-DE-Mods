using System.Collections.Generic;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using RedBird.Core.Memory;
using RedBird.Core.Platform;

namespace RedBird.Backends.NativeX64;

internal static class PrologueAnalyzer
{
	private const int Bitness = 64;

	public static bool TryAnalyze(ulong address, NativeDetourOptions options, ILogger logger, out ulong resolvedAddress, out List<Instruction> instructions, out Dictionary<ulong, List<Instruction>> branchSources)
	{
		resolvedAddress = address;
		instructions = new List<Instruction>();
		branchSources = new Dictionary<ulong, List<Instruction>>();
		if (options.FollowJumps && !TryFollowThunks(ref resolvedAddress, options, logger))
		{
			return false;
		}
		instructions = Disassemble(resolvedAddress, options.FunctionScanLimit);
		if (instructions.Count == 0)
		{
			logger.LogError("Nothing decodable at 0x{Address:X16}.", resolvedAddress);
			return false;
		}
		branchSources = BuildBranchMap(instructions);
		return true;
	}

	private unsafe static bool TryFollowThunks(ref ulong address, NativeDetourOptions options, ILogger logger)
	{
		for (int i = 0; i < options.MaxFollowDepth; i++)
		{
			Instruction instruction = DecodeOne(address);
			if (instruction.IsInvalid)
			{
				logger.LogError("Nothing decodable at 0x{Address:X16}.", address);
				return false;
			}
			FlowControl flowControl = instruction.FlowControl;
			bool flag = (uint)(flowControl - 1) <= 1u;
			bool flag2 = flag;
			flowControl = instruction.FlowControl;
			flag = (uint)(flowControl - 5) <= 1u;
			bool flag3 = flag;
			if (!flag2 && (!flag3 || !options.FollowCalls))
			{
				return true;
			}
			OpKind op0Kind = instruction.Op0Kind;
			if ((uint)(op0Kind - 1) > 2u)
			{
				if (!instruction.IsIPRelativeMemoryOperand)
				{
					logger.LogDebug("Entry at 0x{Address:X16} branches through a register; hooking it in place.", address);
					return true;
				}
				ulong num = *(ulong*)instruction.IPRelativeMemoryAddress;
				if (num == 0L)
				{
					logger.LogWarning("Import thunk at 0x{Address:X16} is unbound; hooking the thunk itself.", address);
					return true;
				}
				if (!ShouldFollow(address, num, options, logger, allowLocalThunk: false))
				{
					return true;
				}
				logger.LogDebug("Followed import thunk 0x{From:X16} -> 0x{To:X16}.", address, num);
				address = num;
			}
			else
			{
				ulong nearBranchTarget = instruction.NearBranchTarget;
				if (!ShouldFollow(address, nearBranchTarget, options, logger, allowLocalThunk: true))
				{
					return true;
				}
				logger.LogDebug("Followed thunk 0x{From:X16} -> 0x{To:X16}.", address, nearBranchTarget);
				address = nearBranchTarget;
			}
		}
		logger.LogError("Jump chain at 0x{Address:X16} exceeded {MaxDepth} levels.", address, options.MaxFollowDepth);
		return false;
	}

	public static bool TrySelectPrologue(ulong functionAddress, List<Instruction> instructions, Dictionary<ulong, List<Instruction>> branchSources, int minimumBytes, ILogger logger, out PrologueSelection? selection)
	{
		selection = null;
		int num = minimumBytes;
		for (int i = 0; i < 16; i++)
		{
			if (!TryTakeAtLeast(instructions, num, out List<Instruction> taken, out int length))
			{
				logger.LogDebug("0x{Address:X16} has only {Length} usable bytes before its end; {Required} are needed.", functionAddress, length, num);
				return false;
			}
			int num2 = WidenForSelfBranches(functionAddress, taken, branchSources, length);
			if (num2 <= num)
			{
				selection = new PrologueSelection
				{
					FunctionAddress = functionAddress,
					Instructions = taken,
					Length = length
				};
				return true;
			}
			logger.LogDebug("Prologue at 0x{Address:X16} widened from {Old} to {New} bytes to cover a branch into it.", functionAddress, num, num2);
			num = num2;
		}
		logger.LogWarning("Prologue selection at 0x{Address:X16} did not converge.", functionAddress);
		return false;
	}

	private static bool TryTakeAtLeast(List<Instruction> instructions, int minimumBytes, out List<Instruction> taken, out int length)
	{
		taken = new List<Instruction>();
		length = 0;
		bool flag = false;
		foreach (Instruction instruction2 in instructions)
		{
			Instruction instruction = instruction2;
			if (flag && !IsPadding(in instruction))
			{
				break;
			}
			taken.Add(instruction);
			length += instruction.Length;
			if (length >= minimumBytes)
			{
				return true;
			}
			if (IsFunctionEnd(in instruction))
			{
				flag = true;
			}
		}
		return false;
	}

	private static int WidenForSelfBranches(ulong functionAddress, List<Instruction> prologue, Dictionary<ulong, List<Instruction>> branchSources, int currentLength)
	{
		ulong num = functionAddress + (ulong)currentLength;
		foreach (Instruction item in prologue)
		{
			if (!branchSources.TryGetValue(item.IP, out List<Instruction> value))
			{
				continue;
			}
			foreach (Instruction item2 in value)
			{
				ulong num2 = item2.IP + (ulong)item2.Length;
				if (num2 > num)
				{
					num = num2;
				}
			}
		}
		return (int)(num - functionAddress);
	}

	private static Dictionary<ulong, List<Instruction>> BuildBranchMap(List<Instruction> instructions)
	{
		Dictionary<ulong, List<Instruction>> dictionary = new Dictionary<ulong, List<Instruction>>();
		ulong num = ((instructions.Count == 0) ? 0 : instructions[0].IP);
		foreach (Instruction instruction in instructions)
		{
			FlowControl flowControl = instruction.FlowControl;
			bool flag = ((flowControl == FlowControl.UnconditionalBranch || flowControl == FlowControl.ConditionalBranch) ? true : false);
			bool num2 = flag;
			bool flag2 = instruction.FlowControl == FlowControl.Call;
			if (!num2 && !flag2)
			{
				continue;
			}
			OpKind op0Kind = instruction.Op0Kind;
			if ((uint)(op0Kind - 1) > 2u)
			{
				continue;
			}
			ulong nearBranchTarget = instruction.NearBranchTarget;
			if (!flag2 || nearBranchTarget != num)
			{
				if (!dictionary.TryGetValue(nearBranchTarget, out var value))
				{
					value = (dictionary[nearBranchTarget] = new List<Instruction>());
				}
				value.Add(instruction);
			}
		}
		return dictionary;
	}

	private unsafe static List<Instruction> Disassemble(ulong address, int limit)
	{
		MemoryCodeReader reader = new MemoryCodeReader((byte*)address, limit);
		Decoder decoder = Decoder.Create(64, reader, address);
		List<Instruction> list = new List<Instruction>(64);
		ulong num = address + (ulong)limit;
		ulong num2 = address;
		int num3 = 0;
		while (num3 < limit)
		{
			Instruction instruction = decoder.Decode();
			if (decoder.LastError != DecoderError.None || instruction.IsInvalid)
			{
				break;
			}
			list.Add(instruction);
			num3 += instruction.Length;
			FlowControl flowControl = instruction.FlowControl;
			bool flag = ((flowControl == FlowControl.UnconditionalBranch || flowControl == FlowControl.ConditionalBranch) ? true : false);
			bool flag2 = flag;
			if (flag2)
			{
				OpKind op0Kind = instruction.Op0Kind;
				bool flag3 = (uint)(op0Kind - 1) <= 2u;
				flag2 = flag3;
			}
			if (flag2)
			{
				ulong nearBranchTarget = instruction.NearBranchTarget;
				if (nearBranchTarget > num2 && nearBranchTarget > address && nearBranchTarget < num)
				{
					num2 = nearBranchTarget;
				}
			}
			if (IsFunctionEnd(in instruction) && instruction.NextIP > num2)
			{
				break;
			}
		}
		return list;
	}

	private static bool ShouldFollow(ulong from, ulong to, NativeDetourOptions options, ILogger logger, bool allowLocalThunk)
	{
		if (ModuleLocator.TryGetContainingModule(to, out var _))
		{
			return true;
		}
		ulong num = ((to > from) ? (to - from) : (from - to));
		if (allowLocalThunk && num <= (ulong)options.LocalThunkRange)
		{
			return true;
		}
		logger.LogInformation("0x{From:X16} jumps to 0x{To:X16}, which is not inside any loaded module - treating it as an existing hook and layering on top rather than following it.", from, to);
		return false;
	}

	private unsafe static Instruction DecodeOne(ulong address)
	{
		MemoryCodeReader reader = new MemoryCodeReader((byte*)address, 16);
		return Decoder.Create(64, reader, address).Decode();
	}

	private static bool IsFunctionEnd(in Instruction instruction)
	{
		bool flag = instruction.Code == Code.Int3;
		if (!flag)
		{
			FlowControl flowControl = instruction.FlowControl;
			bool flag2 = ((flowControl == FlowControl.UnconditionalBranch || flowControl == FlowControl.Return) ? true : false);
			flag = flag2;
		}
		return flag;
	}

	private static bool IsPadding(in Instruction instruction)
	{
		if (instruction.Code != Code.Int3)
		{
			return instruction.Mnemonic == Mnemonic.Nop;
		}
		return true;
	}
}
