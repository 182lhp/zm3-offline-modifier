using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace GmStudio;

public partial class PetEditorWindow
{
    internal Expander AdvancedGrowth { get; private set; } = null!;
    private TextBlock? experienceSummary;
    private readonly Dictionary<int, TextBlock> growthTotals = [];

    private void BuildGrowthEditor()
    {
        var basics = GrowthCard();
        var basicGrid = new UniformGrid { Columns = 3 };
        basicGrid.Children.Add(GrowthField(5, "等级"));
        basicGrid.Children.Add(GrowthField(24, "可用潜力点"));
        basicGrid.Children.Add(GrowthField(31, "潜力极限"));
        basics.Children.Add(basicGrid);

        var qualities = GrowthCard("四项资质", "范围随宠物品种变化");
        var qualityGrid = new UniformGrid { Columns = 4 };
        for (int index = 9; index <= 12; index++)
            qualityGrid.Children.Add(GrowthField(index, PetData.FieldNames[index]));
        qualities.Children.Add(qualityGrid);

        var abilities = GrowthCard("成长能力", "合计 = 基础 + 培养加成");
        var headings = AbilityGrid();
        headings.Margin = new Thickness(0, 0, 0, 6);
        foreach (var (text, column) in new[] { ("能力", 0), ("基础", 1), ("培养加成", 2), ("合计 / 上限", 3) })
        {
            var label = new TextBlock { Text = text, Foreground = (Brush)FindResource("Muted"), FontSize = 11,
                TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Center };
            Grid.SetColumn(label, column);
            headings.Children.Add(label);
        }
        abilities.Children.Add(headings);
        foreach (var (basis, bonus, _, key, _) in PetRules.Abilities)
        {
            var row = AbilityGrid();
            row.Margin = new Thickness(0, 0, 0, 4);
            var name = new TextBlock { Text = PetData.FieldNames[basis], Foreground = (Brush)FindResource("Green"), FontSize = 14, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 11) };
            row.Children.Add(name);
            var baseField = GrowthField(basis, null);
            var bonusField = GrowthField(bonus, null);
            Grid.SetColumn(baseField, 1); row.Children.Add(baseField);
            Grid.SetColumn(bonusField, 2); row.Children.Add(bonusField);
            var total = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Green"), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 11) };
            Grid.SetColumn(total, 3); row.Children.Add(total);
            growthTotals[basis] = total;
            abilities.Children.Add(row);
        }

        experienceSummary = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var advancedHeading = new DockPanel();
        advancedHeading.Children.Add(new TextBlock { Text = "进阶参数", FontWeight = FontWeights.SemiBold, FontSize = 14 });
        advancedHeading.Children.Add(experienceSummary);
        var advancedBody = new StackPanel { Margin = new Thickness(0, 13, 0, 0) };
        advancedBody.Children.Add(new TextBlock { Text = "资质经验分配 · 四项合计最多 250", Foreground = (Brush)FindResource("Muted"),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
        var experienceGrid = new UniformGrid { Columns = 4 };
        foreach (int index in new[] { 20, 21, 22, 23 })
            experienceGrid.Children.Add(GrowthField(index, PetData.FieldNames[index]));
        advancedBody.Children.Add(experienceGrid);
        advancedBody.Children.Add(new TextBlock { Text = "培养进度 · 能力满值时无需填写", Foreground = (Brush)FindResource("Muted"),
            FontSize = 12, Margin = new Thickness(0, 13, 0, 8) });
        var progressGrid = new UniformGrid { Columns = 3 };
        foreach (int index in new[] { 28, 30, 29 })
            progressGrid.Children.Add(GrowthField(index, PetData.FieldNames[index]));
        advancedBody.Children.Add(progressGrid);
        AdvancedGrowth = new Expander { Header = advancedHeading, Content = advancedBody, IsExpanded = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        GrowthFields.Children.Add(new Border { CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource("Surface"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 14, 12), Child = AdvancedGrowth });
    }

    private StackPanel GrowthCard(string? title = null, string? hint = null)
    {
        var content = new StackPanel();
        if (title is not null)
        {
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            if (hint is not null)
            {
                var note = new TextBlock { Text = hint, Foreground = (Brush)FindResource("Muted"), FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                DockPanel.SetDock(note, Dock.Right);
                heading.Children.Add(note);
            }
            heading.Children.Add(new TextBlock { Text = title, Foreground = (Brush)FindResource("GoldInk"), FontSize = 17, FontWeight = FontWeights.Bold });
            content.Children.Add(heading);
        }
        GrowthFields.Children.Add(new Border { CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource("Surface"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 11, 7, 11), Margin = new Thickness(0, 0, 0, 10), Child = content });
        return content;
    }

    private StackPanel GrowthField(int index, string? label)
    {
        var cell = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        if (label is not null)
            cell.Children.Add(new TextBlock { Text = label, Foreground = GrowthAccent(index), FontWeight = FontWeights.SemiBold,
                FontSize = 13, Margin = new Thickness(1, 0, 0, 6) });
        var box = new TextBox { Text = pet.Fields[index], Foreground = GrowthAccent(index), Style = (Style)FindResource("PetGrowthInput") };
        AutomationProperties.SetName(box, PetData.FieldNames[index]);
        cell.Children.Add(box);
        var limit = new TextBlock { FontSize = 11, Foreground = (Brush)FindResource("Muted"),
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        cell.Children.Add(limit);
        fields.Add(index, (box, limit));
        return cell;
    }

    private Brush GrowthAccent(int index) => index switch
    {
        9 => new SolidColorBrush(Color.FromRgb(166,73,57)),
        10 => new SolidColorBrush(Color.FromRgb(42,111,150)),
        11 => new SolidColorBrush(Color.FromRgb(35,115,83)),
        12 or 5 => (Brush)FindResource("GoldInk"),
        31 => new SolidColorBrush(Color.FromRgb(126,68,125)),
        _ => (Brush)FindResource("Green")
    };

    private static Grid AbilityGrid()
    {
        var grid = new Grid();
        foreach (double width in new[] { 0.75, 1.0, 1.0, 0.9 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        return grid;
    }

    private void RefreshGrowthSummary(IReadOnlyList<string> draft)
    {
        foreach (var (basis, bonus, _, key, _) in PetRules.Abilities)
            if (growthTotals.TryGetValue(basis, out var total))
                total.Text = $"{PetRules.Value(draft, basis) + PetRules.Value(draft, bonus)} / {rules.TotalMaximum(key)}";
        if (experienceSummary is not null)
        {
            long used = Enumerable.Range(20, 4).Sum(index => (long)Math.Max(0, PetRules.Value(draft, index)));
            experienceSummary.Text = $"资质经验 {used} / {PetRules.QualityExperienceBudget}";
            experienceSummary.Foreground = used > PetRules.QualityExperienceBudget ? Brushes.Firebrick : (Brush)FindResource("Muted");
        }
    }
}
