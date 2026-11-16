using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameMemReader;

/// <summary>Read-only handle on the game process. Opens with VM_READ only; never writes.</summary>
internal sealed class ProcessMemory : IDisposable
{
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private readonly IntPtr _handle;

    public int ProcessId { get; }
    public string ProcessName { get; }
    public ulong ModuleBase { get; }
    public uint ModuleSize { get; }
    public string ModulePath { get; }

    private ProcessMemory(Process process, ProcessModule module)
    {
        ProcessId = process.Id;
        ProcessName = process.ProcessName;
        ModuleBase = (ulong)module.BaseAddress;
        ModuleSize = (uint)module.ModuleMemorySize;
        ModulePath = module.FileName;
        _handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcess({process.Id}) failed");
    }

    public static ProcessMemory? TryAttach(string moduleName, out string? reason)
    {
        reason = null;
        var candidates = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("Crusader", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var p in candidates)
        {
            try
            {
                foreach (ProcessModule m in p.Modules)
                {
                    if (string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                        return new ProcessMemory(p, m);
                }
            }
            catch (Exception ex)
            {
                reason = $"{p.ProcessName}({p.Id}): {ex.Message}";
            }
        }
        reason ??= candidates.Count == 0
            ? "no running process whose name contains 'Crusader'"
            : $"process found but module {moduleName} not loaded yet";
        return null;
    }

    /// <summary>When set, reads go through the in-game probe (copied on the game's own thread) instead of ReadProcessMemory.</summary>
    public ProbeClient? Probe { get; set; }

    public byte[] Read(ulong address, int length)
    {
        if (Probe != null)
            return Probe.Read(address, length);
        var buffer = new byte[length];
        if (!TryRead(address, buffer))
            throw new InvalidOperationException($"ReadProcessMemory failed at 0x{address:X16} len={length} (error {Marshal.GetLastWin32Error()})");
        return buffer;
    }

    public bool TryRead(ulong address, Span<byte> buffer)
    {
        unsafe
        {
            fixed (byte* p = buffer)
            {
                return ReadProcessMemory(_handle, (IntPtr)(long)address, (IntPtr)p, (IntPtr)buffer.Length, out var read)
                    && read == (IntPtr)buffer.Length;
            }
        }
    }

    public T Read<T>(ulong address) where T : unmanaged
    {
        unsafe
        {
            var bytes = Read(address, sizeof(T));
            return MemoryMarshal.Read<T>(bytes);
        }
    }

    public bool HasExited
    {
        get
        {
            try { return Process.GetProcessById(ProcessId).HasExited; }
            catch { return true; }
        }
    }

    public void Dispose()
    {
        Probe?.Dispose();
        if (_handle != IntPtr.Zero)
            CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, IntPtr buffer, IntPtr size, out IntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
