using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComputerAgent.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AgentDecision
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    private static readonly Dictionary<string, object> Actions = new(StringComparer.Ordinal)
    {
        ["click"] = ActionSchema("click", ("x", Coordinate()), ("y", Coordinate())),
        ["type_text"] = ActionSchema("type_text", ("text", String(160))),
        ["press_key"] = ActionSchema("press_key", ("key", Choice("tab", "shift_tab", "enter", "backspace", "select_all"))),
        ["switch"] = ActionSchema("switch", ("target", Choice("WebPortal", "LegacyApp")), ("goal", String(450))),
        ["finish"] = ActionSchema("finish", ("report", String(2400)))
    };

    public static JsonElement SchemaFor(string previousAction, string? blockedAction) =>
        JsonSerializer.SerializeToElement(new
        {
            anyOf = Actions.Where(action => action.Key != blockedAction
                && (action.Key != "type_text" || previousAction != "type_text")).Select(action => action.Value)
        });

    public required string Action { get; init; }
    public required string Note { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public string? Text { get; init; }
    public string? Key { get; init; }
    public string? Target { get; init; }
    public string? Goal { get; init; }
    public string? Report { get; init; }

    public static AgentDecision Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var decision = document.RootElement.Deserialize<AgentDecision>(JsonOptions);
            if (decision is null || !IsSafeText(decision.Note, 700, multiline: true))
            {
                throw new InvalidOperationException("Model geçerli bir gözlem notu döndürmedi.");
            }

            string[] expectedFields = decision.Action switch
            {
                "click" when decision.X is >= 0 and <= 1000 && decision.Y is >= 0 and <= 1000 => ["x", "y"],
                "type_text" when IsSafeText(decision.Text, 160) => ["text"],
                "press_key" when decision.Key is "tab" or "shift_tab" or "enter" or "backspace" or "select_all" => ["key"],
                "switch" when decision.Target is "WebPortal" or "LegacyApp" && IsSafeText(decision.Goal, 450)
                    => ["target", "goal"],
                "finish" when IsSafeText(decision.Report, 2400, multiline: true) => ["report"],
                _ => throw new InvalidOperationException("Model eylemi veya eylem parametreleri izin verilen aralığın dışında.")
            };

            var expected = new HashSet<string>(expectedFields, StringComparer.Ordinal) { "action", "note" };
            if (document.RootElement.EnumerateObject().Any(property => !expected.Remove(property.Name))
                || expected.Count != 0)
            {
                throw new InvalidOperationException("Model tek eylem için beklenen alanları döndürmedi.");
            }
            return decision;
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("Model yanıtı geçerli eylem JSON'u değil.", error);
        }
    }

    private static bool IsSafeText(string? value, int maxLength, bool multiline = false) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength
        && value.All(character => !char.IsControl(character) || multiline && character is '\r' or '\n' or '\t');

    private static object ActionSchema(string action, params (string Name, object Schema)[] fields)
    {
        var properties = new Dictionary<string, object> { ["note"] = String(700), ["action"] = Choice(action) };
        foreach (var field in fields)
        {
            properties.Add(field.Name, field.Schema);
        }
        return new { type = "object", properties, required = properties.Keys.ToArray(), additionalProperties = false };
    }

    private static object Choice(params string[] choices) => new { type = "string", @enum = choices };
    private static object String(int maxLength) => new { type = "string", minLength = 1, maxLength };
    private static object Coordinate() => new { type = "integer", minimum = 0, maximum = 1000 };
}
