using System;
using System.Runtime.InteropServices;

public static class MinimumWindowSize
{
	public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	public struct Minmaxinfo
	{
		public Point ptReserved;

		public Point ptMaxSize;

		public Point ptMaxPosition;

		public Point ptMinTrackSize;

		public Point ptMaxTrackSize;
	}

	public struct Point
	{
		public int x;

		public int y;
	}

	public const int DefaultValue = -1;

	public const uint WM_GETMINMAXINFO = 36u;

	public const int GWLP_WNDPROC = -4;

	public static int width;

	public static int height;

	public static bool enabled;

	public static HandleRef hMainWindow;

	public static IntPtr unityWndProcHandler;

	public static IntPtr customWndProcHandler;

	public static WndProcDelegate procDelegate;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern bool SetWindowText(IntPtr hwnd, string lpString);

	[DllImport("user32.dll")]
	public static extern IntPtr FindWindow(string className, string windowName);

	public static void SetWindowTitle(string title)
	{
		SetWindowText(FindWindow(null, "Stronghold Crusader Definitive Edition"), title);
	}

	public static void Set(int minWidth, int minHeight, string altWindowTitle = "")
	{
		if (altWindowTitle != "")
		{
			SetWindowTitle(altWindowTitle);
		}
		if (minWidth < 0 || minHeight < 0)
		{
			throw new ArgumentException("Any component of min size cannot be less than 0");
		}
		width = minWidth;
		height = minHeight;
		if (!enabled)
		{
			hMainWindow = new HandleRef(null, GetActiveWindow());
			procDelegate = WndProc;
			customWndProcHandler = Marshal.GetFunctionPointerForDelegate(procDelegate);
			unityWndProcHandler = SetWindowLongPtr(hMainWindow, -4, customWndProcHandler);
			enabled = true;
		}
	}

	public static void Reset()
	{
		if (enabled)
		{
			SetWindowLongPtr(hMainWindow, -4, unityWndProcHandler);
			hMainWindow = new HandleRef(null, IntPtr.Zero);
			unityWndProcHandler = IntPtr.Zero;
			customWndProcHandler = IntPtr.Zero;
			procDelegate = null;
			width = 0;
			height = 0;
			enabled = false;
		}
	}

	public static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if (msg != 36)
		{
			return CallWindowProc(unityWndProcHandler, hWnd, msg, wParam, lParam);
		}
		Minmaxinfo structure = (Minmaxinfo)Marshal.PtrToStructure(lParam, typeof(Minmaxinfo));
		structure.ptMinTrackSize = new Point
		{
			x = width,
			y = height
		};
		Marshal.StructureToPtr(structure, lParam, fDeleteOld: false);
		return DefWindowProc(hWnd, msg, wParam, lParam);
	}

	[DllImport("user32.dll")]
	public static extern IntPtr GetActiveWindow();

	[DllImport("user32.dll", EntryPoint = "CallWindowProcA")]
	public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", EntryPoint = "DefWindowProcA")]
	public static extern IntPtr DefWindowProc(IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	public static IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong)
	{
		if (IntPtr.Size == 8)
		{
			return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
		}
		return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
	}

	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	public static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	public static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);
}
