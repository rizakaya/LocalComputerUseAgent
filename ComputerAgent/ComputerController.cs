using System.ComponentModel;
using System.Runtime.InteropServices;
using ComputerAgent.Models;
using static ComputerAgent.NativeMethods;

namespace ComputerAgent;

internal sealed class ComputerController(TargetWindow webPortal, TargetWindow legacyApp)
{
    private const uint DwmExtendedFrameBounds = 9;
    private const uint DwmCloaked = 14;

    public Task OpenPortalAsync(CancellationToken cancellationToken) =>
        ActivateAsync(webPortal, cancellationToken);

    public async Task ApplyAsync(ScreenFrame frame, AgentDecision decision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFrameCurrent(frame);

        switch (decision.Action)
        {
            case "click":
                Click(frame, decision.X!.Value, decision.Y!.Value);
                break;
            case "type_text":
                Send(frame, TextInputs(decision.Text!));
                break;
            case "press_key":
                Send(frame, KeyInputs(decision.Key!));
                break;
            case "switch":
                TargetWindow target = decision.Target switch
                {
                    "WebPortal" => webPortal,
                    "LegacyApp" => legacyApp,
                    _ => throw new InvalidOperationException("Pencere hedefi izinli değil.")
                };
                await ActivateAsync(target, cancellationToken, frame.Window.Handle);
                break;
            default:
                throw new InvalidOperationException("Eylem girdi için izinli değil.");
        }
    }

    public TargetWindow ForegroundTarget()
    {
        nint foreground = GetForegroundWindow();
        if (foreground == webPortal.Handle)
        {
            return webPortal;
        }
        if (foreground == legacyApp.Handle)
        {
            return legacyApp;
        }
        throw new InvalidOperationException("İzinli uygulama odağı kayboldu.");
    }

    public Rectangle RequireVisibleClient(TargetWindow target)
    {
        if (target != webPortal && target != legacyApp)
        {
            throw new InvalidOperationException("Pencere hedefi izinli değil.");
        }
        RequireWindowIdentity(target);
        var origin = new Position(0, 0);
        if (GetForegroundWindow() != target.Handle || IsIconic(target.Handle)
            || !GetClientRect(target.Handle, out Rect client) || !ClientToScreen(target.Handle, ref origin))
        {
            throw new InvalidOperationException($"{target.Name} ön planda ve görünür olmalı.");
        }
        var bounds = new Rectangle(origin.X, origin.Y, client.Right, client.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0 || !Screen.FromHandle(target.Handle).Bounds.Contains(bounds)
            || !IsUnobstructed(target.Handle, bounds))
        {
            throw new InvalidOperationException($"{target.Name} içerik alanı örtülü veya ekranın dışında.");
        }
        return bounds;
    }

    public void EnsureFrameCurrent(ScreenFrame frame)
    {
        if (RequireVisibleClient(frame.Window) != frame.Bounds)
        {
            throw new InvalidOperationException("Pencere alanı son ekran görüntüsünden sonra değişti.");
        }
    }

    private static async Task ActivateAsync(
        TargetWindow target, CancellationToken cancellationToken, nint expectedSource = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindowIdentity(target);
        nint source = GetForegroundWindow();
        if (expectedSource != 0 && source != expectedSource)
        {
            throw new InvalidOperationException("Pencere geçişinden önce odak değişti.");
        }
        if (IsIconic(target.Handle))
        {
            ShowWindowAsync(target.Handle, RestoreWindow);
        }
        if (GetForegroundWindow() == target.Handle)
        {
            return;
        }

        if (!SetForegroundWindow(target.Handle) && GetForegroundWindow() == source)
        {
            BringAuthorizedWindowToTop(target.Handle, source);
        }
        for (int check = 0; check < 10; check++)
        {
            await Task.Delay(50, cancellationToken);
            if (GetForegroundWindow() == target.Handle)
            {
                return;
            }
        }
        throw new InvalidOperationException($"Windows {target.Name} penceresine geçişi reddetti.");
    }

    private static void BringAuthorizedWindowToTop(nint target, nint foreground)
    {
        // Share the foreground queue only for the synchronous activation call. No input is sent here.
        _ = GetActiveWindow();
        uint callerThread = GetCurrentThreadId();
        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        if (foregroundThread == 0 || foregroundThread == callerThread
            || !AttachThreadInput(callerThread, foregroundThread, true))
        {
            return;
        }
        try
        {
            if (GetForegroundWindow() == foreground)
            {
                BringWindowToTop(target);
            }
        }
        finally
        {
            AttachThreadInput(callerThread, foregroundThread, false);
        }
    }

