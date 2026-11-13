using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GmStudio;

internal sealed class EditorDialog
{
    private readonly Window window;
    private readonly StackPanel fields = new();
    private readonly WrapPanel commands = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 13, 0, 0) };
    private readonly TextBlock error = new()
    {
        Foreground = new SolidColorBrush(Color.FromRgb(162, 75, 43)),
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0)
    };

    internal EditorDialog(Window owner, string title, string subtitle, int width = 480)
    {
        window = new Window
        {
            Owner = owner, Title = title, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = width, MaxHeight = Math.Min(740, SystemParameters.WorkArea.Height - 60), SizeToContent = SizeToContent.Height,
            FontFamily = owner.FontFamily, FontSize = owner.FontSize,
            Background = (Brush)owner.FindResource("Canvas"),
            Foreground = (Brush)owner.FindResource("Ink"), ShowInTaskbar = false
        };
        var layout = new DockPanel { LastChildFill = true };
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 13) };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)owner.FindResource("GoldInk") });
        if (subtitle.Length > 0)
            heading.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) });
        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);
        var bottom = new StackPanel();
        bottom.Children.Add(error);
        bottom.Children.Add(commands);
        DockPanel.SetDock(bottom, Dock.Bottom);
        layout.Children.Add(bottom);
        layout.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        window.Content = new Border { Padding = new Thickness(18), Background = window.Background, Child = layout };
    }

    internal void AddElement(UIElement control) => fields.Children.Add(control);

    internal TextBox Text(string name, string value, bool multiline = false)
    {
        var box = new TextBox { Text = value };
        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextAlignment = TextAlignment.Left;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 120;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        Field(name, box);
        return box;
    }

    internal ComboBox Choices(string name, IEnumerable<string> options, int selected)
    {
        var box = new ComboBox { ItemsSource = options.ToArray(), SelectedIndex = selected };
        Field(name, box);
        return box;
    }

    private void Field(string name, UIElement input)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
        row.Children.Add(new TextBlock { Text = name, Margin = new Thickness(0, 0, 0, 5) });
        row.Children.Add(input);
        fields.Children.Add(row);
    }

    internal Button Action(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, Margin = new Thickness(6, 0, 0, 5) };
        if (primary)
            button.Style = (Style)window.FindResource("Primary");
        button.Click += (_, _) =>
        {
            try { action(); error.Text = ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or ArgumentException)
            { error.Text = ex.Message; }
        };
        commands.Children.Add(button);
        return button;
    }

    internal void ShowReadOnly()
    {
        var close = Action("关闭", () => window.DialogResult = false, true);
        close.IsCancel = close.IsDefault = true;
        window.ShowDialog();
    }

    internal bool Show(Action save)
    {
        Action("取消", () => window.DialogResult = false);
        Action("应用到存档", () => { save(); window.DialogResult = true; }, true);
        return window.ShowDialog() == true;
    }

    internal static int Integer(string value, string label, int min = 0, int max = int.MaxValue)
    {
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= min && number <= max)
            return number;
        throw new InvalidDataException($"{label}应为 {min}–{max} 范围内的整数。");
    }

    internal static decimal Decimal(string value, string label)
    {
        if (decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) && number >= 0 && number <= int.MaxValue)
            return number;
        throw new InvalidDataException($"{label}应为非负数字。");
    }
}

