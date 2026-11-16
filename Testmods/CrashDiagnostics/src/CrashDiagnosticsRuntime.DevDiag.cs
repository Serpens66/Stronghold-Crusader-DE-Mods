using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.NativeSites;
using APIShared.Savegames;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using UnityEngine;

namespace CrashDiagnostics
{
    public sealed partial class CrashDiagnosticsPlugin
    {
        private static CrashDiagnosticsRuntime runtime;

        partial void InitializeDiagnostics(ManualLogSource logger, string pluginDirectory, ref bool active)
        {
            if (runtime != null) { active = runtime.IsActive; return; }
            try
            {
                runtime = new CrashDiagnosticsRuntime(logger, pluginDirectory);
                runtime.Initialize();
                active = runtime.IsActive;
            }
            catch (Exception error)
            {
                logger.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    "] Crash diagnostics initialization failed; gameplay remains unchanged. " + error);
            }
        }
    }

    internal sealed partial class CrashDiagnosticsRuntime
    {
        private const long SessionBudget = 128L * 1024 * 1024;
        private const long RootBudget = 512L * 1024 * 1024;
        private const int MetadataFileLimit = 512 * 1024;
        private readonly ManualLogSource log;
        private readonly string pluginDirectory;
        private readonly Dictionary<string, object> hashCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly object metadataGate = new object();
        private readonly Dictionary<string, object> observerStatus = new Dictionary<string, object>();
        private string sessionDirectory, processSessionId;
        private IntPtr nativeModule;
        private ModApiClient client;
        private IMissionLifecycleCapability missionLifecycle;
        private long missionId, lastTick, saveSequence;
        private int tickKnown, missionTickMarker, lastFrame = -1, renderCallbacks, failureReported;
        private long nextHeartbeat, nextBootstrap;
        private uint previousStatus;
        private volatile bool nativeActive;
        private bool quitting, finalInventoryWritten;

        internal CrashDiagnosticsRuntime(ManualLogSource logger, string directory)
        { log = logger; pluginDirectory = directory; }

        internal bool IsActive => nativeActive;

        internal void Initialize()
        {
            string nativePath = Path.Combine(pluginDirectory, "CrashDiagnostics.Native.dll");
            string collector = Path.Combine(pluginDirectory, "CrashDiagnostics.Collector.exe");
            if (!File.Exists(nativePath) || !File.Exists(collector))
                throw new FileNotFoundException("Native DLL or collector is missing from the diagnostic package.");
            nativeModule = NativeMethods.LoadLibraryExW(nativePath, IntPtr.Zero, 8);
            if (nativeModule == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if (NativeMethods.CD_GetAbiVersion() != 1) throw new InvalidOperationException("Unsupported native ABI version.");
            sessionDirectory = CreateSessionDirectory();
            WriteMetadata("session.json", CreateSessionManifest());
            TryWriteInventory();
            string playerLog = Application.consoleLogPath ?? string.Empty;
            uint result = NativeMethods.CD_Initialize(sessionDirectory, collector,
                Path.Combine(Paths.BepInExRootPath, "LogOutput.log"), playerLog,
                Path.Combine(Paths.BepInExRootPath, "crashdumps"));
            if (result != 0) throw new InvalidOperationException("Native initialization failed with code " + result + ". Evidence: " + sessionDirectory);
            nativeActive = true;
            Record(11, 0, 0, 0);
            client = ApiShared.ForMod(CrashDiagnosticsPlugin.PluginGuid);
            NativeCapabilityDiagnostic diagnostic;
            bool acquired = client.TryGetMissionLifecycle(out missionLifecycle, out diagnostic);
            if (acquired)
                acquired = missionLifecycle.TryRegisterObserver("crash.missions", MissionStarted,
                    MissionEnded, MissionInitializing, out diagnostic);
            SetObserverStatus("missions", acquired, diagnostic?.Reason);

            string reason;
            SetObserverStatus("engineRuns", EngineRunEvents.TryRegister(CrashDiagnosticsPlugin.PluginGuid,
                "crash.engine", EngineBegin, EngineEnd, out reason), reason);
            SetObserverStatus("mapTicks", SimulationTickEvents.TryRegister(CrashDiagnosticsPlugin.PluginGuid,
                "crash.tick", Tick, out reason), reason);
            SetObserverStatus("managedSaves", ManagedSaveEvents.TryRegister(CrashDiagnosticsPlugin.PluginGuid,
                "crash.saves", SaveBegin, SaveEnd, out reason), reason);

            // These publishers retain callbacks after SHCDE destroys the plugin's Unity component.
            CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
            Application.onBeforeRender += BeforeRender;
            Application.quitting += Quitting;
            previousStatus = NativeMethods.CD_GetStatus();
            Log("READY status=" + previousStatus + "; session=" + sessionDirectory +
                "; native observations are first-chance events, not proof of a fatal crash.");
            WriteObserverStatus();
            ReadAutomationRequest();
        }

        private Dictionary<string, object> CreateSessionManifest()
        {
            using (Process process = Process.GetCurrentProcess())
                return new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1, ["processSessionId"] = processSessionId,
                    ["processId"] = process.Id,
                    ["processStartedUtc"] = process.StartTime.ToUniversalTime().ToString("o"),
                    ["captureStartedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["timestampFrequency"] = Stopwatch.Frequency,
                    ["captureStartTimestamp"] = Stopwatch.GetTimestamp(),
                    ["diagnosticBuild"] = true, ["pluginVersion"] = CrashDiagnosticsPlugin.PluginVersion,
                    ["pluginMvid"] = typeof(CrashDiagnosticsPlugin).Module.ModuleVersionId.ToString(),
                    ["unityVersion"] = Application.unityVersion,
                    ["platform"] = Environment.OSVersion.ToString(),
                    ["wineOrProton"] = DetectWine(),
                    ["protonVersion"] = "unknown (Wine evidence does not identify a Proton release)",
                    ["sessionBudgetBytes"] = SessionBudget, ["rootBudgetBytes"] = RootBudget,
                    ["metadataFileLimitBytes"] = MetadataFileLimit,
                    ["noGameplayMutation"] = true, ["noFullHeapDump"] = true,
                    ["limitations"] = new List<object> {
                        "Late registration may miss startup faults and previously compiled code.",
                        "Snapshots from different timestamps are not a consistent whole-process image.",
                        "Existing CrashBreadcrumbs are copied only if already enabled; foreign configuration is unchanged.",
                        "Native synchronized saves that bypass the managed save entry are not observed by ManagedSaveEvents.",
                        "Proton and real multiplayer acceptance are separate required pilot checks."
                    }
                };
        }

        private static string DetectWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandleW("ntdll.dll");
                IntPtr function = ntdll == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(ntdll, "wine_get_version");
                if (function == IntPtr.Zero) return "not detected";
                return "Wine " + Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<WineVersion>(function)());
            }
            catch (Exception error) { return "unknown: " + error.GetType().Name; }
        }

        private string CreateSessionDirectory()
        {
            string root = Path.Combine(Paths.BepInExRootPath, "CrashDiagnostics");
            using (var admission = new Mutex(false, @"Local\SerpsCrashDiagnostics.Admission"))
            {
                bool owns = false;
                try
                {
                    try { owns = admission.WaitOne(TimeSpan.FromSeconds(2)); }
                    catch (AbandonedMutexException) { owns = true; }
                    if (!owns) throw new IOException("Diagnostic session admission is busy.");
                    Directory.CreateDirectory(root);
                    // Reserve each existing session's maximum, not merely its present file sizes.
                    // Unowned leftovers and exported files inside this root still consume real budget.
                    long reservations = 0;
                    foreach (string file in Directory.GetFiles(root))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("A diagnostic-root file is a reparse point.");
                        reservations = checked(reservations + new FileInfo(file).Length);
                    }
                    foreach (string child in Directory.GetDirectories(root))
                    {
                        long actual = CountFiles(child, 0);
                        reservations = checked(reservations + (File.Exists(Path.Combine(child, "session.json")) ?
                            Math.Max(actual, SessionBudget) : actual));
                    }
                    if (reservations > RootBudget - SessionBudget)
                        throw new IOException("The 512 MiB diagnostic budget has no room for a 128 MiB session reservation. Save/export previous evidence, then remove old session folders manually.");
                    using (Process process = Process.GetCurrentProcess())
                    {
                        processSessionId = Guid.NewGuid().ToString("N");
                        string directory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") +
                            "-p" + process.Id.ToString(CultureInfo.InvariantCulture) + "-" + processSessionId.Substring(0, 8));
                        Directory.CreateDirectory(directory);
                        // Reserve inside the admission lock, before the collector starts or other processes admit.
                        string initial = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> {
                            ["schemaVersion"] = 1, ["processSessionId"] = processSessionId,
                            ["processId"] = process.Id, ["reservedBytes"] = SessionBudget });
                        File.WriteAllText(Path.Combine(directory, "session.json"), initial, new UTF8Encoding(false));
                        return directory;
                    }
                }
                finally { if (owns) admission.ReleaseMutex(); }
            }
        }

        private static long CountFiles(string directory, int depth)
        {
            if (depth > 12) throw new IOException("Unexpected diagnostic directory nesting.");
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A diagnostic directory is a reparse point; budget admission was rejected.");
            long result = 0;
            foreach (string file in Directory.GetFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A diagnostic file is a reparse point.");
                result = checked(result + new FileInfo(file).Length);
            }
            foreach (string child in Directory.GetDirectories(directory)) result = checked(result + CountFiles(child, depth + 1));
            return result;
        }

        private void TryWriteInventory()
        {
            // Inventory is supplementary evidence. Unsupported module enumeration or
            // an inventory-file error must not prevent the native capture from starting.
            try { WriteInventory(); }
            catch (Exception error)
            {
                try { Log("INVENTORY_UNAVAILABLE; native capture remains independent. " + error); }
                catch { } // A reporting failure must not promote optional work to a startup gate.
            }
        }

        private void WriteInventory()
        {
            var plugins = new List<object>();
            int pluginCount = Chainloader.PluginInfos.Count;
            foreach (var pair in Chainloader.PluginInfos)
            {
                if (plugins.Count >= 256) break;
                var info = pair.Value;
                plugins.Add(new Dictionary<string, object> {
                    ["guid"] = info.Metadata.GUID, ["name"] = info.Metadata.Name,
                    ["version"] = info.Metadata.Version.ToString(), ["file"] = FileIdentity(info.Location) });
            }
            var assemblies = new List<object>();
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in loadedAssemblies)
            {
                if (assemblies.Count >= 512) break;
                try
                {
                    assemblies.Add(new Dictionary<string, object> {
                        ["name"] = assembly.FullName, ["mvid"] = assembly.ManifestModule.ModuleVersionId.ToString(),
                        ["file"] = assembly.IsDynamic ? null : FileIdentity(assembly.Location), ["dynamic"] = assembly.IsDynamic });
                }
                catch (Exception error) { assemblies.Add(new Dictionary<string, object> { ["name"] = assembly.FullName, ["error"] = error.GetType().Name }); }
            }
            var modules = new List<object>();
            int moduleCount;
            using (Process process = Process.GetCurrentProcess())
            {
                moduleCount = process.Modules.Count;
                foreach (ProcessModule module in process.Modules)
                {
                    if (modules.Count >= 512) break;
                    try
                    {
                        modules.Add(new Dictionary<string, object> {
                            ["name"] = module.ModuleName, ["baseAddress"] = "0x" + module.BaseAddress.ToInt64().ToString("X"),
                            ["imageSize"] = module.ModuleMemorySize, ["file"] = FileIdentity(module.FileName) });
                    }
                    catch (Exception error) { modules.Add(new Dictionary<string, object> { ["name"] = module.ModuleName, ["error"] = error.GetType().Name }); }
                }
            }
            WriteMetadata("inventory.json", new Dictionary<string, object> {
                ["schemaVersion"] = 1, ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                ["plugins"] = plugins, ["assemblies"] = assemblies, ["modules"] = modules,
                ["omittedPlugins"] = Math.Max(0, pluginCount - plugins.Count),
                ["omittedAssemblies"] = Math.Max(0, loadedAssemblies.Length - assemblies.Count),
                ["omittedModules"] = Math.Max(0, moduleCount - modules.Count),
                ["pluginListLimit"] = 256, ["assemblyListLimit"] = 512, ["moduleListLimit"] = 512 });
        }

        private object FileIdentity(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (metadataGate)
            {
                object cached;
                if (hashCache.TryGetValue(path, out cached)) return cached;
                var row = new Dictionary<string, object> {
                    ["path"] = path, ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["identityScope"] = "disk snapshot, cached for this process; not proof of unmodified live code" };
                try
                {
                    var info = new FileInfo(path);
                    row["size"] = info.Length;
                    row["lastWriteUtc"] = info.LastWriteTimeUtc.ToString("o");
                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var sha = SHA256.Create()) row["sha256"] = Hex(sha.ComputeHash(input));
                }
                catch (Exception error) { row["error"] = error.GetType().Name + ": " + error.Message; }
                hashCache[path] = row;
                return row;
            }
        }

        private void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                string nativePath = Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
                var identity = (Dictionary<string, object>)FileIdentity(nativePath);
                string sha = identity.ContainsKey("sha256") ? (string)identity["sha256"] : string.Empty;
                var memory = context.Memory;
                if (memory.Length < 64) throw new InvalidDataException("Native image has no DOS header.");
                int pe = ReadInt32(memory, 0x3c);
                if (pe < 0 || pe > memory.Length - 88 || ReadInt32(memory, pe) != 0x00004550)
                    throw new InvalidDataException("Invalid native PE header.");
                ulong size = unchecked((uint)ReadInt32(memory, pe + 24 + 56));
                uint registered = NativeMethods.CD_RegisterImage(unchecked((ulong)context.ModuleHandle.ToInt64()), size,
                    "CrusaderDE.dll", sha);
                var sections = new List<object>();
                int count = memory[pe + 6] | memory[pe + 7] << 8;
                int optionalSize = memory[pe + 20] | memory[pe + 21] << 8;
                int table = checked(pe + 24 + optionalSize);
                for (int i = 0; i < count && i < 96; i++)
                {
                    int section = checked(table + i * 40);
                    if (section < 0 || section > memory.Length - 40) break;
                    uint characteristics = unchecked((uint)ReadInt32(memory, section + 36));
                    if ((characteristics & 0x20000000) == 0) continue;
                    int length = ReadInt32(memory, section + 8), rva = ReadInt32(memory, section + 12);
                    var row = new Dictionary<string, object> { ["rva"] = rva, ["virtualSize"] = length };
                    if (rva >= 0 && length > 0 && rva <= memory.Length - length && length <= 64 * 1024 * 1024)
                    {
                        using (var hash = SHA256.Create()) row["loadSnapshotSha256"] = Hex(hash.ComputeHash(memory.Slice(rva, length).ToArray()));
                        row["liveSha256"] = HashReadableMemory(new IntPtr(checked(context.ModuleHandle.ToInt64() + rva)), length);
                    }
                    else row["hashUnavailable"] = "outside the load snapshot or above the 64 MiB section hashing limit";
                    sections.Add(row);
                }
                WriteMetadata("images.json", new Dictionary<string, object> {
                    ["schemaVersion"] = 1, ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["name"] = "CrusaderDE.dll", ["baseAddress"] = "0x" + context.ModuleHandle.ToInt64().ToString("X"),
                    ["imageSize"] = size, ["identity"] = identity, ["registrationResult"] = registered,
                    ["executableSections"] = sections,
                    ["liveBytesMayContainHooks"] = true });
                SnapshotNativeSites();
                Log("LIBRARY_REGISTERED result=" + registered + "; hash=" + sha);
            }
            catch (Exception error) { ReportFailure("native image inventory", error); }
        }

        private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;

        private static string HashReadableMemory(IntPtr address, int length)
        {
            byte[] buffer = new byte[64 * 1024];
            using (var sha = SHA256.Create())
            {
                int read = 0;
                while (read < length)
                {
                    int take = Math.Min(buffer.Length, length - read);
                    UIntPtr copied;
                    if (!NativeMethods.ReadProcessMemory(NativeMethods.GetCurrentProcess(),
                        new IntPtr(checked(address.ToInt64() + read)), buffer, new UIntPtr((uint)take), out copied) ||
                        copied.ToUInt64() != (ulong)take) return "unavailable (memory read failed)";
                    sha.TransformBlock(buffer, 0, take, buffer, 0);
                    read += take;
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Hex(sha.Hash);
            }
        }

        private void SnapshotNativeSites()
        {
            var rows = new List<object>();
            var sites = NativeSiteRegistry.Registrations;
            for (int i = 0; i < sites.Count && i < 1024; i++)
            {
                var site = sites[i];
                rows.Add(new Dictionary<string, object> {
                    ["ownerGuid"] = site.OwnerGuid, ["name"] = site.Name,
                    ["rva"] = site.Rva, ["length"] = site.Length, ["mode"] = site.Mode.ToString(),
                    ["sequence"] = site.Sequence, ["capabilityId"] = site.CapabilityId });
            }
            WriteMetadata("native-sites.json", new Dictionary<string, object> {
                ["schemaVersion"] = 1, ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                ["registrations"] = rows, ["totalRegistrations"] = sites.Count,
                ["omittedRegistrations"] = Math.Max(0, sites.Count - rows.Count),
                ["limitations"] = "Cooperative registrations do not enumerate every extender/fixes/third-party trampoline and do not identify a cause." });
        }

        private void MissionInitializing(MissionLifecycleNotification notification) => Mission(notification, 5);
        private void MissionStarted(MissionLifecycleNotification notification) => Mission(notification, 6);
        private void MissionEnded(MissionLifecycleNotification notification) => Mission(notification, 7);

        private void Mission(MissionLifecycleNotification notification, uint kind)
        {
            try
            {
                MissionContext context = notification.Context;
                Interlocked.Exchange(ref missionId, notification.Kind == MissionLifecycleKind.End ? 0 : context.SessionId);
                Volatile.Write(ref tickKnown, 0);
                Volatile.Write(ref missionTickMarker, 0);
                var snapshot = new Dictionary<string, object> {
                    ["schemaVersion"] = 1, ["missionId"] = context.SessionId,
                    ["kind"] = notification.Kind.ToString(), ["phase"] = notification.Phase.ToString(),
                    ["endReason"] = notification.EndReason.ToString(), ["isReplay"] = notification.IsReplay,
                    ["startKind"] = context.StartKind.ToString(), ["fileName"] = Clip(Path.GetFileName(context.FilePath), 256),
                    ["mapName"] = Clip(context.MapName, 256), ["isHost"] = context.IsHost,
                    ["localPlayerId"] = context.LocalPlayerId, ["isSave"] = context.IsSave,
                    ["isEditor"] = context.IsEditor, ["mode"] = Clip(context.Mode.ToDiagnosticString(), 1200),
                    ["timestampUtc"] = DateTime.UtcNow.ToString("o") };
                string json = Shared.DependencyFreeJson.Serialize(snapshot);
                if (Encoding.UTF8.GetByteCount(json) > 4095)
                {
                    snapshot["mapName"] = null;
                    snapshot["fileName"] = null;
                    snapshot["contextNamesOmittedForBudget"] = true;
                    json = Shared.DependencyFreeJson.Serialize(snapshot);
                }
                uint status = NativeMethods.CD_PublishContext(json);
                Record(kind, context.SessionId, (int)notification.Phase, (int)notification.EndReason);
                Log("MISSION kind=" + notification.Kind + "; id=" + context.SessionId + "; contextResult=" + status);
            }
            catch (Exception error) { ReportFailure("mission observation", error); }
        }

        private void EngineBegin() => Record(1, Interlocked.Read(ref missionId), Interlocked.Read(ref lastTick), Volatile.Read(ref tickKnown));
        private void EngineEnd() => Record(2, Interlocked.Read(ref missionId), Interlocked.Read(ref lastTick), Volatile.Read(ref tickKnown));
        private void Tick(int tick)
        {
            Interlocked.Exchange(ref lastTick, tick);
            Volatile.Write(ref tickKnown, 1);
            Record(3, Interlocked.Read(ref missionId), tick, 0);
            if (Interlocked.CompareExchange(ref missionTickMarker, 1, 0) == 0) Record(12, Interlocked.Read(ref missionId), tick, 0);
        }

        private object SaveBegin(ManagedSaveCall call)
        {
            long id = Interlocked.Increment(ref saveSequence);
            Record(8, Interlocked.Read(ref missionId), call.MapSave ? 1 : 0, id);
            return id;
        }

        private void SaveEnd(ManagedSaveCompletion completion, object state)
        {
            long result = completion.Exception != null ? -2 : completion.WasSkipped ? -1 : completion.Result == true ? 1 : 0;
            Record(9, Interlocked.Read(ref missionId), result, state is long ? (long)state : 0);
        }

        private void BeforeRender()
        {
            if (!nativeActive || quitting || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                renderCallbacks++;
                long now = Stopwatch.GetTimestamp();
                if (renderCallbacks == 2) Log("POST_STARTUP_RENDER frame=" + lastFrame + "; static runtime remains active after plugin component cleanup.");
                if (!finalInventoryWritten && renderCallbacks >= 120)
                {
                    finalInventoryWritten = true;
                    TryWriteInventory();
                    SnapshotNativeSites();
                }
                StartRequestedAutomation();
                if (renderCallbacks >= 120 && now >= nextBootstrap)
                {
                    nextBootstrap = now + Stopwatch.Frequency / 10;
                    NativeMethods.CD_Bootstrap(500);
                }
                if (now < nextHeartbeat) return;
                nextHeartbeat = now + Stopwatch.Frequency;
                Record(4, Interlocked.Read(ref missionId), lastFrame, Interlocked.Read(ref lastTick));
                uint status = NativeMethods.CD_GetStatus();
                if (status != previousStatus) { Log("CAPABILITY_STATUS old=" + previousStatus + "; current=" + status); previousStatus = status; }
                if (Volatile.Read(ref missionTickMarker) == 1 && Interlocked.CompareExchange(ref missionTickMarker, 2, 1) == 1)
                    Log("MISSION_TICK id=" + Interlocked.Read(ref missionId) + "; tick=" + Interlocked.Read(ref lastTick));
            }
            catch (Exception error) { ReportFailure("render heartbeat", error); }
        }

        private void Quitting()
        {
            if (!nativeActive || quitting) return;
            quitting = true;
            try { Record(10, Interlocked.Read(ref missionId), 0, 0); NativeMethods.CD_MarkShutdown(); }
            catch (Exception error) { ReportFailure("shutdown marker", error); }
        }

        private void Record(uint kind, long first, long second, long third)
        {
            if (!nativeActive) return;
            try { NativeMethods.CD_RecordEvent(kind, first, second, third); }
            catch { nativeActive = false; } // Never let a diagnostic interop failure escape a simulation observer.
        }

        private void SetObserverStatus(string name, bool available, string reason)
        {
            lock (metadataGate) observerStatus[name] = new Dictionary<string, object> {
                ["available"] = available, ["reason"] = reason ?? string.Empty };
            if (!available) Log("OBSERVER_UNAVAILABLE " + name + ": " + reason);
        }

        private void WriteObserverStatus()
        {
            lock (metadataGate) WriteMetadata("observer-status.json", new Dictionary<string, object> {
                ["schemaVersion"] = 1, ["observers"] = observerStatus });
        }

        private void WriteMetadata(string name, object data)
        {
            lock (metadataGate)
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(Shared.DependencyFreeJson.Serialize(data));
                if (bytes.Length > MetadataFileLimit) throw new IOException("Metadata file budget exceeded: " + name);
                string target = Path.Combine(sessionDirectory, name), temporary = target + ".tmp";
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
            }
        }

        private void ReportFailure(string part, Exception error)
        {
            if (Interlocked.Exchange(ref failureReported, 1) == 0)
                log.LogWarning("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    "] DIAGNOSTIC_COMPONENT_FAILURE part=" + part + "; " + error);
        }

        private void Log(string message) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
        private static string Clip(string text, int maximum) => text == null || text.Length <= maximum ? text : text.Substring(0, maximum);
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr WineVersion();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}
