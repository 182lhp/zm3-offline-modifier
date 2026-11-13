#if QA
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private static JsonObject CreateProbeRole(JsonObject first, int role)
    {
        var second = new JsonObject();
        foreach (var (key, old) in first)
        {
            second[key] = old switch
            {
                JsonArray => new JsonArray(),
                JsonObject => new JsonObject(),
                JsonValue scalar when scalar.TryGetValue<string>(out _) => JsonValue.Create(""),
                JsonValue scalar when scalar.TryGetValue<bool>(out _) => JsonValue.Create(false),
                _ => JsonValue.Create(0)
            };
        }
        second["roleid"] = role;
        second["curLevel"] = 1;
        second["curExp"] = 0;
        second["lhValue"] = 10000;
        second["luckdata"] = 20;
        second["strenthtimes"] = 5;
        second["skillLimit"] = 5;
        second["controlPlayer"] = 1;
        second["isstudyskill"] = new JsonArray(new JsonObject { ["xflevel"] = 0, ["skillName"] = "" },
            new JsonObject { ["xflevel"] = 0, ["skillName"] = "" });
        SkillData.SetPassive(second, [0, 0, 0, 0, 0]);
        second["immortalitylist"] = new JsonArray(Enumerable.Range(0, 6)
            .Select(_ => (JsonNode?)new JsonObject { ["canuse"] = false, ["usenum"] = 0 }).ToArray());
        return second;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    internal async Task QualityProbe(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(game?.Catalog.Count == 450, "The catalog should contain 450 distinct items.");
            Require(Dirty == false && save is not null && slot is not null, "Opening a slot should start clean.");
            File.WriteAllLines(Path.Combine(folder,"catalog-quality-order.txt"),catalogMatches.Select(item=>item.Quality).Distinct());
            Require(CatalogTable.Items.Count == catalogPageSize && catalogMatches.Length == 450 &&
                ItemTitle.Text.Length > 0, "Initial catalog page.");
            VerifyTypeface();
            VerifyFontSettings(folder);
            VerifySidebarSettings(folder);
            await VerifySettingsAndTools(folder);
            VerifyLocalGifts(folder);
            Require(CategoryBox.Items.Count == 9 && game!.Catalog.All(item => CategoryBox.Items.Contains(item.Group)),
                "Every item belongs to a grouped category.");
            Require(game!.Catalog.All(item => item.Icon is not null), "Local illustrations for all 450 catalog items.");
            VerifySkillHintDefinitions();
            Require(game.PetNames.All(pair => game.Portrait("pets", pair.Key) is not null), "Local pet portraits.");
            var petSkillNames = (JsonObject)game.Official["petSkillNames"]!;
            Require(petSkillNames.All(pair => game.Portrait("petSkills", pair.Key) is not null), "Local pet skill illustrations.");
            Require(game.Portrait("pets", "missing_asset") is null, "Missing optional art falls back to a text tile.");
            string corruptImage = Path.Combine(folder, "corrupt-icon.png");
            File.WriteAllText(corruptImage, "not an image");
            Require(GameFolder.ReadImage(corruptImage) is null, "Unreadable images fall back to a text tile.");
            await VerifyCombo(SlotsBox, "save slot");
            await VerifyCombo(PlayersBox, "character");
            await VerifyCombo(CategoryBox, "item category");
            await VerifyCombo(QualityBox, "item quality");
            await VerifyCombo(CatalogRoleBox, "catalog role");
            VerifyDialogCombo();
            VerifyCatalogPager();
            VerifyCatalogRoles();
            Require(Math.Abs(SearchBox.ActualWidth-280)<1,"Catalog search uses a bounded comfortable width.");
            var clearRight=CatalogClearButton.TranslatePoint(new Point(CatalogClearButton.ActualWidth,0),CatalogFilterBar).X;
            Require(Math.Abs(clearRight-CatalogFilterBar.ActualWidth)<1,"Clear is pinned to the filter bar's right edge.");
            if(CatalogFilters.ActualWidth>=670)
            {
                double categoryX=CategoryBox.TranslatePoint(new Point(),CatalogFilters).X;
                double qualityX=QualityBox.TranslatePoint(new Point(),CatalogFilters).X;
                double roleX=CatalogRoleBox.TranslatePoint(new Point(),CatalogFilters).X;
                double searchX=SearchBox.TranslatePoint(new Point(),CatalogFilters).X;
                Require(categoryX<qualityX && qualityX<roleX && roleX<searchX,"Category, quality, applicable role and search are ordered consistently.");
            }
            Capture(folder, "catalog.png");
            CategoryBox.SelectedItem = "制作与技能";
            await CaptureWhenReady(folder, "catalog-crafting.png");
            ClearSearch(this, new RoutedEventArgs());
            VerifyHelp(folder);
            await VerifyCompactLayout(folder);
            await VerifyRoleProfile(folder);

            var original = (JsonObject)save!.Draft.DeepClone();
            var originalPlayer = (JsonObject)original["player1_obj"]!;
            var originalRecords = Inventory.Records(originalPlayer, Inventory.Consumables);
            SearchBox.Text = "枯叶弓";
            Require(ItemSource.Text.Contains("天宫道") && ItemSource.Text.Contains("千里眼") &&
                ItemSourceCard.Visibility == Visibility.Visible, "Item detail shows a verified boss and stage.");
            await CaptureWhenReady(folder, "acquisition.png");
            SearchBox.Text = "太极八卦";
            Require(ItemSource.Text.StartsWith("合成") && ItemSource.Text.Contains("爻阴"), "Crafted items show their recipe.");
            SearchBox.Text = "丹药";
            Require(catalogMatches.Length == game.Catalog.Count(item => item.TypeName == "丹药"), "Fine type is searchable.");
            SearchBox.Text = "wpsmd1";
            Require(CatalogTable.Items.Count == 1, "Code search.");
            Require(ItemTitle.Text == "一品生命丹" && ItemDestination.Text == "道具栏", "Item metadata and destination.");
            QualityBox.SelectedItem = "传说";
            Require(CatalogTable.Items.Count == 0 && !GiveButton.IsEnabled, "Quality filtering and empty result.");
            ClearSearch(this, new RoutedEventArgs());
            Require(catalogMatches.Length == 450 && catalogPage == 1, "Clearing restores the unfiltered catalog.");
            SearchBox.Text = "一品生命丹";
            CategoryBox.SelectedItem = "其他道具";
            QualityBox.SelectedItem = "普通";
            Require(CatalogTable.Items.Count == 1 && JsonNode.DeepEquals(save.Draft, original),
                "Name, type, and quality combine without modifying the draft.");
            ItemQuantity.Text = "3";
            GiveClick(GiveButton, new RoutedEventArgs());
            var rows = Inventory.Records(Character!, Inventory.Consumables);
            Require(rows.Count == originalRecords.Count + 1, "Item staged.");
            Require(rows.Take(originalRecords.Count).SequenceEqual(originalRecords), "Existing inventory retained.");
            var fields = rows[^1].Split('|');
            Require(fields.Length == 28 && fields[2] == "wpsmd1" && fields[3] == "zbwp" && fields[22] == "3",
                "Potion payload format and amount.");
            Require(JsonNode.DeepEquals(GameJson.Read(save.Path), original), "Staging must not write the file.");
            Capture(folder, "detail.png");

            SearchBox.Text = "牛魔王装";
            var fashion = game!.Catalog.First(item => item.Type == "zbsz");
            Inventory.Add(Character!, fashion, 1, true, game);
            var fashionFields = Inventory.Records(Character!, Inventory.Fashion)[^1].Split('|');
            Require(fashionFields.Length == 29 && fashionFields[27] == "never", "Permanent fashion record.");
            var petGear = game.Catalog.First(item => item.PetEquipment);
            Inventory.Add(Character!, petGear, 2, true, game);
            Require(Inventory.Records(Character!, Inventory.PetGear).TakeLast(2).All(entry =>
                entry.Split('|').Length == 13 && entry.StartsWith(petGear.Code + "|")), "Pet equipment layout.");
            string petGearSnapshot=GameJson.Text(Character![Inventory.PetGear]);
            Require(BagFilter.Items.Cast<string>().SequenceEqual(new[] {"全部栏位","装备背包","道具栏","时装栏","技能书栏","当前穿戴"}),
                "The role inventory exposes only character inventory sections.");
            foreach(string filter in BagFilter.Items.Cast<string>().ToArray())
            {
                BagFilter.SelectedItem=filter;ShowBag();
                Require(BagTable.Items.Cast<InventoryRow>().All(row=>row.Field!=Inventory.PetGear),
                    "Pet equipment stays outside every role inventory view, including All.");
            }
            BagFilter.SelectedIndex=0;ShowPets();PetTable.SelectedIndex=0;ShowPetDetails();
            Require(PetBackpackList.Items.Count==Inventory.Records(Character!,Inventory.PetGear).Count &&
                GameJson.Text(Character![Inventory.PetGear])==petGearSnapshot,
                "Pet equipment remains intact and available in the pet equipment backpack.");


            Require(SelectPage(NavSkills), "Skill navigation.");
            await VerifyCombo(SchoolBox, "skill school");
            Require(SkillsTable.Items.Count == 5 && SchoolBox.Items.Count == 2, "Two schools with five skills each.");
            Require(new SkillRow("测试", "test", "测试", 0, 0).LevelLabel == "未学习", "Explicit unlearned label.");
            await VerifySkillRoleSwitch(folder);
            UpdateLayout();
            VerifyRenderedSkillHint((DependencyObject)SkillsTable.ItemContainerGenerator.ContainerFromItem(SkillsTable.Items[0]),
                ((SkillRow)SkillsTable.Items[0]).Code,folder,"hero-skill-tooltip.png");
            VerifyRenderedSkillHint(PassiveList,"",folder,"hero-passive-tooltip.png");
            var passive = VisualDescendants(PassiveList).OfType<ComboBox>().ToArray();
            Require(passive.Length == 5 && passiveRows.Select(row => row.Glyph).Distinct().Count() == 5, "Five illustrated passive controls.");
            int originalPassive = PassiveLevel(Character!, 0);
            int changedPassive = originalPassive == 2 ? 4 : 2;
            passive[0].SetCurrentValue(ComboBox.SelectedValueProperty, changedPassive);
            Require(PassiveLevel(Character!, 0) == originalPassive, "Passive level is staged before Apply.");
            SchoolBox.SelectedIndex = 1 - SchoolBox.SelectedIndex;
            Require(passiveRows[0].DraftLevel == changedPassive, "Changing school keeps staged passive levels.");
            ApplyPassiveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(PassiveLevel(Character!, 0) == changedPassive, "Apply writes the selected passive level.");
            SchoolLevelBox.Text = "2";
            SetSchoolLevel(SchoolBox, new RoutedEventArgs());
            Require(SkillData.SchoolLevel(Character!, (SkillSchool)SchoolBox.SelectedItem) == 2, "Individual school level.");
            SkillLevelBox.Text = "2";
            SetSkillLevel(SkillsTable, new RoutedEventArgs());
            Require(((SkillRow)SkillsTable.SelectedItem).Level == 2, "Individual skill level.");
            MaxAllSkills(SkillsTable, new RoutedEventArgs());
            Require(SkillData.Schools(game.HeroSkills, GameJson.Number(Character!["roleid"])).All(school =>
                SkillData.SchoolLevel(Character!, school) == 5 && SkillData.Rows(Character!, school).All(row => row.Level == 3)),
                "Both schools and active skills are maximized.");
            Require(PassiveLevel(Character!, 4) == 5, "Passive skill update.");
            await CaptureWhenReady(folder, "skills.png");

            await VerifyGardenAndBuffs(folder);

            Require(SelectPage(NavRole), "Gear navigation.");
            await VerifyCombo(BagFilter, "inventory section");
            var weapon = game.Catalog.Single(item => item.Code == "ptdxzg");
            Inventory.Add(Character!, weapon, 1, true, game);
            ShowBag();
            BagTable.SelectedItem = ((IEnumerable<InventoryRow>)BagTable.ItemsSource).Last(item => item.Code == weapon.Code);
            MaxEquipment(BagTable, new RoutedEventArgs());
            var geared = EquipmentData.Parse(Inventory.Records(Character!, Inventory.Gear)[^1], false);
            Require(geared[23] == "7" && geared[9] == "15" && geared[15] == "true",
                "Reference limit +10 minus 7 × coefficient; elemental flags.");
            Require(BagSummary.Text.Contains("+7") && BagStats.ItemsSource.Cast<StatPreview>()
                .Single(stat => stat.Name == "攻击") is { Current: "15", Limit: "15" }, "Inline comparison uses the selected strength.");
            await CaptureWhenReady(folder, "bag.png");
            BagTable.SelectedItem = ((IEnumerable<InventoryRow>)BagTable.ItemsSource).Last(item => item.Code == weapon.Code);
            Exception? dialogFailure = null;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                var child = OwnedWindows.OfType<Window>().SingleOrDefault(w => w.Title.StartsWith("装备调参"));
                if (child is null)
                {
                    dialogFailure = new InvalidDataException("Equipment dialog did not open.");
                    return;
                }
                try
                {
                    Capture(folder, "gear.png", child);
                    ClickDialogButton(child, "属性清零");
                    ClickDialogButton(child, "应用到存档");
                    Require(!child.IsVisible, "Equipment dialog should close after valid inputs.");
                }
                catch (Exception ex)
                {
                    dialogFailure = ex;
                    if (child.IsVisible)
                        child.DialogResult = false;
                }
            });
            EditEquipment(BagTable, new RoutedEventArgs());
            if (dialogFailure is not null)
                throw dialogFailure;
            Require(EquipmentData.Parse(Inventory.Records(Character!, Inventory.Gear)[^1], false)[9] == "0",
                "Equipment dialog applies edited fields.");
            var magic = game.Catalog.Single(item => item.Code == "tjbg");
            Inventory.Add(Character!, magic, 1, true, game, maxed: true);
            Require(EquipmentData.Parse(Inventory.Records(Character!, Inventory.Gear)[^1], false)[20] == "9",
                "Taiji Bagua max level.");
            var squadPet = PetData.New("dragon4");
            squadPet.Maximize(game);
            squadPet.Dress(game, 1, 2, false);
            Require(squadPet.Fields[5] == "90" && squadPet.Equipment.Count == 5 &&
                squadPet.Fields[19].Split('~').Length <= 8 && squadPet.Encode().Contains('&'),
                "Pet stats, skill plan and four-piece set with spirit.");

            Require(SelectPage(NavRole), "Role navigation.");
            Character!["curLevel"] = 87;
            Character!["lhValue"] = 31415;
            ValidateSave(save.Draft);
            Require(SelectPage(NavPets), "Role commit and pet navigation.");
            Require(GameJson.Number(Character!["curLevel"]) == 87 &&
                GameJson.Number(save.Draft["levelUpLimit"]) == 7, "Role and level cap update.");
            var role2 = CreateProbeRole(Character!, 2);
            Require(GameJson.Number(role2["roleid"]) == 2 && GameJson.Number(role2["curLevel"]) == 1 &&
                GameJson.Text(role2[Inventory.Gear]) == "" && GameJson.Text(role2[Inventory.Pets]) == "", "New second-role defaults.");
            var oldPetRecord = Inventory.Records(Character!, Inventory.Pets)[0];
            PetTable.SelectedIndex = 0;
            FightPet(PetTable, new RoutedEventArgs());
            Require(Inventory.ListPets(Character!, game).Count(item => item.Status == "出战") == 1, "Single pet active.");
            PetTable.SelectedIndex = 0;
            RestPet(PetTable, new RoutedEventArgs());
            Require(Inventory.Records(Character!, Inventory.Pets)[0] == oldPetRecord, "Pet fields and equipment retained.");
            string oldPets = GameJson.Text(Character![Inventory.Pets]);
            Character![Inventory.Pets] = oldPets + "}" + squadPet.Encode();
            ShowPets();
            PetTable.SelectedItem = ((IEnumerable<PetRow>)PetTable.ItemsSource).Last();
            Require(PetTitle.Text == "青龙妖圣" && PetGearList.Items.Count == 5 && PetSkillList.Items.Count > 0,
                "Selected pet shows its own skills and worn gear.");
            Require(PetSource.Text.Contains("先获得龙仔") && PetSource.Text.Contains("饕餮"), "Evolution shows the base pet location.");
            await CaptureWhenReady(folder, "pets.png");
            await VerifyPetCreation(folder);
            await VerifyPetProfile(folder);
            VerifyPetEditor(folder);
            VerifyPetGuide(folder);
            Character![Inventory.Pets] = oldPets;
            ShowPets();

            Require(SelectPage(NavVip), "VIP navigation.");
            await VerifyMembership(folder);
            vip!.Draft["lastMoney"] = 7654;
            ShowVip();
            Require(OpenRawPage(), "VIP pending account and JSON navigation.");
            var raw = GameJson.Parse(RawText.Text);
            Require(GameJson.Number(raw["player1_obj"]!["lhValue"]) == 31415, "Raw JSON follows the draft.");
            var invalid = (JsonObject)raw.DeepClone();
            invalid["savedBuffString"] = "2|10|100";
            bool blockedBuff = false;
            try { ValidateAdvancedChanges(invalid, raw); }
            catch (InvalidDataException) { blockedBuff = true; }
            Require(blockedBuff, "Reject the unsupported buff type.");
            raw["gm_test_unknown_field"] = "保留";
            RawText.Text = raw.ToJsonString(GameJson.Format);
            string backupDirectory = Path.Combine(game.Saves, ".gm-backups");
            var previousBackups = Directory.Exists(backupDirectory) ? Directory.GetFiles(backupDirectory, "*.json").ToHashSet() : [];
            var result = SaveDrafts(false);
            Require(result == (true, true) && !Dirty, "Save slot and VIP drafts.");
            Require(GameJson.Text(GameJson.Read(save.Path)["gm_test_unknown_field"]) == "保留", "Raw fields survive save.");
            Require(GameJson.Number(GameJson.Read(vip!.Path)["lastMoney"]) == 7654, "VIP write.");
            var backups = Directory.GetFiles(backupDirectory, "*.json").Except(previousBackups);
            var slotBackup = backups.Single(path => Path.GetFileName(path).StartsWith(Path.GetFileNameWithoutExtension(save.Path) + "-"));
            Require(JsonNode.DeepEquals(GameJson.Read(slotBackup), original), "Exact original backup content.");

            var endpoint = new TestService(game.Saves);
            using var client = new HttpClient(endpoint) { BaseAddress = new Uri("http://127.0.0.1:43999/") };
            var testGame = new GameFolder(game.Root, client);
            await testGame.CheckServerAsync();
            await testGame.PushAsync($"save?slot={slot!.Number}", save);
            await testGame.PushAsync("vip", vip);
            Require(endpoint.Sent.SequenceEqual(new[] { $"/save?slot={slot.Number}", "/vip" }), "Existing API routes.");
            endpoint.ReportedDirectory = folder;
            bool wrongServer = false;
            try { await testGame.CheckServerAsync(); }
            catch (InvalidOperationException) { wrongServer = true; }
            Require(wrongServer, "Reject a local service connected to a different game.");

            await VerifyAutomaticSave(folder);

            File.AppendAllText(save.Path, "\n");
            bool concurrentWrite = false;
            try { save.CheckCurrent(); }
            catch (InvalidOperationException) { concurrentWrite = true; }
            Require(concurrentWrite, "Reject a disk update since last refresh.");

            VerifyPrompts(folder);
            File.WriteAllText(Path.Combine(folder, "qa.txt"),
                $"PASS — applicable-role catalog filtering including universal items, independent editing role, combined filters and reset, right-aligned clear control, character-only inventory filters, pet equipment preservation and pet-page access, bounded catalog search following category and quality, merged role and inventory workspace, retired character editor and navigation removed, live equipment/profile refresh and responsive details, unified automatic local save and same-directory synchronization, offline and timeout fallback, mismatch rejection, disconnected retry, slot/VIP routing and concurrent-write protection, unified settings hub, nested font/sidebar save/cancel/reset/persistence, live preference summaries, simplified navigation, settings directory validation/apply/cancel, original save preservation, advanced JSON entry/return and correct push routing, independent garden and temple levels, five matching temple portraits, temple threshold boundaries and isolated apply/cancel editing, original-art nine-plot garden, accurate type and five-grade rules, planting/cancel/batch/unlock/renew and extra-field preservation, original effect icons and minute presets, safe effect limits and precise durations, retired navigation and endpoints removed, illustrated membership cards, upgrade-only VIP levels and VIP5 lock, recharge preview/apply/cancel/overflow checks, eligible one-click gifts for both roles, claim idempotence and full-bag atomic rollback, compact grouped cultivation editor, collapsed advanced parameters, field limits and read-only capped progress, independent growth and skill editing, removed suit presets and equipment generation, final-form pet evolution for every species, cleared old skills and ordered talents/ultimates, single-pet evolution and cancellation, per-species caps, preserved backpack and equipment, complete skill hover cards in pet/role views, removed pet More menu and annotation, illustrated pet creation for every species, first-form baby and final-form extreme presets independent of form browsing, fixed-height pet selector, three-tab pet profile, permanent stats and suits, equipment round trips, cultivation limits and shortcuts, four roles and 40 active skill icons, inline passive and school editing, catalog pagination, whole-result sorting, sidebar renaming, ordering, validation, cancel, persistence and reset, role portrait, worn items, screenshot stat parity, applied edits and role switching, independent fonts, offline sources, catalog filters, equipment, pets, compact layout at 125% and 150% render scale, centered dropdowns, confirmations, F1 help, plots, buffs, VIP, raw JSON, fresh backups alongside historical backups, protocol and concurrent writes. Monitor scale {VisualTreeHelper.GetDpi(this).DpiScaleX:P0}.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(folder, "qa.txt"), ex.ToString());
            Application.Current.Shutdown(1);
        }
    }

    private async Task CaptureWhenReady(string folder, string name)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(folder, name);
    }

    private async Task VerifyRoleProfile(string folder)
    {
        var localGame = game!;
        for (int role = 1; role <= 4; role++)
            Require(localGame.RoleArtwork.Portrait(role, 0, 0, out bool fallback) is { PixelHeight: > 60 } && !fallback,
                $"Body and weapon art for role {role}.");
        Require(new RoleArtwork(Path.Combine(folder, "missing-assets")).Portrait(1, 0, 0, out _) is null,
            "Missing local art can use the empty state.");

        var sample = new JsonObject
        {
            ["roleid"] = 1, ["curLevel"] = 35, ["curExp"] = 73539, ["luckdata"] = 20, ["isshowfashion"] = true,
            ["ispassiveskill"] = new JsonArray(5, 5, 5, 5, 5),
            ["isstudyskill"] = new JsonArray(new JsonObject { ["skillName"] = "sx~3|" }),
            [Inventory.Worn] = "1||tjbg|zbfb|0|||9610|9610|970|330|0.08|0.08|28|8|true|true|true|true|true|9|3|1|0|0.08|0|0|0}" +
                "10||ssttzssz|zbsz|0|||410|10|26|10|0|0|0|0|true|true|true|true|true|0|0|1|7|0.12|0|0|never|0}" +
                "4||zjbtg|zbwq|0|||10|10|80|10|0|0.05|0|0|true|true|true|true|true|0|0|1|7|0|0|0|0"
        };
        var saved = new JsonObject { ["savedBuffString"] = "7|1000|7558,3|300|7558,4|1|49395,5|500|7558,6|500|7558,8|500|7558" };
        var projection = new RoleProfile(localGame, saved, sample);
        Require(projection is { Level: 35, Experience: 73539, NextExperience: 85000, Power: 2890, Clothing: 10, Weapon: 4 },
            "The reference role matches the screenshot.");
        string Stat(string label) => projection.Metrics.Single(metric => metric.Label == label).Value;
        Require(Stat("生命上限") == RoleProfile.Format(12910) && Stat("魔法上限") == RoleProfile.Format(10960) &&
            Stat("攻击") == RoleProfile.Format(3245) && Stat("防御") == "920" && Stat("暴击") == "38%" &&
            Stat("闪避") == "8%" && Stat("魔抗") == "20%" && Stat("回血") == "35" && Stat("回魔") == "14",
            "Computed attributes match the in-game panel.");
        Require(localGame.RoleArtwork.Portrait(projection.Id, projection.Clothing, projection.Weapon, out bool baseArt) is not null && !baseArt,
            "The reference outfit and weapon can be rendered together.");
        sample["isshowfashion"] = false;
        Require(new RoleProfile(localGame, saved, sample).Clothing == 0, "Hidden fashion uses the normal armor.");

        var draft = (JsonObject)save!.Draft.DeepClone();
        string? playerKey = player?.Key;
        try
        {
            Require(SelectPage(NavRole) && RolePortraitImage.Source is not null && RoleWornSlots.Items.Count >= 6,
                "The role page shows the current slot's portrait and gear.");
            Require(BagTable.IsVisible && BagFilter.IsVisible && RoleProfileContent.IsVisible &&
                FindName("RoleEditor") is null && FindName("NavBag") is null && FindName("BagPage") is null,
                "Role profile and inventory share one page, with the retired editor and navigation removed.");
            var testGear=localGame.Catalog.Single(item=>item.Code=="tjbg");
            Inventory.Add(Character!,testGear,1,true,localGame);ShowBag();
            BagTable.SelectedItem=BagTable.Items.Cast<InventoryRow>().Last(row=>row.Code==testGear.Code && row.Field==Inventory.Gear);
            MoveEquipment(BagTable,new RoutedEventArgs());
            Require(RoleWornSlots.Items.Cast<RoleGearSlot>().Single(row=>row.Label=="法宝").Name==testGear.Name,
                "Wearing an item refreshes the role's worn slots without changing pages.");
            var expectedProfile=new RoleProfile(localGame,save.Draft,Character!);
            Require(RoleProfilePower.Text==$"战力 {RoleProfile.Format(expectedProfile.Power)}","Equipment changes refresh the character metrics.");
            BagTable.SelectedItem=BagTable.Items.Cast<InventoryRow>().Single(row=>row.Code==testGear.Code && row.Field==Inventory.Worn);
            MoveEquipment(BagTable,new RoutedEventArgs());
            Require(RoleWornSlots.Items.Cast<RoleGearSlot>().Single(row=>row.Label=="法宝").Name=="未穿戴",
                "Putting gear back refreshes the profile on the same page.");
            await CaptureWhenReady(folder, "role-profile.png");
            Character!["curLevel"] = 36;
            ShowCharacter();
            Require(RoleProfileLevel.Text == "Lv. 36", "Applied edits refresh the role panel.");
            save.Draft["player2_obj"] = CreateProbeRole(Character!, 2);
            ShowSlot(playerKey);
            PlayersBox.SelectedItem = PlayersBox.Items.Cast<Player>().Single(item => item.Key == "player2_obj");
            Require(RoleProfileName.Text == "唐僧" && RoleProfileLevel.Text == "Lv. 1" &&
                RolePortraitImage.Source is not null, "Switching players refreshes the portrait and statistics.");
        }
        finally
        {
            save.Draft = draft;
            ShowSlot(playerKey);
            SelectPage(NavCatalog);
        }
    }

    private async Task VerifySkillRoleSwitch(string folder)
    {
        var localGame = game!;
        var codes = Enumerable.Range(1, 4).SelectMany(role => SkillData.Schools(localGame.HeroSkills, role))
            .SelectMany(school => school.Codes).ToArray();
        Require(codes.Distinct().Count() == 40 && codes.All(code => localGame.SkillArtwork.Icon(code) is { PixelWidth: > 50 }),
            "All 40 skill codes resolve to the local exported art.");
        Require(new SkillArtwork(Path.Combine(folder, "missing-assets")).Icon(codes[0]) is null, "Missing skill artwork uses a fallback tile.");

        var original = (JsonObject)save!.Draft.DeepClone();
        var baseRole = Character!;
        string? playerKey = player?.Key;
        int firstSchool = SchoolBox.SelectedIndex;
        var originalPalette = passiveRows[0].Tile;
        try
        {
            for (int role = 1; role <= 4; role++)
            {
                if (role != 1)
                {
                    save.Draft["player2_obj"] = CreateProbeRole(baseRole, role);
                    ShowSlot(playerKey);
                    PlayersBox.SelectedItem = PlayersBox.Items.Cast<Player>().Single(item => item.Key == "player2_obj");
                }
                Require(SkillRole.Text.StartsWith(GameJson.Role(role)) && SkillHeroImage.Source is not null,
                    $"Skill page identifies role {role}.");
                if (role != 1)
                    Require(passiveRows[0].Tile != originalPalette, $"Passive palette follows role {role}.");
                for (int school = 0; school < 2; school++)
                {
                    SchoolBox.SelectedIndex = school;
                    var expected = SkillData.Schools(localGame.HeroSkills, role)[school].Codes;
                    var displayed = SkillsTable.Items.Cast<SkillRow>().ToArray();
                    Require(displayed.Select(row => row.Code).SequenceEqual(expected) &&
                        displayed.All(row => row.Icon == localGame.SkillArtwork.Icon(row.Code)),
                        $"Role {role}, school {school} shows the correct skills and original icons.");
                    if (role == 2 && school == 1)
                        await CaptureWhenReady(folder, "skills-tang.png");
                }
            }
        }
        finally
        {
            save.Draft = original;
            ShowSlot(playerKey);
            SchoolBox.SelectedIndex = firstSchool;
            UpdateLayout();
        }
    }

    private void VerifyTypeface()
    {
        Require(RenderedFamilies(FontFamily, "存档").Contains("Microsoft YaHei UI") &&
            RenderedFamilies(FontFamily, "ABCxyz0123456789").Contains("Segoe UI"),
            "Chinese and Latin text render in the intended default typefaces.");
        Require(ItemQuantity.FontFamily == FontFamily && SlotsBox.FontFamily == FontFamily,
            "Numeric inputs and save selectors inherit the interface typeface.");
    }

    private HashSet<string> RenderedFamilies(FontFamily font, string text)
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var sample = new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight, new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                16, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(sample, new Point());
        }

        static IEnumerable<GlyphRun> Glyphs(Drawing node) => node switch
        {
            GlyphRunDrawing glyph => [glyph.GlyphRun],
            DrawingGroup group => group.Children.SelectMany(Glyphs),
            _ => []
        };

        return Glyphs(drawing.Drawing).SelectMany(run => run.GlyphTypeface.FamilyNames.Values).ToHashSet();
    }

    private void ProbeSettingsDialog<T>(Button opener, Action<T> interact, Window? owner = null) where T : Window
    {
        Exception? failure = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            var dialog = (owner ?? this).OwnedWindows.OfType<T>().FirstOrDefault();
            try
            {
                Require(dialog is not null, $"{typeof(T).Name} opened.");
                interact(dialog!);
            }
            catch (Exception ex)
            {
                failure = ex;
                dialog?.Close();
            }
        });
        opener.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (failure is not null)
            throw failure;
    }

    private void VerifyCatalogPager()
    {
        var originalDraft = save!.Draft.DeepClone();
        var catalog = game!.Catalog;
        Require(CatalogSizeBox.SelectedValue is 0 && catalogPageSize ==
            Math.Max(1, (int)((CatalogTable.ActualHeight - CatalogTable.ColumnHeaderHeight - 1) / CatalogTable.RowHeight)),
            "Auto page size fits the viewport.");
        CatalogSizeBox.SelectedValue = 20;
        Require(catalogPageSize == 20 && CatalogPageCount == 23 && CatalogTable.Items.Count == 20, "Fixed page size.");
        Require(UserPreferencesFile.Read<CatalogViewPreferences>(catalogPreferencesPath)?.PageSize == 20, "Page size is persisted.");
        CatalogNext.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(catalogPage == 2 && Equals(CatalogTable.Items[0], catalogMatches[20]) &&
            Equals(CatalogTable.SelectedItem, CatalogTable.Items[0]), "Next page shows and selects the next batch.");
        CatalogPageBox.Text = "23";
        CatalogPageKeyDown(CatalogPageBox, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Require(catalogPage == 23 && CatalogTable.Items.Count == 10 && !CatalogNext.IsEnabled, "Jump to the partial last page.");
        CatalogPrevious.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(catalogPage == 22 && CatalogNext.IsEnabled, "Previous page re-enables next.");

        var column = CatalogTable.Columns[0];
        SortCatalog(CatalogTable, new DataGridSortingEventArgs(column));
        Require(catalogPage == 1 && CatalogTable.Items.Cast<CatalogItem>().SequenceEqual(
            catalog.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Take(20)), "Sort before paginating.");
        SortCatalog(CatalogTable, new DataGridSortingEventArgs(column));
        Require(CatalogTable.Items.Cast<CatalogItem>().SequenceEqual(
            catalog.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Take(20)), "Reverse global sort.");
        catalogSortColumn = null;
        SearchBox.Text = "wpsmd1";
        Require(catalogPage == 1 && CatalogTable.Items.Count == 1 && !CatalogPrevious.IsEnabled && !CatalogNext.IsEnabled,
            "A new search returns to the first page.");
        ClearSearch(this, new RoutedEventArgs());
        CatalogSizeBox.SelectedValue = 0;
        CatalogTable.SelectedIndex = 0;
        Require(JsonNode.DeepEquals(save.Draft, originalDraft), "Catalog navigation keeps the save unchanged.");
    }

    private void VerifyPetEditor(string folder)
    {
        VerifyPetEditingScopes(folder);
        VerifyPetRelease(folder);
    }

    private void VerifyFontSettings(string folder)
    {
        var app = (App)Application.Current;
        var preferences = app.Appearance;
        var originalFont = FontFamily;
        var originalDraft = save!.Draft.DeepClone();
        var chosen = new FontSelection("SimSun", "Consolas");

        void Open(Action<FontSettingsWindow> interact) => ProbePreferenceDialog(settings => settings.FontSettingsButton, interact);

        void Choose(FontSettingsWindow dialog)
        {
            dialog.ChineseBox.SelectedItem = FontPreferences.ChineseOptions.Single(option => option.Id == chosen.Chinese);
            dialog.LatinBox.SelectedItem = FontPreferences.LatinOptions.Single(option => option.Id == chosen.Latin);
            var previewFont = TextElement.GetFontFamily(dialog.FontPreview);
            Require(RenderedFamilies(previewFont, "装备").Contains("SimSun") &&
                RenderedFamilies(previewFont, "VIP125").Contains("Consolas"), "Independent live font previews.");
            Require(ReferenceEquals(FontFamily, originalFont), "Preview must not change the workbench.");
        }

        Open(dialog =>
        {
            Choose(dialog);
            Capture(folder, "font-settings.png", dialog);
            ClickDialogButton(dialog, "取消");
        });
        Require(preferences.Current == FontSelection.Default && !File.Exists(preferences.FilePath), "Cancel leaves preferences untouched.");

        Open(dialog =>
        {
            Choose(dialog);
            ClickDialogButton(dialog, "保存并应用");
        });
        Require(preferences.Current == chosen && new FontPreferences(preferences.FilePath).Current == chosen, "Fonts persist and reload.");
        Require(RenderedFamilies(FontFamily, "中文").Contains("SimSun") &&
            RenderedFamilies(ItemQuantity.FontFamily, "125").Contains("Consolas") &&
            RenderedFamilies(ItemCode.FontFamily, "tjbg").Contains("Consolas"), "The open workbench updates immediately.");
        Require(RawText.FontFamily.Source.StartsWith("Consolas"), "Raw JSON uses an editing typeface.");

        Open(dialog =>
        {
            Require((dialog.ChineseBox.SelectedItem as FontOption)?.Id == chosen.Chinese &&
                (dialog.LatinBox.SelectedItem as FontOption)?.Id == chosen.Latin, "Reopening restores the saved selections.");
            ClickDialogButton(dialog, "恢复默认");
            var previewFont = TextElement.GetFontFamily(dialog.FontPreview);
            Require(RenderedFamilies(previewFont, "中文").Contains("Microsoft YaHei UI") &&
                RenderedFamilies(previewFont, "VIP5").Contains("Segoe UI"), "Reset previews both defaults.");
            ClickDialogButton(dialog, "保存并应用");
        });
        Require(preferences.Current == FontSelection.Default && new FontPreferences(preferences.FilePath).Current == FontSelection.Default,
            "Defaults are saved.");
        Require(JsonNode.DeepEquals(originalDraft, save.Draft) && !Dirty, "Font preferences do not edit the save.");

        string invalid = Path.Combine(folder, "invalid-appearance.json");
        File.WriteAllText(invalid, "{");
        Require(new FontPreferences(invalid).Current == FontSelection.Default, "Invalid preferences fall back to defaults.");
        File.WriteAllText(invalid, """{"Chinese":"Unavailable font","Latin":"Consolas"}""");
        Require(new FontPreferences(invalid).Current == new FontSelection(FontSelection.Default.Chinese, "Consolas"),
            "Each unavailable font falls back independently.");
    }

    private void VerifySidebarSettings(string folder)
    {
        var originalLayout = sidebar.Current.ToArray();
        var originalDraft = save!.Draft.DeepClone();
        Require(sidebar.Defaults.Select(row => row.Name).SequenceEqual(new[]
            { "角色与物品", "图鉴", "角色", "技能", "宠物", "进阶工具", "会员", "药园", "增益" }),
            "Default navigation names and order match the requested sidebar.");
        var originalPage = active;
        void Open(Action<SidebarSettingsWindow> interact) => ProbePreferenceDialog(settings => settings.SidebarSettingsButton, interact);

        static void Rename(SidebarSettingsWindow dialog, string id, string name)
        {
            dialog.ColumnList.SelectedItem = dialog.ColumnList.Items.Cast<SidebarDraftRow>().Single(row => row.Id == id);
            dialog.NameBox.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
            dialog.NameBox.SetCurrentValue(TextBox.TextProperty, name);
            dialog.NameBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }

        void Arrange(SidebarSettingsWindow dialog)
        {
            Rename(dialog, NavGroupCore.Name, "角色图鉴");
            Rename(dialog, NavRole.Name, "人物档案");
            dialog.UpButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dialog.UpButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!dialog.UpButton.IsEnabled && dialog.DownButton.IsEnabled, "Move buttons respect the upper boundary.");
            dialog.DownButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(dialog.ColumnList.SelectedIndex == 1, "Move down retains the selected item.");
            dialog.UpButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var preview = dialog.SidebarPreview.Items.Cast<SidebarDraftRow>().ToArray();
            Require(preview[0].Id == NavRole.Name && preview[0].Name == "人物档案" &&
                preview[1].Name == "角色图鉴", "Live preview reflects order and names.");
            Require(Equals(NavRole.Content, "角色") && SidebarItems.Children[0] == NavGroupCore,
                "Preview does not change the workbench.");
        }

        Open(dialog =>
        {
            Arrange(dialog);
            Capture(folder, "sidebar-settings.png", dialog);
            dialog.Width = dialog.MinWidth;
            dialog.Height = dialog.MinHeight;
            Capture(folder, "sidebar-settings-compact.png", dialog, 144);
            Rename(dialog, NavRole.Name, " ");
            ClickDialogButton(dialog, "保存并应用");
            Require(dialog.SaveError.IsVisible && !File.Exists(sidebar.FilePath), "Blank names are rejected.");
            ClickDialogButton(dialog, "取消");
        });
        Require(sidebar.Current.SequenceEqual(originalLayout) && !File.Exists(sidebar.FilePath), "Cancel leaves layout untouched.");

        Open(dialog =>
        {
            Arrange(dialog);
            ClickDialogButton(dialog, "保存并应用");
        });
        Require(Equals(NavRole.Content, "人物档案") && NavGroupCore.Text == "角色图鉴" &&
            SidebarItems.Children[0] == NavRole && originalPage.IsChecked == true && active == originalPage,
            "Saved layout updates the sidebar and retains the active page.");
        Require(new SidebarPreferences(sidebar.FilePath, sidebar.Defaults).Current.SequenceEqual(sidebar.Current),
            "Layout reloads from preferences.");
        NavRole.RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));
        Require(active == NavRole && RolePage.Visibility == Visibility.Visible, "Renamed navigation still opens the role page.");

        Open(dialog =>
        {
            Require((dialog.ColumnList.Items[0] as SidebarDraftRow)?.Name == "人物档案", "Reopening restores saved names and order.");
            ClickDialogButton(dialog, "恢复默认");
            Require(Equals(NavRole.Content, "人物档案"), "Reset stays in preview until saved.");
            ClickDialogButton(dialog, "保存并应用");
        });
        Require(sidebar.Current.SequenceEqual(originalLayout) && SidebarItems.Children[0] == NavGroupCore &&
            active == NavRole && NavRole.IsChecked == true, "Reset restores the default layout and retains the active page.");
        string stalePath = Path.Combine(folder, "stale-navigation.json");
        File.WriteAllText(stalePath, """[{"Id":"NavBag","Name":"旧背包"},{"Id":"NavRole","Name":"我的角色"},{"Id":"NavRole","Name":"重复"},{"Id":"Unknown","Name":"未知"},null]""");
        var recovered = new SidebarPreferences(stalePath, sidebar.Defaults).Current;
        Require(recovered[0] == new SidebarEntry(NavRole.Name, "我的角色") && !recovered.Any(entry=>entry.Id=="NavBag") && recovered.Count == originalLayout.Length,
            "Incomplete layouts keep recognized entries and append defaults.");
        Require(UserPreferencesFile.Read<SidebarEntry[]>(stalePath)?.SequenceEqual(recovered)==true,
            "Retired navigation entries are removed while recognized names and order remain.");
        File.WriteAllText(stalePath, "[");
        Require(new SidebarPreferences(stalePath, sidebar.Defaults).Current.SequenceEqual(originalLayout),
            "Malformed layouts fall back to defaults.");
        SelectPage(originalPage);
        Require(JsonNode.DeepEquals(originalDraft, save.Draft) && !Dirty, "Sidebar settings do not edit the game save.");
    }

    private async Task VerifyCompactLayout(string folder)
    {
        double width = Width, height = Height;
        Width = MinWidth;
        Height = MinHeight;
        try
        {
            foreach (var (page, name, controls) in new (RadioButton Page, string Name, FrameworkElement[] Controls)[]
            {
                (NavCatalog, "catalog", [CategoryBox, QualityBox, CatalogRoleBox, SearchBox, CatalogClearButton, CatalogTable, CatalogPager, CatalogNext, GiveButton, ItemQuantity]),
                (NavRole, "role", [RoleProfileName, RolePortraitImage, BagFilter, BagTable, TuneButton, RemoveItemButton]),
                (NavPets, "pets", [PetTable, PetActions, PetTitle, PetExperiencePanel, ReleasePetButton]),
                (NavSkills, "skills", [SchoolBox, SkillsTable, SkillLevelBox, SchoolLevelBox, PassiveList, ApplyPassiveButton, SkillSoulBox]),
                (NavGarden, "garden", [GardenTable, GardenExpButton, TempleLevelText, TempleEditButton]),
                (NavBuff, "buffs", [BuffTable, BuffPresetButton, BuffDurationBox]),
                (NavVip, "vip", [VipBalance, VipAll, VipLevelBox, VipRechargeButton, VipClaimButton]),
            })
            {
                Require(SelectPage(page), $"Open compact {name}.");
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var root = (FrameworkElement)Content;
                Capture(folder, $"{name}-compact-125.png", dpi: 120);
                if (page == NavCatalog)
                    Require(CatalogTable.Columns.Sum(column => column.ActualWidth) >= CatalogTable.ActualWidth - 24,
                        "Catalog columns fill the compact grid after automatic repagination.");
                if(page==NavRole)
                    Require(compactRoleLayout==true && Grid.GetRow(BagDetailCard)==2 && BagTable.ActualHeight>55,
                        "The merged role workspace keeps inventory and actions usable at minimum size.");
                Require(SidebarScroll.ScrollableHeight < 1,
                    $"All navigation items fit at the minimum window height. Scroll overflow: {SidebarScroll.ScrollableHeight:F1} DIP.");
                foreach (FrameworkElement control in controls.Concat([SlotsBox, PlayersBox, SaveButton, DirtyBadge, SettingsButton, AdvancedToolsButton]))
                {
                    var bounds = control.TransformToAncestor(root)
                        .TransformBounds(new Rect(control.RenderSize));
                    Require(control.IsVisible && bounds.Width > 0 && bounds.Height > 0 && bounds.Left >= 0 && bounds.Top >= 0 &&
                        bounds.Right <= root.ActualWidth && bounds.Bottom <= root.ActualHeight, $"Visible compact control: {control.Name}");
                }
                if (page == NavPets)
                {
                    Require(PetMetricsScroll.ViewportHeight >= 20 && !PetMetricsScroll.IsAncestorOf(PetSelector),
                        "Only pet combat metrics scroll; portrait and selector stay outside that viewport.");
                    var before = PetExperiencePanel.TranslatePoint(new Point(), PetLeftCard);
                    PetMetricsScroll.ScrollToEnd();
                    UpdateLayout();
                    Require(PetExperiencePanel.TranslatePoint(new Point(), PetLeftCard) == before,
                        "Pet experience remains pinned while combat metrics scroll.");
                    PetMetricsScroll.ScrollToHome();
                }
                Capture(folder, $"{name}-compact-150.png", dpi: 144);
            }
            Require(OpenRawPage(), "Open raw JSON through advanced tools at minimum size.");
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(RawText.IsVisible && ReturnFromRawButton.IsVisible && !SidebarItems.Children.OfType<RadioButton>().Any(b=>b.IsChecked==true),
                "Advanced mode is distinct from common navigation and provides a return action.");
            Capture(folder,"raw-compact-125.png",dpi:120);Capture(folder,"raw-compact-150.png",dpi:144);
        }
        finally
        {
            Width = width;
            Height = height;
            SelectPage(NavCatalog);
            UpdateLayout();
        }
    }

    private static void RequireCentered(FrameworkElement owner, FrameworkElement presenter, string label)
    {
        Require(presenter.ActualWidth > 0 && presenter.ActualHeight > 0, $"{label}: label has no rendered size.");
        Point center = presenter.TranslatePoint(new Point(presenter.ActualWidth / 2, presenter.ActualHeight / 2), owner);
        Require(Math.Abs(center.X - owner.ActualWidth / 2) <= 1.5 &&
            Math.Abs(center.Y - owner.ActualHeight / 2) <= 1.5, $"{label}: label center differs from the control center.");
    }

    private async Task VerifyCombo(ComboBox combo, string label)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var selected = (FrameworkElement)combo.Template.FindName("SelectedItemPresenter", combo);
        RequireCentered(combo, selected, label + " selected item");
        try
        {
            combo.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var options = combo.Items.Cast<object>().Select(option => combo.ItemContainerGenerator.ContainerFromItem(option))
                .OfType<ComboBoxItem>().Where(item => item.ActualWidth > 0).ToArray();
            Require(options.Length > 0, $"{label}: popup did not display options.");
            foreach (ComboBoxItem item in options)
                RequireCentered(item, (FrameworkElement)item.Template.FindName("ChoicePresenter", item), label + " popup item");
        }
        finally { combo.IsDropDownOpen = false; }
    }

    private void VerifyDialogCombo()
    {
        var dialog = new EditorDialog(this, "下拉框验证", "");
        ComboBox choice = dialog.Choices("人物", ["孙悟空", "唐僧"], 0);
        Exception? failure = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async () =>
        {
            var child = OwnedWindows.OfType<Window>().Single(window => window.Title == "下拉框验证");
            try { await VerifyCombo(choice, "dialog"); }
            catch (Exception ex) { failure = ex; }
            finally { child.DialogResult = false; }
        });
        dialog.Show(() => { });
        if (failure is not null)
            throw failure;
    }

    private void VerifyHelp(string folder)
    {
        var disconnected = new HelpWindow(null);
        Require(disconnected.ManualText.Contains("选择游戏目录"), "Help without a connected game folder.");
        disconnected.Close();

        string legacyDirectory = Path.Combine(folder, "legacy-help");
        Directory.CreateDirectory(legacyDirectory);
        File.WriteAllText(Path.Combine(legacyDirectory, "使用说明.txt"), "旧版安装包的本地说明");
        var legacy = new HelpWindow(legacyDirectory);
        Require(legacy.ManualText == "旧版安装包的本地说明", "Older packages still support plain-text manuals.");
        legacy.Close();

        Exception? failure = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            HelpWindow? help = null;
            try
            {
                help = OwnedWindows.OfType<HelpWindow>().Single();
                Require(help.HelpTabs.SelectedIndex == 0, "F1 opens the built-in guide first.");
                Capture(folder, "help.png", help);
                help.HelpTabs.SelectedIndex = 1;
                Require(help.OriginalManual.IsReadOnly, "The original manual is read-only.");
                string manual = File.ReadAllText(Path.Combine(game!.Root, "使用说明.md"));
                Require(help.ManualText == manual && help.ManualSourceBox.Items.Count == 5,
                    "Markdown documents come from the active game folder.");
                string rendered = new TextRange(help.OriginalManual.Document.ContentStart,
                    help.OriginalManual.Document.ContentEnd).Text;
                Require(rendered.Contains("第一次使用") && !rendered.Contains("# 造梦西游") &&
                    help.OriginalManual.Document.Blocks.FirstBlock is Paragraph { FontSize: 24 },
                    "Markdown headings are rendered as styled, readable document blocks.");
                Capture(folder, "help-original.png", help);
                Require(help.OriginalManual.IsVisible, "The original manual is visible on its own tab.");
                Hyperlink firstLink = help.OriginalManual.Document.Blocks.OfType<Paragraph>()
                    .SelectMany(block => block.Inlines.OfType<Hyperlink>())
                    .First(link=>new TextRange(link.ContentStart,link.ContentEnd).Text=="修改器使用手册");
                firstLink.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Require(help.ManualSourceBox.SelectedIndex == 1 && help.ManualText==File.ReadAllText(Path.Combine(game.Root,"资料","修改器使用手册.md")),
                    "Relative Markdown links open the matching local guide without launching a browser.");
                help.ManualSourceBox.SelectedIndex = 2;
                Require(help.ManualText.Contains(".gm-backups"), "Document selector loads the backup guide.");
                Capture(folder, "help-backup.png", help);
                Require(File.ReadAllText(Path.Combine(game.Root, "使用说明.md")) == manual,
                    "Reading and switching documents never rewrite the manual.");
            }
            catch (Exception ex) { failure = ex; }
            finally { help?.Close(); }
        });
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.F1)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        RaiseEvent(key);
        Require(key.Handled, "The F1 shortcut was handled.");
        if (failure is not null)
            throw failure;
        Require(!Dirty, "Reading help must leave the save clean.");
    }

    private void VerifyPrompts(string folder)
    {
        void Inspect(Action show, Action<PromptDialog> inspect, string button)
        {
            Exception? failure = null;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                PromptDialog? prompt = null;
                try
                {
                    prompt = OwnedWindows.OfType<PromptDialog>().Single();
                    inspect(prompt);
                    ClickDialogButton(prompt, button);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    prompt?.Close();
                }
            });
            show();
            if (failure is not null)
                throw failure;
        }

        Require(SelectPage(NavRole), "Role page for max-level confirmation.");
        var before = save!.Draft.DeepClone();
        BagFilter.SelectedIndex=0;ShowBag();BagTable.SelectedIndex=0;
        var removedRow=(InventoryRow)BagTable.SelectedItem;
        void Remove() => RemoveItem(this, new RoutedEventArgs());
        Inspect(Remove, prompt =>
        {
            Require(prompt.Title == "移除物品" && prompt.MessageBody.Text.Contains(removedRow.Name), "Confirmation content.");
            Require(prompt.CancelButton.IsCancel && prompt.CancelButton.IsDefault &&
                System.Windows.Input.FocusManager.GetFocusedElement(prompt) == prompt.CancelButton &&
                !prompt.AcceptButton.IsDefault, "Cancellation receives initial keyboard focus.");
            Capture(folder, "prompt.png", prompt);
        }, "取消");
        Require(JsonNode.DeepEquals(save.Draft, before), "Cancelling a confirmation leaves the draft intact.");
        Inspect(Remove, _ => { }, "移除物品");
        Require(save.Dirty && Inventory.List(Character!,game!).Count()==Inventory.List((JsonObject)before[player!.Key]!,game!).Count()-1,
            "Confirming removes the selected item from the pending save.");
        Inspect(Close, prompt =>
        {
            Require(prompt.Title == "放弃修改", "Closing with an unsaved draft asks for confirmation.");
            Capture(folder, "prompt-discard.png", prompt);
        }, "×");
        Require(IsVisible && save.Dirty, "Dismissing the discard prompt preserves the window and draft.");

        const string error = "文件已保存，但推送没有完成。无法连接这份游戏的本地服务。";
        Inspect(() => ShowError(error), prompt =>
        {
            Require(prompt.MessageBody.Text == error && prompt.MessageBody.IsReadOnly &&
                prompt.CancelButton.Visibility == Visibility.Collapsed, "Error shows selectable details and one action.");
            Capture(folder, "prompt-error.png", prompt);
        }, "知道了");
        save.Draft = (JsonObject)before;
        ShowSlot(player?.Key);
    }

    private void VerifyPetGuide(string folder)
    {
        var before = save!.Draft.DeepClone();
        Exception? failure = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            PetGuideWindow? dialog = null;
            try
            {
                dialog = OwnedWindows.OfType<PetGuideWindow>().Single();
                var families = dialog.FamilyBox.Items.Cast<PetGuideFamily>().ToArray();
                var forms = families.SelectMany(family => family.Forms).ToArray();
                Require(families.Length == 13 && forms.Length == 42 &&
                    forms.SelectMany(form => form.SaveCodes).ToHashSet().SetEquals(game!.PetNames.Select(pair => pair.Key)),
                    "The guide shows 42 named forms and retains all 52 save codes.");
                foreach (string familyCode in new[] { "mouse", "neat", "roomhorse", "terribletiger" })
                {
                    var family = families.Single(entry => entry.Code == familyCode);
                    Require(family.Forms.Select(form => form.Number).SequenceEqual(new[] { 1, 2 }) &&
                        family.Forms[0].SaveCodes.Length == 3 && family.Forms[1].IncludesStage(4),
                        $"Shared save stages display as two named forms for {familyCode}.");
                }
                Require(families.Single(family => family.Code == "nian").Forms.Select(form => form.Entry.Name)
                    .SequenceEqual(new[] { "小年兽", "年兽", "大年兽" }), "The year beast shows three named forms.");
                var catalog = new HashSet<string>(StringComparer.Ordinal);
                foreach (var family in families)
                {
                    dialog.FamilyBox.SelectedItem = family;
                    foreach (var form in family.Forms)
                    {
                        dialog.FormBox.SelectedItem = form;
                        var tabs = dialog.SkillTabs.Items.Cast<PetGuideSection>().ToArray();
                        Require(tabs.Length == 3 && tabs[2].Rows.Length == 11, $"All categories and basic skills for {form.Code}.");
                        var visibleSkills = tabs.SelectMany(tab => tab.Rows).Where(row => !row.IsFuture)
                            .Select(row => row.Art.Code).ToHashSet();
                        foreach (string saveCode in form.SaveCodes)
                        {
                            var rules = new PetRules(game!, saveCode);
                            Require(visibleSkills.SetEquals(rules.SkillDefinitions().Where(rules.MatchesForm).Select(skill => skill.Code)),
                                $"Named form agrees with the skill rules for {saveCode}.");
                        }
                        catalog.UnionWith(tabs.SelectMany(tab => tab.Rows.Select(row => row.Art.Code)));
                    }
                }
                Require(catalog.SetEquals(((JsonObject)game!.Official["petSkillNames"]!).Select(pair => pair.Key)),
                    "The offline guide includes every locally named skill.");
                dialog.FamilyBox.SelectedValue = "mouse";
                var mouseSkills = (PetGuideSection)dialog.SkillTabs.Items[0];
                var boomerang = mouseSkills.Rows.Single(row => row.Art.Code == "hxfb");
                Require(boomerang.IsFuture && boomerang.Condition.Contains("第 2 形态（子鼠大元帅）") &&
                    dialog.GuideForm.Text == "第 1 形态 / 共 2 种", "The boomerang uses the visible form number.");
                dialog.SkillTabs.SelectedIndex = 0;
                Capture(folder, "pet-guide.png", dialog);
                VerifyRenderedSkillHint(dialog.SkillTabs,"sc",folder,"pet-guide-tooltip.png");
                dialog.FormBox.SelectedValue = "mouse4";
                Require(!((PetGuideSection)dialog.SkillTabs.Items[0]).Rows.Single(row => row.Art.Code == "hxfb").IsFuture,
                    "Evolution changes the boomerang status.");
                dialog.SkillTabs.SelectedIndex = 1;
                Require(((PetGuideSection)dialog.SkillTabs.SelectedItem).Rows.Single().Condition.Contains("子鼠精灵"),
                    "Spirit-linked skill names its required item.");
                Capture(folder, "pet-guide-spirit.png", dialog);
                Require(((PetGuideSection)dialog.SkillTabs.SelectedItem).Rows.Single().Status=="穿戴精灵直接获得",
                    "The zodiac guide labels equipment-granted ultimates explicitly.");
                VerifyRenderedSkillHint(dialog.SkillTabs,"zsaoyi",folder,"zodiac-ultimate-tooltip.png");
                dialog.FamilyBox.SelectedValue = "monkey";
                Require(dialog.GuideSource.Text.Contains("蟠桃园"), "The guide retains acquisition information.");
                var natural = ((PetGuideSection)dialog.SkillTabs.SelectedItem).Rows.Single();
                Require(natural.IsFuture && natural.Condition.Contains("烈焰金刚") && natural.Condition.Contains("100%") &&
                    natural.Condition.Contains("无需精灵"), "Natural esoteric skill shows its form and cultivation requirement.");
                Require(natural.Status=="进化后领悟","The natural guide labels evolution-learned ultimates explicitly.");
                Capture(folder,"pet-guide-evolution.png",dialog);
                VerifyRenderedSkillHint(dialog.SkillTabs,"jgaoyi",folder,"evolution-ultimate-tooltip.png");
                dialog.FamilyBox.SelectedValue = "ufo";
                Require(((PetGuideSection)dialog.SkillTabs.SelectedItem).IsEmpty, "Families without an esoteric skill show an empty category.");
                ClickDialogButton(dialog, "关闭");
            }
            catch (Exception ex)
            {
                failure = ex;
                dialog?.Close();
            }
        });
        OpenPetGuide(this, new RoutedEventArgs());
        if (failure is not null)
            throw failure;
        Require(JsonNode.DeepEquals(save.Draft, before), "Viewing the pet guide never edits the save.");
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject node)
    {
        yield return node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var item in VisualDescendants(VisualTreeHelper.GetChild(node, i)))
                yield return item;
    }

    private static void ClickDialogButton(DependencyObject root, string label)
    {
        var button = VisualDescendants(root).OfType<Button>().Single(control => Equals(control.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private void Capture(string folder, string name, Window? target = null, double dpi = 96)
    {
        target ??= this;
        var content = (FrameworkElement)target.Content;
        target.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi / 96),
            (int)Math.Ceiling(content.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name));
        encoder.Save(stream);
    }

    private sealed class TestService : HttpMessageHandler
    {
        private readonly string saves;
        public string ReportedDirectory { get; set; }
        public List<string> Sent { get; } = [];

        internal TestService(string saves)
        {
            this.saves = saves;
            ReportedDirectory = saves;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string route = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && route == "/ping")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(new JsonObject { ["who"] = "zm3", ["saves"] = ReportedDirectory }.ToJsonString())
                };
            string filename = route switch { "/vip" => "vip.json", _ => "slot" + route.Split('=').Last() + ".json" };
            byte[] data = await request.Content!.ReadAsByteArrayAsync(token);
            Require(data.SequenceEqual(await File.ReadAllBytesAsync(Path.Combine(saves, filename), token)), "POST matches persisted data.");
            Sent.Add(route);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
#endif
