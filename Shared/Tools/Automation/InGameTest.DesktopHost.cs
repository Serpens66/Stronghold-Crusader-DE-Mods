using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;

public static class InGameTestDesktopHost
{
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct STARTUPINFO {
        public int cb; public string reserved, desktop, title;
        public int x,y,xsize,ysize,xchars,ychars,fill,flags;
        public short show, reservedSize; public IntPtr reserved2, stdin, stdout, stderr;
    }
    [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION {
        public IntPtr process, thread; public int pid, tid;
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError=true)] static extern IntPtr OpenInputDesktop(int flags, bool inherit, uint access);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool GetUserObjectInformationW(IntPtr obj, int index, StringBuilder buffer, int size, out int needed);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    delegate bool EnumWindow(IntPtr window, IntPtr param);
    [DllImport("user32.dll", SetLastError=true)] static extern bool EnumDesktopWindows(IntPtr desktop, EnumWindow callback, IntPtr param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr window, StringBuilder text, int count);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool CreateProcessW(string app, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes,
        bool inherit, uint flags, IntPtr environment, string directory, ref STARTUPINFO startup, out PROCESS_INFORMATION info);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static string ObjectName(IntPtr obj) {
        var text=new StringBuilder(512); int needed;
        if(!GetUserObjectInformationW(obj,2,text,1024,out needed)) throw new System.ComponentModel.Win32Exception();
        return text.ToString();
    }
    public static string InputDesktop() {
        IntPtr desktop=OpenInputDesktop(0,false,1);
        if(desktop==IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        try{return ObjectName(desktop);}finally{CloseDesktop(desktop);}
    }
    public static bool HasInputWindow(int processId) {
        IntPtr desktop = OpenInputDesktop(0, false, 1);
        if (desktop == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        try {
            foreach (WindowInfo window in Windows(desktop))
                if (window.Pid == processId) return true;
            return false;
        }
        finally { CloseDesktop(desktop); }
    }
    public sealed class WindowInfo { public long Handle; public int Pid; public string Desktop,Title; }
    public static WindowInfo[] Windows(IntPtr desktop) {
        var result=new List<WindowInfo>();
        string desktopName=ObjectName(desktop);
        EnumWindow callback=delegate(IntPtr window,IntPtr unused) {
            uint pid; uint tid=GetWindowThreadProcessId(window,out pid);
            if(tid==0 || pid==0) return true; // A startup window may disappear during enumeration.
            var title=new StringBuilder(512); GetWindowTextW(window,title,512);
            // EnumDesktopWindows already proves desktop membership. Handles returned by
            // GetThreadDesktop for another process must not be used as our own handles.
            result.Add(new WindowInfo{Handle=window.ToInt64(),Pid=(int)pid,Desktop=desktopName,Title=title.ToString()});
            return true;
        };
        if(!EnumDesktopWindows(desktop,callback,IntPtr.Zero)) {
            int error=Marshal.GetLastWin32Error();
            // A fresh desktop can have no top-level windows yet; a false return with
            // ERROR_SUCCESS is not a failure and must not abort the just-started game.
            if(error!=0) throw new System.ComponentModel.Win32Exception(error);
        }
        return result.ToArray();
    }
    public static Process Start(string exe,string arguments,string directory,string desktop) {
        var startup=new STARTUPINFO(); startup.cb=Marshal.SizeOf(typeof(STARTUPINFO)); startup.desktop="WinSta0\\"+desktop;
        PROCESS_INFORMATION info;
        if(!CreateProcessW(exe,new StringBuilder("\""+exe+"\" "+arguments),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,directory,ref startup,out info))
            throw new System.ComponentModel.Win32Exception();
        try {var process=Process.GetProcessById(info.pid); var handle=process.Handle; return process;}
        finally{CloseHandle(info.thread);CloseHandle(info.process);}
    }
}
