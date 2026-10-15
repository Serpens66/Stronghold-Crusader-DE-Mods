using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RedBird.Core.Platform;

namespace RedBird.Backends.NativeX64;

public sealed record NativeDetourOptions
{
	public static NativeDetourOptions Default { get; } = new NativeDetourOptions();

	public DetourScheme AllowedSchemes { get; init; } = DetourScheme.Recommended;

	public bool FollowJumps { get; init; } = true;

	public bool FollowCalls { get; init; }

	public int MaxFollowDepth { get; init; } = 5;

	public int FunctionScanLimit { get; init; } = 16384;

	public HookChainingPolicy Chaining { get; init; }

	public bool SafeUnhook { get; init; } = true;

	public bool SuspendThreadsDuringPatch { get; init; } = PlatformInfo.IsWindows || (PlatformInfo.IsLinux && ProcMaps.IsAvailable && RuntimeInformation.ProcessArchitecture == Architecture.X64);

	public int LocalThunkRange { get; init; } = 4096;

	public ILoggerFactory? LoggerFactory { get; init; }

	internal DetourScheme[] ResolveOrder()
	{
		List<DetourScheme> list = new List<DetourScheme>();
		if ((AllowedSchemes & DetourScheme.Indirect) != DetourScheme.None)
		{
			list.Add(DetourScheme.Indirect);
		}
		if ((AllowedSchemes & DetourScheme.Absolute) != DetourScheme.None)
		{
			list.Add(DetourScheme.Absolute);
		}
		if ((AllowedSchemes & DetourScheme.PushRet) != DetourScheme.None && !PlatformInfo.HasRedZone)
		{
			list.Add(DetourScheme.PushRet);
		}
		if ((AllowedSchemes & DetourScheme.PushRetRedZoneSafe) != DetourScheme.None)
		{
			list.Add(DetourScheme.PushRetRedZoneSafe);
		}
		return list.ToArray();
	}
}
