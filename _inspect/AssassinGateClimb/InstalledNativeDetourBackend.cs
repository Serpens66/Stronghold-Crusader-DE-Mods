using System;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;

namespace RedBird.Backends.NativeX64;

public sealed class NativeDetourBackend : IHookBackend
{
	private readonly NativeDetourOptions _options;

	public static NativeDetourBackend Instance { get; } = new NativeDetourBackend();

	public string Name => "NativeX64";

	public NativeDetourOptions Options => _options;

	public NativeDetourBackend()
		: this(NativeDetourOptions.Default)
	{
	}

	public NativeDetourBackend(NativeDetourOptions options)
	{
		_options = options ?? throw new ArgumentNullException("options");
		if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
		{
			throw new PlatformNotSupportedException(string.Format("{0} emits x86-64 machine code; this process is {1}.", "NativeDetourBackend", RuntimeInformation.ProcessArchitecture));
		}
	}

	public IDetour<TFunction> CreateDetour<TFunction>(in DetourRequest<TFunction> request) where TFunction : Delegate
	{
		if (request.TargetAddress == 0L)
		{
			throw new ArgumentException("Detour target address is zero.", "request");
		}
		if ((object)request.Callback == null)
		{
			throw new ArgumentException("Detour callback is null.", "request");
		}
		return new NativeDetour<TFunction>(in request, _options);
	}

	IDetour<TFunction> IHookBackend.CreateDetour<TFunction>(in DetourRequest<TFunction> request)
	{
		return CreateDetour(in request);
	}
}
