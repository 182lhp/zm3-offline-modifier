#if QA
using System.IO;
using System.Text.Json.Nodes;
namespace GmStudio;
public partial class MainWindow
{
    private void VerifyUnifiedRules(string folder)
    {
        Require(VipData.Thresholds.SequenceEqual(new[]{0,1000,5000,10000,20000,50000}),"VIP rule extraction preserves every threshold.");
        Require(GardenData.Thresholds.SequenceEqual(new[]{0,100,300,600,1100,1800,2700,3900,5400}) && GardenData.TempleThresholds.SequenceEqual(new[]{0,60,120,180,240}),"Garden and temple thresholds remain independent.");
        Require(GameRules.BagCapacity(49)==50&&GameRules.BagCapacity(50)==75&&GameRules.PetCapacity(9999)==5&&GameRules.PetCapacity(10000)==10,"C# shared inventory boundaries match the game.");
        var original=GameJson.Read(Path.Combine(game!.Root,"schema","official_zmxy3.json"));
        Require(JsonNode.DeepEquals(original,game.Official),"Unified official references preserve all extracted source fields.");
        string probe=Path.Combine(folder,"rules-mismatch");Directory.CreateDirectory(Path.Combine(probe,"schema"));File.WriteAllText(Path.Combine(probe,"schema","game_rules.json"),"{}");
        try{GameRules.ValidateDirectory(probe);throw new Exception("Mismatched rule versions were accepted");}catch(InvalidDataException){}
        var report=new JsonObject{["status"]="PASS",["revision"]=GameRules.Revision,["sha256"]=GameRules.ExpectedSha256,["legacyFactsPreserved"]=true,["mismatchedVersionsRejected"]=true};
        File.WriteAllText(Path.Combine(folder,"rules-qa.json"),report.ToJsonString());
    }
}
#endif
