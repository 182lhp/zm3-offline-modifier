using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GmStudio;

public partial class MainWindow : Window
{
    private GameFolder? game;
    private EditableFile? save;
    private EditableFile? vip;
    private Slot? slot;
    private Player? player;
    private RadioButton active = null!;
    private bool loading = true;
    private bool busy;
    private bool rawPending;
    private static readonly Brush Positive = new SolidColorBrush(Color.FromRgb(18, 107, 87));
    private static readonly Brush Warning = new SolidColorBrush(Color.FromRgb(162, 75, 43));

    private JsonObject? Character => player is null ? null : save?.Draft[player.Key] as JsonObject;
    private bool Dirty => rawPending ||
        save?.Dirty == true || vip?.Dirty == true;

    public MainWindow(string? root)
    {
        InitializeComponent();
        InitializeRuntimeStatus();
        Width = Math.Clamp(SystemParameters.WorkArea.Width, MinWidth, Width);
        Height = Math.Clamp(SystemParameters.WorkArea.Height, MinHeight, Height);
        active = NavCatalog;
        InitializeSidebar();
        InitializeCatalogPager();
        BagFilter.ItemsSource = new[] { "全部栏位" }.Concat(Inventory.Labels.Where(pair => pair.Key != Inventory.PetGear).Select(pair => pair.Value));
        BagFilter.SelectedIndex = 0;
        InitAdvanced();
        loading = false;
        if (root is not null)
            Guard(() => Connect(root));
        else
            UpdateStatus();
    }