    private static void RequireWindowIdentity(TargetWindow target)
    {
        if (GetWindowThreadProcessId(target.Handle, out uint actualProcess) == 0
            || actualProcess != target.ProcessId || !IsWindowVisible(target.Handle))
        {
            throw new InvalidOperationException($"{target.Name} için kayıtlı pencere artık kullanılamıyor.");
        }
    }

    private static bool IsUnobstructed(nint target, Rectangle bounds)
    {
        bool reachedTarget = false;
        EnumWindows((window, _) =>
        {
            if (window == target)
            {
                reachedTarget = true;
                return false;
            }
            if (!IsWindowVisible(window) || IsIconic(window)
                || DwmGetWindowAttribute(window, DwmCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return true;
            }
            bool hasBounds = GetDwmWindowRect(window, DwmExtendedFrameBounds, out Rect rect, Marshal.SizeOf<Rect>()) == 0
                || GetWindowRect(window, out rect);
            return hasBounds && !bounds.IntersectsWith(rect.ToRectangle());
        }, 0);
        return reachedTarget;
    }

    private void Click(ScreenFrame frame, int normalizedX, int normalizedY)
    {
        var point = new Position(
            frame.Bounds.Left + ScaleToPixel(normalizedX, frame.Bounds.Width),
            frame.Bounds.Top + ScaleToPixel(normalizedY, frame.Bounds.Height));
        if (GetAncestor(WindowFromPoint(point), RootWindow) != frame.Window.Handle)
        {
            throw new InvalidOperationException("Tıklama noktası kayıtlı hedef pencereye ait değil.");
        }

        Rectangle desktop = SystemInformation.VirtualScreen;
        var move = new Input
        {
            Value = new InputValue
            {
                Mouse = new Mouse
                {
                    X = ToAbsolute(point.X - desktop.Left, desktop.Width),
                    Y = ToAbsolute(point.Y - desktop.Top, desktop.Height),
                    Flags = MouseMove | Absolute | VirtualDesktop
                }
            }
        };
        Send(frame,
        [
            move,
            new Input { Value = new InputValue { Mouse = new Mouse { Flags = LeftDown } } },
            new Input { Value = new InputValue { Mouse = new Mouse { Flags = LeftUp } } }
        ]);
    }

    internal static int ScaleToPixel(int normalized, int size)
    {
        if (normalized is < 0 or > 1000 || size <= 0)
        {
            throw new InvalidOperationException("Tıklama koordinatı görüntü alanının dışında.");
        }
        return (int)(((long)normalized * (size - 1) + 500) / 1000);
    }

    private static int ToAbsolute(int pixel, int size) => (int)(((long)pixel * 65536 + 32768) / size);

    private static Input[] TextInputs(string value) =>
    [
        ..KeyInputs("select_all"),
        ..value.SelectMany(character => new[]
        {
            Keyboard(0, character, UnicodeKey),
            Keyboard(0, character, UnicodeKey | KeyUp)
        })
    ];

    private static Input[] KeyInputs(string key) => key switch
    {
        "tab" => Keystroke(0x09),
        "shift_tab" => Keystroke(0x09, modifier: 0x10),
        "enter" => Keystroke(0x0D),
        "backspace" => Keystroke(0x08),
        "select_all" => Keystroke(0x41, modifier: 0x11),
        _ => throw new InvalidOperationException("Tuş izinli değil.")
    };

    private static Input[] Keystroke(ushort key, ushort modifier = 0) => modifier == 0
        ? [Keyboard(key), Keyboard(key, flags: KeyUp)]
        : [Keyboard(modifier), Keyboard(key), Keyboard(key, flags: KeyUp), Keyboard(modifier, flags: KeyUp)];

    private static Input Keyboard(ushort key, ushort scanCode = 0, uint flags = 0) => new()
    {
        Type = KeyboardInput,
        Value = new InputValue { Key = new Key { VirtualKey = key, ScanCode = scanCode, Flags = flags } }
    };

    private void Send(ScreenFrame frame, Input[] inputs)
    {
        EnsureFrameCurrent(frame);
        foreach (int key in new[] { 0x01, 0x02, 0x10, 0x11, 0x12, 0x5B, 0x5C })
        {
            if (GetAsyncKeyState(key) < 0)
            {
                throw new InvalidOperationException("Fare düğmesi veya değiştirici tuş basılıyken girdi durduruldu.");
            }
        }
        if (GetForegroundWindow() != frame.Window.Handle)
        {
            throw new InvalidOperationException("Girdi öncesinde pencere odağı kayboldu.");
        }
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows girdi dizisini tamamlayamadı.");
        }
    }
}
