from pathlib import Path
import re
r=Path.cwd();b=r/'Testmods/AIBuildDiagnoseTest';c=r/'Testmods/CanarariWoodTest'
def read(p):return p.read_text(encoding='utf-8-sig')
def write(p,s):
    with p.open('w',encoding='utf-8',newline='') as f:f.write(s.replace('\r\n','\n').replace('\n','\r\n'))
p=b/'src/Diagnostics/NearbyWoodEvidence.cs';s=read(p)
start=s.index('        private void SnapshotWoodCells');end=s.index('        internal static readonly int[] NearbyDx',start)
write(p,s[:start]+s[end:])
p=c/'src/DisposableProbePolicy.cs';s=read(p).replace('using System.IO;','using System.IO;\nusing System.Security.Cryptography;')
s=s.replace('    }\n}', '''        internal static bool HasExpectedHash(string path, string expected)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""),
                    expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}''')
write(p,s)
for name,variable,expected in [('FarmSiteContractProbe.cs','path','SourceHash'),('CanariFarmSwapProbe.cs','session.SaveFileName','OriginalHash')]:
    p=c/'src'/name;s=read(p)
    pattern=r'!File.Exists\('+re.escape(variable)+r'\) \|\|\s*!string.Equals\(Hash\('+re.escape(variable)+r'\), '+expected+r',\s*StringComparison.OrdinalIgnoreCase\)'
    s,n=re.subn(pattern,'!DisposableProbePolicy.HasExpectedHash('+variable+', '+expected+')',s);assert n==1,(name,n)
    start=s.index('        private static string Hash(');end=s.index('\n        }',start)+len('\n        }')
    s=s[:start]+s[end:];s=s.replace('using System.Security.Cryptography;\n','')
    write(p,s)
p=c/'verify.ps1';s=read(p).replace("string\\.Equals\\(Hash\\(path\\), SourceHash",'DisposableProbePolicy\\.HasExpectedHash\\(path, SourceHash')
write(p,s)
p=b/'tests/Diagnostics.Tests/DisposableProbeTests.cs';s=read(p)
s=s.replace('using CanarariWoodTest;','using CanarariWoodTest;\nusing System;\nusing System.IO;')
s=s.replace('        [TestMethod]', '''        [TestMethod]
        public void HashGuardRejectsMissingAndChangedSaveCopies()
        {
            string path = Path.Combine(Path.GetTempPath(), "CanarariCopy-" + Guid.NewGuid().ToString("N") + ".sav");
            const string emptyHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
            try
            {
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
                File.WriteAllBytes(path, Array.Empty<byte>());
                Assert.IsTrue(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, new string('0', 64)));
                File.WriteAllText(path, "changed");
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        [TestMethod]''',1)
write(p,s)
# Remove unused moved remnants rather than retaining obsolete implementations.
p=c/'src/CanarariWoodRuntime.cs';s=read(p)
start=s.index('        private static AiCoarseCellSample FindCoarseCell');end=s.index('\n        }',start)+len('\n        }')
s=s[:start]+s[end:];write(p,s)
