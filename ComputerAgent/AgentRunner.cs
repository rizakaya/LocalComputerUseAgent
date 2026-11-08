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
        var initialObservations = new Dictionary<string, string>(StringComparer.Ordinal);
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

                string otherWindowNotes = string.Join('\n', windowNotes
                    .Where(entry => entry.Key != frame.Window.Name)
                    .Select(entry => $"{entry.Key}: {entry.Value}"));
                unchangedFrames = previousFrame?.Window == frame.Window
                    && frame.Png.AsSpan().SequenceEqual(previousFrame.Png) ? unchangedFrames + 1 : 0;
                string? blockedAction = unchangedFrames >= 2 ? previousKind : null;
                var (decision, currentObservation) = await DecideWithFocusAsync(activeGoal, frame,
                    otherWindowNotes, initialObservations.GetValueOrDefault(frame.Window.Name),
                    previousAction, previousKind, blockedAction, token);
                if (decision.Action == blockedAction || previousKind == "type_text" && decision.Action == "type_text")
                {
                    throw new InvalidOperationException("Modelden farklı bir eylem bekleniyordu.");
                }
                token.ThrowIfCancellationRequested();
                controller.EnsureFrameCurrent(frame);
                note = decision.Note;
                observation = currentObservation;
                initialObservations.TryAdd(frame.Window.Name, observation);
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
                    Console.WriteLine(decision.Report);
                    return;
                }

                await controller.ApplyAsync(frame, decision, token);
                expectedWindow = decision.Target ?? expectedWindow;
                activeGoal = decision.Goal ?? activeGoal;
                previousAction = action;
                previousKind = decision.Action;
                previousFrame = frame;
            }
            ReportStop($"{MaxTurns} model turu sınırına ulaşıldı.", observation, note);
        }
        catch (OperationCanceledException)
        {
            string reason = cancellationToken.IsCancellationRequested ? "Kullanıcı iptal etti."
                : deadline.IsCancellationRequested ? "10 dakikalık çalışma süresi doldu."
                : "Ollama yanıt süresini aştı.";
            ReportStop(reason, observation, note);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or ExternalException)
        {
            ReportStop(error.Message, observation, note);
        }
    }

    private async Task<(AgentDecision Decision, string Observation)> DecideWithFocusAsync(
        string task, ScreenFrame frame, string otherWindowNotes, string? initialObservation, string previousAction,
        string previousKind, string? blockedAction,
        CancellationToken cancellationToken)
    {
        using var inference = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = ollama.DecideAsync(task, frame, otherWindowNotes, initialObservation, previousAction,
            previousKind, blockedAction, inference.Token);
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

    private static void ReportStop(string reason, string observation, string note)
    {
        Console.WriteLine();
        Console.WriteLine($"Çalıştırma durdu. {reason}");
        Console.WriteLine(observation.Length == 0 ? "Henüz model gözlemi yok." : $"Son model gözlemi\n{observation}");
        if (note.Length > 0)
        {
            Console.WriteLine($"Son çalışma notu\n{note}");
        }
    }
}
