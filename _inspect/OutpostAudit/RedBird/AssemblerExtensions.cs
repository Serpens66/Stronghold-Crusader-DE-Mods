using System;
using System.ComponentModel;
using Iced.Intel;

namespace RedBird.X64.Extensions;

public static class AssemblerExtensions
{
	public enum X64ArgumentSourceType
	{
		FromOriginalStack,
		FromImmediate
	}

	public record X64StackArgument(X64ArgumentSourceType SourceType, ulong Value);

	public static void AddUnrestrictedJmp(this Assembler assembler, ulong address)
	{
		byte[] array = new byte[6] { 255, 37, 0, 0, 0, 0 };
		assembler.db(array);
		assembler.dq(address);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void AddUnrestrictedCallRaw(this Assembler assembler, ulong address)
	{
		byte[] array = new byte[6] { 255, 21, 0, 0, 0, 0 };
		assembler.db(array);
		assembler.dq(address);
	}

	public static void AddUnrestrictedCall(this Assembler assembler, ulong address)
	{
		Label label = assembler.CreateLabel();
		Label label2 = assembler.CreateLabel();
		assembler.jmp(label);
		assembler.Label(ref label2);
		assembler.dq(address);
		assembler.Label(ref label);
		assembler.call(AssemblerRegisters.__qword_ptr[label2]);
	}

	public static void AddInstructions(this Assembler assembler, Instruction[] instructions)
	{
		foreach (Instruction instruction in instructions)
		{
			assembler.AddInstruction(instruction);
		}
	}

	public static void AddInstructions(this Assembler assembler, ReadOnlySpan<Instruction> instructions)
	{
		ReadOnlySpan<Instruction> readOnlySpan = instructions;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			Instruction instruction = readOnlySpan[i];
			assembler.AddInstruction(instruction);
		}
	}

	public static void PushAllGPR(this Assembler assembler)
	{
		assembler.push(AssemblerRegisters.rax);
		assembler.push(AssemblerRegisters.rbx);
		assembler.push(AssemblerRegisters.rcx);
		assembler.push(AssemblerRegisters.rdx);
		assembler.push(AssemblerRegisters.rsi);
		assembler.push(AssemblerRegisters.rdi);
		assembler.push(AssemblerRegisters.rbp);
		assembler.push(AssemblerRegisters.r8);
		assembler.push(AssemblerRegisters.r9);
		assembler.push(AssemblerRegisters.r10);
		assembler.push(AssemblerRegisters.r11);
		assembler.push(AssemblerRegisters.r12);
		assembler.push(AssemblerRegisters.r13);
		assembler.push(AssemblerRegisters.r14);
		assembler.push(AssemblerRegisters.r15);
	}

	public static void PopAllGPR(this Assembler assembler)
	{
		assembler.pop(AssemblerRegisters.r15);
		assembler.pop(AssemblerRegisters.r14);
		assembler.pop(AssemblerRegisters.r13);
		assembler.pop(AssemblerRegisters.r12);
		assembler.pop(AssemblerRegisters.r11);
		assembler.pop(AssemblerRegisters.r10);
		assembler.pop(AssemblerRegisters.r9);
		assembler.pop(AssemblerRegisters.r8);
		assembler.pop(AssemblerRegisters.rbp);
		assembler.pop(AssemblerRegisters.rdi);
		assembler.pop(AssemblerRegisters.rsi);
		assembler.pop(AssemblerRegisters.rdx);
		assembler.pop(AssemblerRegisters.rcx);
		assembler.pop(AssemblerRegisters.rbx);
		assembler.pop(AssemblerRegisters.rax);
	}

	public static void PushAllVolatile(this Assembler assembler, bool preserveRAX = true)
	{
		if (preserveRAX)
		{
			assembler.push(AssemblerRegisters.rax);
		}
		assembler.push(AssemblerRegisters.rcx);
		assembler.push(AssemblerRegisters.rdx);
		assembler.push(AssemblerRegisters.r8);
		assembler.push(AssemblerRegisters.r9);
		assembler.push(AssemblerRegisters.r10);
		assembler.push(AssemblerRegisters.r11);
	}

