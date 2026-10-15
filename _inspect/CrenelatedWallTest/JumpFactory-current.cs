using System;
using System.IO;
using Iced.Intel;
using RedBird.Core.Memory;

namespace RedBird.Backends.NativeX64;

internal static class JumpFactory
{
	private const int Bitness = 64;

	public static int GetPatchSize(DetourScheme scheme)
	{
		return scheme switch
		{
			DetourScheme.Indirect => 6, 
			DetourScheme.Absolute => 14, 
			DetourScheme.PushRet => 12, 
			DetourScheme.PushRetRedZoneSafe => 23, 
			_ => throw new ArgumentOutOfRangeException("scheme", scheme, "Unknown detour scheme."), 
		};
	}

	public static DetourPatch? TryBuild(DetourScheme scheme, ulong functionAddress, ulong callbackAddress, out string? failureReason)
	{
		failureReason = null;
		object obj = scheme switch
		{
			DetourScheme.Indirect => BuildIndirect(functionAddress, callbackAddress, out failureReason), 
			DetourScheme.Absolute => BuildAbsolute(callbackAddress), 
			DetourScheme.PushRet => BuildAssembled(scheme, functionAddress, delegate(Assembler a)
			{
				a.mov(AssemblerRegisters.rax, callbackAddress);
				a.push(AssemblerRegisters.rax);
				a.ret();
			}), 
			DetourScheme.PushRetRedZoneSafe => BuildAssembled(scheme, functionAddress, delegate(Assembler a)
			{
				a.lea(AssemblerRegisters.rsp, AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp - 128L]);
				a.push(AssemblerRegisters.rax);
				a.mov(AssemblerRegisters.rax, callbackAddress);
				a.xchg(AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp], AssemblerRegisters.rax);
				a.ret(128);
			}), 
			_ => null, 
		};
		if (obj == null && failureReason == null)
		{
			failureReason = $"the {scheme} entry jump could not be encoded at 0x{functionAddress:X16}";
		}
		return (DetourPatch?)obj;
	}

	private unsafe static DetourPatch? BuildIndirect(ulong functionAddress, ulong callbackAddress, out string? failureReason)
	{
		failureReason = null;
		IntPtr intPtr = NativeMemoryManager.AllocateStub(functionAddress, 8);
		if (intPtr == IntPtr.Zero)
		{
			failureReason = NativeMemoryManager.LastAllocationFailure ?? "no nearby pointer slot was available";
			return null;
		}
		long num = (long)intPtr - (long)(functionAddress + 6);
		if (num < int.MinValue || num > int.MaxValue)
		{
			failureReason = $"the pointer slot at 0x{(ulong)(long)intPtr:X16} is outside rel32 range";
			return null;
		}
		if (!NativeMemoryManager.WriteStub(intPtr, callbackAddress))
		{
			failureReason = $"the pointer slot at 0x{(ulong)(long)intPtr:X16} was not writable";
			return null;
		}
		byte[] array = new byte[6] { 255, 37, 0, 0, 0, 0 };
		fixed (byte* ptr = array)
		{
			*(int*)(ptr + 2) = (int)num;
		}
		return new DetourPatch
		{
			Scheme = DetourScheme.Indirect,
			Bytes = array,
			PointerSlot = intPtr
		};
	}

	private static DetourPatch BuildAbsolute(ulong callbackAddress)
	{
		return new DetourPatch
		{
			Scheme = DetourScheme.Absolute,
			Bytes = CodePatch.BuildAbsoluteJump(callbackAddress)
		};
	}

	private static DetourPatch? BuildAssembled(DetourScheme scheme, ulong functionAddress, Action<Assembler> build)
	{
		Assembler assembler = new Assembler(64);
		build(assembler);
		using MemoryStream memoryStream = new MemoryStream();
		if (!assembler.TryAssemble(new StreamCodeWriter(memoryStream), functionAddress, out string _, out AssemblerResult _))
		{
			return null;
		}
		return new DetourPatch
		{
			Scheme = scheme,
			Bytes = memoryStream.ToArray()
		};
	}
}
