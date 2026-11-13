using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class VipRechargeWindow : Window
{
    private readonly JsonObject original;
    private bool ready;
    internal int Amount { get; private set; }
    internal bool AddToTotal => CountTotal.IsChecked == true;

    internal VipRechargeWindow(Window owner, JsonObject vip)
    {
        original = (JsonObject)vip.DeepClone();
        InitializeComponent();
        Owner = owner;
        MaxHeight = SystemParameters.WorkArea.Height;
        ready = true;
        UpdatePreview();
    }
    private void Changed(object sender, TextChangedEventArgs e) { if (ready) UpdatePreview(); }
    private void ToggleTotal(object sender, RoutedEventArgs e) { if (ready) UpdatePreview(); }
    private void ChooseAmount(object sender, RoutedEventArgs e)
    {
        AmountBox.Text = (string)((Button)sender).Tag;
    }
    private int ReadAmount()
    {
        if (!int.TryParse(AmountBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int amount) || amount < 1)
            throw new InvalidDataException("请输入 1–2,147,483,647 之间的整数点券数。");
        return amount;
    }
    private void UpdatePreview()
    {
        try
        {
            var result = VipData.RechargePreview(original, ReadAmount(), AddToTotal);
            BalancePreview.Text = result.Balance.ToString("N0", CultureInfo.InvariantCulture);
            LevelPreview.Text = $"VIP {VipData.Level(result.Total)}";
            TotalPreview.Text = $"累计充值 {VipData.Money(original, "allMoney"):N0} → {result.Total:N0} 点券";
            ErrorText.Visibility = Visibility.Collapsed;
            ConfirmButton.IsEnabled = true;
        }
        catch (InvalidDataException ex)
        {
            BalancePreview.Text = LevelPreview.Text = "—";
            TotalPreview.Text = "";
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
        }
    }
    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        if (!ConfirmButton.IsEnabled) return;
        Amount = ReadAmount();
        DialogResult = true;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
