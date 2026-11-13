#ifndef ZM3_GEOMETRY_H
#define ZM3_GEOMETRY_H
#include <windows.h>
#include <stdlib.h>
#define GAME_W 940
#define GAME_H 590
static int rounded_ratio(int value,int top,int bottom){return (int)(((long long)value*top+bottom/2)/bottom);}
static void fit_client(int *w,int *h,int maxw,int maxh){
 if(*w>maxw)*w=maxw;if(*h>maxh)*h=maxh;
 if((long long)*w*GAME_H>(long long)*h*GAME_W)*w=rounded_ratio(*h,GAME_W,GAME_H);
 else *h=rounded_ratio(*w,GAME_H,GAME_W);
 if(*w<1)*w=1;if(*h<1)*h=1;
}
static void sizing_rect(RECT *r,int edge,int fw,int fh,int oldw,int oldh,int maxw,int maxh){
 int w=r->right-r->left-fw,h=r->bottom-r->top-fh;
 BOOL horizontal=edge==WMSZ_LEFT||edge==WMSZ_RIGHT;
 BOOL vertical=edge==WMSZ_TOP||edge==WMSZ_BOTTOM;
 if(!horizontal&&!vertical)horizontal=(long long)abs(w-oldw)*GAME_H>=(long long)abs(h-oldh)*GAME_W;
 if(horizontal){if(w<470)w=470;h=rounded_ratio(w,GAME_H,GAME_W);}
 else {if(h<295)h=295;w=rounded_ratio(h,GAME_W,GAME_H);}
 fit_client(&w,&h,maxw,maxh);
 if(edge==WMSZ_LEFT||edge==WMSZ_TOPLEFT||edge==WMSZ_BOTTOMLEFT)r->left=r->right-w-fw;else r->right=r->left+w+fw;
 if(edge==WMSZ_TOP||edge==WMSZ_TOPLEFT||edge==WMSZ_TOPRIGHT)r->top=r->bottom-h-fh;else r->bottom=r->top+h+fh;
}
#endif
