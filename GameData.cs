using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal static class GameJson
{
    internal static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static JsonObject Read(string file) => Parse(File.ReadAllText(file));
    internal static JsonObject Parse(string text) =>
        JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("JSON 的根节点应为对象。");

    internal static string Text(JsonNode? node) => node?.ToString() ?? "";
    internal static int Number(JsonNode? node, int fallback = 0) =>
        int.TryParse(Text(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : fallback;
    internal static string Role(int role) => role switch
    {
        1 => "孙悟空", 2 => "唐僧", 3 => "猪八戒", 4 => "沙僧", _ => "未知角色"
    };
}

internal sealed class EditableFile
{
    private byte[]? originalBytes;
    private JsonObject original;
    internal string Path { get; }
    internal JsonObject Draft { get; set; }
    internal bool Dirty => !JsonNode.DeepEquals(Draft, original);
    internal int PetLimitRepairs { get; private set; }

    internal EditableFile(string path, JsonObject? fallback = null, bool repairPets = false)
    {
        Path = path;
        originalBytes = StorageTransaction.ReadForEdit(path);
        original = originalBytes is not null ? ReadBytes(originalBytes) :
            fallback ?? throw new FileNotFoundException("存档文件已经移动或删除。", path);
        Draft = (JsonObject)original.DeepClone();
        if (repairPets) RepairPetLimits();
    }

    internal int RepairPetLimits()
    {
        int count = PetData.RepairPotentialLimits(Draft);
        PetLimitRepairs += count;
        return count;
    }

    private static JsonObject ReadBytes(byte[] bytes) =>
        GameJson.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));

    internal void CheckCurrent()
    {
        byte[]? disk = File.Exists(Path) ? File.ReadAllBytes(Path) : null;
        if (disk is null ? originalBytes is not null : originalBytes is null || !disk.AsSpan().SequenceEqual(originalBytes))
            throw new InvalidOperationException("磁盘文件已被游戏或其他编辑器更新。请先刷新存档，再重新编辑。");
    }

    internal byte[] PersistedBytes()
    {
        CheckCurrent();
        return originalBytes ?? throw new InvalidOperationException("请先保存。");
    }

    internal string? Save()
    {
        string root=StorageTransaction.RootFor(Path)??throw new InvalidDataException("存档不在游戏目录中。");
        using var held=StorageTransaction.Acquire(root);StorageTransaction.RecoverLocked(root);
        var write=PrepareWrite(root);
        var backups=StorageTransaction.CommitLocked(root,[write],"modifier",Guid.NewGuid().ToString("N"));
        AcceptPersisted(write.After);return backups.GetValueOrDefault(write.Relative);
    }

    internal StorageWrite PrepareWrite(string root)
    {
        CheckCurrent();RepairPetLimits();
        byte[] data=!Dirty&&originalBytes is not null?originalBytes:System.Text.Encoding.UTF8.GetBytes(Draft.ToJsonString(GameJson.Format));
        return new(System.IO.Path.GetRelativePath(root,Path).Replace('\\','/'),originalBytes,data);
    }

    internal void AcceptPersisted(byte[] data)
    {
        originalBytes=data;
        original = (JsonObject)Draft.DeepClone();
        PetLimitRepairs = 0;
    }
}

internal sealed record Slot(int Number, string File, string Label)
{
    public override string ToString() => Label;
}

internal sealed record Player(string Key, string Label)
{
    public override string ToString() => Label;
}

public sealed record CatalogItem(
    string Code, string Name, string Type, string TypeName, string Quality,
    string User, string Description, string Bag, int ShowId, bool PetEquipment, string? IconPath)
{
    public string Destination => Inventory.Labels[Bag];
    public string Initial => Name.Length > 0 ? Name[..1] : "物";
    public string Group => PetEquipment ? "宠物装备" : Type switch
    {
        "zbwq" or "zbfj" => "武器防具",
        "zbsp" or "zbtx" => "饰品头衔",
        "zbfb" => "法宝",
        "zbsz" or "zbcb" => "时装",
        _ when TypeName is "制作书" or "技能书" => "制作与技能",
        _ when TypeName is "材料" or "宝石" or "强化石" => "强化与材料",
        _ => "其他道具"
    };
    private readonly Lazy<BitmapImage?> icon = new(() => GameFolder.ReadImage(IconPath));
    public BitmapImage? Icon => icon.Value;
}

public sealed record InventoryRow(string Field, int Index, string Code, string Name, string Category, string Quantity, CatalogItem? Item)
{
    public BitmapImage? Icon => Item?.Icon;
    public string Initial => Name.Length > 0 ? Name[..1] : "物";
}

