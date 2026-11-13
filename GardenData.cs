using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed record GardenPlant(int Type, string Name, string Artwork, string Description)
{
    public BitmapImage? Icon => UtilityArtwork.Image(Artwork);
    public override string ToString() => Name;
}
internal sealed record GardenTile(int Id, GardenRow? Row, bool Unlocked)
{
    public string Title => $"地块 {Id}";
    public string Name => Row is null ? Unlocked ? "空地" : "未开垦" : GardenData.Plant(Row.Type).Name;
    public string State => Row?.Status ?? (Unlocked ? "可种植" : "未解锁");
    public string Grade => Row?.Type is 2 or 3 or 5 ? GardenData.Grade(Row.Level) : Unlocked ? "选择后种植" : $"药园 {Id} 级开启";
    public BitmapImage? Soil => UtilityArtwork.Image(Row?.Type == 4 ? "garden-used.png" : Unlocked || Row is not null ? "garden-soil.png" : "garden-locked.png");
    public BitmapImage? Crop => Row?.Type is 2 or 3 or 5 ? GardenData.Plant(Row.Type).Icon : null;
}

internal static class GardenData
{
    internal static readonly int[] Thresholds=GameRules.Integers("garden","thresholds");
    internal static readonly int[] TempleThresholds=GameRules.Integers("garden","templeThresholds");
    internal static int TempleLevel(JsonObject? save)=>TempleLevel(GameJson.Number(save?["landgodExp"]));
    internal static int TempleLevel(int experience)=>Enumerable.Range(0,TempleThresholds.Length).Last(i=>Math.Max(0,experience)>=TempleThresholds[i])+1;
    internal static readonly GardenPlant[] Plants=GameRules.Rows("garden","plants").Select(row=>new GardenPlant(GameJson.Number(row!["type"]),GameJson.Text(row["name"]),GameJson.Text(row["artwork"]),GameJson.Text(row["description"]))).ToArray();
    internal static GardenPlant Plant(int type) => Plants.FirstOrDefault(p => p.Type == type) ?? new(type,$"未知类型 {type}","garden-soil.png","保留原始类型，请核对当前游戏包的数据。");
    internal static string Grade(int grade) => grade is >= 1 and <= 5 ? new[] {"一品","二品","三品","四品","五品"}[grade-1] : $"品级 {grade}";
    internal static int Level(JsonObject? save) => Enumerable.Range(0,Thresholds.Length).Last(i => Math.Max(0,GameJson.Number(save?["gardenExp"])) >= Thresholds[i]) + 1;
    internal static JsonArray Plots(JsonObject save)
    {
        if (save["gardenList"] is JsonArray list) return list;
        save["gardenList"] = new JsonArray(); return (JsonArray)save["gardenList"]!;
    }
    internal static IReadOnlyList<GardenRow> Rows(JsonObject? save) =>
        (save?["gardenList"] as JsonArray ?? new()).Select((node,index)=>(node,index)).Where(p=>p.node is JsonObject)
        .Select(p=>new GardenRow(p.index,GameJson.Number(p.node!["gid"]),GameJson.Number(p.node["type"]),
            GameJson.Number(p.node["plantingLevel"]),GameJson.Text(p.node["date"]))).ToArray();
    internal static void Validate(JsonObject plot, JsonObject save, int? editIndex)
    {
        int id=GameJson.Number(plot["gid"]);
        if (id is <1 or >9 || Rows(save).Any(r=>r.Index!=editIndex && r.Id==id)) throw new InvalidDataException("地块编号应为 1–9，且不能重复。");
        if (GameJson.Number(plot["plantingLevel"],-1) is <0 or >5 || GameJson.Number(plot["type"],-1)<0)
            throw new InvalidDataException("植物品级应为 0–5，类型应为非负整数。");
        string date=GameJson.Text(plot["date"]);
        if (date.Length>0 && !DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new InvalidDataException("日期格式为 YYYY-MM-DD，也可以留空。");
    }
    internal static void Apply(JsonObject save, JsonObject plot, bool all)
    {
        int id=GameJson.Number(plot["gid"]);
        var rows=Rows(save); int? oldIndex=rows.FirstOrDefault(r=>r.Id==id)?.Index;
        Validate(plot,save,oldIndex);
        var existing=save["gardenList"] as JsonArray ?? new();
        if (existing.Count!=rows.Count || rows.Any(r=>r.Id is <1 or >9) || rows.Select(r=>r.Id).Distinct().Count()!=rows.Count)
            throw new InvalidDataException("地块数据有重复或无效记录，请先在原始 JSON 中核对。");
        int targetLevel=Math.Max(Level(save),Math.Max(id,rows.Select(r=>r.Id).DefaultIfEmpty(1).Max()));
        var list=new JsonArray();
        for (int key=1;key<=targetLevel;key++)
        {
            var previous=rows.FirstOrDefault(r=>r.Id==key);
            var entry=previous is null ? new JsonObject {["gid"]=key,["type"]=1,["plantingLevel"]=0,["date"]=""}
                : (JsonObject)existing[previous.Index]!.DeepClone();
            if (all || key==id)
            {
                if (!all) entry=(JsonObject)plot.DeepClone();
                else foreach(string field in new[]{"type","plantingLevel","date"}) entry[field]=plot[field]?.DeepClone();
                entry["gid"]=key;
            }
            list.Add(entry);
        }
        save["gardenList"]=list;
        save["gardenExp"]=Math.Max(GameJson.Number(save["gardenExp"]),Thresholds[targetLevel-1]);
    }
    internal static int TopGrade(JsonObject save)
    {
        int count=0;
        foreach(var plot in (save["gardenList"] as JsonArray ?? new()).OfType<JsonObject>())
            if(GameJson.Number(plot["type"]) is 2 or 3 or 5 && GameJson.Number(plot["plantingLevel"])!=5)
            { plot["plantingLevel"]=5; count++; }
        return count;
    }
    internal static int Renew(JsonObject save, int? id=null)
    {
        int count=0;
        foreach(var plot in (save["gardenList"] as JsonArray ?? new()).OfType<JsonObject>())
            if(GameJson.Number(plot["type"])==4 && (id is null || GameJson.Number(plot["gid"])==id))
            { plot["type"]=1; plot["plantingLevel"]=0; count++; }
        return count;
    }
}
