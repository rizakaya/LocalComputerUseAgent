using System.Globalization;
using System.Text.RegularExpressions;

namespace LegacyApp;

internal sealed partial class InvoiceForm : Form
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly HashSet<string> orderNumbers = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox customer = new() { MaxLength = 120, TabIndex = 0 };
    private readonly TextBox orderNumber = new() { MaxLength = 40, TabIndex = 1 };
    private readonly TextBox amount = new() { MaxLength = 30, TabIndex = 2 };
    private readonly Label message = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "Henüz taslak fatura yok."
    };
    private readonly DataGridView invoices = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        RowTemplate = { Height = 39 },
        ColumnHeadersHeight = 39,
        BackgroundColor = Color.White,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        TabStop = false
    };

    public InvoiceForm()
    {
        Text = "LegacyApp | Muhasebe";
        Font = new Font("Segoe UI", 15);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 680);
        MinimumSize = new Size(780, 440);
        BackColor = Color.FromArgb(241, 246, 249);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(30, 22, 30, 28),
            ColumnCount = 1, RowCount = 6
        };
        foreach (int height in new[] { 53, 43, 86, 38, 43 })
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(Heading("LegacyApp · Muhasebe", 25), 0, 0);
        layout.Controls.Add(Heading("Yeni taslak", 18), 0, 1);
        layout.Controls.Add(CreateEditor(), 0, 2);
        layout.Controls.Add(message, 0, 3);
        layout.Controls.Add(Heading("Fatura listesi", 18), 0, 4);
        layout.Controls.Add(invoices, 0, 5);

        invoices.Columns.Add("OrderNumber", "Sipariş no");
        invoices.Columns.Add("Customer", "Müşteri");
        invoices.Columns.Add("Amount", "Tutar");
        invoices.Columns.Add("Status", "Durum");
        ActiveControl = customer;
    }

    private TableLayoutPanel CreateEditor()
    {
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        foreach (int width in new[] { 29, 25, 23, 23 })
        {
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        }
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddField(editor, 0, "Müşteri adı", customer);
        AddField(editor, 1, "Sipariş no", orderNumber);
        AddField(editor, 2, "Tutar (TL)", amount);

        var save = new Button
        {
            Text = "Taslağı kaydet", Dock = DockStyle.Fill, TabIndex = 3,
            BackColor = Color.FromArgb(19, 87, 90), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 2, 2, 8)
        };
        save.Click += (_, _) => SaveDraft();
        editor.Controls.Add(save, 3, 1);
        AcceptButton = save;
        return editor;
    }

    private static void AddField(TableLayoutPanel editor, int column, string title, TextBox input)
    {
        editor.Controls.Add(new Label
        {
            Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
        }, column, 0);
        input.Dock = DockStyle.Top;
        input.Margin = new Padding(2, 2, 15, 0);
        input.AccessibleName = title;
        input.Enter += (_, _) => input.BackColor = Color.FromArgb(232, 243, 255);
        input.Leave += (_, _) => input.BackColor = SystemColors.Window;
        editor.Controls.Add(input, column, 1);
    }

    private static Label Heading(string title, int size) => new()
    {
        Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", size, FontStyle.Bold)
    };

    private void SaveDraft()
    {
        string name = customer.Text.Trim();
        string number = orderNumber.Text.Trim();
        string enteredAmount = amount.Text.Trim();
        if (name.Length == 0 || number.Length == 0)
        {
            ShowMessage("Müşteri adı ve sipariş no zorunludur.", error: true);
            return;
        }

        const NumberStyles styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
        if (!TurkishAmount().IsMatch(enteredAmount)
            || !decimal.TryParse(enteredAmount, styles, Turkish, out decimal value)
            || value <= 0)
        {
            ShowMessage("Pozitif bir TL tutarı girin. Örnek 12500,50.", error: true);
            return;
        }

        if (!orderNumbers.Add(number))
        {
            ShowMessage($"{number} için bir taslak zaten var. Mevcut kayıt korundu.", error: true);
            return;
        }

        invoices.Rows.Add(number, name, value.ToString("N2", Turkish) + " TL", "Taslak");
        invoices.ClearSelection();
        customer.Clear();
        orderNumber.Clear();
        amount.Clear();
        customer.Focus();
        ShowMessage($"{number} için taslak kaydedildi.", error: false);
    }

    private void ShowMessage(string text, bool error)
    {
        message.Text = text;
        message.ForeColor = error ? Color.Firebrick : Color.DarkGreen;
    }

    [GeneratedRegex(@"\A(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,2})?\z")]
    private static partial Regex TurkishAmount();
}
