from pathlib import Path

def write(p, s):
    Path(p).write_bytes(s.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))

p = Path('Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs')
s = p.read_text(encoding='utf-8-sig')
a = s.index('        private int cursorRequestSequence, cursorRequestWriter;')
b = s.index('        private int cursorSampleState', a)
s = s[:a] + '''        private readonly CursorPreviewCache cursorCache = new CursorPreviewCache(CursorRefreshInterval);
        private long cursorEpoch;
        private int cursorValidationActive;
''' + s[b:]
s = s.replace('            ProcessCursorPreview(now);\n', '')
a = s.index('                        if (PublishCursorRequest')
b = s.index('                            finalResult =', a)
s = s[:a] + '''                        Interlocked.Increment(ref cursorRequestsPublished);
                        bool allowed;
                        if (TryValidateCursorPreview(player, unitId, targetX, targetY,
                                tileAfter, targetPcl, sourcePcl, snapshot.Fingerprint,
                                out allowed))
                        {
''' + s[b:]
a = s.index('        private bool PublishCursorRequest(')
b = s.index('            NativeMaskSnapshot snapshot = currentMasks;', s.index('        private void ProcessCursorPreview(', a))
s = s[:a] + '''        private bool TryValidateCursorPreview(int player, int unitId, int targetX,
            int targetY, int targetTile, int targetPcl, int sourcePcl,
            ulong fingerprint, out bool allowed)
        {
            allowed = true;
            if (!Installed || Interlocked.CompareExchange(ref cursorValidationActive, 1, 0) != 0)
                return false;
            try
            {
                uint thread = GetCurrentThreadId();
                byte* activeSlot = (byte*)threadSlots + ((thread &
                    (DirectionFilterAdapterEmitter.ThreadSlotCount - 1)) * ThreadSlotStride);
                byte* tacticalSlot = (byte*)tacticalThreadSlots + ((thread &
                    (AiTacticalTargetAdapterEmitter.ThreadSlotCount - 1)) *
                    AiTacticalTargetAdapterEmitter.ThreadSlotStride);
                if (Volatile.Read(ref *(int*)(activeSlot + DirectionFilterAdapterEmitter.SlotDepthOffset)) != 0 ||
                    Volatile.Read(ref *(int*)(tacticalSlot + AiTacticalTargetAdapterEmitter.SlotDepthOffset)) != 0)
                    return false;
                long epoch = Volatile.Read(ref cursorEpoch);
                int generation = Volatile.Read(ref policyGeneration);
                long now = Stopwatch.GetTimestamp();
''' + s[b:]
a = s.index('        private bool TryValidateCursorPreview(')
b = s.index('        private void CaptureCursorDecisionSample', a)
part = s[a:b]
part = part.replace(') return;', ') return false;').replace('                return;', '                return false;')
part = part.replace('if (startX >= 800 || startY >= 800 ||', 'if (startX < 0 || startY < 0 || startX >= 800 || startY >= 800 ||')
part = part.replace('            long started = Stopwatch.GetTimestamp();', '''            CursorPreviewCache.Key key = new CursorPreviewCache.Key(player, unitId,
                unchecked((int)unit->r_GlobalId), startX, startY, targetX, targetY,
                targetPcl, sourcePcl, epoch, generation, fingerprint);
            if (cursorCache.TryGet(key, now, out allowed))
            {
                if (!CursorSnapshotMatches(snapshot, epoch, generation)) return false;
                Interlocked.Increment(ref cursorCacheHits);
                Interlocked.Increment(ref cursorExactCacheHits);
                return true;
            }
            long started = Stopwatch.GetTimestamp();''')
part = part.replace('referenceScope.Snapshot.Fingerprint != fingerprint', '!ReferenceEquals(referenceScope.Snapshot, snapshot)')
part = part.replace('filteredScope.Snapshot.Fingerprint != fingerprint', '!ReferenceEquals(filteredScope.Snapshot, snapshot)')
part = part.replace('if (currentMasks.Fingerprint != fingerprint) return false;', 'if (!CursorSnapshotMatches(snapshot, epoch, generation)) return false;')
part = part.replace('            bool allowed = !EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(', '            allowed = !EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(')
x = part.index('            Interlocked.Increment(ref cursorCacheSequence);')
y = part.index('            if (Interlocked.CompareExchange(ref cursorSampleState', x)
part = part[:x] + '''            if (unit->r_GlobalId != unchecked((uint)key.Global) ||
                unit->r_CurrentTilePositionX != startX || unit->r_CurrentTilePositionY != startY ||
                unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != player)
                return false;
            cursorCache.Put(key, now, allowed);

''' + part[y:]
part = part.rstrip()
assert part.endswith('        }')
part = part[:-9] + '''            return CursorSnapshotMatches(snapshot, epoch, generation);
            }
            finally
            {
                Volatile.Write(ref cursorValidationActive, 0);
            }
        }

        private bool CursorSnapshotMatches(NativeMaskSnapshot snapshot, long epoch, int generation) =>
            ReferenceEquals(currentMasks, snapshot) &&
            Volatile.Read(ref cursorEpoch) == epoch &&
            Volatile.Read(ref policyGeneration) == generation;

'''
s = s[:a] + part + s[b:]
for line in ['            Volatile.Write(ref cursorRequestSequence, 0);\n',
             '            Volatile.Write(ref cursorRequestWriter, 0);\n',
             '            Volatile.Write(ref cursorCacheSequence, 0);\n',
             '            Volatile.Write(ref cursorCacheValid, 0);\n',
             '            Volatile.Write(ref nextCursorValidationAt, 0);\n']:
    s = s.replace(line, '')
s = s.replace('            Volatile.Write(ref cursorSampleState, 0);',
    '            Interlocked.Increment(ref cursorEpoch);\n            cursorCache.Clear();\n            Volatile.Write(ref cursorSampleState, 0);')
s = s.replace('cursorRefreshMs=200', 'cursorCacheTtlMs=200, cursorCacheEntries=32, cursorValidation=on-demand')
write(p, s)
for p in ['Testmods/EnemyGatePathfindingTest/EnemyGatePathfindingTest.csproj',
          'Testmods/EnemyGatePathfindingTest/EnemyGatePathfindingTest.PolicyTests.csproj']:
    s = Path(p).read_text(encoding='utf-8-sig')
    s = s.replace('    <Compile Include="src\\EnemyGatePathfindingPolicy.cs"',
        '    <Compile Include="src\\CursorPreviewCache.cs" />\n    <Compile Include="src\\EnemyGatePathfindingPolicy.cs"')
    write(p, s)
