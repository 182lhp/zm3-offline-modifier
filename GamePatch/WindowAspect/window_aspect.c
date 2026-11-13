#define UNICODE
#define _UNICODE
#define _WIN32_WINNT 0x0A00
#include <windows.h>
#include <commctrl.h>
#include "geometry.h"

static const WCHAR READY[]=L"ZM3.GameAspect.Ready.v1";
typedef struct {int fw,fh;BOOL sizing;} Context;
static void frame(HWND hwnd,Context *c){
 RECT outer,inner;GetWindowRect(hwnd,&outer);GetClientRect(hwnd,&inner);
 if(!IsIconic(hwnd)&&!IsZoomed(hwnd)){
  c->fw=outer.right-outer.left-(inner.right-inner.left);
  c->fh=outer.bottom-outer.top-(inner.bottom-inner.top);
 }
}
static RECT work(HWND hwnd){
 MONITORINFO m={sizeof(m)};GetMonitorInfoW(MonitorFromWindow(hwnd,MONITOR_DEFAULTTONEAREST),&m);return m.rcWork;
}
static LRESULT CALLBACK aspect_proc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp,UINT_PTR id,DWORD_PTR value){
 Context *c=(Context*)value;
 if(msg==WM_NCDESTROY){RemovePropW(hwnd,READY);RemoveWindowSubclass(hwnd,aspect_proc,id);free(c);return DefSubclassProc(hwnd,msg,wp,lp);}
 if(msg==WM_ENTERSIZEMOVE){frame(hwnd,c);c->sizing=TRUE;}
 if(msg==WM_EXITSIZEMOVE)c->sizing=FALSE;
 if(msg==WM_SIZING){
  RECT client;GetClientRect(hwnd,&client);RECT area=work(hwnd);
  sizing_rect((RECT*)lp,(int)wp,c->fw,c->fh,client.right,client.bottom,area.right-area.left-c->fw,area.bottom-area.top-c->fh);
  return TRUE;
 }
 if(msg==WM_GETMINMAXINFO){
  LRESULT result=DefSubclassProc(hwnd,msg,wp,lp);
  MINMAXINFO *info=(MINMAXINFO*)lp;MONITORINFO monitor={sizeof(monitor)};
  GetMonitorInfoW(MonitorFromWindow(hwnd,MONITOR_DEFAULTTONEAREST),&monitor);
  int w=monitor.rcWork.right-monitor.rcWork.left-c->fw,h=monitor.rcWork.bottom-monitor.rcWork.top-c->fh;
  fit_client(&w,&h,w,h);w+=c->fw;h+=c->fh;
  info->ptMinTrackSize.x=470+c->fw;info->ptMinTrackSize.y=295+c->fh;
  info->ptMaxTrackSize.x=w;info->ptMaxTrackSize.y=h;
  info->ptMaxSize.x=w;info->ptMaxSize.y=h;
  info->ptMaxPosition.x=monitor.rcWork.left-monitor.rcMonitor.left+(monitor.rcWork.right-monitor.rcWork.left-w)/2;
  info->ptMaxPosition.y=monitor.rcWork.top-monitor.rcMonitor.top+(monitor.rcWork.bottom-monitor.rcWork.top-h)/2;
  return result;
 }
 if(msg==WM_DPICHANGED){
  UINT dpi=HIWORD(wp);RECT r={0,0,940,590};
  AdjustWindowRectExForDpi(&r,(DWORD)GetWindowLongPtrW(hwnd,GWL_STYLE),GetMenu(hwnd)!=NULL,(DWORD)GetWindowLongPtrW(hwnd,GWL_EXSTYLE),dpi);
  c->fw=r.right-r.left-940;c->fh=r.bottom-r.top-590;
  RECT *next=(RECT*)lp;RECT area=work(hwnd);
  sizing_rect(next,WMSZ_BOTTOMRIGHT,c->fw,c->fh,940,590,area.right-area.left-c->fw,area.bottom-area.top-c->fh);
 }
 if(msg==WM_WINDOWPOSCHANGING){
  WINDOWPOS *p=(WINDOWPOS*)lp;
  if(!(p->flags&SWP_NOSIZE)&&!IsIconic(hwnd)&&p->cx>c->fw&&p->cy>c->fh){
   int w=p->cx-c->fw,h=p->cy-c->fh;RECT area=work(hwnd);
   fit_client(&w,&h,area.right-area.left-c->fw,area.bottom-area.top-c->fh);
   p->cx=w+c->fw;p->cy=h+c->fh;
  }
 }
 return DefSubclassProc(hwnd,msg,wp,lp);
}

__declspec(dllexport) LRESULT CALLBACK AspectHook(int code,WPARAM wp,LPARAM lp){
 if(code>=0){
  CWPSTRUCT *message=(CWPSTRUCT*)lp;
  if(message->message==RegisterWindowMessageW(L"ZM3.GameAspect.Attach.v1")&&message->wParam==0x5A4D3301&&!GetPropW(message->hwnd,READY)){
   Context *c=(Context*)calloc(1,sizeof(Context));frame(message->hwnd,c);
   if(SetWindowSubclass(message->hwnd,aspect_proc,0x5A4D33,(DWORD_PTR)c)){
    HMODULE pinned=NULL;
    if(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)aspect_proc,&pinned))
     SetPropW(message->hwnd,READY,(HANDLE)1);
    else {RemoveWindowSubclass(message->hwnd,aspect_proc,0x5A4D33);free(c);}
   }else free(c);
  }
 }
 return CallNextHookEx(NULL,code,wp,lp);
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(instance);return TRUE;}
