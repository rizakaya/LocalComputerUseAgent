namespace ComputerAgent.Models;

internal sealed record TargetWindow(string Name, nint Handle, int ProcessId);

internal sealed record ScreenFrame(TargetWindow Window, Rectangle Bounds, byte[] Png);
