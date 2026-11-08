using System.Runtime.InteropServices;
using System.Text.Json;
using ComputerAgent.Models;

namespace ComputerAgent;

internal sealed class AgentRunner(ComputerController controller, OllamaClient ollama)
{
    private const int MaxTurns = 60;
    private static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(10);
    private readonly ScreenCapture screen = new(controller);

    public async Task RunAsync(string task, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(RunLimit);
        var token = deadline.Token;
        var windowNotes = new Dictionary<string, string>(StringComparer.Ordinal);
        var initialRows = new Dictionary<string, TableRecord[]>(StringComparer.Ordinal);
        var initialObservations = new Dictionary<string, string>(StringComparer.Ordinal);
        TableRecord[] expectedRecords = [];
        TransferProgress? progress = null;
        string note = "";
        string observation = "";
        string previousAction = "none";
        string previousKind = "none";
        string activeGoal = task;
        string expectedWindow = "WebPortal";
        ScreenFrame? previousFrame = null;
        int unchangedFrames = 0;

        try
        {
            await ollama.RequireLocalVisionModelAsync(token);
            await controller.OpenPortalAsync(token);
            for (int turn = 1; turn <= MaxTurns; turn++)
            {
                await Task.Delay(300, token);
                var frame = await screen.CaptureAsync(expectedWindow, token);
                var reading = await WithFocusAsync(frame, ct => ollama.ReadScreenAsync(frame, ct), token);
                controller.EnsureFrameCurrent(frame);
                observation = reading.Observation;
                var visibleRows = reading.ReadRows();
                initialRows.TryAdd(frame.Window.Name, visibleRows);
                initialObservations.TryAdd(frame.Window.Name, observation);
                progress = expectedRecords.Length == 0 ? null
                    : TransferProgress.Compare(expectedRecords, initialRows[frame.Window.Name], visibleRows);
                if (progress is { Missing.Length: 0 })
                {
                    Console.WriteLine($"[{turn:00}/{MaxTurns}] {frame.Window.Name} · finish");
                    Console.WriteLine();
                    Console.WriteLine(progress.Report());
                    return;
                }

                string otherWindowNotes = string.Join('\n', windowNotes
                    .Where(entry => entry.Key != frame.Window.Name)
                    .Select(entry => $"{entry.Key}: {entry.Value}"));
                unchangedFrames = previousFrame?.Window == frame.Window
                    && frame.Png.AsSpan().SequenceEqual(previousFrame.Png) ? unchangedFrames + 1 : 0;
                string? blockedAction = unchangedFrames >= 2 ? previousKind : null;
                var decision = await WithFocusAsync(frame, ct => ollama.DecideAsync(activeGoal, frame, reading,
                    otherWindowNotes, initialObservations[frame.Window.Name], progress, previousAction, previousKind, blockedAction, ct), token);
                if (decision.Action == blockedAction || previousKind == "type_text" && decision.Action == "type_text")
                {
                    throw new InvalidOperationException("Modelden farklı bir eylem bekleniyordu.");
                }
                token.ThrowIfCancellationRequested();
                controller.EnsureFrameCurrent(frame);
                note = decision.Note;
                windowNotes[frame.Window.Name] = note;

                string action = decision.Action switch
                {
                    "click" => $"click ({decision.X}, {decision.Y})",
                    "press_key" => $"press_key {decision.Key}",
                    "switch" => $"switch {decision.Target}",
                    _ => decision.Action
                };
                Console.WriteLine($"[{turn:00}/{MaxTurns}] {frame.Window.Name} · {action}");
                if (decision.Action == "finish")
                {
                    Console.WriteLine();
                    Console.WriteLine(progress?.Report() ?? decision.Report);
                    return;
                }

                if (decision.Goal is not null)
                {
                    var records = await WithFocusAsync(frame,
                        ct => ollama.SelectTransferRecordsAsync(decision.Goal, note, reading, ct), token);
                    expectedRecords = TransferProgress.BindToSource(records, visibleRows);
                    progress = expectedRecords.Length == 0 ? null : TransferProgress.Compare(expectedRecords, [], []);
                }
                await controller.ApplyAsync(frame, decision, token);
                expectedWindow = decision.Target ?? expectedWindow;
                activeGoal = decision.Goal ?? activeGoal;
                previousAction = action;
                previousKind = decision.Action;
                previousFrame = frame;
            }
            ReportStop($"{MaxTurns} eylem turu sınırına ulaşıldı.", observation, note, progress);
        }
        catch (OperationCanceledException)
        {
            string reason = cancellationToken.IsCancellationRequested ? "Kullanıcı iptal etti."
                : deadline.IsCancellationRequested ? "10 dakikalık çalışma süresi doldu."
                : "Ollama yanıt süresini aştı.";
            ReportStop(reason, observation, note, progress);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or ExternalException)
        {
            ReportStop(error.Message, observation, note, progress);
        }
    }

    private async Task<T> WithFocusAsync<T>(
        ScreenFrame frame, Func<CancellationToken, Task<T>> request, CancellationToken cancellationToken)
    {
        using var inference = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = request(inference.Token);
        try
        {
            while (await Task.WhenAny(pending, Task.Delay(100, inference.Token)) != pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (controller.ForegroundTarget() != frame.Window)
                {
                    throw new InvalidOperationException("Model yanıtı beklenirken uygulama odağı değişti.");
                }
            }
            return await pending;
        }
        finally
        {
            await inference.CancelAsync();
        }
    }

    private static void ReportStop(string reason, string observation, string note, TransferProgress? progress)
    {
        Console.WriteLine();
        Console.WriteLine($"Çalıştırma durdu. {reason}");
        Console.WriteLine(observation.Length == 0 ? "Henüz model gözlemi yok." : $"Son model gözlemi\n{observation}");
        if (note.Length > 0)
        {
            Console.WriteLine($"Son çalışma notu\n{note}");
        }
        if (progress is not null)
        {
            Console.WriteLine(progress.Report());
        }
    }
}
