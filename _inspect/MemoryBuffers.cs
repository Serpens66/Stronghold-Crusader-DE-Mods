using System.Collections.Generic;

public class MemoryBuffers
{
	public class MemBuffer
	{
		public short[] memory;

		public EngineInterface.PlayState gameState;

		public byte[] radarMap;

		public byte[] MPChores;

		public int numTiles;

		public int bufferID;

		public bool locked;

		public bool gotData;

		public bool processed;

		public bool beingFilled;

		public long frameID;
	}

	public static MemoryBuffers instance;

	public const int NUM_BUFFERS = 6;

	public List<MemBuffer> bufferPool = new List<MemBuffer>();

	public static int commonFrameID;

	public static void init()
	{
		instance = new MemoryBuffers();
	}

	public MemoryBuffers()
	{
		for (int i = 0; i < 6; i++)
		{
			MemBuffer item = new MemBuffer
			{
				memory = new short[14700000],
				radarMap = null,
				MPChores = new byte[1000000],
				bufferID = i,
				numTiles = 0,
				frameID = 0L,
				locked = false,
				gotData = false,
				beingFilled = false
			};
			bufferPool.Add(item);
		}
	}

	public void GenerateRadarBuffers(int size)
	{
		for (int i = 0; i < 6; i++)
		{
			bufferPool[i].radarMap = new byte[size * size * 4];
		}
	}

	public void resetBuffers()
	{
		for (int i = 0; i < 6; i++)
		{
			bufferPool[i].numTiles = 0;
			bufferPool[i].frameID = 0L;
			bufferPool[i].locked = false;
			bufferPool[i].gotData = false;
			bufferPool[i].beingFilled = false;
		}
	}

	public MemBuffer getFreeBuffer(bool writing)
	{
		foreach (MemBuffer item in bufferPool)
		{
			if (!item.locked && !item.gotData)
			{
				item.locked = true;
				if (writing)
				{
					item.frameID = commonFrameID;
					item.processed = false;
					item.beingFilled = true;
					commonFrameID++;
				}
				return item;
			}
		}
		return null;
	}

	public void returnBuffer(MemBuffer buf)
	{
		if (buf.beingFilled)
		{
			buf.beingFilled = false;
			buf.gotData = true;
		}
		buf.locked = false;
	}

	public MemBuffer getNextBufferToRender()
	{
		long num = long.MaxValue;
		MemBuffer memBuffer = null;
		foreach (MemBuffer item in bufferPool)
		{
			if (!item.locked && !item.processed && item.gotData && !item.beingFilled && item.frameID < num)
			{
				memBuffer = item;
				num = item.frameID;
			}
		}
		if (memBuffer != null)
		{
			memBuffer.processed = true;
			memBuffer.locked = true;
			memBuffer.gotData = false;
		}
		return memBuffer;
	}
}
