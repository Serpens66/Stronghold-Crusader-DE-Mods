using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Logging;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;

namespace RedBird.X64.Hooks;

public sealed class X64InlineHook : IHook, IDisposable
{
	private const int Bitness = 64;

	private const int AbsoluteJumpSize = 14;

	private readonly ILogger _logger;

	private readonly ulong _targetAddress;

	private readonly Assembler? _asm;

	private readonly List<Delegate> _pinnedDelegates = new List<Delegate>();

	private Instruction[] _overwrittenInstructions = Array.Empty<Instruction>();

	private byte[] _originalBytesBackup = Array.Empty<byte>();

	private int _displacedByteCount;

	private IntPtr _stubAddress = IntPtr.Zero;

	public string Name { get; }

	public ulong TargetAddress => _targetAddress;

	public HookState State { get; private set; }

	public bool IsInstalled => State == HookState.Installed;

	public IntPtr StubAddress => _stubAddress;

	public int DisplacedByteCount => _displacedByteCount;

	public X64InlineHook(ulong targetAddress, int minHookSize, ILogger? logger = null, string? name = null)
	{
		_logger = logger ?? NullLogger.Instance;
		Name = name ?? $"inline@0x{targetAddress:X16}";
		if (targetAddress == 0L)
		{
			State = HookState.Failed;
			return;
		}
		_targetAddress = targetAddress;
		_asm = new Assembler(64);
		DecodeDisplacedInstructions(Math.Max(minHookSize, 14));
		CaptureOriginalBytes();
	}

	private unsafe void DecodeDisplacedInstructions(int minBytes)
	{
		MemoryCodeReader reader = new MemoryCodeReader((byte*)_targetAddress, minBytes + 16);
		Decoder decoder = Decoder.Create(64, reader, _targetAddress);
		List<Instruction> list = new List<Instruction>();
		int i;
		Instruction item;
		for (i = 0; i < minBytes; i += item.Length)
		{
			item = decoder.Decode();
			if (item.IsInvalid)
			{
				break;
			}
			list.Add(item);
		}
		_overwrittenInstructions = list.ToArray();
		_displacedByteCount = i;
		if (i < 14)
		{
			State = HookState.Failed;
			throw new InvalidOperationException($"Only {i} bytes could be decoded at 0x{_targetAddress:X16}, but an absolute JMP needs {14}. Installing here would overwrite bytes that cannot be restored.");
		}
	}

	private void CaptureOriginalBytes()
	{
		if (_displacedByteCount != 0)
		{
			_originalBytesBackup = new byte[_displacedByteCount];
			Marshal.Copy((IntPtr)(long)_targetAddress, _originalBytesBackup, 0, _displacedByteCount);
		}
	}

	public void Generate(InlineHookGenerator generator)
	{
		if (_targetAddress != 0L && _asm != null)
		{
			ulong num = _targetAddress + (ulong)_displacedByteCount;
			generator(_asm, _overwrittenInstructions.AsSpan(), num);
			_asm.AddUnrestrictedJmp(num);
			Compile();
			State = HookState.Prepared;
		}
	}

	private void Compile()
	{
		using MemoryStream memoryStream = new MemoryStream();
		StreamCodeWriter writer = new StreamCodeWriter(memoryStream);
		int num = _asm.Instructions.Count * 15 + 64;
		_stubAddress = NativeMemoryManager.AllocateStub(_targetAddress, num);
		if (_stubAddress == IntPtr.Zero)
		{
			State = HookState.Failed;
			throw new InvalidOperationException($"Failed to allocate a stub near 0x{_targetAddress:X16}: no free region within +/-1.75 GB.");
		}
		if (!_asm.TryAssemble(writer, (ulong)(long)_stubAddress, out string errorMessage, out AssemblerResult _))
		{
			State = HookState.Failed;
			throw new InvalidOperationException($"Iced failed to assemble the stub for 0x{_targetAddress:X16}: {errorMessage}");
		}
		byte[] array = memoryStream.ToArray();
		if (array.Length > num)
		{
			State = HookState.Failed;
			throw new InvalidOperationException($"Stub for 0x{_targetAddress:X16} encoded to {array.Length} bytes but only {num} were reserved.");
		}
		if (!NativeMemoryManager.WriteStub(_stubAddress, (ReadOnlySpan<byte>)array))
		{
			State = HookState.Failed;
			throw new InvalidOperationException($"Could not write the stub for 0x{_targetAddress:X16}.");
		}
		_logger.InlineHookStubCompiled(Name, (ulong)(long)_stubAddress, array.Length, _targetAddress);
	}

	public unsafe void Enable()
	{
		HookState state = State;
		if ((state != HookState.Installed && (uint)(state - 4) > 1u) || 1 == 0)
		{
			if (_targetAddress == 0L || _stubAddress == IntPtr.Zero)
			{
				throw new InvalidOperationException("Hook [" + Name + "] has no stub; call Generate before Enable.");
			}
			byte[] array = new byte[_displacedByteCount];
			array[0] = byte.MaxValue;
			array[1] = 37;
			fixed (byte* ptr = array)
			{
				*(long*)(ptr + 6) = (long)_stubAddress;
			}
			for (int i = 14; i < array.Length; i++)
			{
				array[i] = 144;
			}
			CodePatch.Write(_targetAddress, array);
			State = HookState.Installed;
			_logger.InlineHookEnabled(Name, _targetAddress, (ulong)(long)_stubAddress);
		}
	}

	public void Disable()
	{
		if (State == HookState.Installed && _targetAddress != 0L)
		{
			RestoreOriginalBytes();
			State = HookState.Disabled;
			_logger.InlineHookDisabled(Name, _targetAddress);
		}
	}

	private void RestoreOriginalBytes()
	{
		if (_originalBytesBackup.Length == 0)
		{
			_logger.LogError("[{Name}] no byte snapshot for 0x{TargetAddress:X16}; refusing to restore and leaving the hook installed.", Name, _targetAddress);
			return;
		}
		try
		{
			CodePatch.Write(_targetAddress, _originalBytesBackup);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "[{Name}] failed to restore the original bytes at 0x{TargetAddress:X16}.", Name, _targetAddress);
		}
	}

	public IntPtr PinDelegate(Delegate callback)
	{
		_pinnedDelegates.Add(callback);
		return Marshal.GetFunctionPointerForDelegate(callback);
	}

	public void Dispose()
	{
		if (State == HookState.Disposed)
		{
			return;
		}
		try
		{
			Disable();
		}
		finally
		{
			_pinnedDelegates.Clear();
			_stubAddress = IntPtr.Zero;
			State = HookState.Disposed;
		}
	}
}
