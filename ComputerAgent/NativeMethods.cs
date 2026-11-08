using System.Runtime.InteropServices;

namespace ComputerAgent;

internal static class NativeMethods
{
    public const uint RootWindow = 2;
    public const int RestoreWindow = 9;
    public const uint KeyboardInput = 1;
    public const uint KeyUp = 0x0002;
    public const uint UnicodeKey = 0x0004;
    public const uint MouseMove = 0x0001;
    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
    public const uint VirtualDesktop = 0x4000;
    public const uint Absolute = 0x8000;

    public delegate bool WindowCallback(nint window, nint state);

    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] public static extern nint GetActiveWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(nint window);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint source, uint target, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback, nint state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(nint window, ref Position position);
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(Position position);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint window, uint kind);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static extern int GetDwmWindowRect(nint window, uint attribute, out Rect value, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Position(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputValue Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputValue
    {
        [FieldOffset(0)] public Mouse Mouse;
        [FieldOffset(0)] public Key Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Mouse
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Key
    {
        public ushort VirtualKey, ScanCode;
        public uint Flags, Time;
        public nuint ExtraInfo;
    }
}
