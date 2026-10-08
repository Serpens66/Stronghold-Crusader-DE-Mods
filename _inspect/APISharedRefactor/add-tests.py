from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
def read(p):return p.read_text(encoding='utf-8-sig')
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
p=ROOT/'_inspect/APISharedTests/Program.cs';s=read(p)
s=s.replace('            TestPublicSurface();','            TestPublicSurface();\n            TestOwnerBoundClient();',1)
marker='        private static void TestReadinessAndIndependentCapabilities()'
method='''        private static void TestOwnerBoundClient()
        {
            foreach (string invalid in new[] { null, "", "  " })
            {
                bool rejected = false;
                try { ApiShared.ForMod(invalid); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "owner-bound entry must reject empty GUIDs immediately");
            }
            var runtime = new ApiSharedRuntime();
            var client = new ModApiClient("Foreign.Author.MyMod", runtime);
            Assert(client.OwnerGuid == "Foreign.Author.MyMod" && client.State == NativeApiState.Pending,
                "arbitrary foreign GUID does not require Serps profiles");
            Assert(!client.TryGetGatehouseTiming(out _, out var pending) && pending.State == NativeCapabilityState.Pending,
                "owner-bound client preserves pending capability diagnostics");
            int calls = 0;
            client.WhenReady(_ => throw new InvalidOperationException("test-early"));
            client.WhenReady(c => { Assert(ReferenceEquals(c, client), "callback retains owner-bound client"); calls++; });
            runtime.Initialize(ModuleBase, CreatePeImage(0x4000, true), "UNKNOWN", new FakeMemory(), null, null, false);
            Assert(calls == 1, "early callback failure must not block later consumers");
            client.WhenReady(_ => throw new InvalidOperationException("test-late"));
            client.WhenReady(_ => calls++);
            Assert(calls == 2, "late callbacks are synchronous and equally isolated");
            Assert(!client.TryGetGatehouseTiming(out _, out var unsupported) && unsupported.State == NativeCapabilityState.UnsupportedBuild,
                "global Ready does not imply native service support");
            Assert(typeof(IApiShared).Assembly.GetType("APIShared.UnitCommands.UnitCommandPathAPI").IsNotPublic,
                "specialized command runtime is not a public third-party contract");
            Assert(!typeof(IApiShared).Assembly.GetExportedTypes().Any(t => t.Namespace == "Shared"),
                "APIShared no longer exports historical Shared contracts");
        }

'''
s=s.replace(marker,method+marker)
write(p,s)
# Add actual foreign-profile coverage to the existing mode fixture tests.
p=ROOT/'_inspect/HostClientPresetTests/Program.cs';s=read(p)
marker='        string policySource = File.ReadAllText('
index=s.index(marker)
s=s[:index]+'''        var foreignProfile = new GameplayModActivationProfile("Foreign.Author.Policy", "Foreign policy",
            GameplayModAllowedContext.CustomGame | GameplayModAllowedContext.MapEditor, allowRealMultiplayer: false);
        Check(GameplayModModePolicy.IsAllowed(foreignProfile, CaptureModeFixture(editor: true), out _),
            "foreign caller-defined profile must permit its chosen editor context");
        Check(!GameplayModModePolicy.IsAllowed(foreignProfile, default(GameModeSnapshot), out _),
            "foreign optional profile must reject unknown contexts");
        Check(!foreignProfile.AllowRealMultiplayer,
            "foreign multiplayer restriction is independent of Serps GUID tables");
'''+s[index:]
write(p,s)
p=ROOT/'APIShared/build.bat';s=read(p)
marker='"%MSBUILD%" "%PROJECT_DIR%..\\_inspect\\APISharedPresetConsumerTests\\APISharedPresetConsumerTests.csproj"'
start=s.index(marker);end=s.index('\npopd',start)
s=s[:end]+'''\n"%MSBUILD%" "%PROJECT_DIR%examples\\ThirdPartyMod\\ThirdPartyMod.csproj" /t:Rebuild /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd'''+s[end:]
write(p,s)
p=ROOT/'_inspect/APISharedRefactor/projects.txt';write(p,read(p)+'APIShared/examples/ThirdPartyMod/ThirdPartyMod.csproj\n')
print('Regression tests and public-example compilation wired.')
