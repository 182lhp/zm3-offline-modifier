#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private void VerifySkillHintDefinitions()
    {
        var names=(JsonObject)game!.Official["petSkillNames"]!;
        Require(names.Count==56 && names.All(pair=>PetEntry.Skill(game,pair.Key) is { Hint: {} hint } &&
            hint.Description.Length>=6 && hint.Name==GameJson.Text(pair.Value) && hint.Code==pair.Key &&
            hint.Condition.Length>0 && !hint.Description.Contains("未收录")),"Every one of the 56 pet skills has a complete effect and learning condition.");
        foreach(string code in new[]{"smjc","mfjc","gjjc","fyjc"})
            Require(PetEntry.Skill(game,code).Description!="提升主人","Truncated owner-buff descriptions are repaired.");
        Require(PetEntry.Skill(game,"qlfj").Description.Contains("反击"),"Counterattack explains its result, rather than ending at '有'.");
        foreach(string code in new[]{"jgaoyi","tmaoyi","bhaoyi","xwaoyi","zqaoyi","qlaoyi","ysaoyi"})
        {
            var hint=PetEntry.Skill(game,code).Hint!;
            Require(hint.Acquisition=="进化后领悟" && hint.Condition.Contains("第四阶") && hint.Condition.Contains("再次达到100%") && hint.Condition.Contains("无需精灵"),
                $"{code}: natural ultimates explicitly require final evolution and renewed full potential, without a spirit.");
        }
        foreach(var (family,code,spirit) in new[]{("mouse","zsaoyi","子鼠精灵"),("neat","cnaoyi","丑牛精灵"),("terribletiger","yhaoyi","寅虎精灵")})
        {
            var hint=PetEntry.Skill(game,code).Hint!;
            var pet=PetData.New(family+"1");
            var rule=new PetRules(game,family+"1").Skills(pet.Fields,[]).Single(skill=>skill.Code==code);
            var candidate=new PetSkillCandidate(PetEntry.Skill(game,code),rule);
            Require(hint.Acquisition=="穿戴精灵直接获得" && hint.Condition.Contains($"穿戴{spirit}直接获得") &&
                hint.Condition.Contains("第一形态") && hint.Condition.Contains("无需超进化") && hint.Condition.Contains("空余技能格") &&
                candidate.Hint!.Acquisition==hint.Acquisition && candidate.Hint.Condition.Contains("第一形态") && candidate.Hint.Condition.Contains("当前状态"),
                $"{code}: spirit grants retain their own source and first-form eligibility even on a locked candidate.");
        }
        var codes=game.HeroSkills.OfType<JsonObject>().SelectMany(row=>((JsonArray)row["codes"]!).Select(GameJson.Text)).ToArray();
        Require(codes.Length==40 && codes.All(code=>SkillHelp.Heroes.TryGetValue(code,out var description) && description.Length>=6),
            "All 40 role skills have actual effect descriptions.");
    }

    private void VerifyRenderedSkillHint(DependencyObject root,string code,string folder,string filename)
    {
        var host=VisualDescendants(root).Prepend(root).OfType<FrameworkElement>().First(element=>element.ToolTip is ToolTip);
        var tooltip=(ToolTip)host.ToolTip;
        try
        {
            tooltip.PlacementTarget=host;tooltip.IsOpen=true;
            tooltip.UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);tooltip.UpdateLayout();
            Require(tooltip.Content is SkillHint hint && hint.Code==code && hint.Description.Length>=6,
                $"{filename}: the icon's real tooltip receives its complete skill data.");
            var texts=VisualDescendants(tooltip).OfType<TextBlock>().Select(block=>block.Text).ToArray();
            Require(texts.Contains(((SkillHint)tooltip.Content).Description) && tooltip.ActualWidth>100 && tooltip.ActualWidth<=360,
                $"{filename}: the hover card renders wrapped effect text within its width.");
            if(((SkillHint)tooltip.Content).Acquisition is {Length:>0} acquisition)
                Require(texts.Contains(acquisition),$"{filename}: the tooltip visibly displays the acquisition type.");
            var dpi=VisualTreeHelper.GetDpi(tooltip);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(tooltip.ActualWidth*dpi.DpiScaleX),
                (int)Math.Ceiling(tooltip.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
            bitmap.Render(tooltip);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output=File.Create(Path.Combine(folder,filename));encoder.Save(output);
        }
        finally { tooltip.IsOpen=false; }
    }
}
#endif
