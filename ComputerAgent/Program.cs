using System.ComponentModel;
using System.Text;
using ComputerAgent;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (!TryReadOptions(args, out string? legacyExe, out string model))
{
    Console.Error.WriteLine("Kullanım: ComputerAgent [--legacy-exe <LegacyApp.exe yolu>] [--model <Ollama etiketi>]");
    return 2;
}

legacyExe ??= GetDefaultLegacyExe();
if (!string.Equals(Path.GetFileName(legacyExe), "LegacyApp.exe", StringComparison.OrdinalIgnoreCase)
    || !File.Exists(legacyExe))
{
    Console.Error.WriteLine("LegacyApp.exe bulunamadı. Çözümü derleyin veya --legacy-exe ile geçerli yolu belirtin.");
    return 2;
}
legacyExe = Path.GetFullPath(legacyExe);

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.Cancel();
};

using var ollama = new OllamaClient(model);
using var windows = new DemoWindows();
AgentRunner? runner = null;
Console.WriteLine("Görevi tek satırda yazın. Çıkmak için çıkış yazın.");

try
{
    while (!stop.IsCancellationRequested)
    {
        Console.Write("Görev> ");
        string? task = await Console.In.ReadLineAsync(stop.Token);
        if (task is null || task.Trim() is "çıkış" or "exit")
        {
            break;
        }
        if (string.IsNullOrWhiteSpace(task))
        {
            continue;
        }
        if (task.Length > 4000)
        {
            Console.WriteLine("Görev en fazla 4000 karakter olabilir.");
            continue;
        }

        runner ??= new AgentRunner(await windows.LaunchAsync(legacyExe, stop.Token), ollama);
        await runner.RunAsync(task, stop.Token);
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    // The user exited from the console.
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
    or Win32Exception)
{
    Console.Error.WriteLine($"Demo başlatılamadı. {error.Message}");
    return 1;
}
return 0;

static bool TryReadOptions(string[] args, out string? legacyExe, out string model)
{
    legacyExe = null;
    model = "qwen3-vl:8b-instruct";
    for (int index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            return false;
        }
        switch (args[index])
        {
            case "--legacy-exe":
                legacyExe = args[index + 1];
                break;
            case "--model":
                model = args[index + 1];
                break;
            default:
                return false;
        }
    }
    return true;
}

static string? GetDefaultLegacyExe()
{
    var targetDirectory = new DirectoryInfo(AppContext.BaseDirectory);
    var configurationDirectory = targetDirectory.Parent;
    if (configurationDirectory?.Parent is not { Name: "bin", Parent: { Name: "ComputerAgent", Parent: { } solutionDirectory } })
    {
        return null;
    }

    return Path.Combine(solutionDirectory.FullName, "LegacyApp", "bin", configurationDirectory.Name,
        targetDirectory.Name, "LegacyApp.exe");
}
