using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBird.Core.PInvoke;
using RedBird.Core.Platform;

namespace RedBird.Core.Memory;

public static class NativeMemoryManager
{
	private sealed class StubSlab(IntPtr baseAddress, bool isWriteExecute)
	{
		public IntPtr BaseAddress { get; } = baseAddress;

		public bool IsWriteExecute { get; } = isWriteExecute;

		public int Offset { get; set; }

		public bool HasSpace(int size)
		{
			return Offset + size <= 65536;
		}

		public bool Reaches(ulong targetAddress)
		{
			ulong num = (ulong)(long)BaseAddress;
			return ((num > targetAddress) ? (num - targetAddress) : (targetAddress - num)) < 1879048192;
		}

		public bool Contains(IntPtr address, int length)
		{
			ulong num = (ulong)(long)address;
			ulong num2 = (ulong)(long)BaseAddress;
			ulong num3 = num2 + 65536;
			ulong num4 = num + (ulong)length;
			if (num >= num2 && num4 >= num)
			{
				return num4 <= num3;
			}
			return false;
		}
	}

	private const int SlabSize = 65536;

	private const int SlabAlignment = 16;

	private const ulong RipReach = 1879048192uL;

	private const int MaxPlacementAttempts = 32;

	private static readonly object _slabLock = new object();

	private static readonly List<StubSlab> _slabs = new List<StubSlab>();

	[ThreadStatic]
	private static string? _lastAllocationFailure;

	internal static string? LastAllocationFailure => _lastAllocationFailure;

	public static bool CanQueryProtection
	{
		get
		{
			if (!PlatformInfo.IsWindows)
			{
				return ProcMaps.IsAvailable;
			}
			return true;
		}
	}

	public static MemoryProtection AssumedProtection { get; set; } = MemoryProtection.ExecuteRead;

	public static IntPtr AllocateStub(ulong targetAddress, int size)
	{
		return AllocateStubCore(targetAddress, size, requireNear: true);
	}

	public static IntPtr AllocateStub(int size)
	{
		return AllocateStubCore(0uL, size, requireNear: false);
	}

	private static IntPtr AllocateStubCore(ulong targetAddress, int size, bool requireNear)
	{
		_lastAllocationFailure = null;
		if (size <= 0)
		{
			throw new ArgumentOutOfRangeException("size", "A stub must contain at least one byte.");
		}
		int num = (int)(((long)size + 16L - 1) & -16);
		if (num > 65536)
		{
			throw new ArgumentOutOfRangeException("size", $"A single stub cannot exceed the {65536} byte slab size.");
		}
		lock (_slabLock)
		{
			for (int num2 = _slabs.Count - 1; num2 >= 0; num2--)
			{
				StubSlab stubSlab = _slabs[num2];
				if (stubSlab.HasSpace(num) && (!requireNear || stubSlab.Reaches(targetAddress)))
				{
					IntPtr result = stubSlab.BaseAddress + stubSlab.Offset;
					stubSlab.Offset += num;
					return result;
				}
			}
			IntPtr intPtr;
			if (requireNear)
			{
				ulong low = ((targetAddress > 1879048192) ? (targetAddress - 1879048192) : 0);
				ulong high = ((targetAddress > 18446744071830503423uL) ? ulong.MaxValue : (targetAddress + 1879048192));
				intPtr = (IntPtr)(long)BoundAlloc(low, high, 65536uL, MemoryProtection.ExecuteReadWrite);
			}
			else
			{
				intPtr = Alloc(65536);
			}
			if (intPtr == IntPtr.Zero)
			{
				if (_lastAllocationFailure == null)
				{
					_lastAllocationFailure = (requireNear ? $"No {65536}-byte executable slab could be placed near 0x{targetAddress:X16}." : $"The operating system refused an unconstrained {65536}-byte executable slab.");
				}
				return IntPtr.Zero;
			}
			MemoryProtection oldProtection;
			bool flag = Protect(intPtr, 65536, MemoryProtection.ExecuteReadWrite, out oldProtection);
			if (!flag && !Protect(intPtr, 65536, MemoryProtection.ExecuteRead, out oldProtection))
			{
				Free(intPtr, 65536);
				_lastAllocationFailure = $"A slab was allocated at 0x{(ulong)(long)intPtr:X16}, but its protection could not be changed to executable.";
				return IntPtr.Zero;
			}
			StubSlab item = new StubSlab(intPtr, flag)
			{
				Offset = num
			};
			_slabs.Add(item);
			return intPtr;
		}
	}

