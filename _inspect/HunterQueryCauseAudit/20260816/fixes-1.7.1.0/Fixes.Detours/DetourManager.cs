using System;
using System.Collections.Generic;

namespace Fixes.Detours;

internal class DetourManager
{
	private static readonly Lazy<DetourManager> _lazy = new Lazy<DetourManager>(() => new DetourManager());

	internal List<IDisposable> nativeDetours;

	public static DetourManager Instance => _lazy.Value;

	private DetourManager()
	{
		nativeDetours = new List<IDisposable>();
	}

	internal void ApplyNative(IntPtr libraryHandle, System.ReadOnlySpan<byte> memory)
	{
		nativeDetours.AddRange(new _003C_003Ez__ReadOnlyArray<IDisposable>(new IDisposable[2]
		{
			new FixesBuildingDetours(libraryHandle, memory),
			new FixesAIDetours(libraryHandle, memory)
		}));
	}

	internal void Unload()
	{
		foreach (IDisposable nativeDetour in nativeDetours)
		{
			nativeDetour?.Dispose();
		}
		nativeDetours.Clear();
	}
}
