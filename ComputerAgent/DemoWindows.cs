using System.Diagnostics;
using ComputerAgent.Models;

namespace ComputerAgent;

internal sealed class DemoWindows : IDisposable
{
    private readonly DirectoryInfo profile = Directory.CreateTempSubdirectory("LocalComputerUseAgent-");
    private Process? edge;
    private Process? accounting;

    public async Task<ComputerController> LaunchAsync(string legacyExe, CancellationToken cancellationToken)
    {
        string edgeExe = FindEdge();
        accounting = Start(legacyExe);
        TargetWindow legacy = await WaitForWindowAsync(accounting, "LegacyApp", cancellationToken);

        edge = Start(edgeExe,
            "--kiosk", "http://localhost:5000",
            "--edge-kiosk-type=fullscreen", "--no-first-run",
            $"--user-data-dir={profile.FullName}");
        TargetWindow portal = await WaitForWindowAsync(edge, "WebPortal", cancellationToken);
        return new ComputerController(portal, legacy);
    }

    private static string FindEdge()
    {
        foreach (var folder in new[]
        {
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.LocalApplicationData
        })
        {
            string root = Environment.GetFolderPath(folder);
            string path = Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
            if (Path.IsPathFullyQualified(path) && File.Exists(path))
            {
                return path;
            }
        }
        throw new InvalidOperationException("Microsoft Edge kurulum yolu bulunamadı.");
    }

    private static Process Start(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return Process.Start(start) ?? throw new InvalidOperationException($"{Path.GetFileName(path)} başlatılamadı.");
    }

    private static async Task<TargetWindow> WaitForWindowAsync(
        Process process, string name, CancellationToken cancellationToken)
    {
        for (int check = 0; check < 100; check++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Refresh();
            if (process.HasExited)
            {
                break;
            }
            nint window = process.MainWindowHandle;
            if (window != 0 && NativeMethods.IsWindowVisible(window))
            {
                return new TargetWindow(name, window, process.Id);
            }
            await Task.Delay(150, cancellationToken);
        }
        throw new InvalidOperationException($"{name} için başlatılan süreçte görünür pencere bulunamadı.");
    }

    public void Dispose()
    {
        Close(edge);
        Close(accounting);
        try
        {
            profile.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Edge can hold a file in the temporary profile briefly after exit.
        }
        catch (UnauthorizedAccessException)
        {
            // Leave the temporary profile for Windows cleanup if it is still locked.
        }
    }

    private static void Close(Process? process)
    {
        if (process is null)
        {
            return;
        }
        using (process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2500))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2500);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited while it was being closed.
            }
        }
    }
}
