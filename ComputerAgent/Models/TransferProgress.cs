namespace ComputerAgent.Models;

internal sealed record TransferProgress(
    TableRecord[] Created, TableRecord[] Existing, TableRecord[] Conflicts, TableRecord[] Missing,
    TableRecord[] Unexpected)
{
    public static TableRecord[] BindToSource(TableRecord[] selected, TableRecord[] visible)
    {
        var records = new List<TableRecord>(selected.Length);
        foreach (var record in selected)
        {
            var source = visible.FirstOrDefault(row => SameKey(row, record))
                ?? throw new InvalidOperationException("Aktarım için seçilen kayıt kaynak tablosunda görünmüyor.");
            var values = new List<string>();
            foreach (string selectedValue in record.Values)
            {
                string value = Normalize(selectedValue);
                // The input form may omit the unit. Verify using the full source cell as displayed.
                string? sourceValue = source.Values.FirstOrDefault(cell =>
                    Normalize(cell) == value || char.IsDigit(value[0]) && Normalize(cell).StartsWith(value + " ", StringComparison.Ordinal));
                values.Add(sourceValue
                    ?? throw new InvalidOperationException("Aktarım için seçilen değer kaynak tablosunda doğrulanamadı."));
            }
            records.Add(new TableRecord(source.Key, values.ToArray()));
        }
        return records.ToArray();
    }

    public static TransferProgress Compare(TableRecord[] expected, TableRecord[] initial, TableRecord[] current)
    {
        var created = new List<TableRecord>();
        var existing = new List<TableRecord>();
        var conflicts = new List<TableRecord>();
        var missing = new List<TableRecord>();
        foreach (var record in expected)
        {
            var saved = current.FirstOrDefault(row => SameKey(row, record));
            if (saved is null)
            {
                missing.Add(record);
            }
            else if (!record.Values.All(value => saved.Values.Any(cell => Normalize(cell) == Normalize(value))))
            {
                conflicts.Add(record);
            }
            else if (initial.Any(row => SameKey(row, record)))
            {
                existing.Add(record);
            }
            else
            {
                created.Add(record);
            }
        }
        var unexpected = current.Where(row => !initial.Any(record => SameKey(record, row))
            && !expected.Any(record => SameKey(record, row))).ToArray();
        return new TransferProgress(created.ToArray(), existing.ToArray(), conflicts.ToArray(), missing.ToArray(), unexpected);
    }

    private static bool SameKey(TableRecord left, TableRecord right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left.Key.Trim(), right.Key.Trim());

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public string Report() => string.Join(Environment.NewLine,
        Section("Yeni doğrulanan", Created),
        Section("Mevcut eşleşme", Existing),
        Section("Uyuşmazlık", Conflicts),
        Section("Eksik veya doğrulanmamış", Missing),
        Section("Beklenmeyen yeni kayıt", Unexpected));

    private static string Section(string title, TableRecord[] records) =>
        $"{title} ({records.Length})" + string.Concat(records.Select(record =>
            $"{Environment.NewLine}- {record.Key.Trim()} | {string.Join(" | ", record.Values)}"));
}
