using System.Net.Http;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private readonly DispatcherTimer runtimeTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    private bool checkingRuntime;
    private void InitializeRuntimeStatus()
    {
#if !QA
        runtimeTimer.Tick+=async (_,_)=>await RefreshRuntimeStatus();
        Loaded+=async (_,_)=>{runtimeTimer.Start();await RefreshRuntimeStatus();};
        Closed+=(_,_)=>runtimeTimer.Stop();
#endif
    }
    private void ShowRuntimeState(RuntimeServiceState state)
    {
        RuntimeStatusText.Text=state.GameRunning switch{true=>"游戏运行中",false=>"游戏未运行",_=>"游戏状态待确认"};
        RuntimeStatusText.Foreground=state.GameRunning==true?Positive:Warning;
        RuntimeStatusText.ToolTip=state.GameRunning==true?"已核对当前游戏目录的运行进程。角色修改保存后仍需重新读档。":"本地服务与游戏进程分别检测；存档可以离线保存。";
    }
    private async Task RefreshRuntimeStatus()
    {
        if(game is not { } current||checkingRuntime||busy)return;
        checkingRuntime=true;
        try
        {
            var status=await current.RuntimeStatusAsync();
            if(ReferenceEquals(game,current))ShowRuntimeState(status);
        }
        catch(Exception error) when(error is HttpRequestException or TaskCanceledException)
        {
            if(ReferenceEquals(game,current)){RuntimeStatusText.Text="服务未连接";RuntimeStatusText.Foreground=Warning;}
        }
        catch(Exception error) when(Handled(error))
        {
            if(ReferenceEquals(game,current)){RuntimeStatusText.Text="服务状态异常";RuntimeStatusText.Foreground=Warning;RuntimeStatusText.ToolTip=error.Message;}
        }
        finally{checkingRuntime=false;}
    }
}
