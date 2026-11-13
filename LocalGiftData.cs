using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Media;

namespace GmStudio;

internal sealed record LocalGiftRow(JsonObject Data)
{
    public string Name=>GameJson.Text(Data["name"]);
    public string Code=>GameJson.Text(Data["code"]);
    public string Status=>Data["enabled"]?.GetValue<bool>()==false?"已停用":"启用中";
    public override string ToString()=>Name;
}
internal sealed record GiftRewardRow(JsonObject Data,ImageSource? Icon)
{
    public string Name=>GameJson.Text(Data["name"]);
    public string Count {get=>GameJson.Text(Data["count"]);set=>Data["count"]=int.TryParse(value,out int number)?JsonValue.Create(number):JsonValue.Create(value);}
    public string Detail=>GameJson.Text(Data["kind"]) switch {"legacy"=>"原版礼包 · 奖励整套领取", "pet"=>GameJson.Text(Data["profile"]).StartsWith("original-")?"1 级 · 原版随机资质":"宠物 · 新增后休息", "points"=>"点券 · 每次兑换增加到账户", "soul"=>"灵魂 · 发给目标角色",_=>GameJson.Text(Data["id"])};
}
internal static class LocalGiftData
{
    internal static readonly string[] Presets=["自定义奖励","积分神秘礼包","手机客户端礼包","年兽礼包","中秋礼包","游戏盒签到礼包"];
    internal static string FilePath(GameFolder game)=>GamePaths.GiftState(game.Root);
    internal static JsonObject Read(GameFolder game)
    {
        if(File.Exists(Path.Combine(game.Root,"tools","local_gifts.transaction.json")))throw new InvalidOperationException("有待恢复的兑换事务，请先启动游戏完成自动恢复。");
        if(!File.Exists(FilePath(game)))return new JsonObject{["version"]=1,["revision"]=0,["codes"]=new JsonArray(),["claims"]=new JsonArray()};
        var data=GameJson.Read(FilePath(game));
        if(GameJson.Number(data["version"])!=1 || data["codes"] is not JsonArray || data["claims"] is not JsonArray)throw new InvalidDataException("本地兑换数据格式无效。");
        return data;
    }
    internal static IDisposable Acquire(GameFolder game)
    {
        var held=StorageTransaction.Acquire(game.Root);
        try{StorageTransaction.RecoverLocked(game.Root);return held;}
        catch{held.Dispose();throw;}
    }
    internal static void Save(GameFolder game,JsonArray originalCodes,JsonArray editedCodes)
    {
        Validate(editedCodes);
        using var held=Acquire(game);
        var latest=Read(game);
        if(!JsonNode.DeepEquals(latest["codes"],originalCodes))throw new InvalidOperationException("兑换码配置已被其他窗口修改，请刷新后再编辑。领取记录已保留。");
        latest["codes"]=editedCodes.DeepClone();latest["revision"]=GameJson.Number(latest["revision"])+1;
        string path=FilePath(game);byte[]? before=File.Exists(path)?File.ReadAllBytes(path):null;
        StorageTransaction.CommitLocked(game.Root,[new("tools/local_gifts.json",before,Encoding.UTF8.GetBytes(latest.ToJsonString(GameJson.Format)))],"modifier-config",Guid.NewGuid().ToString("N"));
    }
    internal static void Validate(JsonArray codes)
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>(StringComparer.Ordinal);
        if(codes.Any(c=>c is not JsonObject))throw new InvalidDataException("兑换码配置中存在无效记录。");
        foreach(var data in codes.OfType<JsonObject>())
        {
            string code=GameJson.Text(data["code"]).Trim().ToUpperInvariant();data["code"]=code;
            if(!System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Z0-9_-]{4,64}$"))throw new InvalidDataException("兑换码应为4–64位字母、数字、横线或下划线。");
            if(!seen.Add(code))throw new InvalidDataException("兑换码重复："+code);
            if(!ids.Add(GameJson.Text(data["id"])) || GameJson.Text(data["id"]).Length==0)throw new InvalidDataException("兑换码标识重复或为空。");
            if(GameJson.Text(data["name"]).Trim().Length is <1 or >60)throw new InvalidDataException("礼包名称应为1–60个字符。");
            if(GameJson.Text(data["rule"]) is not ("once" or "daily" or "limited"))throw new InvalidDataException("请选择领取规则。");
            if(GameJson.Text(data["target"]) is not ("all" or "player1" or "player2"))throw new InvalidDataException("请选择发放角色。");
            foreach(string field in new[]{"perSaveLimit","totalLimit"})if(GameJson.Number(data[field],-1)<0)throw new InvalidDataException("领取次数不能小于0。");
            if(GameJson.Text(data["rule"])=="limited" && GameJson.Number(data["perSaveLimit"])<1)throw new InvalidDataException("自定义规则的每存档次数至少为1。");
            string date=GameJson.Text(data["expires"]);
            if(date.Length>0 && !DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new InvalidDataException("到期日期应为 yyyy-MM-dd，或留空永久有效。");
            if(data["rewards"] is not JsonArray rewards || rewards.Count is <1 or >100)throw new InvalidDataException("每个礼包需要1–100项奖励。");
            if(rewards.Any(r=>r is not JsonObject))throw new InvalidDataException("礼包中存在无效奖励记录。");
            foreach(var reward in rewards.OfType<JsonObject>())
            {
                string kind=GameJson.Text(reward["kind"]);int amount=GameJson.Number(reward["count"]);
                if(kind is not ("item" or "pet" or "points" or "soul" or "legacy") || amount<1 || (kind is "item" or "pet") && amount>GameRules.Int("inventory","stack"))throw new InvalidDataException("奖励类型或数量无效。");
                if(kind=="legacy" && (amount!=1 || rewards.Count!=1))throw new InvalidDataException("原版礼包为整套奖励，数量固定1；自定义奖励请替换奖励列表。");
            }
        }
    }
    internal static JsonObject New()=>new(){["id"]=Guid.NewGuid().ToString("N"),["code"]="",["name"]="新的礼包",["description"]="",["enabled"]=true,["rule"]="once",["perSaveLimit"]=1,["totalLimit"]=0,["expires"]="",["target"]="all",["rewards"]=new JsonArray()};
    internal static JsonObject Item(GameFolder game,CatalogItem item,int count=1,bool originalFashion=false)
    {
        var player=new JsonObject();Inventory.Add(player,item,1,true,game);
        string record=Inventory.Records(player,item.Bag).Single();
        return new JsonObject{["kind"]="item",["id"]=item.Code,["name"]=item.Name,["count"]=count,["bag"]=item.Bag,["record"]=record,["stackable"]=item.Bag==Inventory.Consumables,["fashionDate"]=originalFashion};
    }
    internal static JsonObject Pet(GameFolder game,PetData pet,string profile="")=>new(){["kind"]="pet",["id"]=pet.Fields[0],["name"]=GameJson.Text(game.PetNames[pet.Fields[0]]),["count"]=1,["record"]=pet.Encode(),["profile"]=profile};
    internal static JsonArray Preset(GameFolder game,int preset)
    {
        var rewards=new JsonArray();
        void Add(string code,int count=1,bool fashion=false)
        {
            if(!game.CatalogByCode.TryGetValue(code,out var item))throw new InvalidDataException("当前游戏包缺少礼包物品："+code);
            rewards.Add(Item(game,item,count,fashion));
        }
        void AddPet(string code,string profile)=>rewards.Add(Pet(game,PetData.New(code),profile));
        switch(preset)
        {
            case 1:Add("ptsmsrsz",fashion:true);break;
            case 2:AddPet("turtle1","original-turtle");Add("p_cygsjl");Add("wpcsd");break;
            case 3:AddPet("nian1","original-nian");Add("wphhd");Add("wpcsd");Add("xnhdl");break;
            case 4:Add("mpyj");Add("gjs3");Add("ghyb",5);Add("yxsmsrsz",fashion:true);break;
            case 5:Add("_4399ptsjwj");Add("tlzsp");Add("xns1");Add("gjs1");break;
        }
        return rewards;
    }
    internal static GiftRewardRow Row(GameFolder game,JsonObject reward)
    {
        string code=GameJson.Text(reward[GameJson.Text(reward["kind"])=="legacy"?"icon":"id"]);
        ImageSource? icon=GameJson.Text(reward["kind"])=="pet"?game.Portrait("pets",code):game.CatalogByCode.TryGetValue(code,out var item)?item.Icon:null;
        return new(reward,icon);
    }
}
