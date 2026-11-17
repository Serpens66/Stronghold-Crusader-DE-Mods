using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;

namespace Shared
{
    // Foreign-patch detection for our hooks in CrusaderDE.dll (AGENTS/native-hooks.md#fremde-patches-erkennen).
    // The Script Extender takes the load snapshot before any plugin writes code, so a live byte inside a
    // hook's displaced span that differs from the snapshot belongs to an earlier patch: the Script Extender,
    // another mod or another of our mods. RedBird does not check this itself; an inline hook would relocate
    // a foreign jump as if it were game code, and a detour would overwrite it.
    // - Install: Commit prepares a transaction without writing, compares every displaced span with the
    //   snapshot and only then enables it. A deviation logs an Error and throws before any byte is written,
    //   so only that feature stays Vanilla. A foreign hook directly before or behind the span is outside it.
    // - Later: Watch records the bytes a hook published; Recheck compares them again (each mission or editor
    //   start) and logs every new deviation once as an Error. Nothing is ever rewritten.
    // Pure compares of a few bytes per hook; no scans.
    internal static class NativePatchGuard
    {
        private static readonly object Sync = new object();
        private static readonly List<WatchedSite> Watched = new List<WatchedSite>();
        private static ScanRegion image;
        private static ManualLogSource log;

        // Once per mod, before its first hook: the Script Extender's load snapshot of CrusaderDE.dll.
        internal static void Bind(CrusaderLibraryLoadContext context, ManualLogSource logSource)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            Bind(context.Region, logSource);
        }

