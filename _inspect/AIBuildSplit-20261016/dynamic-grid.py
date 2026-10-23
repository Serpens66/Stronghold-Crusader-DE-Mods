from pathlib import Path
r=Path.cwd();b=r/'Testmods/AIBuildDiagnoseTest'
def read(p):return p.read_text(encoding='utf-8-sig')
def write(p,s):
    with p.open('w',encoding='utf-8',newline='') as f:f.write(s.replace('\r\n','\n').replace('\n','\r\n'))
p=b/'src/Diagnostics/AiBuildDiagnostic.cs';s=read(p)
s=s.replace('''        /// <summary>Copies one monitored coarse cell; intended for the diagnostic tick publisher.</summary>
        public static AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
            => CaptureEconomyGridEvidence(state, mode, 69, 97);''','''        /// <summary>Copies the shared native search-origin cell, not a fixed scenario target.
        /// The origin is shared last-search state and does not attribute a player or prove a final site.</summary>
        public static AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
        {
            if (!HasObserver || state == 0 || moduleBase == 0)
                return new AiEconomyGridEvidence("unavailable", state, mode, -1, -1, -1, -1, -1, null);
            try
            {
                // Same hash-bound AIV fields already used by LogFirstSearchOrigin and the wood replay.
                int x = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x18783C)));
                int y = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1A083C)));
                return CaptureEconomyGridEvidence(state, mode, x, y);
            }
            catch (Exception ex)
            {
                return new AiEconomyGridEvidence("search-origin-unavailable:" + ex.GetType().Name,
                    state, mode, -1, -1, -1, -1, -1, null);
            }
        }''')
assert 'state, mode, 69, 97' not in s
write(p,s)
p=b/'src/AIBuildDiagnoseRuntime.cs';s=read(p)
s=s.replace('$"state=0x{evidence.State:X}, status={evidence.Status}, " +',
'''$"state=0x{evidence.State:X}, status={evidence.Status}, " +
                $"cell=({evidence.CoarseX},{evidence.CoarseY}), sampleSource=shared-search-origin, playerAttribution=unknown, " +''')
s=s.replace("value.Append(evidence.Status).Append(':').Append(evidence.ReferenceComponent)",
"value.Append(evidence.CoarseX).Append(',').Append(evidence.CoarseY).Append(':')\n                .Append(evidence.Status).Append(':').Append(evidence.ReferenceComponent)")
write(p,s)
p=b/'verify.ps1';s=read(p)
s=s.replace("if ($generalRuntime -match 'test_canari", "if ($runtimeText -match 'CaptureEconomyGridEvidence\\(state, mode, 69, 97\\)' -or\n    $generalRuntime -notmatch 'sampleSource=shared-search-origin' -or\n    $generalRuntime -match 'test_canari")
write(p,s)
p=b/'tests/Diagnostics.Tests/ObservationTests.cs';s=read(p)
s=s.replace('''            Assert.AreEqual("unavailable", AiBuildDiagnostic.CaptureEconomyGridEvidence(0UL, -1).Status);''',
'''            AiEconomyGridEvidence unavailable = AiBuildDiagnostic.CaptureEconomyGridEvidence(0UL, -1);
            Assert.AreEqual("unavailable", unavailable.Status);
            Assert.AreEqual(-1, unavailable.CoarseX);
            Assert.AreEqual(-1, unavailable.CoarseY);''')
write(p,s)
p=b/'docs/DIAGNOSTICS.md';s=read(p).replace('detailed than other building types. No complete automatic cause detector is claimed.',
'''detailed than other building types. No complete automatic cause detector is claimed.
Economy-grid overview snapshots follow the shared native last-search origin;
their logs name the sampled cell and do not attribute that shared state to a Lord.
Per-player origins and explicitly captured candidate tiles remain separate evidence.''')
write(p,s)
