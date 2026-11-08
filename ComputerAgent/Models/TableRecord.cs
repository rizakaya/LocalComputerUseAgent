using System.Text.Json;
using System.Text.RegularExpressions;

namespace ComputerAgent.Models;

internal sealed record TableRecord(string Key, string[] Values)
{
    public static readonly object ListSchema = new
    {
        type = "array", maxItems = 24,
        items = new
        {
            type = "object",
            properties = new
            {
                key = new { type = "string", minLength = 1, maxLength = 120 },
                values = new { type = "array", minItems = 1, maxItems = 8, items = new { type = "string", maxLength = 160 } }
            },
            required = new[] { "key", "values" },
            additionalProperties = false
        }
    };

    public static bool IsValid(TableRecord[]? records) =>
        records is { Length: <= 24 }
        && records.All(record => record is not null && IsCell(record.Key, 120)
            && record.Values is { Length: >= 1 and <= 8 } && record.Values.All(value => IsCell(value, 160)))
        && records.Select(record => record.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == records.Length;

    private static bool IsCell(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
}

internal sealed record ScreenReading(string Observation)
{
    private static readonly Regex Columns = new(@" {2,}|\t+", RegexOptions.NonBacktracking);

    public static ScreenReading Parse(string json)
    {
        var reading = JsonSerializer.Deserialize<ScreenReading>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (reading is null || string.IsNullOrWhiteSpace(reading.Observation) || reading.Observation.Length > 1300
            || reading.Observation.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            throw new InvalidOperationException("Model geçerli bir ekran okuması döndürmedi.");
        }
        return reading;
    }

    public TableRecord[] ReadRows()
    {
        var rows = new List<TableRecord>();
        int columns = 0;
        foreach (string line in Observation.Split('\n'))
        {
            string trimmed = line.Trim();
            string[] cells = trimmed.Length >= 2 && trimmed.StartsWith('|') && trimmed.EndsWith('|')
                ? trimmed[1..^1].Split('|', StringSplitOptions.TrimEntries)
                : Columns.Split(line.Trim(' ', '\r')).Select(cell => cell.Trim()).ToArray();

            if (cells.Length < 2)
            {
                columns = 0;
                continue;
            }
            if (cells.All(cell => cell.Length > 0 && cell.All(character => character is '-' or ':')))
            {
                columns = cells.Length;
                rows.Clear();
                continue;
            }
            if (columns != cells.Length)
            {
                columns = cells.Length;
                rows.Clear();
                continue;
            }
            if (cells.All(cell => cell.Length > 0 && !cell.StartsWith('(') && !cell.StartsWith('[')))
            {
                rows.Add(new TableRecord(cells[0], cells[1..]));
            }
        }
        TableRecord[] result = rows.ToArray();
        return TableRecord.IsValid(result) ? result : [];
    }
}