public sealed record PetRow(int Index, string Code, string Name, string Level, string Status, BitmapImage? Icon)
{
    public string Initial => Name.Length > 0 ? Name[..1] : "宠";
}

internal sealed record PetEntry(string Code, string Name, BitmapImage? Icon, string Description = "")
{
    public string Initial => Name.Length > 0 ? Name[..1] : "宠";
    public string Caption => Hint?.Acquisition is { Length: > 0 } acquisition ? acquisition : Code;
    public SkillHint? Hint { get; init; }

    internal static PetEntry Skill(GameFolder game, string code)
    {
        string name = GameJson.Text((game.Official["petSkillNames"] as JsonObject)?[code]);
        string label = name.Length > 0 ? name : code;
        return new PetEntry(code, label, game.Portrait("petSkills", code), SkillHelp.PetDescription(game,code))
            { Hint=SkillHelp.Pet(game,code,label) };
    }

    internal static PetEntry Equipment(GameFolder game, string raw)
    {
        string code = raw.Split('|', 2)[0];
        var item = game.CatalogByCode.GetValueOrDefault(code);
        return new PetEntry(code, item?.Name ?? code, item?.Icon ?? game.Portrait("items", code));
    }
}

internal sealed partial class GameFolder
{
    internal static BitmapImage? ReadImage(string? path)
    {
        if (path is null)
            return null;
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException
            or ArgumentException or FileFormatException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static readonly Regex SlotName = new(@"^slot([0-9]+)\.json$", RegexOptions.IgnoreCase);
    private static readonly HttpClient LocalHttp = new(new HttpClientHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false
    }) { BaseAddress = new Uri("http://127.0.0.1:43999/"), Timeout = TimeSpan.FromSeconds(3) };
    private readonly HttpClient http;
    private readonly JsonObject icons;
    private readonly Dictionary<(string Group, string Code), BitmapImage?> portraitCache = new();

    internal string Root { get; }
    internal RoleArtwork RoleArtwork { get; }
    internal SkillArtwork SkillArtwork { get; }
    internal string Saves => Path.Combine(Root, "saves");
    internal IReadOnlyList<CatalogItem> Catalog { get; }
    internal IReadOnlyDictionary<string, CatalogItem> CatalogByCode { get; }
    internal JsonObject EquipmentReference { get; }
    internal JsonObject PetEquipmentReference { get; }
    internal JsonObject EquipmentStrength { get; }
    internal JsonObject PetEquipmentStrength { get; }
    internal JsonObject PetNames { get; }
    internal JsonObject PetSkillDescriptions { get; }
    internal JsonArray HeroSkills { get; }
    internal JsonObject Official { get; }
    internal JsonObject Extreme { get; }
    internal JsonArray SpecialPets { get; }
    internal JsonObject FieldNames { get; }

    internal static bool IsGameFolder(string path) =>
        File.Exists(Path.Combine(path, "game", "launcher.swf")) &&
        File.Exists(Path.Combine(path, "schema", "library_final.json")) &&
        Directory.Exists(Path.Combine(path, "saves"));

    internal GameFolder(string path, HttpClient? service = null)
    {
        http = service ?? LocalHttp;
        Root = Path.GetFullPath(path);
        RoleArtwork = new RoleArtwork(Root);
        SkillArtwork = new SkillArtwork(Root);
        if (!IsGameFolder(Root))
            throw new InvalidDataException("请选择同时包含 game、saves 和 schema 的单机版目录。");
        GameRules.ValidateDirectory(Root);
        var metadata = GameRules.Section("catalog");
        icons = ReadOptional(Path.Combine(Root,"schema"), "zmxy3_icons.json");
        var references=GameRules.Section("references");
        EquipmentReference=(JsonObject)references["equip"]!;PetEquipmentReference=(JsonObject)references["petequip"]!;
        EquipmentStrength=GameRules.Section("equipmentStrength");PetEquipmentStrength=GameRules.Section("petEquipmentStrength");
        Official=GameRules.Section("official");PetNames=(JsonObject)Official["petNames"]!;HeroSkills=(JsonArray)Official["heroSkills"]!;
        Extreme=GameRules.Section("extreme");PetSkillDescriptions=GameRules.Section("petSkillDescriptions");
        SpecialPets=(JsonArray)GameRules.Node("specialPets");FieldNames=GameRules.Section("fieldNames");
        Catalog = (metadata["items"] as JsonArray ?? throw new InvalidDataException("物品参考表缺少 items。"))
            .OfType<JsonObject>()
            .Where(row => !string.IsNullOrWhiteSpace(GameJson.Text(row["fill"])))
            .Select(ToItem)
            .DistinctBy(item => item.Code)
            .OrderBy(item => item.Bag != Inventory.Consumables)
            .ToArray();
        CatalogByCode = Catalog.ToDictionary(item => item.Code);
    }

