using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;

namespace RedBird.Backends.NativeX64;

public sealed class NativeDetour<TFunction> : IDetour<TFunction>, IDetour, IHook, IDisposable where TFunction : Delegate
{
	private readonly ILogger _logger;

	private readonly NativeDetourOptions _options;

	private readonly TFunction _callback;

	private readonly byte[] _originalBytes;

	private readonly byte[] _patchBytes;

	private readonly InstructionPointerMap? _instructionPointers;

	private readonly object _gate = new object();

	private TFunction? _original;

	private DetourIntermediaryStubs? _intermediary;

	private ulong _registeredAddress;

	public string Name { get; }

	public ulong TargetAddress { get; }

	public HookState State { get; private set; }

	public bool IsInstalled => State == HookState.Installed;

	public IntPtr TrampolineAddress { get; }

	public int TrampolineSize { get; }

	public IntPtr HookEntryPointAddress { get; }

	public IntPtr OriginalEntryPointAddress { get; }

	public DetourScheme Scheme { get; }

	public int DisplacedByteCount { get; }

	public IntPtr PointerSlot { get; }

	public int ChainDepth { get; private set; }

	internal int LastPatchSuspendedThreadCount { get; private set; }

	internal int LastPatchRelocatedThreadCount { get; private set; }

	public TFunction Original => _original ?? throw new InvalidOperationException($"Detour [{Name}] at 0x{TargetAddress:X16} has no trampoline.");

	internal NativeDetour(in DetourRequest<TFunction> request, NativeDetourOptions options)
	{
		Name = request.Name;
		_options = options;
		_callback = request.Callback;
		_logger = request.Logger ?? NullLogger.Instance;
		ulong targetAddress = request.TargetAddress;
		IntPtr functionPointerForDelegate = Marshal.GetFunctionPointerForDelegate(_callback);
		if (!PrologueAnalyzer.TryAnalyze(targetAddress, options, _logger, out ulong resolvedAddress, out List<Instruction> instructions, out Dictionary<ulong, List<Instruction>> branchSources))
		{
			throw new NotSupportedException($"Could not analyse the function at 0x{targetAddress:X16} for [{Name}].");
		}
		TargetAddress = resolvedAddress;
		if (resolvedAddress != targetAddress)
		{
			_logger.LogInformation("[{Name}]: followed a thunk from 0x{Requested:X16} to 0x{Resolved:X16}.", Name, targetAddress, resolvedAddress);
		}
		bool num = LooksAlreadyHooked(instructions);
		int depth = DetourRegistry.GetDepth(resolvedAddress);
		if ((num || depth > 0) && options.Chaining == HookChainingPolicy.Refuse)
		{
			throw new NotSupportedException($"0x{resolvedAddress:X16} is already detoured ({depth} known layer(s) from this engine) and Chaining is set to Refuse.");
		}
		if (num || depth > 0)
		{
			LogLevel logLevel = ((options.Chaining == HookChainingPolicy.WarnAndAllow) ? LogLevel.Warning : LogLevel.Information);
			_logger.Log(logLevel, "[{Name}] is layering on top of an existing detour at 0x{Address:X16} ({Layers} known layer(s)). Unhook in reverse order.", Name, resolvedAddress, depth);
		}
		List<string> list = new List<string>();
		DetourScheme[] array = options.ResolveOrder();
		foreach (DetourScheme detourScheme in array)
		{
			int patchSize = JumpFactory.GetPatchSize(detourScheme);
			if (!PrologueAnalyzer.TrySelectPrologue(resolvedAddress, instructions, branchSources, patchSize, _logger, out PrologueSelection selection) || selection == null)
			{
				list.Add($"{detourScheme}: the function does not expose {patchSize} safe bytes at an instruction boundary");
				_logger.LogDebug("[{Name}]: scheme {Scheme} needs {Size} bytes, which the prologue cannot give.", Name, detourScheme, patchSize);
				continue;
			}
			int trampolineSize;
			InstructionPointerMap instructionPointers;
			string failureReason;
			IntPtr intPtr = TrampolineBuilder.Build(selection, _logger, out trampolineSize, out instructionPointers, out failureReason);
			if (intPtr == IntPtr.Zero)
			{
				list.Add(string.Format("{0}: {1}", detourScheme, failureReason ?? "trampoline construction failed"));
				continue;
			}
			DetourIntermediaryStubs detourIntermediaryStubs = null;
			IntPtr intPtr2 = functionPointerForDelegate;
			IntPtr ptr = intPtr;
			if (request.IntermediaryFactory != null)
			{
				DetourIntermediaryContext context = new DetourIntermediaryContext(DetourArchitecture.X64, resolvedAddress, functionPointerForDelegate, intPtr);
				detourIntermediaryStubs = request.IntermediaryFactory.Create(in context) ?? throw new InvalidOperationException($"The intermediary factory returned null for [{Name}] at 0x{resolvedAddress:X16}.");
				intPtr2 = detourIntermediaryStubs.HookEntryPoint;
				ptr = detourIntermediaryStubs.OriginalEntryPoint;
			}
			string failureReason2;
			DetourPatch detourPatch = JumpFactory.TryBuild(detourScheme, resolvedAddress, (ulong)(long)intPtr2, out failureReason2);
			if (detourPatch == null)
			{
				detourIntermediaryStubs?.Dispose();
				list.Add(string.Format("{0}: {1}", detourScheme, failureReason2 ?? "entry-jump construction failed"));
				_logger.LogDebug("[{Name}]: scheme {Scheme} could not be built here.", Name, detourScheme);
				continue;
			}
			TrampolineAddress = intPtr;
			TrampolineSize = trampolineSize;
			HookEntryPointAddress = intPtr2;
			OriginalEntryPointAddress = ptr;
			Scheme = detourScheme;
			DisplacedByteCount = selection.Length;
			PointerSlot = detourPatch.PointerSlot;
			_intermediary = detourIntermediaryStubs;
			_instructionPointers = instructionPointers;
			_originalBytes = CodePatch.Read(resolvedAddress, selection.Length);
			_patchBytes = new byte[selection.Length];
			Array.Copy(detourPatch.Bytes, _patchBytes, detourPatch.Bytes.Length);
			for (int j = detourPatch.Bytes.Length; j < _patchBytes.Length; j++)
			{
				_patchBytes[j] = 144;
			}
			_original = Marshal.GetDelegateForFunctionPointer<TFunction>(ptr);
			_registeredAddress = resolvedAddress;
			ChainDepth = DetourRegistry.AddLayer(resolvedAddress);
			State = HookState.Prepared;
			_logger.LogInformation("[{Name}] prepared at 0x{Address:X16} using {Scheme}: {Displaced} bytes displaced, trampoline 0x{Trampoline:X16}.", Name, resolvedAddress, detourScheme, selection.Length, (ulong)(long)intPtr);
			return;
		}
		State = HookState.Failed;
		string text = ((list.Count == 0) ? "No schemes were enabled." : ("Attempts: " + string.Join("; ", list) + "."));
		throw new NotSupportedException($"No enabled detour scheme could be prepared at 0x{resolvedAddress:X16} for [{Name}]. " + text);
	}

