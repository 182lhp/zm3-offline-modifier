using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
namespace GmStudio;

internal static partial class GameRules
{
    private static readonly JsonObject Data=ReadEmbedded();
    private static JsonObject ReadEmbedded()
    {
        using var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("GmStudio.Rules.json")??throw new InvalidDataException("缺少统一规则数据。");
        using var buffer=new MemoryStream();input.CopyTo(buffer);byte[] bytes=buffer.ToArray();Validate(bytes);return GameJson.Parse(System.Text.Encoding.UTF8.GetString(bytes));
    }
    private static void Validate(byte[] bytes)
    {
        if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=ExpectedSha256)throw new InvalidDataException("规则数据与程序版本不一致，请使用同一次构建发布的文件。");
    }
    internal static string Revision=>GameJson.Text(Data["revision"]);
    internal static void ValidateDirectory(string root)
    {
        string file=GamePaths.Within(root,"schema/game_rules.json");
        // Legacy installations use the byte-identical embedded artifact, never a second set of constants.
        if(File.Exists(file))Validate(File.ReadAllBytes(file));
    }
    internal static JsonNode Node(params string[] path)
    {
        JsonNode? node=Data;foreach(string key in path)node=node?[key];return node?.DeepClone()??throw new InvalidDataException("规则缺少字段："+string.Join('/',path));
    }
    internal static JsonObject Section(string name)=>(JsonObject)Node(name);
    internal static int Int(string area,string key)=>Node("rules",area,key).GetValue<int>();
    internal static string Text(string area,string key)=>Node("rules",area,key).GetValue<string>();
    internal static JsonArray Rows(string area,string key)=>(JsonArray)Node("rules",area,key);
    internal static int[] Integers(string area,string key)=>Rows(area,key).Select(x=>x!.GetValue<int>()).ToArray();
    internal static string[] Strings(string area,string key)=>Rows(area,key).Select(x=>x!.GetValue<string>()).ToArray();
    internal static Dictionary<string,string> Texts(string area,string key)=>((JsonObject)Node("rules",area,key)).ToDictionary(x=>x.Key,x=>GameJson.Text(x.Value));
    internal static int BagCapacity(int level)=>Int("inventory",level>=Int("inventory","expandLevel")?"expandedBag":"bag");
    internal static int PetCapacity(int recharge)=>Int("inventory",recharge>=Integers("vip","thresholds")[Int("inventory","petsVipLevel")]?"expandedPets":"pets");
}
