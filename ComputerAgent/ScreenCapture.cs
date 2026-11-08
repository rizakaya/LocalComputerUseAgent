using System.Drawing.Imaging;
using ComputerAgent.Models;

namespace ComputerAgent;

internal sealed class ScreenCapture(ComputerController controller)
{
    public async Task<ScreenFrame> CaptureAsync(string expectedWindow, CancellationToken cancellationToken)
    {
        for (int check = 0; check < 32; check++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TargetWindow target = controller.ForegroundTarget();
            if (target.Name != expectedWindow)
            {
                throw new InvalidOperationException("Etkin uygulama son eylemden sonra değişti.");
            }
            if (TryCapture(target) is { } frame)
            {
                return frame;
            }
            await Task.Delay(250, cancellationToken);
        }
        throw new InvalidOperationException($"{expectedWindow} kullanılabilir bir ekran görüntüsü oluşturmadı.");
    }

    private ScreenFrame? TryCapture(TargetWindow target)
    {
        Rectangle bounds = controller.RequireVisibleClient(target);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        if (!HasVisibleContent(bitmap))
        {
            return null;
        }
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        var frame = new ScreenFrame(target, bounds, png.ToArray());
        controller.EnsureFrameCurrent(frame);
        return frame;
    }

    private static bool HasVisibleContent(Bitmap bitmap)
    {
        // Edge briefly shows a solid background while entering kiosk mode or drawing the first page.
        int firstColor = bitmap.GetPixel(0, 0).ToArgb();
        for (int y = 1; y < 12; y++)
        {
            for (int x = 1; x < 12; x++)
            {
                if (bitmap.GetPixel(x * bitmap.Width / 12, y * bitmap.Height / 12).ToArgb() != firstColor)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