    private static JsonObject ReadOptional(string directory, string file)
    {
        string path = Path.Combine(directory, file);
        return File.Exists(path) ? GameJson.Read(path) : new();
    }

    private string? IconPath(string group, string code)
    {
        string mapped = GameJson.Text((icons[group] as JsonObject)?[code]);
        string file = Path.GetFileName(mapped);
        string extension = Path.GetExtension(file).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg"
            ? Path.Combine(Root, "icons", group, file)
            : null;
    }

    internal BitmapImage? Portrait(string group, string code)
    {
        var key = (group, code);
        if (!portraitCache.TryGetValue(key, out var image))
        {
            image = (group == "pets" ? PetArtwork.Nian(code) : null) ?? ReadImage(IconPath(group, code));
            portraitCache.Add(key, image);
        }
        return image;
    }

    private CatalogItem ToItem(JsonObject row)
    {
        string code = GameJson.Text(row["fill"]);
        string type = GameJson.Text(row["type"]);
        bool pet = GameJson.Text(row["class"]) == "PetEquipObj" || GameJson.Text(row["group"]) == "宠物装备";
        string bag = pet ? Inventory.PetGear : GameJson.Text(row["bag"]) switch
        {
            "dj" => Inventory.Consumables,
            "sz" => Inventory.Fashion,
            "jns" => Inventory.Skills,
            _ => Inventory.Gear
        };
        return new CatalogItem(code, GameJson.Text(row["name"]), type,
            GameJson.Text(row["typeCn"]), GameJson.Text(row["quality"]), GameJson.Text(row["user"]),
            GameJson.Text(row["inst"]), bag, GameJson.Number(row["showid"], 1), pet,
            IconPath("items", code));
    }

    internal IReadOnlyList<Slot> Slots()
    {
        var slots = new List<Slot>();
        foreach (string file in Directory.EnumerateFiles(Saves, "slot*.json"))
        {
            Match match = SlotName.Match(Path.GetFileName(file));
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int number))
                continue;
            string label = $"存档 {number}";
            try
            {
                JsonObject data = GameJson.Read(file);
                if (data["player1_obj"] is JsonObject player)
                    label += $" · {GameJson.Role(GameJson.Number(player["roleid"]))}";
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                label += " · 读取异常";
            }
            slots.Add(new Slot(number, file, label));
        }
        return slots.OrderBy(slot => slot.Number).ToArray();
    }

    internal static IReadOnlyList<Player> Players(JsonObject save) => save
        .Where(row => Regex.IsMatch(row.Key, @"^player[1-3]_obj$") && row.Value is JsonObject)
        .OrderBy(row => row.Key)
        .Select(row => new Player(row.Key, $"角色 {row.Key[6]} · {GameJson.Role(GameJson.Number(row.Value!["roleid"]))}"))
        .ToArray();

    internal EditableFile OpenVip() => new(Path.Combine(Saves, "vip.json"), new JsonObject
    {
        ["allMoney"] = 0, ["lastMoney"] = 0, ["paiedMoney"] = 0,
        ["vipsavelist"] = new JsonArray(false, 0, false, 0, false, 0, 0, 0)
    });

    internal async Task CheckServerAsync(string? requestId=null)
    {
        using var request=ServiceRequest(HttpMethod.Get,"ping",requestId);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var status = GameJson.Parse(await response.Content.ReadAsStringAsync());
        string remote = GameJson.Text(status["saves"]);
        if (GameJson.Text(status["who"]) != "zm3" || string.IsNullOrWhiteSpace(remote) ||
            !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(remote)),
                Path.TrimEndingDirectorySeparator(Saves), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("本地服务连接的是另一个游戏目录，已停止推送。");
    }

    internal async Task PushAsync(string endpoint, EditableFile file)
    {
        using var content = new ByteArrayContent(file.PersistedBytes());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var response = await http.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        file.CheckCurrent();
    }
}

