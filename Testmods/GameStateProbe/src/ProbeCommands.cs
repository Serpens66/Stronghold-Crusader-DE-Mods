using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace GameStateProbe
{
    /// <summary>
    /// Commands (all read-only except "quit"):
    ///   ping                                   tick, thread, context
    ///   mem   {"va":"0x..","len":n}            raw copy of committed readable memory (base64)
    ///   memv  {"ranges":[{"va","len"},...]}    several ranges copied in the same tick
    ///   eval  {"path":"Type::A.B[3].GetX()","depth":2,"limit":50,"props":false}
    ///   types {"filter":"Unit"}                loaded type names
    ///   members {"type":"Name","static":true}  fields/properties/methods of a type
    ///   quit                                   Application.Quit on the main thread (ends a test session)
    /// </summary>
    internal static class ProbeCommands
    {
        public static object Execute(Dictionary<string, object> req, string context)
        {
            string cmd = ProbeServer.Str(req, "cmd") ?? "";
            object result;
            switch (cmd)
            {
                case "ping": result = null; break;
                case "mem": result = Mem(req); break;
                case "memv": result = MemV(req); break;
                case "eval": result = Eval(req); break;
                case "types": result = Types(req); break;
                case "members": result = Members(req); break;
                case "quit":
                    UnityMainThreadDispatcherQuit();
                    result = "quit requested";
                    break;
                default: return ProbeServer.Error("unknown cmd '" + cmd + "'");
            }
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["context"] = context,
                ["tick"] = ProbeServer.LastTick,
                ["thread"] = Thread.CurrentThread.ManagedThreadId,
                ["time"] = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                ["result"] = result
            };
        }

        private static void UnityMainThreadDispatcherQuit() =>
            SHCDESE.API.UnityMainThreadDispatcher.EnqueueStatic(UnityEngine.Application.Quit);

        // ------------------------------------------------------------------ memory

        private static object Mem(Dictionary<string, object> req) =>
            Copy(ParseAddress(ProbeServer.Str(req, "va")), ProbeServer.Int(req, "len", 16));

        private static object MemV(Dictionary<string, object> req)
        {
            var list = new List<object>();
            if (!(req.TryGetValue("ranges", out var r) && r is List<object> ranges)) throw new ArgumentException("ranges missing");
            foreach (var o in ranges)
            {
                var d = (Dictionary<string, object>)o;
                list.Add(Copy(ParseAddress(ProbeServer.Str(d, "va")), ProbeServer.Int(d, "len", 16)));
            }
            return list;
        }

        private static Dictionary<string, object> Copy(ulong va, int len)
        {
            if (len <= 0 || len > 64 * 1024 * 1024) throw new ArgumentException("len must be 1..64 MiB");
            // Never dereference unknown memory in-process: every page of the range must be committed and readable.
            if (!IsReadable(va, (ulong)len)) throw new InvalidOperationException("range 0x" + va.ToString("X16") + "+" + len + " not fully readable");
            var bytes = new byte[len];
            Marshal.Copy(new IntPtr((long)va), bytes, 0, len);
            return new Dictionary<string, object> { ["va"] = "0x" + va.ToString("X16"), ["len"] = len, ["base64"] = Convert.ToBase64String(bytes) };
        }

        private static ulong ParseAddress(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("va missing");
            return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ulong.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : ulong.Parse(text, CultureInfo.InvariantCulture);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public ulong BaseAddress, AllocationBase;
            public uint AllocationProtect, Alignment1;
            public ulong RegionSize;
            public uint State, Protect, Type, Alignment2;
        }

        [DllImport("kernel32.dll")]
        private static extern UIntPtr VirtualQuery(UIntPtr address, out MemoryBasicInformation info, UIntPtr length);

        internal static bool IsReadable(ulong va, ulong len)
        {
            // PAGE_EXECUTE (0x10) is execute-only and not readable.
            const uint MEM_COMMIT = 0x1000, PAGE_NOACCESS = 0x01, PAGE_EXECUTE = 0x10, PAGE_GUARD = 0x100;
            ulong end = va + len;
            for (ulong p = va; p < end;)
            {
                if (VirtualQuery(new UIntPtr(p), out var mbi, new UIntPtr((uint)Marshal.SizeOf<MemoryBasicInformation>())) == UIntPtr.Zero) return false;
                if (mbi.State != MEM_COMMIT || (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) != 0 || mbi.Protect == 0
                    || (mbi.Protect & 0xFF) == PAGE_EXECUTE) return false;
                ulong next = mbi.BaseAddress + mbi.RegionSize;
                if (next <= p) return false;
                p = next;
            }
            return true;
        }

        // ------------------------------------------------------------------ reflection

        private static object Eval(Dictionary<string, object> req)
        {
            var path = ProbeServer.Str(req, "path") ?? throw new ArgumentException("path missing");
            var value = PathEvaluator.Evaluate(path);
            var dumper = new ObjectDumper(
                ProbeServer.Int(req, "depth", 2),
                ProbeServer.Int(req, "limit", 50),
                req.TryGetValue("props", out var p) && p is bool b && b);
            return dumper.Dump(value);
        }

        private static object Types(Dictionary<string, object> req)
        {
            var filter = ProbeServer.Str(req, "filter") ?? "";
            int limit = ProbeServer.Int(req, "limit", 200);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); } catch { return Enumerable.Empty<Type>(); } })
                .Where(t => t.FullName != null && t.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 && !t.FullName.Contains("<"))
                .Select(t => (object)(t.FullName + ", " + t.Assembly.GetName().Name))
                .OrderBy(s => (string)s)
                .Take(limit)
                .ToList();
        }

        private static object Members(Dictionary<string, object> req)
        {
            var type = PathEvaluator.ResolveType(ProbeServer.Str(req, "type") ?? throw new ArgumentException("type missing"));
            bool onlyStatic = !(req.TryGetValue("static", out var s) && s is bool sb && !sb);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | (onlyStatic ? 0 : BindingFlags.Instance) | BindingFlags.DeclaredOnly;
            var list = new List<object>();
            foreach (var f in type.GetFields(flags)) list.Add("field " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
            foreach (var p in type.GetProperties(flags)) list.Add("prop " + p.PropertyType.Name + " " + p.Name);
            foreach (var m in type.GetMethods(flags).Where(m => !m.IsSpecialName))
                list.Add("method " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name)) + ")");
            return list;
        }
    }
}
