using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed record PetSkillCandidate(PetEntry Entry, PetSkillRule Rule)
{
    public string Code => Entry.Code;
    public string Name => Entry.Name;
    public string Initial => Entry.Initial;
    public BitmapImage? Icon => Entry.Icon;
    public string Caption => $"{(Rule.Acquisition.Length>0 ? Rule.Acquisition : Rule.Category)} · {Code}{(Rule.Locked is null ? "" : " · 未解锁")}";
    public SkillHint? Hint => Entry.Hint is {} hint ? hint with
        { Condition = Rule.Locked is null ? hint.Condition : hint.Condition+"\n当前状态："+Rule.Locked } : null;
}

internal enum PetEditSection { Growth, Skills }

public partial class PetEditorWindow : Window
{
    internal PetEditSection Section { get; }
    private readonly GameFolder game;
    private readonly PetData pet;
    private readonly PetRules rules;
    private readonly int levelBonus;
    private readonly string[] originalCodes;
    private readonly ObservableCollection<PetEntry> learned = [];
    private readonly Dictionary<int, (TextBox Box, TextBlock Limit)> fields = [];

    internal PetEditorWindow(Window owner, GameFolder game, PetData pet, PetRow row, int levelBonus, PetEditSection section)
    {
        Section = section;
        this.game = game;
        this.pet = pet;
        this.levelBonus = levelBonus;
        rules = new PetRules(game, pet.Fields[0]);
        originalCodes = pet.Fields[19].Split('~', StringSplitOptions.RemoveEmptyEntries);
        foreach (string code in originalCodes)
            learned.Add(PetEntry.Skill(game, code));

        InitializeComponent();
        string label = section == PetEditSection.Growth ? "等级、资质与培养" : "宠物技能";
        Title = $"编辑{label} · 修改器";
        EditorPurpose.Text = section == PetEditSection.Growth ? "培养编辑" : "技能编辑";
        var page = section == PetEditSection.Growth ? GrowthTab : SkillsTab;
        EditorTabs.Items.Clear();
        EditorTabs.Items.Add(page);
        EditorTabs.SelectedItem = page;
        Owner = owner;
        Width = Math.Clamp(SystemParameters.WorkArea.Width, MinWidth, Width);
        Height = Math.Clamp(SystemParameters.WorkArea.Height, MinHeight, Height);
        PetPortrait.Content = row;
        PetName.Text = row.Name;
        PetMeta.Text = $"{row.Level} 级 · {row.Status}";

        if (section == PetEditSection.Growth) BuildGrowthEditor();

        LearnedSkills.ItemsSource = learned;
        LearnedSkills.SelectedIndex = learned.Count > 0 ? 0 : -1;
        LearnedSkills.SelectionChanged += (_, _) => UpdateSkillActions();
        AvailableSkills.SelectionChanged += (_, _) => UpdateSkillActions();
        SkillSearch.TextChanged += (_, _) => ShowSkillChoices();
        SkillKindBox.ItemsSource = new[] { "全部", "基础", "专属", "奥义" };
        SkillKindBox.SelectedIndex = 0;
        SkillKindBox.SelectionChanged += (_, _) => ShowSkillChoices();
        foreach (var (index, field) in fields)
        {
            field.Box.PreviewTextInput += (_, e) => e.Handled = !CanInsert(index, field.Box, e.Text);
            DataObject.AddPastingHandler(field.Box, (_, e) =>
            {
                if (e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text || !CanInsert(index, field.Box, text))
                    e.CancelCommand();
            });
            field.Box.TextChanged += (_, _) =>
            {
                ClearError();
                RefreshFieldLimits();
                if (index is 15 or 25 or 31)
                    ShowSkillChoices();
            };
        }
        RefreshFieldLimits();
        ShowSkillChoices();

        EditorTabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == EditorTabs)
                ClearError();
        };
    }

    private string[] DraftFields()
    {
        var draft = pet.Fields.ToArray();
        foreach (var (index, field) in fields)
            draft[index] = field.Box.Text;
        return draft;
    }

    private void RefreshFieldLimits()
    {
        var draft = DraftFields();
        var edits = EditedFields(draft);
        foreach (var (index, field) in fields)
        {
            var range = rules.Range(index, draft, levelBonus);
            field.Limit.Text = range.Max is null ? "可累积" : range.Min == range.Max ? $"{range.Min}" : $"{range.Min}–{range.Max}";
            if (index == 31) field.Limit.Text += "%";
            field.Box.ToolTip = null;
            bool fullProgress = index is 28 or 29 or 30 && range.Max == 0 && draft[index] == "0";
            field.Box.IsReadOnly = fullProgress;
            if (fullProgress) field.Limit.Text = "能力已满";
            if (edits.Any(edit => PetRules.Affects(index, edit)) && !FitsRange(draft[index], range))
                field.Box.BorderBrush = Brushes.Firebrick;
            else
                field.Box.ClearValue(Control.BorderBrushProperty);
        }
        RefreshGrowthSummary(draft);
    }

    private int[] EditedFields(IReadOnlyList<string> draft) => fields.Keys.Where(index => draft[index] != pet.Fields[index]).ToArray();

    private bool CanInsert(int index, TextBox box, string text)
    {
        string proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, text);
        var draft = DraftFields();
        draft[index] = proposed;
        var range = rules.Range(index, draft, levelBonus);
        if (FitsRange(proposed, range))
            return true;
        ShowError(FieldRangeMessage(index, range));
        return false;
    }

    private static bool FitsRange(string text, (int Min, int? Max) range) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
        value >= range.Min && (range.Max is null || value <= range.Max);

    private static string FieldRangeMessage(int index, (int Min, int? Max) range)
    {
        string text = range.Max is null ? "应为非负整数" : range.Min == range.Max
            ? $"应为 {range.Min}" : $"应在 {range.Min}–{range.Max} 之间";
        return $"{PetData.FieldNames[index]}{text}。";
    }

    private void ShowSkillChoices()
    {
        string search = SkillSearch.Text.Trim();
        string? kind = SkillKindBox.SelectedItem as string;
        string? selected = (AvailableSkills.SelectedItem as PetSkillCandidate)?.Code;
        var used = learned.Select(entry => entry.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = rules.Skills(DraftFields(), pet.Equipment)
            .Where(rule => !used.Contains(rule.Code) && (kind is null or "全部" || rule.Category == kind))
            .Select(rule => new PetSkillCandidate(PetEntry.Skill(game, rule.Code), rule))
            .Where(entry => entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                PetRules.Aliases(entry.Code).Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AvailableSkills.ItemsSource = matches;
        AvailableSkills.SelectedItem = matches.FirstOrDefault(entry => entry.Code == selected) ?? matches.FirstOrDefault();
        AvailableHeading.Text = $"可选技能 · {matches.Length}";
        UpdateSkillActions();
    }

    private void UpdateSkillActions()
    {
        int index = LearnedSkills.SelectedIndex;
        var draft = DraftFields();
        int capacity = rules.Capacity(draft);
        LearnedHeading.Text = $"已学技能 · {learned.Count} / {capacity}";
        RemoveSkillButton.IsEnabled = index >= 0;
        bool canMove = CanMoveSkill(index, draft);
        MoveSkillUpButton.IsEnabled = canMove && index > 0;
        MoveSkillDownButton.IsEnabled = canMove && index < learned.Count - 1;
        var selected = AvailableSkills.SelectedItem as PetSkillCandidate;
        AddSkillButton.IsEnabled = selected is not null && selected.Rule.Locked is null && learned.Count < capacity;
        SkillDetails.Text = selected is null ? "只列出当前品种与形态的技能。":
            selected.Rule.Locked ?? (learned.Count >= capacity ? "技能槽已满，可先移除一项或提高悟性。" :
                selected.Entry.Description);
    }

    private void AddSkill(object sender, RoutedEventArgs e)
    {
        if (AvailableSkills.SelectedItem is PetSkillCandidate candidate)
            RunAction(() => Learn(candidate));
    }

    private void Learn(PetSkillCandidate candidate)
    {
        var draft = DraftFields();
        var rule = rules.Skills(draft, pet.Equipment).FirstOrDefault(skill => skill.Code == candidate.Code);
        if (rule is null || rule.Locked is not null)
            throw new InvalidDataException(rule?.Locked ?? "这只宠物无法领悟该技能。");
        if (learned.Count >= rules.Capacity(draft))
            throw new InvalidDataException("技能槽已满，可先移除一项或提高悟性。");
        var entry = candidate.Entry;
        if (learned.Any(item => item.Code.Equals(candidate.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("这项技能已经学过。");
        learned.Add(entry);
        LearnedSkills.SelectedItem = entry;
        LearnedSkills.ScrollIntoView(entry);
        ShowSkillChoices();
    }

    private void RemoveSkill(object sender, RoutedEventArgs e)
    {
        int index = LearnedSkills.SelectedIndex;
        if (index < 0) return;
        ClearError();
        learned.RemoveAt(index);
        LearnedSkills.SelectedIndex = Math.Min(index, learned.Count - 1);
        ShowSkillChoices();
    }

    private void MoveSkillUp(object sender, RoutedEventArgs e) => MoveSkill(-1);
    private void MoveSkillDown(object sender, RoutedEventArgs e) => MoveSkill(1);

    private bool CanMoveSkill(int index, IReadOnlyList<string> draft) =>
        index >= 0 && index < learned.Count &&
        rules.Skills(draft, pet.Equipment).Any(rule => rule.Code == learned[index].Code && rule.Locked is null);

    private void MoveSkill(int offset)
    {
        int index = LearnedSkills.SelectedIndex;
        int next = index + offset;
        if (next < 0 || next >= learned.Count || !CanMoveSkill(index, DraftFields())) return;
        ClearError();
        learned.Move(index, next);
        LearnedSkills.SelectedIndex = next;
        LearnedSkills.ScrollIntoView(learned[next]);
        UpdateSkillActions();
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        var changed = DraftFields();
        foreach (var (index, field) in fields)
        {
            if (changed[index] == pet.Fields[index])
                continue;
            try
            {
                changed[index] = MainWindow.Input(field.Box, PetData.FieldNames[index], 0)
                    .ToString(CultureInfo.InvariantCulture);
            }
            catch (InvalidDataException ex)
            {
                FocusField(index, ex.Message);
                return;
            }
        }
        var edits = EditedFields(changed);
        foreach (int index in fields.Keys.Where(index => edits.Any(edit => PetRules.Affects(index, edit))))
        {
            var range = rules.Range(index, changed, levelBonus);
            if (FitsRange(changed[index], range))
                continue;
            FocusField(index, FieldRangeMessage(index, range));
            return;
        }
        if (edits.Contains(5) && rules.Range(6, changed, levelBonus).Max is int experienceLimit &&
            PetRules.Value(changed, 6) > experienceLimit)
            changed[6] = experienceLimit.ToString(CultureInfo.InvariantCulture);
        var codes = learned.Select(entry => entry.Code).ToArray();
        var added = codes.Except(originalCodes).ToArray();
        int capacity = rules.Capacity(changed);
        if (codes.Length > capacity && (added.Length > 0 || capacity < rules.Capacity(pet.Fields)))
        {
            if (Section == PetEditSection.Growth)
                FocusField(15, $"当前已学 {codes.Length} 项技能，降低后的悟性最多容纳 {capacity} 项。请先在属性页调整技能，再降低悟性。");
            else
                ShowError($"当前悟性最多可容纳 {capacity} 项技能，请先移除多余技能。");
            return;
        }
        var available = rules.Skills(changed, pet.Equipment).Where(skill => skill.Locked is null).Select(skill => skill.Code).ToHashSet();
        if (added.Any(code => !available.Contains(code)))
        {
            ShowError("新加的技能当前无法领悟，请检查宠物形态、潜力极限和精灵装备。");
            return;
        }
        if (Section == PetEditSection.Skills && !codes.SequenceEqual(originalCodes))
            changed[19] = string.Join('~', codes);
        Array.Copy(changed, pet.Fields, changed.Length);
        DialogResult = true;
    }

    private void FocusField(int index, string message)
    {
        var field = fields[index];
        EditorTabs.SelectedItem = GrowthTab;
        if (index is >= 20 and <= 23 or 28 or 29 or 30) AdvancedGrowth.IsExpanded = true;
        UpdateLayout();
        field.Box.BringIntoView();
        field.Box.Focus();
        field.Box.SelectAll();
        ShowError(message);
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void RunAction(Action action)
    {
        ClearError();
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { ShowError(ex.Message); }
    }

    private void ClearError() => EditorError.Visibility = Visibility.Collapsed;

    private void ShowError(string message)
    {
        EditorError.Text = message;
        EditorError.Visibility = Visibility.Visible;
    }
}
