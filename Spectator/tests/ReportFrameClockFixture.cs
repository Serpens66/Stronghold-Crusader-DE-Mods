using System.Collections.Generic;

// External buffer boundary only. InstalledContracts validates these private signatures
// against the real game; this fixture exercises the production reflection adapter.
public class EngineInterface { public class PlayState { } }
public class MemoryBuffers
{
    public class MemBuffer { public EngineInterface.PlayState gameState; public long frameID; }
    public static MemoryBuffers instance;
    private static int commonFrameID;
    private readonly List<MemBuffer> bufferPool;
    public MemoryBuffers(int nextFrame, params MemBuffer[] buffers)
    { commonFrameID = nextFrame; bufferPool = new List<MemBuffer>(buffers); }
}