    private void Connect(string root)
    {
        var next = new GameFolder(root);
        var nextVip = next.OpenVip();
        var slots = next.Slots();
        var initial = slots.FirstOrDefault();
        var initialSave = initial is null ? null : new EditableFile(initial.File, repairPets: true);

        game = next;
        vip = nextVip;
        save = initialSave;
        slot = initial;
        SettingsButton.ToolTip = $"当前游戏目录：{next.Root}";
        AdvancedToolsButton.ContextMenu = null;
        CatalogBadge.Text = $"离线图鉴 · {next.Catalog.Count} 项物品";
        loading = true;
        try
        {
            SlotsBox.ItemsSource = slots;
            SlotsBox.SelectedItem = slot;
            CategoryBox.ItemsSource = new[] { "全部分类", "武器防具", "饰品头衔", "法宝", "时装",
                "宠物装备", "制作与技能", "强化与材料", "其他道具" };
            CategoryBox.SelectedIndex = 0;
            QualityBox.ItemsSource = new[] { "全部品质" }.Concat(next.Catalog.Select(item => item.Quality)
                .Where(quality => !string.IsNullOrWhiteSpace(quality)).Distinct().OrderByDescending(CatalogQualityRank));
            QualityBox.SelectedIndex = 0;
            CatalogRoleBox.ItemsSource = new[] { "全部角色", "通用", "孙悟空", "唐僧", "猪八戒", "沙僧" };
            CatalogRoleBox.SelectedIndex = 0;
            SearchBox.Clear();
        }
        finally { loading = false; }
        ShowVip();
        ShowSlot();
        FilterCatalog();
        Report(slot is null ? "目录已连接。请先在游戏里创建一个存档，然后点刷新。" : $"{slot.Label} 已加载。");
    }

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        if (game is null)
        {
            ShowSettings(sender, e);
            return;
        }
        if (!ConfirmDiscard())
            return;
        Guard(() =>
        {
            var slots = game.Slots();
            var next = slots.FirstOrDefault(item => item.Number == slot?.Number) ?? slots.FirstOrDefault();
            var nextSave = next is null ? null : new EditableFile(next.File, repairPets: true);
            var nextVip = game.OpenVip();
            vip = nextVip;
            slot = next;
            save = nextSave;
            loading = true;
            try
            {
                SlotsBox.ItemsSource = slots;
                SlotsBox.SelectedItem = slot;
            }
            finally { loading = false; }
            ShowVip();
            ShowSlot();
            Report(slot is null ? "未找到存档。先在游戏里创建一个存档。" : $"{slot.Label} 已从磁盘刷新。");
        });
    }

    private void SlotChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || SlotsBox.SelectedItem is not Slot next)
            return;
        if ((rawPending || save?.Dirty == true) && !ConfirmDiscard(slotOnly: true))
        {
            ReselectSlot();
            return;
        }
        try
        {
            var candidate = new EditableFile(next.File, repairPets: true);
            save = candidate;
            slot = next;
            ShowSlot();
            Report($"{next.Label} 已加载。");
        }
        catch (Exception ex) when (Handled(ex))
        {
            ReselectSlot();
            ShowError(ex.Message);
        }
    }

    private void ReselectSlot()
    {
        loading = true;
        try { SlotsBox.SelectedItem = slot; }
        finally { loading = false; }
    }

    private void ShowSlot(string? preferredPlayer = null)
    {
        var players = save is null ? [] : GameFolder.Players(save.Draft);
        player = players.FirstOrDefault(row => row.Key == preferredPlayer) ?? players.FirstOrDefault();
        loading = true;
        try
        {
            PlayersBox.ItemsSource = players;
            PlayersBox.SelectedItem = player;
            RawText.Text = save?.Draft.ToJsonString(GameJson.Format) ?? "";
            RawText.IsEnabled = save is not null;
        }
        finally { loading = false; }
        rawPending = false;
        ShowCharacter();
        ShowGarden();
        ShowBuffs();
        ShowVip();
    }

    private void PlayerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || PlayersBox.SelectedItem is not Player next)
            return;
        try
        {
            if (rawPending)
            {
                ApplyRaw();
                next = ((IEnumerable<Player>)PlayersBox.ItemsSource).FirstOrDefault(row => row.Key == next.Key) ?? player!;
            }
            player = next;
            loading = true;
            try { PlayersBox.SelectedItem = player; }
            finally { loading = false; }
            ShowCharacter();
        }
        catch (Exception ex) when (Handled(ex))
        {
            loading = true;
            try { PlayersBox.SelectedItem = player; }
            finally { loading = false; }
            ShowError(ex.Message);
        }
    }

    private void ShowCharacter()
    {
        loading = true;
        try
        {
            JsonObject? character = Character;
            PetTools.IsEnabled = character is not null;
        }
        finally { loading = false; }
        if (active == NavRole)
            ShowRoleProfile();
        ShowBag();
        ShowPets();
        ShowSkills();
        ItemDetails();
        UpdateStatus();
    }

    private static string Value(JsonObject? source, string name, string fallback = "0") =>
        GameJson.Text(source?[name]) is { Length: > 0 } value ? value : fallback;

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton button)
            SelectPage(button);
    }

    private bool SelectPage(RadioButton button)
    {
        try
        {
            ApplyForms();
            active = button;
            showingRaw = false;
            RefreshPageVisibility();
            if (button == NavRole)
                ShowRoleProfile();
            return true;
        }
        catch (Exception ex) when (Handled(ex))
        {
            RefreshNavigationState();
            ShowError(ex.Message);
            return false;
        }
    }

    private Visibility Show(RadioButton button) => !showingRaw && active == button ? Visibility.Visible : Visibility.Collapsed;

    private void ItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!loading)
            ItemDetails();
    }

    private void ItemDetails()
    {
        var item = CatalogTable.SelectedItem as CatalogItem;
        ItemTitle.Text = item?.Name ?? "请选择物品";
        ItemCode.Text = item?.Code ?? "";
        ItemDescription.Text = item is null
            ? game is null ? "连接游戏目录后，即可浏览离线图鉴。" : "从左侧列表选择物品。"
            : string.IsNullOrWhiteSpace(item.Description) ? $"{item.TypeName} · {item.Quality}".Trim(' ', '·') : item.Description;
        ItemUser.Text = item is null ? "" : string.IsNullOrWhiteSpace(item.User) ? "适用角色 · 通用" : $"适用角色 · {item.User}";
        ItemType.Text = item is null ? "" : item.TypeName;
        ItemSource.Text = AcquisitionGuide.ForItem(item?.Code);
        ItemSourceCard.Visibility = item is not null && (ItemSource.Text != AcquisitionGuide.UnknownItem ||
            item.PetEquipment || EquipmentData.Wearable(item.Type)) ? Visibility.Visible : Visibility.Collapsed;
        ItemDestination.Text = item?.Destination ?? "—";
        ItemTarget.Text = player is null ? "先选择存档和角色" : $"发放给 {player.Label}";
        ItemGrantFields.IsEnabled = GiveButton.IsEnabled = item is not null && Character is not null;
        UseReference.Visibility = item is not null && item.Bag != Inventory.Consumables ? Visibility.Visible : Visibility.Collapsed;
        GiveMaxed.Visibility = item is not null && (item.PetEquipment || EquipmentData.Wearable(item.Type))
            ? Visibility.Visible : Visibility.Collapsed;
        GiveMaxed.IsEnabled = GiveMaxed.Visibility == Visibility.Visible;
    }

    private void AddTen(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ItemQuantity.Text, out int current))
            current = 0;
        int max = (CatalogTable.SelectedItem as CatalogItem)?.PetEquipment == true ? GameRules.Int("inventory","petGearBatch") : GameRules.Int("inventory","stack");
        ItemQuantity.Text = (Math.Clamp(current, 0, max - 10) + 10).ToString(CultureInfo.InvariantCulture);
    }

    private void GiveClick(object sender, RoutedEventArgs e) => Guard(Give);

    private void Give()
    {
        if (game is null || Character is not JsonObject character || CatalogTable.SelectedItem is not CatalogItem item)
            return;
        int count = Input(ItemQuantity, "数量", 1, item.PetEquipment ? GameRules.Int("inventory","petGearBatch") : GameRules.Int("inventory","stack"));
        Inventory.Add(character, item, count, UseReference.IsChecked == true, game, GiveMaxed.IsEnabled && GiveMaxed.IsChecked == true);
        ShowBag();
        Report($"{item.Name} × {count} 已加入{item.Destination}，未保存。");
    }

    private void BagFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading)
            ShowBag();
    }

    private void ShowBag()
    {
        string? category = BagFilter.SelectedItem as string;
        var selected = BagTable.SelectedItem as InventoryRow;
        var rows = Character is JsonObject character && game is not null
            ? Inventory.List(character, game).Where(row => row.Field != Inventory.PetGear && (category == "全部栏位" || row.Category == category)).ToArray()
            : Array.Empty<InventoryRow>();
        BagTable.ItemsSource = rows;
        var next = rows.FirstOrDefault(row => selected is not null &&
            row.Field == selected.Field && row.Index == selected.Index && row.Code == selected.Code);
        BagTable.SelectedItem = next;
        if (next is not null && BagTable.IsVisible)
            BagTable.ScrollIntoView(next);
        MaxWornButton.IsEnabled = Character is JsonObject current && Inventory.Records(current, Inventory.Worn).Count > 0;
        ShowBagDetails();
        if(active==NavRole) ShowRoleProfile();
    }

    private void RemoveItem(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character || BagTable.SelectedItem is not InventoryRow row)
            return;
        if (!Confirm("移除物品", $"从{row.Category}移除 {row.Name} × {row.Quantity}？", "移除物品", caution: true))
            return;
        var entries = Inventory.Records(character, row.Field);
        entries.RemoveAt(row.Index);
        character[row.Field] = string.Join('}', entries);
        ShowBag();
        Report($"{row.Name} 已从存档移除。");
    });

    private void ShowPets()
    {
        save?.RepairPetLimits();
        var selected = PetTable.SelectedItem as PetRow;
        var rows = Character is JsonObject character && game is not null
            ? Inventory.ListPets(character, game).ToArray() : Array.Empty<PetRow>();
        PetTable.ItemsSource = rows;
        var next = rows.FirstOrDefault(row => selected is not null &&
            row.Index == selected.Index && PetData.Family(row.Code) == PetData.Family(selected.Code)) ?? rows.FirstOrDefault();
        PetTable.SelectedItem = next;
        if (next is not null && PetTable.IsVisible)
            PetTable.ScrollIntoView(next);
        ShowPetDetails();
    }

    private void RestPet(object sender, RoutedEventArgs e) => ChangePet(false);
    private void FightPet(object sender, RoutedEventArgs e) => ChangePet(true);

    private void ChangePet(bool fight) => Guard(() =>
    {
        if (Character is not JsonObject character || PetTable.SelectedItem is not PetRow row)
            return;
        Inventory.ChangePet(character, row, fight);
        ShowPets();
        Report($"{row.Name} 已设为{(fight ? "出战" : "休息")}，等待保存。");
    });

    private void RawFormChanged(object sender, TextChangedEventArgs e)
    {
        if (loading || save is null)
            return;
        rawPending = true;
        UpdateStatus();
    }

    private void ApplyRawClick(object sender, RoutedEventArgs e) => Guard(() =>
    {
        ApplyRaw();
        Report("原始 JSON 已应用到存档。");
    });

    private void ApplyForms()
    {
        ApplyRaw();
    }

    private void ApplyRaw()
    {
        if (!rawPending || save is null)
            return;
        var candidate = GameJson.Parse(RawText.Text);
        ValidateSave(candidate);
        ValidateAdvancedChanges(candidate, save.Draft);
        save.Draft = candidate;
        ShowSlot(player?.Key);
    }

    private void PopulateRaw()
    {
        loading = true;
        try { RawText.Text = save?.Draft.ToJsonString(GameJson.Format) ?? ""; }
        finally { loading = false; }
    }

    private static int RequiredCap(JsonObject draft)
    {
        int highest = 0;
        foreach (Player role in GameFolder.Players(draft))
        {
            var character = (JsonObject)draft[role.Key]!;
            highest = Math.Max(highest, GameJson.Number(character["curLevel"]));
            foreach (string pet in Inventory.Records(character, Inventory.Pets))
                if (int.TryParse(pet.Split('|', 7).ElementAtOrDefault(5), out int level))
                    highest = Math.Max(highest, level);
        }
        return Math.Clamp(highest - 80, 0, 10);
    }

    private static void ValidateMap(int stage, int part)
    {
        if (stage < 1 || part < 1 || (stage < 20 && (stage > 4 || part > 3)))
            throw new InvalidDataException("普通世界大关应为 1–4，小关应为 1–3。新世界编号从 20 开始。");
    }

    private static void ValidateSave(JsonObject draft)
    {
        if (!decimal.TryParse(GameJson.Text(draft["version"]), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal version) || version != 38.7m)
            throw new InvalidDataException("此版本支持的存档版本为 38.7。");
        var players = GameFolder.Players(draft);
        if (players.Count == 0)
            throw new InvalidDataException("存档中没有可读取的角色。");
        ValidateMap(GameJson.Number(draft["curBigStage"]), GameJson.Number(draft["curBigLevel"]));
        int cap = GameJson.Number(draft["levelUpLimit"], -1);
        if (cap is < 0 or > 10)
            throw new InvalidDataException("等级上限加成应为 0–10。");
        foreach (Player role in players)
        {
            var data = (JsonObject)draft[role.Key]!;
            if (GameJson.Number(data["roleid"]) is < 1 or > 4 ||
                GameJson.Number(data["curLevel"]) is < 1 or > 90 ||
                GameJson.Number(data["curExp"], -1) < 0 || GameJson.Number(data["lhValue"], -1) < 0)
                throw new InvalidDataException($"{role.Label}的角色编号、等级、经验或灵魂超出范围。");
        }
        if (RequiredCap(draft) > cap)
            draft["levelUpLimit"] = RequiredCap(draft);
        PetData.RepairPotentialLimits(draft);
    }

    private static void ValidateAdvancedChanges(JsonObject draft, JsonObject previous)
    {
        foreach (var (key, segments, label) in new[]
        {
            ("constellationsLevel", 8, "星座等级"), ("fourPhalanx", 4, "四方阵")
        })
        {
            if (JsonNode.DeepEquals(draft[key], previous[key]))
                continue;
            var parts = GameJson.Text(draft[key]).Split('_');
            if (parts.Length != segments || parts.Any(part => !Regex.IsMatch(part, @"^-?\d+(?:-\d+)?$")))
                throw new InvalidDataException($"{label}应包含 {segments} 段有效数据，以 _ 分隔。");
        }
        if (!JsonNode.DeepEquals(draft["savedBuffString"], previous["savedBuffString"]))
        {
            foreach (string row in GameJson.Text(draft["savedBuffString"]).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] values = row.Split('|');
                if (values.Length != 3 || !int.TryParse(values[0], out int kind) || kind is < 0 or > 8 or 2 ||
                    !decimal.TryParse(values[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _) ||
                    !int.TryParse(values[2], out int frames) || frames is < 1 or > 86400)
                    throw new InvalidDataException("增益数据格式无效。支持 0–8 中除 2 以外的类型，时长为 1–86400 帧。");
            }
        }
    }

    internal static int Input(TextBox box, string label, int minimum, int maximum = int.MaxValue)
    {
        if (int.TryParse(box.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
            value >= minimum && value <= maximum)
            return value;
        string hint = maximum == int.MaxValue ? $"至少为 {minimum}" : $"介于 {minimum} 和 {maximum} 之间";
        throw new InvalidDataException($"{label}应为{hint}的整数。");
    }

    private (bool Save, bool Vip) SaveDrafts(bool pushing,string? requestId=null)
    {
        ApplyForms();
        bool saveSlot = save is not null && (save.Dirty || pushing && (showingRaw || active != NavVip));
        bool saveVip = vip is not null && (vip.Dirty || pushing && !showingRaw && active == NavVip);
        if (saveSlot)
        {
            ValidateSave(save!.Draft);
            save.CheckCurrent();
        }
        if (saveVip)
            vip!.CheckCurrent();
        var files=new List<EditableFile>();
        if(saveSlot)files.Add(save!);
        if(saveVip)files.Add(vip!);
        int backups=StorageTransaction.SaveFiles(files,requestId??Guid.NewGuid().ToString("N"));
        PopulateRaw();
        if (saveSlot) ShowPetDetails();
        Report(saveSlot || saveVip ? $"修改已保存到磁盘{(backups > 0 ? "，原文件已备份" : "")}。" : "没有需要保存的修改。");
        return (saveSlot, saveVip);
    }

    private bool ConfirmDiscard(bool slotOnly = false) =>
        !(slotOnly ? rawPending || save?.Dirty == true : Dirty) ||
        Confirm("放弃修改", "还有未保存的修改。继续将丢弃这些修改。", "放弃修改", caution: true);

    private bool Confirm(string title, string message, string action, bool caution = false) =>
        PromptDialog.Confirm(this, title, message, action, caution);

    private void OnClosing(object? sender, CancelEventArgs e) => e.Cancel = busy || !ConfirmDiscard();

    private void HelpClick(object sender, RoutedEventArgs e) => ShowHelp();

    private void ShowHelp() => new HelpWindow(game?.Root) { Owner = this }.ShowDialog();

    private async void OnShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            ShowHelp();
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;
        if (e.Key == Key.S && SaveButton.IsEnabled)
        {
            e.Handled = true;
            await SaveAsync();
        }
        else if (e.Key == Key.F && SelectPage(NavCatalog))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private static bool Handled(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or
            ArgumentException or InvalidDataException;

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) when (Handled(ex)) { ShowError(ex.Message); }
    }

    private void ShowError(string message)
    {
        Report(message, error: true);
        PromptDialog.Alert(this, "操作未完成", message);
    }

    private void Report(string message, bool error = false)
    {
        Status.Text = save is { PetLimitRepairs: > 0, Dirty: true } && !error
            ? message + $" 已修正 {save.PetLimitRepairs} 处宠物潜力极限，尚未保存。" : message;
        if (Dirty && !error && !Status.Text.Contains("未保存") && !Status.Text.Contains("等待保存"))
            Status.Text += "（未保存）";
        Status.Foreground = error ? Warning : Positive;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        DirtyBadge.Text = Dirty ? "● 有待保存修改" : "无待保存修改";
        DirtyBadge.Foreground = Dirty ? Warning : Positive;
        SaveButton.IsEnabled = !busy && (save is not null || vip is not null);
    }
}