	public static void PopAllVolatile(this Assembler assembler, bool preserveRAX = true)
	{
		assembler.pop(AssemblerRegisters.r11);
		assembler.pop(AssemblerRegisters.r10);
		assembler.pop(AssemblerRegisters.r9);
		assembler.pop(AssemblerRegisters.r8);
		assembler.pop(AssemblerRegisters.rdx);
		assembler.pop(AssemblerRegisters.rcx);
		if (preserveRAX)
		{
			assembler.pop(AssemblerRegisters.rax);
		}
	}

	public static void PushX64FastcallRegisters(this Assembler assembler)
	{
		assembler.push(AssemblerRegisters.rcx);
		assembler.push(AssemblerRegisters.rdx);
		assembler.push(AssemblerRegisters.r8);
		assembler.push(AssemblerRegisters.r9);
	}

	public static void PopX64FastcallRegisters(this Assembler assembler)
	{
		assembler.pop(AssemblerRegisters.r9);
		assembler.pop(AssemblerRegisters.r8);
		assembler.pop(AssemblerRegisters.rdx);
		assembler.pop(AssemblerRegisters.rcx);
	}

	public static void PushVolatileXMM(this Assembler assembler)
	{
		AssemblerRegisterXMM[] array = new AssemblerRegisterXMM[6]
		{
			AssemblerRegisters.xmm0,
			AssemblerRegisters.xmm1,
			AssemblerRegisters.xmm2,
			AssemblerRegisters.xmm3,
			AssemblerRegisters.xmm4,
			AssemblerRegisters.xmm5
		};
		int imm = array.Length * 16;
		assembler.sub(AssemblerRegisters.rsp, imm);
		for (int i = 0; i < array.Length; i++)
		{
			assembler.movdqu(AssemblerRegisters.__xmmword_ptr[AssemblerRegisters.rsp + i * 16], array[i]);
		}
	}

	public static void PopVolatileXMM(this Assembler assembler)
	{
		AssemblerRegisterXMM[] array = new AssemblerRegisterXMM[6]
		{
			AssemblerRegisters.xmm0,
			AssemblerRegisters.xmm1,
			AssemblerRegisters.xmm2,
			AssemblerRegisters.xmm3,
			AssemblerRegisters.xmm4,
			AssemblerRegisters.xmm5
		};
		int imm = array.Length * 16;
		for (int num = array.Length - 1; num >= 0; num--)
		{
			assembler.movdqu(array[num], AssemblerRegisters.__xmmword_ptr[AssemblerRegisters.rsp + num * 16]);
		}
		assembler.add(AssemblerRegisters.rsp, imm);
	}