	public unsafe static bool WriteStub(IntPtr destination, ReadOnlySpan<byte> source)
	{
		if (destination == IntPtr.Zero || source.IsEmpty)
		{
			return false;
		}
		lock (_slabLock)
		{
			StubSlab stubSlab = null;
			foreach (StubSlab slab in _slabs)
			{
				if (slab.Contains(destination, source.Length))
				{
					stubSlab = slab;
					break;
				}
			}
			if (stubSlab == null)
			{
				return false;
			}
			bool flag = !stubSlab.IsWriteExecute;
			if (flag && !Protect(destination, source.Length, MemoryProtection.ReadWrite, out var oldProtection))
			{
				return false;
			}
			try
			{
				fixed (byte* source2 = source)
				{
					Buffer.MemoryCopy(source2, (void*)destination, source.Length, source.Length);
				}
			}
			finally
			{
				if (flag)
				{
					Protect(destination, source.Length, MemoryProtection.ExecuteRead, out oldProtection);
				}
			}
			InstructionCache.Flush((ulong)(long)destination, source.Length);
			return true;
		}
	}

	public unsafe static bool WriteStub<T>(IntPtr destination, T value) where T : unmanaged
	{
		return WriteStub(destination, new ReadOnlySpan<byte>(&value, sizeof(T)));
	}

	public static bool FinalizeStub(IntPtr address, int length)
	{
		lock (_slabLock)
		{
			foreach (StubSlab slab in _slabs)
			{
				if (slab.Contains(address, Math.Max(length, 1)))
				{
					throw new InvalidOperationException($"FinalizeStub was called on 0x{(ulong)(long)address:X16}, which is inside a shared stub slab. That would revoke write access from every other stub on the same page, including ones not yet handed out. Use WriteStub to write stub contents instead.");
				}
			}
		}
		MemoryProtection oldProtection;
		return Protect(address, length, MemoryProtection.ExecuteRead, out oldProtection);
	}

	public static IntPtr Alloc(int size, MemoryProtection protection = MemoryProtection.ExecuteReadWrite)
	{
		if (PlatformInfo.IsWindows)
		{
			return WindowsMemoryMethods.VirtualAlloc(IntPtr.Zero, (UIntPtr)(ulong)size, (NativeMethods.Win32AllocationType)12288u, (NativeMethods.Win32MemoryPermissions)MemoryProtectionConverter.ToWin32(protection));
		}
		return PosixMap(IntPtr.Zero, (ulong)size, protection, fixedNoReplace: false);
	}

	public static bool Free(IntPtr address, int size)
	{
		if (PlatformInfo.IsWindows)
		{
			return WindowsMemoryMethods.VirtualFree(address, UIntPtr.Zero, 32768u);
		}
		return PosixNativeMethods.Unmap(address, (UIntPtr)(ulong)size);
	}

