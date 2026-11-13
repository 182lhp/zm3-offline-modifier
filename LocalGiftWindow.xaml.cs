using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GmStudio;

public partial class LocalGiftWindow:Window
{
    private readonly GameFolder game;
    private JsonArray originalCodes,codes;
    private JsonObject? selected;
    private bool loading,discard;
    internal LocalGiftWindow(Window owner,GameFolder game)
    {
        this.game=game;InitializeComponent();Owner=owner;
        Width=Math.Min(Width,SystemParameters.WorkArea.Width);Height=Math.Min(Height,SystemParameters.WorkArea.Height);
        using(var held=LocalGiftData.Acquire(game))originalCodes=(JsonArray)LocalGiftData.Read(game)["codes"]!.DeepClone();
        codes=(JsonArray)originalCodes.DeepClone();PresetBox.ItemsSource=LocalGiftData.Presets;PresetBox.SelectedIndex=0;
        RefreshList();if(codes.Count>0)CodeList.SelectedIndex=0;else EditorPanel.IsEnabled=false;
    }
    private void Guard(Action action){try{action();}catch(Exception ex) when(ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException){StatusText.Text=ex.Message;StatusText.Foreground=System.Windows.Media.Brushes.Firebrick;}}
    private void RefreshList(string? selectId=null)
    {
        loading=true;
        string term=SearchBox?.Text?.Trim()??"";
        CodeList.ItemsSource=codes.OfType<JsonObject>().Where(d=>term.Length==0 || (GameJson.Text(d["name"])+" "+GameJson.Text(d["code"])).Contains(term,StringComparison.OrdinalIgnoreCase)).Select(d=>new LocalGiftRow(d)).ToArray();
        CodeSummary.Text=$"兑换码 · {codes.Count}";
        if(selectId is not null)CodeList.SelectedItem=CodeList.Items.Cast<LocalGiftRow>().FirstOrDefault(r=>GameJson.Text(r.Data["id"])==selectId);
        loading=false;
    }
    private void StoreFields()
    {
        if(selected is null || loading)return;
        RewardGrid.CommitEdit(DataGridEditingUnit.Cell,true);RewardGrid.CommitEdit(DataGridEditingUnit.Row,true);
        selected["name"]=NameBox.Text.Trim();selected["code"]=CodeBox.Text.Trim().ToUpperInvariant();selected["description"]=DescriptionBox.Text.Trim();
        selected["enabled"]=EnabledBox.IsChecked==true;selected["rule"]=(RuleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"once";
        selected["target"]=(TargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"all";
        if(!int.TryParse(PerSaveBox.Text,out int per) || !int.TryParse(TotalBox.Text,out int total))throw new InvalidDataException("领取次数请输入整数。");
        selected["perSaveLimit"]=per;selected["totalLimit"]=total;selected["expires"]=ExpiryBox.SelectedDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"";
    }
    private void LoadFields(JsonObject data)
    {
        selected=data;loading=true;EditorPanel.IsEnabled=true;
        NameBox.Text=GameJson.Text(data["name"]);CodeBox.Text=GameJson.Text(data["code"]);DescriptionBox.Text=GameJson.Text(data["description"]);
        EnabledBox.IsChecked=data["enabled"]?.GetValue<bool>()!=false;
        RuleBox.SelectedItem=RuleBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>i.Tag.ToString()==GameJson.Text(data["rule"]));
        TargetBox.SelectedItem=TargetBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>i.Tag.ToString()==GameJson.Text(data["target"]));
        PerSaveBox.Text=GameJson.Text(data["perSaveLimit"]);TotalBox.Text=GameJson.Text(data["totalLimit"]);
        ExpiryBox.SelectedDate=DateTime.TryParseExact(GameJson.Text(data["expires"]),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
        loading=false;RuleChanged(this,new SelectionChangedEventArgs(Selector.SelectionChangedEvent,Array.Empty<object>(),Array.Empty<object>()));RefreshRewards();RefreshClaims();
    }
    private void CodeChanged(object sender,SelectionChangedEventArgs e)
    {
        if(loading)return;
        var next=(CodeList.SelectedItem as LocalGiftRow)?.Data;
        Guard(()=>{StoreFields();if(next is not null)LoadFields(next);});
    }
    private void SearchChanged(object sender,TextChangedEventArgs e){if(CodeList is not null && codes is not null)Guard(()=>{StoreFields();RefreshList();});}
    private void NewClick(object sender,RoutedEventArgs e)=>Guard(()=>{StoreFields();var data=LocalGiftData.New();codes.Add(data);SearchBox.Clear();RefreshList(GameJson.Text(data["id"]));LoadFields(data);CodeBox.Focus();});
    private void DuplicateClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null)return;StoreFields();var data=(JsonObject)selected.DeepClone();data["id"]=Guid.NewGuid().ToString("N");data["code"]="";data["name"]=GameJson.Text(data["name"])+" 副本";codes.Add(data);SearchBox.Clear();RefreshList(GameJson.Text(data["id"]));LoadFields(data);CodeBox.Focus();
    });
    private void DeleteClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null)return;
        codes.Remove(selected);selected=null;RefreshList();if(CodeList.Items.Count>0)CodeList.SelectedIndex=0;else EditorPanel.IsEnabled=false;
        StatusText.Text="兑换码已从编辑列表移除，保存后生效。历史领取记录继续保留。";
    });
    private void RuleChanged(object sender,SelectionChangedEventArgs e){if(PerSaveBox is not null)PerSaveBox.IsEnabled=(RuleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()=="limited";}
    private JsonArray Rewards=>(JsonArray)(selected?["rewards"]??throw new InvalidOperationException("请先新建或选择兑换码。"));
    private void RefreshRewards()=>RewardGrid.ItemsSource=Rewards.OfType<JsonObject>().Select(r=>LocalGiftData.Row(game,r)).ToArray();
    private void LoadPresetClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null || PresetBox.SelectedIndex<=0)return;
        selected["rewards"]=LocalGiftData.Preset(game,PresetBox.SelectedIndex);if(NameBox.Text is "新的礼包" or "")NameBox.Text=LocalGiftData.Presets[PresetBox.SelectedIndex];RefreshRewards();
    });
    private void AddItemClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null)return;var picker=new LocalGiftItemWindow(this,game);if(picker.ShowDialog()==true && picker.Selected is { } item){Rewards.Add(LocalGiftData.Item(game,item));RefreshRewards();}
    });
    private void AddPetClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null)return;var picker=new PetCreateWindow(this,game);if(picker.ShowDialog()==true && picker.CreatedPet is { } pet){Rewards.Add(LocalGiftData.Pet(game,pet));RefreshRewards();}
    });
    private void AddResource(string kind,string name){if(selected is null)return;Rewards.Add(new JsonObject{["kind"]=kind,["name"]=name,["count"]=1000});RefreshRewards();}
    private void AddSoulClick(object sender,RoutedEventArgs e)=>Guard(()=>AddResource("soul","灵魂"));
    private void AddPointsClick(object sender,RoutedEventArgs e)=>Guard(()=>AddResource("points","点券"));
    private void RemoveRewardClick(object sender,RoutedEventArgs e){if(RewardGrid.SelectedItem is GiftRewardRow row){Rewards.Remove(row.Data);RefreshRewards();}}
    private void RefreshClaims()
    {
        using var held=LocalGiftData.Acquire(game);var state=LocalGiftData.Read(game);
        var rows=((JsonArray)state["claims"]!).OfType<JsonObject>().Where(c=>selected is not null && GameJson.Text(c["codeId"])==GameJson.Text(selected["id"])).Reverse().ToArray();
        UsageText.Text=$"已领取 {rows.Length} 次";
        ClaimsGrid.ItemsSource=rows.Select(c=>new{Time=DateTimeOffset.FromUnixTimeSeconds(GameJson.Number(c["time"])).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm"),Slot="存档 "+GameJson.Text(c["slot"]),Name=GameJson.Text(c["name"])}).ToArray();
    }
    private void RefreshClaimsClick(object sender,RoutedEventArgs e)=>Guard(RefreshClaims);
    private void SaveClick(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        StoreFields();LocalGiftData.Save(game,originalCodes,codes);originalCodes=(JsonArray)codes.DeepClone();RefreshList(selected is null?null:GameJson.Text(selected["id"]));RefreshClaims();StatusText.Text="兑换码已保存。游戏内重新验证兑换码即可使用，无需重启。";StatusText.Foreground=(System.Windows.Media.Brush)FindResource("Green");
    });
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void WindowClosing(object? sender,CancelEventArgs e)
    {
        if(discard)return;
        try{StoreFields();if(JsonNode.DeepEquals(codes,originalCodes))return;}catch(Exception){ }
        if(!PromptDialog.Confirm(this,"放弃修改","兑换码还有未保存的修改，要放弃并关闭吗？","放弃修改",true)){e.Cancel=true;return;}discard=true;
    }
}
