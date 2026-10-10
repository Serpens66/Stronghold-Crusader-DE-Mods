# UU-ImGUI shutdown audit (2026-10-10)

Native game baseline: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audited installed Script Extender assembly: 2.14.1.0. Bundled UU-ImGUI: 1.6.7.

Binary identities (SHA-256):

- universal-unity-imgui-api.dll: D98415528BA84F0DFF17F283F72D0003D65ECA8F30993E5BAFE1D425BA8CE9B7.
- cimguiaio.dll: BF43F0854C7CA51C33862D092E53E39FAE8FC6528C8103E4563D000EAF507E80.

The original WM_CLOSE branch destroys ImNodes, ImPlot and ImPlot3D contexts and
calls UnHookSafely(true). The latter resets ImGuiInitialized before removing native
render hooks. A concurrent Present can therefore initialize again. Reinstallation
can store the hook's own WndProc as its predecessor and recursively forward WM_CLOSE.
ProcessExit/Disable also removes native hooks and unloads the native DLL after its
managed OnDestroyed/currentContext=null prefix.

The 14:03:09 crash reached cimguiaio+0xEBD38 through imnodes_DestroyContext
(export 0xE8FC0 -> 0xEBD20). A null argument falls back to the current-context pointer
at RVA 0x361F48; the function dereferences the resulting pointer without a null check.
Context destruction clears that pointer. The associated log contains reinitialization,
equal old/new WndProc pointers and repeated WM_CLOSE. This supports the dependency
shutdown race; a later successful close does not refute it.

The game native crash-handler registration/handler paths are at RVAs 0x1A49C0 and
0x1A49F0; exit wrappers are 0x1E3734 -> 0x1E3544. The existing managed
Application.quitting publisher restores the original VEH, but the failing WM_CLOSE
occurs before that publisher. The normal managed menu path is
MainViewModel.ButtonExit -> ExitApplication -> FatControler.ExitApp -> Application.Quit.
FatControler.ExitApp is public, instance, parameterless and returns void in the real
installed Assembly-CSharp.dll. Normal menu exit therefore also reaches the shutdown
lifecycle; the defect is not restricted to the Alt+F4 input gesture.

APIShared's optional fix retains native resources and native hooks until process exit,
latches shutdown atomically, forwards window messages to a validated predecessor and
uses existing render trampolines after shutdown. It preserves the managed Disable
prefix. Public APIShared contracts and the Extender fork are unchanged.

Controlled generated-body tests cover blocked in-flight rendering, repeated close,
native trampoline arguments/results, predecessor preservation and managed OnDestroyed
notification. They do not establish Unity Mono integration by themselves.

The initial game integration attempts at 14:44:20, 14:44:52 and 14:45:30 skipped the
fix with ArgumentNullException (localType); their successful closes are not acceptance
evidence. The installed MonoMod SRE generator resolves local Cecil types for DeclareLocal;
the audited Present/Resize bodies contain function-pointer locals. The revised manipulator
represents these address-only locals as IntPtr, preserving field and calli signatures.
The regression injects a Cecil FunctionPointerType independently of the test CLR.
The 14:50:26 retry still failed with localType; at 14:55:07 the metadata-based local
normalization advanced to NotImplementedException in DynamicILInfo.GetTokenFor(byte[]).
Both attempts remained inactive. They must not be counted as successful fix tests.

A separate process embeds the exact installed mono-2.0-bdwgc.dll and uses the game's
mscorlib, MonoMod and UU-ImGUI assemblies. It reproduces Mono's reflected function
pointer representation (System.MonoFNPtrFakeClass), the failed DynamicMethod backend,
and successful MethodBuilder compilation. GetFunctionPointer forces actual JIT;
PrepareMethod alone did not expose Cecil-generated field signature failures.

The production fix now permanently routes only copies of its eight audited methods
through MonoMod's MethodBuilder backend, including the Detour original backups.
Other methods call the previous generator, and MONOMOD_DMD_TYPE is untouched.
Additional SHA-256 identities:

- MonoMod.Utils.dll: 9D1495F147AC93C4F81F84538C1A326E8F8A6AEFC78D6289D798F3CE1162C5E9.
- MonoMod.RuntimeDetour.dll: 40E49BB314391CD7BDDC2644F8553EEBA92C194B940836B103DF16955C464E0C.

The isolated Mono test loads the installed production APIShared.dll, installs its
actual compiler, installs the actual WndProc manipulator, and invokes the real patched
UU method with synthetic arguments. It also installs the actual Present/Resize
manipulators and exercises their native trampolines with rooted ThisCall delegates:
argument/result checks produce 9 and 21. Original backups and an unrelated generated
method are also tested. The expected default/Cecil failures are negative controls;
the production checks pass (process exit 0, mono-production-compiler.log).
Build and installation at 15:04:50 succeeded, with 176 regular tests passing;
full game startup and shutdown-path acceptance remained pending.

The 15:07:11 game start exposed a separate loader contract: the game's patched
assembly module is named data-<address>, not a readable file. The fix now reads
original native-pointer metadata only when function-pointer locals require repair;
ordinary locals in a memory-loaded game assembly do not need a metadata file.
The metadata source for the audited UU dependency is its validated Assembly.Location.
The new memory-assembly regression passes; build/install at 15:10:17 passed 177 tests.
The isolated Unity Mono test also loads the real Assembly-CSharp bytes in memory
and successfully generates ExitApp, while the production WndProc/Present/Resize
tests continue to pass. The host lacks Unity engine internal-call registrations,
so it deliberately does not execute Application.Quit and emits its unresolved-icall
diagnostic. This is not a test of Unity shutdown.

Full game acceptance on the tested 0.6.0 development build:

| Path | Shutdown marker | Result |
| --- | --- | --- |
| Main menu Alt+F4 | 15:12:19.033 WM_CLOSE | Clean shutdown |
| Editor Alt+F4 | 15:14:22.763 WM_CLOSE | Clean shutdown |
| Skirmish Alt+F4 | 15:15:12.077 WM_CLOSE | Clean shutdown |
| Normal menu exit | 15:15:43.344 menu ExitApp | Clean shutdown |

Every preserved Player-log snapshot contains one install, one persistent Present,
one shutdown marker, and ProcessExit; none contains an Error/Fatal line or the former
native context-destruction messages. In the main-menu run the persistent Present marker
follows APIShared's established post-startup-cleanup HUD marker (15:11:15.120).
The user confirmed all runs ended without crash or hang. Unity Player.log preserves
the shutdown records that are absent from the appended BepInEx disk log, so both
logs must be consulted; snapshots are preserved in .inspect/ImGuiShutdownFix/game-*.

After acceptance, the final review adds an already-active initialization guard and
avoids an Interlocked write on every Present for the one-time log marker. APIShared's
active source version advances to 0.6.1; existing public capability minimums remain
0.6.0. These game runs establish integration for the reviewed binaries and paths;
the controlled interleaving tests provide the race-specific evidence.

Supporting artifacts are in workspace .inspect/ImGuiShutdownFix, including decompiled
installed MonoMod generators, LibD3D11 IL, validation logs and build logs. Re-audit both
dependency hashes, all managed/IL contracts and runtime behavior after an Extender update.