	public void Enable()
	{
		lock (_gate)
		{
			HookState state = State;
			if ((state != HookState.Installed && (uint)(state - 4) > 1u) || 1 == 0)
			{
				ThreadPatchResult threadPatchResult = ApplyPatch(installing: true, _patchBytes);
				LastPatchSuspendedThreadCount = threadPatchResult.SuspendedThreadCount;
				LastPatchRelocatedThreadCount = threadPatchResult.RelocatedThreadCount;
				State = HookState.Installed;
				if (threadPatchResult.RelocatedThreadCount > 0)
				{
					_logger.LogDebug("[{Name}] redirected {Count} suspended thread(s) from the displaced prologue into its trampoline.", Name, threadPatchResult.RelocatedThreadCount);
				}
				_logger.LogDebug("[{Name}] installed at 0x{Address:X16}.", Name, TargetAddress);
			}
		}
	}

	public void Disable()
	{
		lock (_gate)
		{
			if (State == HookState.Installed)
			{
				if (_options.SafeUnhook && !TargetStillHoldsOurPatch())
				{
					_logger.LogError("[{Name}] cannot uninstall from 0x{Address:X16}: the entry point no longer holds this detour's patch, so another layer went on top. Unhook in reverse order, or set SafeUnhook to false to restore regardless.", Name, TargetAddress);
					return;
				}
				LastPatchSuspendedThreadCount = ApplyPatch(installing: false, _originalBytes).SuspendedThreadCount;
				LastPatchRelocatedThreadCount = 0;
				State = HookState.Disabled;
				_logger.LogDebug("[{Name}] removed from 0x{Address:X16}.", Name, TargetAddress);
			}
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (State == HookState.Disposed)
			{
				return;
			}
			try
			{
				Disable();
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "[{Name}] failed to uninstall during dispose.", Name);
			}
			if (State == HookState.Installed && _registeredAddress != 0L && DetourRegistry.GetDepth(_registeredAddress) > ChainDepth)
			{
				return;
			}
			_original = null;
			_intermediary?.Dispose();
			_intermediary = null;
			State = HookState.Disposed;
			if (_registeredAddress != 0L)
			{
				DetourRegistry.RemoveLayer(_registeredAddress);
				_registeredAddress = 0uL;
			}
		}
		GC.SuppressFinalize(this);
	}

	private unsafe bool TargetStillHoldsOurPatch()
	{
		byte* ptr = (byte*)TargetAddress;
		for (int i = 0; i < _patchBytes.Length; i++)
		{
			if (ptr[i] != _patchBytes[i])
			{
				return false;
			}
		}
		return true;
	}

	private ThreadPatchResult ApplyPatch(bool installing, byte[] bytes)
	{
		if (!_options.SuspendThreadsDuringPatch)
		{
			CodePatch.Write(TargetAddress, bytes);
			return default(ThreadPatchResult);
		}
		return ThreadPatchScope.Apply(_instructionPointers ?? throw new InvalidOperationException("The detour has no instruction-pointer relocation map."), installing, TargetAddress, bytes);
	}

	private static bool LooksAlreadyHooked(List<Instruction> instructions)
	{
		if (instructions.Count == 0)
		{
			return false;
		}
		FlowControl flowControl = instructions[0].FlowControl;
		if ((uint)(flowControl - 1) > 1u)
		{
			return false;
		}
		return true;
	}

	public override string ToString()
	{
		return $"{Name} @ 0x{TargetAddress:X16} [{Scheme}, {DisplacedByteCount} bytes, {State}]";
	}
}
