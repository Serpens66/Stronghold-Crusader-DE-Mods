using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction.Descriptors;

namespace RedBird.X64.Hooks.Transaction;

public sealed class HookTransaction : IDisposable
{
	private readonly ScanRegion _region;

	private readonly bool _ownsRegion;

	private readonly ILoggerFactory? _loggerFactory;

	private readonly ILogger _logger;

	private readonly List<HookDescriptor> _descriptors = new List<HookDescriptor>();

	private readonly List<IHook> _hooks = new List<IHook>();

	private bool _disposed;

	public string Name { get; }

	public HookTransactionOptions Options { get; }

	public TransactionState State { get; private set; }

	public CommitResult? Result { get; private set; }

	public IReadOnlyList<IHook> Hooks => new ReadOnlyCollection<IHook>(_hooks);

	internal IReadOnlyList<HookDescriptor> Descriptors => _descriptors;

	internal List<IHook> HookSink => _hooks;

	public HookTransaction(ScanRegion region, ILoggerFactory? loggerFactory = null, HookTransactionOptions? options = null, [CallerMemberName] string name = "")
	{
		_region = region ?? throw new ArgumentNullException("region");
		_loggerFactory = loggerFactory;
		_logger = loggerFactory?.CreateLogger("HookTransaction[" + name + "]") ?? NullLogger.Instance;
		Options = options ?? HookTransactionOptions.Default;
		Name = name;
	}

	[Obsolete("Copies the whole region. Use ScanRegion.FromModule/FromPointer to wrap live memory instead, and share one region across all your transactions.")]
	public HookTransaction(ReadOnlySpan<byte> memory, ulong imageBase, ILoggerFactory? loggerFactory = null, TransactionFailureMode failureMode = TransactionFailureMode.ContinueOnError)
		: this(ScanRegion.Copy(memory, imageBase), loggerFactory, new HookTransactionOptions
		{
			FailureMode = failureMode
		}, ".ctor")
	{
		_ownsRegion = true;
	}

	internal void BeginSessionCommit()
	{
		ThrowIfNotBuilding();
		State = TransactionState.Committing;
	}

	internal void CompleteSessionCommit(CommitResult result, bool rolledBack = false)
	{
		Result = result;
		State = (rolledBack ? TransactionState.RolledBack : TransactionState.Committed);
	}

	public DetourHandle<TFunction> AddDetour<TFunction>(DetourHandle<TFunction> handle, HookTarget target, TFunction callback, [CallerArgumentExpression("handle")] string name = "") where TFunction : Delegate
	{
		DetourHandleNullThrow(handle, name);
		ArgumentNullThrow(callback, "callback");
		Register(new DetourDescriptor<TFunction>(name, target, handle, callback), handle, name);
		return handle;
	}

	public DetourHandle<TFunction> AddDetour<TFunction>(HookTarget target, TFunction callback, [CallerArgumentExpression("callback")] string name = "") where TFunction : Delegate
	{
		return AddDetour(new DetourHandle<TFunction>(), target, callback, name);
	}

	public DetourHandle<TFunction> AddDetour<TFunction>(DetourHandle<TFunction> handle, HookTarget target, TFunction callback, IDetourIntermediaryFactory intermediaryFactory, [CallerArgumentExpression("handle")] string name = "") where TFunction : Delegate
	{
		DetourHandleNullThrow(handle, name);
		ArgumentNullThrow(callback, "callback");
		ArgumentNullThrow(intermediaryFactory, "intermediaryFactory");
		Register(new DetourDescriptor<TFunction>(name, target, handle, callback, intermediaryFactory), handle, name);
		return handle;
	}

	public DetourHandle<TFunction> AddDetour<TFunction>(HookTarget target, TFunction callback, IDetourIntermediaryFactory intermediaryFactory, [CallerArgumentExpression("callback")] string name = "") where TFunction : Delegate
	{
		return AddDetour(new DetourHandle<TFunction>(), target, callback, intermediaryFactory, name);
	}

	public HookHandle<X64InlineHook> AddInline(HookHandle<X64InlineHook> handle, HookTarget target, InlineHookGenerator generator, int hookSize = 14, [CallerArgumentExpression("handle")] string name = "")
	{
		ArgumentNullThrow(handle, "handle");
		ArgumentNullThrow(generator, "generator");
		Register(new InlineDescriptor(name, target, handle, generator, hookSize), handle, name);
		return handle;
	}

	public HookHandle<X64AssemblyPatch> AddAssemblyPatch(HookHandle<X64AssemblyPatch> handle, HookTarget target, AssemblyPatchGenerator generator, int maxByteCount = 0, [CallerArgumentExpression("handle")] string name = "")
	{
		ArgumentNullThrow(handle, "handle");
		ArgumentNullThrow(generator, "generator");
		Register(new AssemblyPatchDescriptor(name, target, handle, generator, maxByteCount), handle, name);
		return handle;
	}