	public static void X64FastcallSafe(this Assembler assembler, ulong targetAddress, int totalArgumentCount, Action<Assembler>? prepareArgumentsAction = null, bool preserveRAX = true, bool preserveXMM = false, params X64StackArgument[] stackArguments)
	{
		if (totalArgumentCount < 0)
		{
			throw new ArgumentOutOfRangeException("totalArgumentCount");
		}
		if (stackArguments == null)
		{
			throw new ArgumentNullException("stackArguments");
		}
		int num = Math.Max(0, totalArgumentCount - 4);
		if (num != stackArguments.Length)
		{
			throw new ArgumentException("The number of provided stack arguments does not match the expected count.", "stackArguments");
		}
		int num2 = (preserveRAX ? 7 : 6) * 8;
		int num3 = (preserveXMM ? 96 : 0);
		checked
		{
			int num4 = 32 + num * 8;
			num4 = (num4 + 15) & -16;
			int imm = num4 + 16;
			foreach (X64StackArgument x64StackArgument in stackArguments)
			{
				if ((object)x64StackArgument == null)
				{
					throw new ArgumentException("A stack-argument descriptor cannot be null.", "stackArguments");
				}
				X64ArgumentSourceType sourceType = x64StackArgument.SourceType;
				if (sourceType != X64ArgumentSourceType.FromOriginalStack && sourceType != X64ArgumentSourceType.FromImmediate)
				{
					throw new ArgumentOutOfRangeException("stackArguments", $"Unknown x64 argument source {x64StackArgument.SourceType}.");
				}
				if (x64StackArgument.SourceType == X64ArgumentSourceType.FromOriginalStack)
				{
					if (x64StackArgument.Value > int.MaxValue)
					{
						throw new ArgumentOutOfRangeException("stackArguments", "An original stack index is too large.");
					}
					_ = num3 + num2 + 40 + (int)x64StackArgument.Value * 8;
				}
			}
			assembler.PushAllVolatile(preserveRAX);
			if (preserveXMM)
			{
				assembler.PushVolatileXMM();
			}
			prepareArgumentsAction?.Invoke(assembler);
			assembler.mov(AssemblerRegisters.r11, AssemblerRegisters.rsp);
			assembler.and(AssemblerRegisters.rsp, -16);
			assembler.sub(AssemblerRegisters.rsp, imm);
			assembler.mov(AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp + num4], AssemblerRegisters.r11);
			for (int j = 0; j < stackArguments.Length; j = unchecked(j + 1))
			{
				X64StackArgument x64StackArgument2 = stackArguments[j];
				int num5 = 32 + j * 8;
				switch (x64StackArgument2.SourceType)
				{
				case X64ArgumentSourceType.FromOriginalStack:
				{
					int num6 = unchecked((int)x64StackArgument2.Value);
					int num7 = num3 + num2 + 40 + num6 * 8;
					assembler.mov(AssemblerRegisters.rax, AssemblerRegisters.__qword_ptr[AssemblerRegisters.r11 + num7]);
					assembler.mov(AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp + num5], AssemblerRegisters.rax);
					break;
				}
				case X64ArgumentSourceType.FromImmediate:
					assembler.mov(AssemblerRegisters.rax, x64StackArgument2.Value);
					assembler.mov(AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp + num5], AssemblerRegisters.rax);
					break;
				}
			}
			assembler.AddUnrestrictedCall(targetAddress);
			assembler.mov(AssemblerRegisters.rsp, AssemblerRegisters.__qword_ptr[AssemblerRegisters.rsp + num4]);
			if (preserveXMM)
			{
				assembler.PopVolatileXMM();
			}
			assembler.PopAllVolatile(preserveRAX);
		}
	}

	public static void X64AlignedCall(this Assembler assembler, ulong targetAddress)
	{
		assembler.push(AssemblerRegisters.rbp);
		assembler.mov(AssemblerRegisters.rbp, AssemblerRegisters.rsp);
		assembler.and(AssemblerRegisters.rsp, -16);
		assembler.sub(AssemblerRegisters.rsp, 32);
		assembler.AddUnrestrictedCall(targetAddress);
		assembler.mov(AssemblerRegisters.rsp, AssemblerRegisters.rbp);
		assembler.pop(AssemblerRegisters.rbp);
	}

	public static void RelocateRegistersToX64FastcallRegisterArgs(this Assembler assembler, params AssemblerRegister64[] sourceRegisters)
	{
		if (sourceRegisters == null || sourceRegisters.Length == 0)
		{
			return;
		}
		if (sourceRegisters.Length > 4)
		{
			throw new ArgumentException("Cannot pass more than 4 GPR arguments with this helper.", "sourceRegisters");
		}
		Register[] array = new Register[4]
		{
			Register.RCX,
			Register.RDX,
			Register.R8,
			Register.R9
		};
		for (int i = 0; i < sourceRegisters.Length; i++)
		{
			AssemblerRegister64 assemblerRegister = sourceRegisters[i];
			AssemblerRegister64 assemblerRegister2 = new AssemblerRegister64(array[i]);
			if (assemblerRegister != assemblerRegister2)
			{
				assembler.mov(assemblerRegister2, assemblerRegister);
			}
		}
	}
}