internal static class EquipmentDialog
{
    internal static string? Edit(Window owner, GameFolder game, string raw, bool pet, string name)
    {
        var data = EquipmentData.Parse(raw, pet);
        string code = EquipmentData.Code(data, pet);
        var stats = pet ? EquipmentData.Pet : EquipmentData.Hero;
        var references = pet ? game.PetEquipmentReference : game.EquipmentReference;
        bool known = references[code] is not null;
        var dialog = new EditorDialog(owner, $"装备调参 · {name}", known
            ? $"{code} · 上限按当前强化等级计算。超出上限的属性会被游戏重置。"
            : $"{code} · 这件装备没有独立参考记录。可用一键极品填入旧版工具使用的默认值。", 610);
        TextBox? strength = pet || EquipmentData.Strengthen(data[3])
            ? dialog.Text("强化等级", data[pet ? 11 : 23]) : null;
        TextBox? magic = !pet && data[3] == "zbfb" ? dialog.Text("法宝等级", data[20]) : null;
        TextBox? quantity = !pet ? dialog.Text("数量", data[22]) : null;
        TextBox? fashion = !pet && EquipmentData.Fashion(data[3]) ? dialog.Text("时装期限（永久填 never）", data[27]) : null;
        TextBox? extra = !pet ? dialog.Text("额外 / 重铸字段", data[EquipmentData.Fashion(data[3]) ? 28 : 27]) : null;
        var flags = new CheckBox[pet ? 0 : 5];
        if (!pet)
        {
            var elements = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            string[] names = ["金", "木", "水", "火", "土"];
            for (int i = 0; i < names.Length; i++)
            {
                flags[i] = new CheckBox { Content = names[i], IsChecked = data[15 + i] is "true" or "1", Margin = new Thickness(0, 0, 14, 0) };
                elements.Children.Add(flags[i]);
            }
            dialog.AddElement(elements);
        }
        var controls = new Dictionary<AttributeField, (TextBox Box, TextBlock Cap)>();
        foreach (AttributeField stat in stats)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            var box = new TextBox { Text = data[stat.Index] };
            var cap = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new TextBlock { Text = stat.Label, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(box, 1);
            Grid.SetColumn(cap, 2);
            row.Children.Add(box);
            row.Children.Add(cap);
            controls[stat] = (box, cap);
            dialog.AddElement(row);
        }
        int CurrentStrength() => EquipmentData.StrengthLevel(strength?.Text ?? data[pet ? 11 : 23]);
        void RefreshLimits()
        {
            foreach (var (stat, pair) in controls)
            {
                decimal? limit = EquipmentData.Limit(game, data, pet, stat, CurrentStrength());
                pair.Cap.Text = limit.HasValue ? $"当前上限 {EquipmentData.Number(limit.Value)}" : "无参考上限";
            }
        }
        RefreshLimits();
        if (strength is not null)
            strength.TextChanged += (_, _) => RefreshLimits();
        dialog.Action("一键极品", () =>
        {
            EquipmentData.Maximize(game, data, pet);
            if (strength is not null)
                strength.Text = data[pet ? 11 : 23];
            if (magic is not null)
                magic.Text = data[20];
            if (fashion is not null)
                fashion.Text = data[27];
            foreach (var (stat, pair) in controls)
                pair.Box.Text = data[stat.Index];
            foreach (var flag in flags)
                flag.IsChecked = true;
        });
        dialog.Action("属性清零", () => { foreach (var field in controls.Values) field.Box.Text = "0"; });
        if (!dialog.Show(() =>
        {
            if (strength is not null)
                data[pet ? 11 : 23] = EditorDialog.Integer(strength.Text, "强化等级", 0, GameRules.Int("equipment",pet?"petStrengthMax":"heroStrengthMax")).ToString(CultureInfo.InvariantCulture);
            if (magic is not null)
                data[20] = EditorDialog.Integer(magic.Text, "法宝等级", 0, code == "tjbg" ? 9 : 15).ToString(CultureInfo.InvariantCulture);
            if (quantity is not null)
                data[22] = EditorDialog.Integer(quantity.Text, "数量", 1, GameRules.Int("inventory","stack")).ToString(CultureInfo.InvariantCulture);
            if (fashion is not null)
                data[27] = SafeText(fashion.Text.Trim(), "时装期限");
            if (extra is not null)
                data[EquipmentData.Fashion(data[3]) ? 28 : 27] = SafeText(extra.Text.Trim(), "额外字段");
            for (int i = 0; i < flags.Length; i++)
                data[15 + i] = flags[i].IsChecked == true ? "true" : "false";
            foreach (var (stat, pair) in controls)
            {
                decimal value = pair.Box.Text.Length == 0 ? 0 : EditorDialog.Decimal(pair.Box.Text, stat.Label);
                decimal? limit = EquipmentData.Limit(game, data, pet, stat, CurrentStrength());
                if (limit.HasValue && value > limit.Value)
                    throw new InvalidDataException($"{stat.Label}超过当前上限 {EquipmentData.Number(limit.Value)}。");
                data[stat.Index] = EquipmentData.Number(value);
            }
        }))
            return null;
        return string.Join('|', data);
    }

    internal static string SafeText(string text, string label)
    {
        if (text.IndexOfAny(['|', '}', ';', '&']) >= 0)
            throw new InvalidDataException($"{label}包含存档保留的分隔符。");
        return text;
    }
}