internal static class Inventory
{
    internal const string Gear = "bagSaveString";
    internal const string Worn = "curSaveString";
    internal const string Consumables = "bagdjSaveString";
    internal const string Fashion = "bagszSaveString";
    internal const string Skills = "bagjnsSaveString";
    internal const string PetGear = "petEquipSaveString";
    internal const string Pets = "petSave";
    internal static readonly Dictionary<string, string> Labels = new()
    {
        [Gear] = "装备背包", [Consumables] = "道具栏", [Fashion] = "时装栏",
        [Skills] = "技能书栏", [PetGear] = "宠物装备", [Worn] = "当前穿戴"
    };
    private static readonly (int Position, string Name)[] HeroStats =
    [
        (7, "ehp"), (8, "emp"), (9, "eatt"), (10, "edef"), (11, "emiss"),
        (12, "ecrit"), (13, "eahp"), (14, "eamp"), (21, "eupdata"), (24, "magicdef")
    ];
    private static readonly string[] PetStats =
        ["ehp", "emp", "eatt", "edef", "emiss", "ecrit", "eahp", "eamp", "magicdef", "speed", "", "eatblood"];

    internal static List<string> Records(JsonObject player, string field) =>
        GameJson.Text(player[field]).Split('}', StringSplitOptions.RemoveEmptyEntries).ToList();

    internal static void Add(JsonObject player, CatalogItem item, int count, bool reference, GameFolder game, bool maxed = false)
    {
        if (count < 1 || count > (item.PetEquipment ? GameRules.Int("inventory","petGearBatch") : GameRules.Int("inventory","stack")))
            throw new InvalidDataException(item.PetEquipment ? "宠物装备数量应为 1–50。" : "数量应为 1–9999。");
        var records = Records(player, item.Bag);
        if (item.PetEquipment)
        {
            string[] pet = Enumerable.Repeat("0", 13).ToArray();
            pet[0] = item.Code;
            if (reference && game.PetEquipmentReference[item.Code] is JsonObject stats)
                for (int i = 0; i < PetStats.Length; i++)
                    if (PetStats[i].Length > 0 && GameJson.Text(stats[PetStats[i]]) is { Length: > 0 } value)
                        pet[i + 1] = value;
            if (maxed)
                EquipmentData.Maximize(game, pet, true);
            records.AddRange(Enumerable.Repeat(string.Join('|', pet), count));
        }
        else
        {
            bool fashion = item.Type is "zbsz" or "zbcb";
            string[] fields = Enumerable.Repeat("0", fashion ? 29 : 28).ToArray();
            fields[0] = item.ShowId.ToString(CultureInfo.InvariantCulture);
            fields[1] = fields[5] = fields[6] = "";
            fields[2] = item.Code;
            fields[3] = item.Type;
            for (int i = 15; i <= 19; i++)
                fields[i] = "false";
            fields[20] = "1";
            fields[22] = count.ToString(CultureInfo.InvariantCulture);
            if (fashion)
                fields[27] = "never";
            if (reference && game.EquipmentReference[item.Code] is JsonObject stats)
                foreach (var (position, name) in HeroStats)
                    if (GameJson.Text(stats[name]) is { Length: > 0 } value)
                        fields[position] = value;
            if (maxed && EquipmentData.Wearable(item.Type))
                EquipmentData.Maximize(game, fields, false);
            records.Add(string.Join('|', fields));
        }
        player[item.Bag] = string.Join('}', records);
    }

    internal static IEnumerable<InventoryRow> List(JsonObject player, GameFolder game)
    {
        foreach (var (field, category) in Labels)
        {
            var records = Records(player, field);
            for (int index = 0; index < records.Count; index++)
            {
                string[] parts = records[index].Split('|');
                string code = field == PetGear ? parts[0] : parts.ElementAtOrDefault(2) ?? parts[0];
                string quantity = field == PetGear || parts.Length <= 22 || parts[22].Length == 0 ? "1" : parts[22];
                var item = game.CatalogByCode.GetValueOrDefault(code);
                yield return new InventoryRow(field, index, code, item?.Name ?? code, category, quantity, item);
            }
        }
    }

    internal static IEnumerable<PetRow> ListPets(JsonObject player, GameFolder game)
    {
        var records = Records(player, Pets);
        for (int index = 0; index < records.Count; index++)
        {
            string[] parts = records[index].Split('|', 20);
            if (parts.Length < 19)
                continue;
            string name = GameJson.Text(game.PetNames[parts[0]]);
            yield return new PetRow(index, parts[0], name.Length > 0 ? name : parts[0], parts[5],
                parts[18] == "1" ? "出战" : "休息", game.Portrait("pets", parts[0]));
        }
    }

    internal static void ChangePet(JsonObject player, PetRow selected, bool fight)
    {
        var records = Records(player, Pets);
        for (int i = 0; i < records.Count; i++)
        {
            if (i != selected.Index && !fight)
                continue;
            string[] parts = records[i].Split('|', 20);
            if (parts.Length < 19)
                continue;
            parts[18] = i == selected.Index && fight ? "1" : "0";
            records[i] = string.Join('|', parts);
        }
        player[Pets] = string.Join('}', records);
    }
}