	public HookHandle<X64AssemblyPatch> AddAssemblyPatch(HookTarget target, AssemblyPatchGenerator generator, int maxByteCount = 0, [CallerArgumentExpression("generator")] string name = "")
	{
		return AddAssemblyPatch(new HookHandle<X64AssemblyPatch>(), target, generator, maxByteCount, name);
	}

	public HookHandle<X64AssemblyPatch> AddBytePatch(HookHandle<X64AssemblyPatch> handle, HookTarget target, ReadOnlySpan<byte> bytes, int maxByteCount = 0, [CallerArgumentExpression("handle")] string name = "")
	{
		ArgumentNullThrow(handle, "handle");
		if (bytes.IsEmpty)
		{
			throw new ArgumentException("The byte patch is empty; there is nothing to write.", "bytes");
		}
		byte[] copy = bytes.ToArray();
		return AddAssemblyPatch(handle, target, delegate(Assembler asm, ulong _)
		{
			asm.db(copy);
		}, maxByteCount, name);
	}

	public HookHandle<X64InlineHook> AddContextHook(HookHandle<X64InlineHook> handle, HookTarget target, ContextHookDelegate callback, ContextHookOptions? options = null, [CallerArgumentExpression("handle")] string name = "")
	{
		ArgumentNullThrow(handle, "handle");
		ArgumentNullThrow(callback, "callback");
		Register(new ContextHookDescriptor(name, target, handle, callback, options ?? ContextHookOptions.Default), handle, name);
		return handle;
	}

	public HookHandle<X64InlineHook> AddContextHook(HookTarget target, ContextHookDelegate callback, ContextHookOptions? options = null, [CallerArgumentExpression("callback")] string name = "")
	{
		return AddContextHook(new HookHandle<X64InlineHook>(), target, callback, options, name);
	}

	public HookHandle<X64FunctionCloneHook> AddCloneHook(HookHandle<X64FunctionCloneHook> handle, HookTarget target, IEnumerable<FunctionClonePatch> patches, [CallerArgumentExpression("handle")] string name = "")
	{
		ArgumentNullThrow(handle, "handle");
		ArgumentNullThrow(patches, "patches");
		Register(new CloneHookDescriptor(name, target, handle, patches), handle, name);
		return handle;
	}

	private void Register<THook>(HookDescriptor descriptor, HookHandle<THook> handle, string name) where THook : class, IHook
	{
		ThrowIfNotBuilding();
		if (descriptor.Target.RequiresScan)
		{
			descriptor.Pattern = CompiledPattern.Parse(descriptor.Target.Text);
		}
		handle.Register(name);
		_descriptors.Add(descriptor);
	}

	public CommitResult Commit()
	{
		ThrowIfNotBuilding();
		State = TransactionState.Committing;
		try
		{
			Result = HookCommitPipeline.Execute(_region, _descriptors, Options, _loggerFactory, _logger, _hooks);
			State = TransactionState.Committed;
			return Result;
		}
		catch
		{
			State = TransactionState.RolledBack;
			throw;
		}
	}

	public void EnableAll()
	{
		foreach (IHook hook in _hooks)
		{
			try
			{
				hook.Enable();
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "[{Name}] failed to enable.", hook.Name);
			}
		}
	}

	public void DisableAll()
	{
		for (int num = _hooks.Count - 1; num >= 0; num--)
		{
			try
			{
				_hooks[num].Disable();
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "[{Name}] failed to disable.", _hooks[num].Name);
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		if (Options.OwnsHooks)
		{
			for (int num = _hooks.Count - 1; num >= 0; num--)
			{
				try
				{
					_hooks[num].Dispose();
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "[{Name}] failed to dispose.", _hooks[num].Name);
				}
			}
			_hooks.Clear();
		}
		if (_ownsRegion)
		{
			_region.Dispose();
		}
	}

	private void ThrowIfNotBuilding()
	{
		if (State != TransactionState.Building)
		{
			throw new InvalidOperationException($"Hook transaction [{Name}] is {State}; registrations are closed and it cannot be committed again.");
		}
	}

	private static void ArgumentNullThrow(object? value, string parameterName)
	{
		if (value == null)
		{
			throw new ArgumentNullException(parameterName);
		}
	}

	private static void DetourHandleNullThrow<TFunction>(DetourHandle<TFunction>? handle, string expression) where TFunction : Delegate
	{
		if (handle != null)
		{
			return;
		}
		string text = (string.IsNullOrWhiteSpace(expression) ? "the supplied handle" : ("[" + expression + "]"));
		throw new ArgumentNullException("handle", "Detour handle " + text + " is null. Initialize the field with new DetourHandle<" + typeof(TFunction).Name + ">(), or call the overload which starts with HookTarget and use its returned handle. Registration validates the handle before resolving the target, so an export target has not been looked up yet.");
	}
}
