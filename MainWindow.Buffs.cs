using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed record BuffDuration(string Label,int Frames)
{
    public override string ToString()=>Label;
}
internal sealed record BuffTile(int Type,BuffKind? Kind,BuffRow? Current)
{
    public string Name=>Kind?.Name ?? $"其他增益 #{Type}";
    public BitmapImage? Icon=>UtilityArtwork.Image($"buff-{Type}.jpg");
    public string ValueText=>Current is null ? "—" : Type==1 ? $"{Current.Value} 倍" : Type==0 ? $"+{Current.Value}%" : $"+{Current.Value:N0}";
    public string State=>Current is null ? "未启用" : Current.Frames<=0 ? "已结束" : Kind is null ? "旧记录 · 可移除" : "已启用";
    public string Duration=>Current is null ? "选择后设置" : FormatDuration(Current.Frames);
    internal static string FormatDuration(int frames)
    {
        int seconds=(int)Math.Ceiling(Math.Max(0,frames)/(double)GameRules.Int("buff","framesPerSecond"));
        return seconds>=3600 ? $"{seconds/3600d:0.#} 小时" : seconds>=60 ? $"{seconds/60} 分 {seconds%60} 秒" : $"{seconds} 秒";
    }
}

public partial class MainWindow
{
    private bool updatingBuffDetails;
    private void ShowBuffs()
    {
        BuffPage.IsEnabled=save is not null;
        int selected=(BuffTable.SelectedItem as BuffTile)?.Type ?? 0;
        var rows=BuffData.Rows(save?.Draft);
        var tiles=BuffData.Kinds.Select(k=>new BuffTile(k.Id,k,rows.LastOrDefault(r=>r.Type==k.Id)))
            .Concat(rows.Where(r=>!BuffData.Kinds.Any(k=>k.Id==r.Type)).DistinctBy(r=>r.Type).Select(r=>new BuffTile(r.Type,null,r))).ToArray();
        BuffTable.ItemsSource=tiles;
        BuffTable.SelectedItem=tiles.FirstOrDefault(t=>t.Type==selected) ?? tiles.FirstOrDefault();
        BuffSummary.Text=$"已启用 {rows.Where(r=>r.Frames>0).Select(r=>r.Type).Distinct().Count()} 种增益";
        ShowBuffSelection();
    }
    private void BuffSelected(object sender,SelectionChangedEventArgs e)=>ShowBuffSelection();
    private void ShowBuffSelection()
    {
        if(BuffApplyButton is null || BuffTable.SelectedItem is not BuffTile tile) return;
        updatingBuffDetails=true;
        try
        {
            BuffDetailIcon.Source=tile.Icon; BuffDetailTitle.Text=tile.Name;
            BuffDetailDescription.Text=tile.Type switch
            {
                0=>"提高关卡中的物品掉落率。",1=>"提高角色获得的经验倍数。",3=>"增加角色攻击数值。",4=>"增加角色的生命回复能力。",
                5=>"增加角色生命上限。",6=>"增加角色防御数值。",8=>"游戏支持的另一项攻击加成，可与普通攻击加成分别设置。",
                _=>"这是存档中已有的其他记录。保留原值，可在这里移除。"
            };
            BuffValueBox.IsEnabled=BuffDurationBox.IsEnabled=BuffFramesBox.IsEnabled=BuffApplyButton.IsEnabled=tile.Kind is not null;
            BuffRemoveButton.IsEnabled=tile.Current is not null;
            BuffValueLabel.Text=tile.Type==1 ? "经验倍数" : tile.Type==0 ? "掉落率加成（%）" : "增加数值";
            BuffValueBox.Text=(tile.Current?.Value ?? tile.Kind?.Default ?? 0).ToString(CultureInfo.InvariantCulture);
            var durations=GameRules.Rows("buff","durationPresets").Select(row=>new BuffDuration(GameJson.Text(row!["label"]),GameJson.Number(row["frames"]))).ToList();
            int frames=tile.Current?.Frames ?? BuffData.MaxFrames;
            if(!durations.Any(d=>d.Frames==frames)) durations.Insert(0,new($"当前 · {BuffTile.FormatDuration(frames)}",frames));
            durations.Add(new("自定义时长…",0));
            BuffDurationBox.ItemsSource=durations;
            BuffDurationBox.SelectedItem=durations.First(d=>d.Frames==frames);
            BuffFramesBox.Text=frames.ToString(CultureInfo.InvariantCulture);
            BuffExactDuration.Visibility=Visibility.Collapsed;
            BuffApplyButton.Content=tile.Current is null ? "启用增益" : "更新增益";
            BuffLimitHint.Text=tile.Kind is null ? "此记录不提供新增和改值。" : tile.Kind.Limit is int cap ? $"游戏上限 {cap}，应用时会检查。最长 1 小时。" : "输入非负整数，最长 1 小时。同类增益会更新为本次设置。";
        }
        finally {updatingBuffDetails=false;}
    }
    private void BuffDurationChanged(object sender,SelectionChangedEventArgs e)
    {
        if(updatingBuffDetails || BuffExactDuration is null || BuffDurationBox.SelectedItem is not BuffDuration duration) return;
        BuffExactDuration.Visibility=duration.Frames==0 ? Visibility.Visible : Visibility.Collapsed;
        if(duration.Frames>0) BuffFramesBox.Text=duration.Frames.ToString(CultureInfo.InvariantCulture);
    }
    private void AddBuff(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null || BuffTable.SelectedItem is not BuffTile {Kind: { } kind}) return;
        int value=Input(BuffValueBox,"增益强度",0), frames=Input(BuffFramesBox,"持续帧数",1,BuffData.MaxFrames);
        BuffData.Put(save.Draft,kind,value,frames);ShowBuffs();
        Report(kind.Limit is int cap && value>cap ? $"{kind.Name}已按上限 {cap} 更新，等待保存。" : $"{kind.Name}已更新，等待保存。");
    });
    private void MaxBuffs(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null) return;
        BuffData.AddAll(save.Draft);ShowBuffs();Report("已添加 7 种推荐增益，持续 1 小时，等待保存。");
    });
    private void RemoveBuff(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null || BuffTable.SelectedItem is not BuffTile {Current:not null} tile) return;
        BuffData.Remove(save.Draft,tile.Type);ShowBuffs();Report($"{tile.Name}已移除，等待保存。");
    });
    private void ClearBuffs(object sender,RoutedEventArgs e)
    {
        if(save is null || !Confirm("清空增益","移除当前存档中的全部增益记录？","清空增益",caution:true)) return;
        save.Draft["savedBuffString"]="";ShowBuffs();Report("增益已清空，等待保存。");
    }
    private void BuffMore(object sender,RoutedEventArgs e)
    {
        if(sender is not Button button) return;
        var menu=new ContextMenu {PlacementTarget=button};var clear=new MenuItem {Header="清空全部增益…"};clear.Click+=ClearBuffs;menu.Items.Add(clear);menu.IsOpen=true;
    }
}