        // Region must hold the unpatched image at its live base address (tests use a copy).
        internal static void Bind(ScanRegion snapshot, ManualLogSource logSource)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (Sync)
            {
                image = snapshot;
                log = logSource;
            }
        }

        internal static int WatchedCount
        {
            get { lock (Sync) return Watched.Count; }
        }

        // Replaces HookTransaction.Commit() for a transaction created with InstallOnCommit = false: RedBird
        // prepares every hook without writing, the spans are compared, then all hooks are enabled and
        // watched. Returns the prepared result unchanged when it is incomplete (nothing was written; the
        // caller rejects it as before). Throws on a foreign patch before any byte is written.
        internal static CommitResult Commit(HookTransaction transaction, string feature)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.Options.InstallOnCommit)
                throw new InvalidOperationException(feature + ": NativePatchGuard.Commit needs a transaction with " +
                    "InstallOnCommit = false; it enables the hooks itself after the span check.");
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess) return result;
            foreach (IHook hook in result.Installed) RequireUnpatched(hook, feature);
            var enabled = new List<IHook>(result.Installed.Count);
            try
            {
                foreach (IHook hook in result.Installed)
                {
                    hook.Enable();
                    enabled.Add(hook);
                }
            }
            catch
            {
                // Same rollback as RedBird's RollbackAndThrow: only this unpublished candidate, restoring
                // the original bytes that were just verified against the snapshot.
                for (int index = enabled.Count - 1; index >= 0; index--)
                {
                    try { enabled[index].Disable(); }
                    catch (Exception error) { Error(feature + ": rollback of " + enabled[index].Name + " failed: " + error.Message); }
                }
                throw;
            }
            foreach (IHook hook in enabled) Watch(hook, feature);
            return result;
        }

        // For a prepared hook that the caller enables itself: throws (after an Error) if its displaced span
        // already differs from the snapshot.
        internal static void RequireUnpatched(IHook hook, string feature)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            if (hook.IsInstalled)
                throw new InvalidOperationException(feature + ": " + hook.Name + " is already enabled; check it before Enable.");
            RequireSpanUnpatched(SpanOf(hook, feature), hook.Name, feature);
        }

        // For a span whose length is known before the hook exists (e.g. an audited detour entry): lets a
        // hook framework that decodes live bytes never see a foreign patch. Same Error as above.
        internal static void RequireUnpatched(ulong address, int length, string name, string feature)
        {
            if (address == 0 || length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            RequireSpanUnpatched(new HookSpan(address, length), name, feature);
        }

        private static void RequireSpanUnpatched(HookSpan span, string name, string feature)
        {
            ScanRegion reference = RequireImage(feature);
            int offset = OffsetOf(reference, span, feature);
            byte[] expected = reference.Span.Slice(offset, span.Length).ToArray();
            byte[] live = ReadLive(span.Address, span.Length);
            int difference = FirstDifference(expected, live);
            if (difference >= 0)
            {
                string message = "NATIVE_FOREIGN_PATCH: " + feature + " stays Vanilla; " + name +
                    " would overwrite a foreign patch at RVA 0x" + offset.ToString("X", CultureInfo.InvariantCulture) +
                    " (" + span.Length.ToString(CultureInfo.InvariantCulture) + " bytes, first difference at +" +
                    difference.ToString(CultureInfo.InvariantCulture) + "): expected " + Hex(expected) + ", live " + Hex(live) +
                    ". " + DescribeJump(live, difference, span.Address);
                Error(message);
                throw new InvalidOperationException(message);
            }
        }

        // After a successful Enable: records the published bytes for Recheck. A candidate that its owner later
        // rolls back (Disable/Dispose after a failed post-check) is dropped by Recheck, not reported.
        internal static void Watch(IHook hook, string feature)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            if (!hook.IsInstalled)
                throw new InvalidOperationException(feature + ": " + hook.Name + " is not enabled and cannot be watched.");
            HookSpan span = SpanOf(hook, feature);
            ScanRegion reference = RequireImage(feature);
            int offset = OffsetOf(reference, span, feature);
            var site = new WatchedSite(hook, feature, span.Address, offset, ReadLive(span.Address, span.Length));
            lock (Sync) Watched.Add(site);
        }

        // Compares every published span with its recorded bytes. Logs each new deviation once as an Error
        // and returns how many were new. Call on the main thread at mission and editor start.
        internal static int Recheck()
        {
            WatchedSite[] sites;
            lock (Sync) sites = Watched.ToArray();
            int reported = 0;
            foreach (WatchedSite site in sites)
            {
                if (!site.Hook.IsInstalled)
                {
                    lock (Sync) Watched.Remove(site);
                    continue;
                }
                byte[] live;
                try { live = ReadLive(site.Address, site.Published.Length); }
                catch (Exception error)
                {
                    Error("NATIVE_FOREIGN_PATCH_LATE: " + site.Feature + ": RVA 0x" + site.Rva.ToString("X", CultureInfo.InvariantCulture) +
                        " could not be read: " + error.Message);
                    continue;
                }
                int difference = FirstDifference(site.Published, live);
                if (difference < 0)
                {
                    site.Reported = null;
                    continue;
                }
                if (site.Reported != null && FirstDifference(site.Reported, live) < 0) continue;
                site.Reported = live;
                reported++;
                Error("NATIVE_FOREIGN_PATCH_LATE: " + site.Feature + " (" + site.Name + ") was overwritten after it was " +
                    "published; it can run wrongly or crash until the other patch is removed. RVA 0x" +
                    site.Rva.ToString("X", CultureInfo.InvariantCulture) + " (" + site.Published.Length.ToString(CultureInfo.InvariantCulture) +
                    " bytes, first difference at +" + difference.ToString(CultureInfo.InvariantCulture) + "): published " +
                    Hex(site.Published) + ", live " + Hex(live) + ". " + DescribeJump(live, difference, site.Address));
            }
            return reported;
        }

        private static HookSpan SpanOf(IHook hook, string feature)
        {
            int length;
            if (hook is X64InlineHook inline) length = inline.DisplacedByteCount;
            else if (hook is X64AssemblyPatch patch) length = patch.OverwrittenByteCount;
            else
            {
                // NativeDetour<TFunction> is generic; its public DisplacedByteCount is read by name.
                PropertyInfo property = hook.GetType().GetProperty("DisplacedByteCount", BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(int))
                    throw new NotSupportedException(feature + ": " + hook.Name + " (" + hook.GetType().Name +
                        ") has no known displaced span; NativePatchGuard cannot check it.");
                length = (int)property.GetValue(hook, null);
            }
            if (hook.TargetAddress == 0 || length <= 0)
                throw new InvalidOperationException(feature + ": " + hook.Name + " has no prepared span.");
            return new HookSpan(hook.TargetAddress, length);
        }

        private static ScanRegion RequireImage(string feature)
        {
            lock (Sync)
            {
                if (image == null) throw new InvalidOperationException(feature + ": NativePatchGuard is not bound to the load snapshot.");
                return image;
            }
        }

        private static int OffsetOf(ScanRegion reference, HookSpan span, string feature)
        {
            if (span.Address < reference.BaseAddress || span.Address - reference.BaseAddress > (ulong)(reference.Length - span.Length))
                throw new InvalidOperationException(feature + ": span 0x" + span.Address.ToString("X", CultureInfo.InvariantCulture) +
                    " lies outside CrusaderDE.dll (a detour that followed a jump out of the module?).");
            return (int)(span.Address - reference.BaseAddress);
        }

        private static byte[] ReadLive(ulong address, int length)
        {
            var bytes = new byte[length];
            Marshal.Copy(new IntPtr(unchecked((long)address)), bytes, 0, length);
            return bytes;
        }

        private static int FirstDifference(byte[] expected, byte[] live)
        {
            int length = Math.Min(expected.Length, live.Length);
            for (int index = 0; index < length; index++)
                if (expected[index] != live[index]) return index;
            return expected.Length == live.Length ? -1 : length;
        }

        // Hint at the originator: a jump that covers the first difference and the module it leads into.
        internal static string DescribeJump(byte[] live, int difference, ulong address)
        {
            for (int start = difference; start >= 0 && start >= difference - 13; start--)
            {
                ulong target;
                int length;
                if (start + 5 <= live.Length && live[start] == 0xE9)
                {
                    length = 5;
                    target = unchecked(address + (ulong)start + 5 + (ulong)(long)BitConverter.ToInt32(live, start + 1));
                }
                else if (start + 6 <= live.Length && live[start] == 0xFF && live[start + 1] == 0x25)
                {
                    length = 6;
                    ulong slot = unchecked(address + (ulong)start + 6 + (ulong)(long)BitConverter.ToInt32(live, start + 2));
                    if (!TryReadPointer(live, address, slot, out target)) return "Indirect jump at +" + start + "; its pointer is unreadable.";
                }
                else continue;
                if (start + length <= difference) continue;
                return "Jump at +" + start.ToString(CultureInfo.InvariantCulture) + " to 0x" + target.ToString("X", CultureInfo.InvariantCulture) +
                    " in " + ModuleOf(target) + ".";
            }
            return "No jump covers the first difference.";
        }

        private static bool TryReadPointer(byte[] live, ulong address, ulong slot, out ulong value)
        {
            value = 0;
            if (slot >= address && slot + 8 <= address + (ulong)live.Length)
            {
                value = BitConverter.ToUInt64(live, (int)(slot - address));
                return true;
            }
            try
            {
                byte[] bytes = CommittedMemory.ReadUpTo(new IntPtr(unchecked((long)slot)), 8);
                if (bytes.Length < 8) return false;
                value = BitConverter.ToUInt64(bytes, 0);
                return true;
            }
            catch { return false; }
        }

        private static string ModuleOf(ulong address)
        {
            try
            {
                if (!GetModuleHandleExW(ModuleFromAddress | ModuleUnchangedRefcount, new IntPtr(unchecked((long)address)), out IntPtr module) ||
                    module == IntPtr.Zero)
                    return "memory outside any module (a hook stub of another mod or the Script Extender)";
                var name = new StringBuilder(1024);
                return GetModuleFileNameW(module, name, (uint)name.Capacity) == 0 ? "an unnamed module" : name.ToString();
            }
            catch { return "an unknown module"; }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);

        private static void Error(string message)
        {
            ManualLogSource target;
            lock (Sync) target = log;
            try { DebugLogHelper.LogError(target, message); } catch { }
        }


        private readonly struct HookSpan
        {
            internal HookSpan(ulong address, int length)
            {
                Address = address;
                Length = length;
            }

            internal ulong Address { get; }
            internal int Length { get; }
        }

        private sealed class WatchedSite
        {
            internal WatchedSite(IHook hook, string feature, ulong address, int rva, byte[] published)
            {
                Hook = hook;
                Feature = feature;
                Name = hook.Name;
                Address = address;
                Rva = rva;
                Published = published;
            }

            internal IHook Hook { get; }
            internal string Feature { get; }
            internal string Name { get; }
            internal ulong Address { get; }
            internal int Rva { get; }
            internal byte[] Published { get; }
            // Live bytes of the last reported deviation; Recheck runs on one thread at a time.
            internal byte[] Reported { get; set; }
        }

        private const uint ModuleFromAddress = 0x4;
        private const uint ModuleUnchangedRefcount = 0x2;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetModuleHandleExW(uint flags, IntPtr address, out IntPtr module);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder name, uint size);
    }
}
