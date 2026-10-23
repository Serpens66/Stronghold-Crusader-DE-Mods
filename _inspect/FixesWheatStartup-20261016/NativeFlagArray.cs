using System;
using System.Threading;

namespace RedBird.Core.Memory;

public sealed class NativeFlagArray
{
	private readonly NativeStateBlock _owner;

	private readonly int _offset;

	public int Length { get; }

	public ulong Address => _owner.GetAddress(_offset, Length);

	public unsafe bool this[int index]
	{
		get
		{
			return Volatile.Read(ref *GetElementPointer(index)) != 0;
		}
		set
		{
			Volatile.Write(ref *GetElementPointer(index), value ? ((byte)1) : ((byte)0));
		}
	}

	internal NativeFlagArray(NativeStateBlock owner, int offset, int length)
	{
		_owner = owner;
		_offset = offset;
		Length = length;
	}

	public ulong GetAddress(int index)
	{
		ValidateIndex(index);
		return _owner.GetAddress(_offset + index, 1);
	}

	public unsafe void Fill(bool value)
	{
		byte value2 = (value ? ((byte)1) : ((byte)0));
		for (int i = 0; i < Length; i++)
		{
			Volatile.Write(ref *GetElementPointer(i), value2);
		}
	}

	private unsafe byte* GetElementPointer(int index)
	{
		ValidateIndex(index);
		return _owner.GetPointer(_offset + index, 1);
	}

	private void ValidateIndex(int index)
	{
		if ((uint)index >= (uint)Length)
		{
			throw new ArgumentOutOfRangeException("index");
		}
	}
}
