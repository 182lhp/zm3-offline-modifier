#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
namespace GmStudio;
public partial class MainWindow
{
    private void VerifyLocalGifts(string folder)
    {
        string path=LocalGiftData.FilePath(game!);
        Require(!File.Exists(path),"Gift tests use a clean copied fixture.");
        var definitions=new JsonArray();
        for(int preset=1;preset<LocalGiftData.Presets.Length;preset++)
        {
            var data=LocalGiftData.New();data["code"]="QA-GIFT-"+preset;data["name"]=LocalGiftData.Presets[preset];data["rewards"]=LocalGiftData.Preset(game!,preset);
            foreach(var reward in ((JsonArray)data["rewards"]!).OfType<JsonObject>())
            {
                Require(LocalGiftData.Row(game!,reward).Icon is not null,"Original gift illustrations exist.");
                Require(GameJson.Text(reward["record"]).Length>0,"Presets use materialized current-package records.");
            }
            definitions.Add(data);
        }
        LocalGiftData.Validate(definitions);
        File.WriteAllText(Path.Combine(folder,"gift-presets.json"),new JsonObject{["version"]=1,["revision"]=0,["codes"]=definitions.DeepClone(),["claims"]=new JsonArray()}.ToJsonString(GameJson.Format));
        var dialog=new LocalGiftWindow(this,game!);dialog.Show();dialog.UpdateLayout();
        Require(!dialog.EditorPanel.IsEnabled,"Empty manager has no phantom selection.");
        ClickDialogButton(dialog,"新建");dialog.NameBox.Text="中秋本地礼包";dialog.CodeBox.Text="QA-MANAGER";dialog.PresetBox.SelectedIndex=4;ClickDialogButton(dialog,"载入并替换奖励");
        Require(dialog.RewardGrid.Items.Count==4,"Load original gift rewards without awarding a player.");
        Require(!dialog.RewardGrid.IsReadOnly,"Reward quantities are editable through the grid.");
        ((GiftRewardRow)dialog.RewardGrid.Items[0]).Count="3";
        ClickDialogButton(dialog,"保存兑换码");
        var state=LocalGiftData.Read(game!);var original=(JsonArray)state["codes"]!.DeepClone();
        Require(GameJson.Number(state["codes"]![0]!["rewards"]![0]!["count"])==3,"Edited reward quantities persist.");
        Capture(folder,"local-gifts.png",dialog);
        dialog.Width=900;dialog.Height=620;dialog.UpdateLayout();Capture(folder,"local-gifts-compact.png",dialog);
        ClickDialogButton(dialog,"关闭");
        ((JsonArray)state["claims"]!).Add(new JsonObject{["codeId"]=GameJson.Text(state["codes"]![0]!["id"]),["slot"]=0,["name"]="历史领取",["time"]=1794484800});
        File.WriteAllText(path,state.ToJsonString(GameJson.Format));
        var edit=(JsonArray)original.DeepClone();edit[0]!["description"]="changed";LocalGiftData.Save(game!,original,edit);
        Require(((JsonArray)LocalGiftData.Read(game!)["claims"]!).Count==1,"Saving definitions merges concurrent claim history.");
        bool conflict=false;try{LocalGiftData.Save(game!,original,edit);}catch(InvalidOperationException){conflict=true;}Require(conflict,"Reject conflicting definition edits.");
        var duplicate=(JsonArray)edit.DeepClone();var row=(JsonObject)edit[0]!.DeepClone();row["id"]=Guid.NewGuid().ToString("N");duplicate.Add(row);
        bool invalid=false;try{LocalGiftData.Validate(duplicate);}catch(InvalidDataException){invalid=true;}Require(invalid,"Duplicate redemption codes are rejected.");
        var picker=new LocalGiftItemWindow(this,game!);picker.Show();picker.UpdateLayout();Capture(folder,"gift-item-picker.png",picker);picker.Close();
        File.Delete(path);
    }
}
#endif
