using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebPortal.Pages;

public sealed class IndexModel : PageModel
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Order[] Orders =
    [
        new("SIP-1001", "Arda Kaya", 9_999.99m, "Onaylandı"),
        new("SIP-1002", "Ece Aydın", 10_000.00m, "Onaylandı"),
        new("SIP-1003", "Deniz Yılmaz", 10_000.01m, "Onaylandı"),
        new("SIP-1004", "Elif Demir", 24_750.50m, "Onaylandı"),
        new("SIP-1005", "Mert Şahin", 18_300.00m, "Bekliyor"),
        new("SIP-1006", "İpek Akın", 32_000.00m, "İptal")
    ];

    public static IReadOnlyList<string> Statuses { get; } = ["Onaylandı", "Bekliyor", "İptal"];
    public IReadOnlyList<Order> Results { get; private set; } = [];
    public string Query { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string Minimum { get; private set; } = "";
    public decimal? AppliedMinimum { get; private set; }
    public string? FilterError { get; private set; }

    public void OnGet(string? q, string? status, string? min)
    {
        Query = q?.Trim() ?? "";
        Status = Statuses.Contains(status) ? status! : "";
        Minimum = min?.Trim() ?? "";
        decimal minimumAmount = 0;
        if (Minimum.Length > 0
            && (!decimal.TryParse(Minimum, NumberStyles.AllowDecimalPoint, Turkish, out minimumAmount)
                || decimal.Round(minimumAmount, 2) != minimumAmount))
        {
            FilterError = "Tutar sınırını binlik ayraç olmadan yazın. Örnek 10000,00.";
            return;
        }
        AppliedMinimum = Minimum.Length == 0 ? null : minimumAmount;
        Results = Orders.Where(order =>
            (Status.Length == 0 || order.Status == Status)
            && (Minimum.Length == 0 || order.Amount > minimumAmount)
            && (order.Number.Contains(Query, StringComparison.OrdinalIgnoreCase)
                || Turkish.CompareInfo.IndexOf(order.Customer, Query, CompareOptions.IgnoreCase) >= 0))
            .ToArray();
    }

    public static string FormatAmount(decimal amount) => amount.ToString("N2", Turkish) + " TL";

    public sealed record Order(string Number, string Customer, decimal Amount, string Status);
}