	public static bool Protect(IntPtr address, int size, MemoryProtection newProtection, out MemoryProtection oldProtection)
	{
		oldProtection = MemoryProtection.NoAccess;
		if (size <= 0)
		{
			return false;
		}
		MemoryProtectionRegion[] array;
		try
		{
			array = MemoryProtectionScope.CaptureRegions(address, size);
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		catch (OverflowException)
		{
			return false;
		}
		uint nativeProtection = array[0].NativeProtection;
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i].NativeProtection != nativeProtection)
			{
				return false;
			}
		}
		if (PlatformInfo.IsWindows)
		{
			NativeMethods.Win32MemoryPermissions oldProtection2;
			bool result = WindowsMemoryMethods.VirtualProtect(address, (UIntPtr)(ulong)size, (NativeMethods.Win32MemoryPermissions)MemoryProtectionConverter.ToWin32(newProtection), out oldProtection2);
			oldProtection = MemoryProtectionConverter.FromWin32((uint)oldProtection2);
			return result;
		}
		long address2 = (long)address;
		oldProtection = MemoryProtectionConverter.FromPosix((int)nativeProtection);
		IntPtr address3 = (IntPtr)(long)PlatformInfo.AlignDownToPage((ulong)address2);
		UIntPtr length = PlatformInfo.PageSpan((ulong)address2, size);
		return PosixNativeMethods.Protect(address3, length, MemoryProtectionConverter.ToPosix(newProtection));
	}

	public static MemoryProtection QueryProtection(ulong address)
	{
		if (PlatformInfo.IsWindows)
		{
			if (WindowsMemoryMethods.VirtualQuery((IntPtr)(long)address, out var information, (UIntPtr)(ulong)Marshal.SizeOf<NativeMethods.Win32MEMORY_BASIC_INFORMATION>()) != UIntPtr.Zero)
			{
				return MemoryProtectionConverter.FromWin32(information.Protect);
			}
			return AssumedProtection;
		}
		if (ProcMaps.TryFindRegion(address, out var region))
		{
			return region.Protection;
		}
		return AssumedProtection;
	}

	public unsafe static bool Write<T>(IntPtr processHandle, IntPtr address, T data) where T : unmanaged
	{
		uint num = (uint)Unsafe.SizeOf<T>();
		if (PlatformInfo.IsWindows)
		{
			uint num2 = 0u;
			return NativeMethods.WriteProcessMemory(processHandle, address, (byte*)(&data), num, &num2);
		}
		Unsafe.CopyBlock((void*)address, &data, num);
		return true;
	}

	public unsafe static bool Read<T>(IntPtr processHandle, IntPtr address, out T data) where T : unmanaged
	{
		data = default(T);
		uint num = (uint)Unsafe.SizeOf<T>();
		fixed (T* ptr = &data)
		{
			if (PlatformInfo.IsWindows)
			{
				uint num2 = 0u;
				return NativeMethods.ReadProcessMemory(processHandle, address, (byte*)ptr, num, &num2);
			}
			Unsafe.CopyBlock(ptr, (void*)address, num);
			return true;
		}
	}

	private static ulong BoundAlloc(ulong low, ulong high, ulong size, MemoryProtection protection)
	{
		if (!PlatformInfo.IsWindows)
		{
			return PosixBoundAlloc(low, high, size, protection);
		}
		return WindowsBoundAlloc(low, high, size, protection);
	}

	private static ulong PosixBoundAlloc(ulong low, ulong high, ulong size, MemoryProtection protection)
	{
		ulong num = (ulong)PlatformInfo.PageSize;
		low = AlignUp(low, num);
		high &= ~(num - 1);
		if (low >= high || high - low < size)
		{
			_lastAllocationFailure = $"The requested POSIX proximity window is empty after page alignment: [0x{low:X16}, 0x{high:X16}).";
			return 0uL;
		}
		ulong num2 = (low + (high - low) / 2) & ~(num - 1);
		if (num2 > high - size)
		{
			num2 = (high - size) & ~(num - 1);
		}
		IntPtr intPtr = PosixMap((IntPtr)(long)num2, size, protection, fixedNoReplace: false);
		if (intPtr != IntPtr.Zero)
		{
			if (IsWithin((ulong)(long)intPtr, low, high, size))
			{
				return (ulong)(long)intPtr;
			}
			PosixNativeMethods.Unmap(intPtr, (UIntPtr)size);
		}
		if (ProcMaps.IsAvailable)
		{
			ulong num3 = low;
			int num4 = 0;
			ulong address;
			while (num4 < 32 && ProcMaps.TryFindFreeGap(num3, high, size, num, out address))
			{
				num4++;
				IntPtr intPtr2 = PosixMap((IntPtr)(long)address, size, protection, fixedNoReplace: true);
				if (intPtr2 != IntPtr.Zero && IsWithin((ulong)(long)intPtr2, low, high, size))
				{
					return (ulong)(long)intPtr2;
				}
				if (intPtr2 != IntPtr.Zero)
				{
					PosixNativeMethods.Unmap(intPtr2, (UIntPtr)size);
				}
				num3 = address + num;
				if (num3 >= high)
				{
					break;
				}
			}
			_lastAllocationFailure = $"POSIX could not map {size} bytes inside [0x{low:X16}, 0x{high:X16}); the kernel hint landed outside the window and {num4} exact placement attempt(s) were exhausted.";
			return 0uL;
		}
		_lastAllocationFailure = $"POSIX could not map {size} bytes inside [0x{low:X16}, 0x{high:X16}); the kernel ignored or refused the in-range hint and /proc/self/maps is unavailable for exact placement.";
		return 0uL;
	}

	private static bool IsWithin(ulong address, ulong low, ulong high, ulong size)
	{
		if (address >= low && address <= high)
		{
			return size <= high - address;
		}
		return false;
	}

	private static IntPtr PosixMap(IntPtr address, ulong size, MemoryProtection protection, bool fixedNoReplace)
	{
		int num = 2 | PosixNativeMethods.MAP_ANONYMOUS;
		if (fixedNoReplace && PlatformInfo.IsLinux)
		{
			num |= 0x100000;
		}
		IntPtr intPtr = PosixNativeMethods.Map(address, (UIntPtr)size, MemoryProtectionConverter.ToPosix(protection), num, -1, 0L);
		if (intPtr != PosixNativeMethods.MAP_FAILED && intPtr != IntPtr.Zero)
		{
			return intPtr;
		}
		if ((protection & MemoryProtection.Write) == 0 || (protection & MemoryProtection.Execute) == 0)
		{
			return IntPtr.Zero;
		}
		intPtr = PosixNativeMethods.Map(address, (UIntPtr)size, MemoryProtectionConverter.ToPosix(MemoryProtection.ReadWrite), num, -1, 0L);
		if (!(intPtr == PosixNativeMethods.MAP_FAILED))
		{
			return intPtr;
		}
		return IntPtr.Zero;
	}

	private static ulong WindowsBoundAlloc(ulong low, ulong high, ulong size, MemoryProtection protection)
	{
		WindowsMemoryMethods.GetSystemInfo(out var systemInfo);
		ulong num = systemInfo.dwAllocationGranularity;
		if (num == 0L)
		{
			_lastAllocationFailure = "GetSystemInfo returned an allocation granularity of zero.";
			return 0uL;
		}
		ulong value = (ulong)(long)systemInfo.lpMinimumApplicationAddress;
		ulong num2 = (ulong)(long)systemInfo.lpMaximumApplicationAddress;
		low = Math.Max(AlignUp(low, num), AlignUp(value, num));
		high = Math.Min(high & ~(num - 1), num2 & ~(num - 1));
		if (low >= high || size > high - low)
		{
			_lastAllocationFailure = $"The requested Windows proximity window is empty after clamping: [0x{low:X16}, 0x{high:X16}).";
			return 0uL;
		}
		string failure;
		ulong num3 = TryVirtualAlloc2(low, high, size, protection, out failure);
		if (num3 != 0L)
		{
			if (IsWithin(num3, low, high, size))
			{
				return num3;
			}
			WindowsMemoryMethods.VirtualFree((IntPtr)(long)num3, UIntPtr.Zero, 32768u);
		}
		ulong num4 = WindowsBoundAllocFallback(low, high, size, protection, num);
		if (num4 == 0L)
		{
			string text = (string.IsNullOrEmpty(failure) ? string.Empty : (" VirtualAlloc2: " + failure));
			_lastAllocationFailure = $"Windows could not reserve {size} bytes inside [0x{low:X16}, 0x{high:X16}); the VirtualQuery/VirtualAlloc fallback was exhausted.{text}";
		}
		return num4;
	}

	internal static ulong WindowsBoundAllocFallback(ulong low, ulong high, ulong size, MemoryProtection protection, ulong granularity)
	{
		if (granularity == 0L || (granularity & (granularity - 1)) != 0L)
		{
			throw new ArgumentOutOfRangeException("granularity", "Allocation granularity must be a power of two.");
		}
		if (low >= high || size == 0L || size > high - low)
		{
			return 0uL;
		}
		ulong num = low;
		while (num < high && size <= high - num)
		{
			if (WindowsMemoryMethods.VirtualQuery((IntPtr)(long)num, out var information, (UIntPtr)(ulong)Marshal.SizeOf<NativeMethods.Win32MEMORY_BASIC_INFORMATION>()) == UIntPtr.Zero)
			{
				_lastAllocationFailure = $"VirtualQuery failed at 0x{num:X16} with Win32 error {Marshal.GetLastWin32Error()}.";
				break;
			}
			ulong num2 = (ulong)(long)information.BaseAddress;
			ulong num3 = (ulong)(long)information.RegionSize;
			if (num3 == 0L || num2 > (ulong)(-1L - (long)num3))
			{
				break;
			}
			ulong num4 = num2 + num3;
			if (information.State == 65536)
			{
				ulong num5 = AlignUp(Math.Max(num, num2), granularity);
				ulong num6 = Math.Min(num4, high);
				if (num5 >= low && num5 <= num6 && size <= num6 - num5)
				{
					IntPtr intPtr = WindowsMemoryMethods.VirtualAlloc((IntPtr)(long)num5, (UIntPtr)size, (NativeMethods.Win32AllocationType)12288u, (NativeMethods.Win32MemoryPermissions)MemoryProtectionConverter.ToWin32(protection));
					if (intPtr != IntPtr.Zero && IsWithin((ulong)(long)intPtr, low, high, size))
					{
						return (ulong)(long)intPtr;
					}
					if (num5 <= (ulong)(-1L - (long)granularity))
					{
						num = num5 + granularity;
						continue;
					}
				}
			}
			ulong num7 = AlignUp(num4, granularity);
			if (num7 <= num)
			{
				break;
			}
			num = num7;
		}
		return 0uL;
	}

	private unsafe static ulong TryVirtualAlloc2(ulong low, ulong high, ulong size, MemoryProtection protection, out string? failure)
	{
		failure = null;
		try
		{
			NativeMethods.Win32MEM_ADDRESS_REQUIREMENTS win32MEM_ADDRESS_REQUIREMENTS = new NativeMethods.Win32MEM_ADDRESS_REQUIREMENTS
			{
				LowestStartingAddress = (IntPtr)(long)low,
				HighestEndingAddress = (IntPtr)(long)(high - 1),
				Alignment = IntPtr.Zero
			};
			NativeMethods.Win32MEM_EXTENDED_PARAMETER win32MEM_EXTENDED_PARAMETER = new NativeMethods.Win32MEM_EXTENDED_PARAMETER
			{
				Type = 1uL
			};
			win32MEM_EXTENDED_PARAMETER.Value.Pointer = (IntPtr)(&win32MEM_ADDRESS_REQUIREMENTS);
			IntPtr intPtr = WindowsMemoryMethods.VirtualAlloc2(IntPtr.Zero, IntPtr.Zero, (UIntPtr)size, (NativeMethods.Win32AllocationType)12288u, (NativeMethods.Win32MemoryPermissions)MemoryProtectionConverter.ToWin32(protection), &win32MEM_EXTENDED_PARAMETER, 1u);
			if (intPtr == IntPtr.Zero)
			{
				failure = $"failed with Win32 error {Marshal.GetLastWin32Error()}.";
			}
			return (ulong)(long)intPtr;
		}
		catch (EntryPointNotFoundException)
		{
			failure = "is unavailable on this Windows version.";
			return 0uL;
		}
		catch (DllNotFoundException)
		{
			failure = "kernel32.dll could not be loaded.";
			return 0uL;
		}
	}

	private static ulong AlignUp(ulong value, ulong alignment)
	{
		ulong num = alignment - 1;
		if (value > (ulong)(-1L - (long)num))
		{
			return 0xFFFFFFFFFFFFFFFFuL & ~num;
		}
		return (value + num) & ~num;
	}
}
