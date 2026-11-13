package export
{
   import AssetsClass.Fulushou;
   import AssetsClass.HundunDoor;
   import AssetsClass.LiuShaHeChanllengeBossAssets;
   import AssetsClass.MidAutumnInterfaceAssets;
   import AssetsClass.MysteriousShopAssets;
   import AssetsClass.ThreeBrothers;
   import base.BaseBitmapDataPool;
   import config.Config;
   import config.HeroSkillProtect;
   import event.CommonEvent;
   import export.renewalse.RenewalseThisSZ;
   import flash.display.Bitmap;
   import flash.display.Loader;
   import flash.utils.getDefinitionByName;
   import flash.utils.Timer;
   import flash.filters.GlowFilter;
   import flash.display.BitmapData;
   import flash.geom.Matrix;
   import flash.geom.Rectangle;
   import flash.geom.Point;
   import flash.text.TextField;
   import flash.text.TextFormat;
   import flash.display.DisplayObject;
   import flash.display.MovieClip;
   import flash.display.SimpleButton;
   import flash.display.Sprite;
   import flash.events.Event;
   import flash.events.MouseEvent;
   import flash.net.URLLoader;
   import flash.net.URLRequest;
   import manager.PayManager;
   import mvc.controllor.RandomControllor;
   
   public class SelectPLace extends Sprite
   {
      
      public var s1_1:MovieClip;
      
      public var s1_2:MovieClip;
      
      public var s1_3:MovieClip;
      
      public var s2_1:MovieClip;
      
      public var s2_2:MovieClip;
      
      public var s2_3:MovieClip;
      
      public var s3_1:MovieClip;
      
      public var s3_2:MovieClip;
      
      public var s3_3:MovieClip;
      
      public var btnnmg:MovieClip;
      
      public var kls:SimpleButton;
      
      public var llbt:SimpleButton;
      
      public var sssl:MovieClip;
      
      public var flsbtn:MovieClip;
      
      public var sssd:MovieClip;
      
      public var dsgbtn:MovieClip;
      
      public var hdbtn:MovieClip;
      
      public var stzmc:MovieClip;
      
      public var moonmc:MovieClip;
      
      public var dajimc:MovieClip;
      
      public var sunmc:MovieClip;
      
      public var hymc:MovieClip;
      
      public var showfirstmc:MovieClip;
      
      public var yuelaomc:MovieClip;
      
      public var lsxsmc:MovieClip;
      
      public var zshdmc:MovieClip;
      
      private var currentPayPart:String = "";
      
      public var s4_1:MovieClip;
      
      public var s4_2:MovieClip;
      
      public var s4_3:MovieClip;
      
      private var gc:Config;
      
      private var urlLoader:URLLoader;
      
      public var sorrybag:MovieClip;
      
      private var renewalse:RenewalseThisSZ;
      
      public var kaixuemc:SimpleButton;
      
      private static var localActivitiesCollapsed:Boolean = false;
      private var localActivityToggle:Sprite;
      private var localActivityOverlay:Sprite;
      private var localActivityBitmaps:Array = [];
      private var localMenu:MapMenu;
      private static var localSaveIdentity:String="";
      public static function beginNewSave():void
      {
         localSaveIdentity="";
         for(var i:int=0;i<32;i++)localSaveIdentity+=int(Math.random()*16).toString(16);
      }
      public static function useSaveIdentity(data:*):void
      {
         if(!data || (!data.player1_obj && !data.player2_obj))return;
         localSaveIdentity=data && data._localSaveId?String(data._localSaveId):"";
      }
      public static function attachSaveIdentity(data:*):void
      {
         if(localSaveIdentity=="")beginNewSave();
         data._localSaveId=localSaveIdentity;
      }
      private var localGiftCatalog:Object;
      private var localGiftImageCache:Object={};
      private var localGiftImageWaiting:Object={};
      private var localGiftImageOrder:Array=[];
      private var localGiftImageBytes:Number=0;
      private var localGiftHallCards:Sprite;
      private var localGiftHallPager:TextField;
      private var localGiftHallPrevious:Sprite;
      private var localGiftHallNext:Sprite;
      private var localGiftHallGeneration:int=0;
      private var localGiftAutoPreviewTimer:Timer;
      private var localGiftHallPage:int=0;
      private var localGiftCodePage:int=0;
      private var localGiftCodeMatches:Array=[];
      private var localGiftCodePicker:Sprite;
      private var localGiftCodeRows:Sprite;
      private var localGiftCodeSearch:TextField;
      private var localGiftLegacyBefore:Object;
      private var localGiftLegacyAfter:Object;
      private var localGiftActivity:int=0;
      private var localGiftHallArt:Array=[];
      private var localGiftDetailArt:Array=[];
      private var localGiftRewardArt:Array=[];
      private var localGiftRewardIcons:Sprite;
      private var localGiftRewardList:Array=[];
      private var localGiftRewardPage:int=0;
      private var localGiftPanel:Sprite;
      private var localGiftCode:TextField;
      private var localGiftTitle:TextField;
      private var localGiftRewards:TextField;
      private var localGiftStatus:TextField;
      private var localGiftVerify:Sprite;
      private var localGiftClaim:Sprite;
      private var localGiftLoader:URLLoader;
      private var localGiftWatchdog:Timer;
      private var localGiftCommitted:Boolean=false;
      private static var gameLogInstalled:Boolean=false;
      private static var gameLogCount:int=0;

      private var localGiftToken:String = "";
      private var localGiftBusy:Boolean = false;
      private var localGiftUncertain:Boolean = false;
      private var localGiftSlot:int;
      private var localGiftPhase:String = "";
      private var localGiftSnapshot:Object;
      private var localStarIdle:Bitmap;
      private var localStarIdleData:BitmapData;

      public function SelectPLace()
      {
         super();
         gc = Config.getInstance();
         this.addEventListener("addedToStage",added,false,0,true);
         this.addEventListener("removedFromStage",removed,false,0,true);
         renewalse = new RenewalseThisSZ();
         renewalse.name = "renewalseThisSZ";
         this.tabChildren = false;
         this.tabEnabled = false;
         this.init();
         kaixuemc.visible = false;
      }
      
      private function init() : void
      {
         var i:* = 0;
         var j:* = 0;
         if(!gc.isHideDebug)
         {
            gc.curBigStage = 4;
         }
         for(i = 0; i < 3; )
         {
            for(j = 0; j < 3; )
            {
               this["s" + (i + 1) + "_" + (j + 1)].buttonMode = true;
               j++;
            }
            i++;
         }
      }
      
      private function added(e:Event) : void
      {
         this.installGameLog();
         var i:* = 0;
         var k:* = 0;
         var j:* = 0;
         i = 0;
         var dio:DisplayObject = null;
         if(!gc.hasGetMoney)
         {
            if(GMain.serviceHold)
            {
               GMain.serviceHold.getBalance();
            }
         }
         gc.eventManger.addEventListener("BuySuccess",paymentSuccess);
         gc.eventManger.addEventListener("BuyFail",paymentFail);
         var maxStage:int = int(gc.curBigStage);
         if(maxStage >= 20)
         {
            maxStage = 4;
         }
         if(gc.curBigStage >= 2 || gc.curBigStage == 1 && gc.curBigLevel > 1)
         {
            this.showfirstmc.visible = false;
         }
         for(i = 0; i < maxStage; )
         {
            k = uint(i + 1);
            if(k < maxStage)
            {
               k = 3;
            }
            else
            {
               k = gc.curBigLevel;
            }
            for(j = 0; j < k; )
            {
               this["s" + (i + 1) + "_" + (j + 1)].addEventListener("click",onSelected);
               this["s" + (i + 1) + "_" + (j + 1)].addEventListener("rollOver",mOver);
               this["s" + (i + 1) + "_" + (j + 1)].addEventListener("rollOut",mOut);
               j++;
            }
            i++;
         }
         this["s" + maxStage + "_" + gc.curBigLevel].gotoAndStop(2);
         if(gc.playerGuide.checkPlayerIsAttach(1))
         {
            btnnmg.buttonMode = true;
            btnnmg.addEventListener("click",nmgClick);
            btnnmg.addEventListener("rollOver",mOver);
            btnnmg.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(1))
            {
               this.flashMovieClip(btnnmg);
               gc.playerGuide.setNoNeedToFlash(1);
            }
         }
         if(gc.playerGuide.checkPlayerIsAttach(2))
         {
            sssl.buttonMode = true;
            sssl.addEventListener("click",ssslClick);
            sssl.addEventListener("rollOver",mOver);
            sssl.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(2))
            {
               this.flashMovieClip(sssl);
               gc.playerGuide.setNoNeedToFlash(2);
            }
         }
         if(gc.playerGuide.checkPlayerIsAttach(5))
         {
            flsbtn.buttonMode = true;
            flsbtn.addEventListener("click",fulClick);
            flsbtn.addEventListener("rollOver",mOver);
            flsbtn.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(5))
            {
               this.flashMovieClip(flsbtn);
               gc.playerGuide.setNoNeedToFlash(5);
            }
         }
         if(gc.playerGuide.checkPlayerIsAttach(9))
         {
            sssd.buttonMode = true;
            sssd.addEventListener("click",sssdClick);
            sssd.addEventListener("rollOver",mOver);
            sssd.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(9))
            {
               this.flashMovieClip(sssd);
               gc.playerGuide.setNoNeedToFlash(9);
            }
         }
         if(gc.playerGuide.checkPlayerIsAttach(4))
         {
            dsgbtn.buttonMode = true;
            dsgbtn.addEventListener("click",dsgClick);
            dsgbtn.addEventListener("rollOver",mOver);
            dsgbtn.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(4))
            {
               this.flashMovieClip(dsgbtn);
               gc.playerGuide.setNoNeedToFlash(4);
            }
         }
         if(gc.playerGuide.checkPlayerIsAttach(7))
         {
            stzmc.buttonMode = true;
            stzmc.addEventListener("click",stzClick);
            stzmc.addEventListener("rollOver",mOver);
            stzmc.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(7))
            {
               this.flashMovieClip(stzmc);
               gc.playerGuide.setNoNeedToFlash(7);
            }
         }
         moonmc.gotoAndStop(2);
         moonmc.buttonMode = true;
         moonmc.addEventListener("click",moonClick);
         moonmc.addEventListener("rollOver",mOver);
         moonmc.addEventListener("rollOut",mOut);
         if(gc.dataObject.kaixuelibao2016 == false || gc.dataObject.kaixuelibao2016 == "false")
         {
            kaixuemc.visible = true;
            kaixuemc.addEventListener("click",kaixueClick);
         }
         sunmc.gotoAndStop(2);
         sunmc.buttonMode = true;
         sunmc.addEventListener("click",sunClick);
         sunmc.addEventListener("rollOver",mOver);
         sunmc.addEventListener("rollOut",mOut);
         dajimc.gotoAndStop(2);
         dajimc.buttonMode = true;
         dajimc.addEventListener("click",changeDaJi);
         dajimc.addEventListener("rollOver",mOver);
         dajimc.addEventListener("rollOut",mOut);
         lsxsmc.gotoAndStop(2);
         lsxsmc.buttonMode = true;
         lsxsmc.addEventListener("click",changeLsxs);
         lsxsmc.addEventListener("rollOver",mOver);
         lsxsmc.addEventListener("rollOut",mOut);
         zshdmc.gotoAndStop(2);
         zshdmc.addEventListener("click",ziShuActivity);
         zshdmc.addEventListener("rollOver",mOver);
         zshdmc.addEventListener("rollOut",mOut);
         zshdmc.visible = false;
         hymc.gotoAndStop(2);
         hymc.buttonMode = true;
         hymc.addEventListener("click",hyClick);
         hymc.addEventListener("rollOver",hyMOver);
         hymc.addEventListener("rollOut",mOut);
         yuelaomc.gotoAndStop(2);
         yuelaomc.buttonMode = true;
         yuelaomc.addEventListener("click",yuelaoClick);
         yuelaomc.addEventListener("rollOver",ylMOver);
         yuelaomc.addEventListener("rollOut",mOut);
         if(gc.playerGuide.checkPlayerIsAttach(8))
         {
            hdbtn.buttonMode = true;
            hdbtn.addEventListener("click",newWorldClick);
            hdbtn.addEventListener("rollOver",mOver);
            hdbtn.addEventListener("rollOut",mOut);
            if(gc.playerGuide.checkIsNeedToFlash(8))
            {
               this.flashMovieClip(hdbtn);
               gc.playerGuide.setNoNeedToFlash(8);
            }
         }
         kls.enabled = false;
         if(gc.playerGuide.checkPlayerIsAttach(6))
         {
            kls.enabled = true;
            kls.addEventListener("click",inToKunLunMountains);
            if(gc.playerGuide.checkIsNeedToFlash(6))
            {
               this.flashMovieClip(kls);
               gc.playerGuide.setNoNeedToFlash(6);
            }
         }
         llbt.enabled = false;
         if(gc.playerGuide.checkPlayerIsAttach(3))
         {
            llbt.enabled = true;
            llbt.addEventListener("click",inToLingLongTower);
            if(gc.playerGuide.checkIsNeedToFlash(3))
            {
               this.flashMovieClip(llbt);
               gc.playerGuide.setNoNeedToFlash(3);
            }
         }
         var dary:Array = gc.dataObjectInstance.curDate.split("-");
         if(dary[1] == "01")
         {
            if(int(dary[2]) > 22)
            {
               sorrybag.visible = false;
            }
            else
            {
               sorrybag.visible = true;
               if(gc.hasgetsorry == 0 || gc.hasgetsorry == 1)
               {
                  sorrybag.buttonMode = true;
                  sorrybag.addEventListener("click",sorryClick);
               }
               else
               {
                  sorrybag.visible = false;
               }
            }
         }
         else
         {
            sorrybag.visible = false;
         }
         for(i = 0; i < this.numChildren; )
         {
            dio = this.getChildAt(i);
            if(dio is MapMenu)
            {
               this.removeChild(dio);
            }
            i++;
         }
         var menu:MapMenu = new MapMenu();
         menu.x = -1;
         menu.y = 0;
         this.addChild(menu);
         this.localMenu = menu;
         this.initLocalActivityControls();
         gc.eventManger.addEventListener("ShowOtherScene",showOtherScene);
         gc.eventManger.addEventListener("ChanllengeBossInster",chanllenge);
         gc.isintheselectmap = true;
         gc.eventManger.addEventListener("SAVESEVENTSUCCESS",checkGoToNewGuide);
         if(gc.isHideDebug)
         {
         }
         BaseBitmapDataPool.removeScoketOtherHeroData();
      }
      
      private function flashMovieClip(mc:DisplayObject) : void
      {
         var guidemc:MovieClip = AUtils.getNewObj("yellowArrow") as MovieClip;
         guidemc.x = mc.x;
         guidemc.y = mc.y - 100;
         this.addChild(guidemc);
      }
      
      protected function checkGoToNewGuide(evt:CommonEvent) : void
      {
         if(gc.isGoToNewGuideStage)
         {
            this.gotoNewGuide();
         }
      }
      
      private function removed(e:Event) : void
      {
         this.localGiftBusy = false;this.localGiftUncertain = false;this.closeLocalGiftExchange();
         this.closeLocalActivities();
         for each(var icon:MovieClip in [moonmc,sunmc,dajimc,lsxsmc])
         {
            icon.removeEventListener("mouseOver",this.localActivityHoverIn);
            icon.removeEventListener("mouseOut",this.localActivityHoverOut);
         }
         if(this.localStarIdle && this.localStarIdle.parent) this.localStarIdle.parent.removeChild(this.localStarIdle);
         if(this.localStarIdleData) this.localStarIdleData.dispose();
         this.localStarIdle = null;
         this.localStarIdleData = null;
         if(this.localActivityToggle){this.localActivityToggle.removeEventListener("click",this.toggleLocalActivities);this.localActivityToggle.removeEventListener("rollOver",this.localToggleOver);this.localActivityToggle.removeEventListener("rollOut",this.localToggleOut);}
         var i:* = 0;
         var j:* = 0;
         for(i = 0; i < 3; )
         {
            for(j = 0; j < 3; )
            {
               this["s" + (i + 1) + "_" + (j + 1)].removeEventListener("click",onSelected);
               this["s" + (i + 1) + "_" + (j + 1)].removeEventListener("rollOver",mOver);
               this["s" + (i + 1) + "_" + (j + 1)].removeEventListener("rollOut",mOut);
               j++;
            }
            i++;
         }
         gc.eventManger.removeEventListener("BuySuccess",paymentSuccess);
         gc.eventManger.removeEventListener("BuyFail",paymentFail);
         renewalse = null;
         btnnmg.removeEventListener("click",nmgClick);
         btnnmg.removeEventListener("rollOver",mOver);
         btnnmg.removeEventListener("rollOut",mOut);
         sssl.removeEventListener("click",ssslClick);
         sssl.removeEventListener("rollOver",mOver);
         sssl.removeEventListener("rollOut",mOut);
         flsbtn.removeEventListener("click",fulClick);
         flsbtn.removeEventListener("rollOver",mOver);
         flsbtn.removeEventListener("rollOut",mOut);
         sssd.removeEventListener("click",sssdClick);
         sssd.removeEventListener("rollOver",mOver);
         sssd.removeEventListener("rollOut",mOut);
         kls.removeEventListener("click",inToKunLunMountains);
         llbt.removeEventListener("click",inToLingLongTower);
         sorrybag.removeEventListener("click",sorryClick);
         stzmc.removeEventListener("click",stzClick);
         stzmc.removeEventListener("rollOver",mOver);
         stzmc.removeEventListener("rollOut",mOut);
         dsgbtn.removeEventListener("click",dsgClick);
         dsgbtn.removeEventListener("rollOver",mOver);
         dsgbtn.removeEventListener("rollOut",mOut);
         moonmc.removeEventListener("click",moonClick);
         moonmc.removeEventListener("rollOver",mOver);
         moonmc.removeEventListener("rollOut",mOut);
         kaixuemc.removeEventListener("click",kaixueClick);
         sunmc.removeEventListener("click",sunClick);
         sunmc.removeEventListener("rollOver",mOver);
         sunmc.removeEventListener("rollOut",mOut);
         dajimc.removeEventListener("click",changeDaJi);
         dajimc.removeEventListener("rollOver",mOver);
         dajimc.removeEventListener("rollOut",mOut);
         lsxsmc.removeEventListener("click",changeLsxs);
         lsxsmc.removeEventListener("rollOver",mOver);
         lsxsmc.removeEventListener("rollOut",mOut);
         hymc.removeEventListener("click",hyClick);
         hymc.removeEventListener("rollOver",hyMOver);
         hymc.removeEventListener("rollOut",mOut);
         yuelaomc.removeEventListener("click",yuelaoClick);
         yuelaomc.removeEventListener("rollOver",ylMOver);
         yuelaomc.removeEventListener("rollOut",mOut);
         gc.eventManger.removeEventListener("ShowOtherScene",showOtherScene);
         gc.eventManger.removeEventListener("ChanllengeBossInster",chanllenge);
         gc.eventManger.removeEventListener("SAVESEVENTSUCCESS",checkGoToNewGuide);
         gc.isintheselectmap = false;
      }
      
      private function kaixueClick(e:MouseEvent) : void
      {
         if(gc.dataObject.kaixuelibao2016 == false || gc.dataObject.kaixuelibao2016 == "false")
         {
            gc.dataObject.kaixuelibao2016 = true;
            if(gc.player1.roleid > 0)
            {
               gc.putQhsInBackPack(gc.player1,"wpqhs1",9);
               gc.putQhsInBackPack(gc.player1,"wpxt",999);
               gc.player1.zblist.push(gc.allEquip.findByName("_4399zswj"));
            }
            if(gc.player2.roleid > 0)
            {
               gc.putQhsInBackPack(gc.player2,"wpqhs1",9);
               gc.putQhsInBackPack(gc.player2,"wpxt",999);
               gc.player2.zblist.push(gc.allEquip.findByName("_4399zswj"));
            }
            kaixuemc.visible = false;
            gc.ts.setTxt("恭喜您获得 强化石x9， 玄铁x999， 忠实玩家头衔x1");
            gc.stage.addChild(gc.ts);
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
      }
      
      private function gotoNewGuide() : void
      {
         gc.isGoToNewGuideStage = false;
         gc.curStageAndCurLevel.curStage = 49;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function hostClick() : void
      {
         gc.curStageAndCurLevel.curStage = 1;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function mOver(e:MouseEvent) : void
      {
         e.currentTarget.gotoAndStop(3);
      }
      
      private function hyMOver(e:MouseEvent) : void
      {
         if(int(gc.dataObject.houyifb) == 0)
         {
            e.currentTarget.gotoAndStop(4);
         }
         else
         {
            e.currentTarget.gotoAndStop(3);
         }
      }
      
      private function ylMOver(e:MouseEvent) : void
      {
         if(int(gc.dataObject.yuelaoFB) == 0)
         {
            e.currentTarget.gotoAndStop(4);
         }
         else
         {
            e.currentTarget.gotoAndStop(3);
         }
      }
      
      private function mOut(e:MouseEvent) : void
      {
         var maxStage:int = int(gc.curBigStage);
         if(maxStage >= 20)
         {
            maxStage = 4;
         }
         if(e.currentTarget != this["s" + maxStage + "_" + gc.curBigLevel])
         {
            e.currentTarget.gotoAndStop(1);
         }
         else
         {
            e.currentTarget.gotoAndStop(2);
         }
      }
      
      private function onSelected(e:MouseEvent) : void
      {
         gc.curStageAndCurLevel.curStage = int(String(e.currentTarget.name).substr(1,1));
         gc.curStageAndCurLevel.curLevel = int(String(e.currentTarget.name).substr(3,1));
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      public function doAfterChangeIn() : void
      {
         this.visible = true;
      }
      
      private function submitScore(e:*) : void
      {
         gc.ts.setTxt("暂未开启");
         gc.stage.addChild(gc.ts);
      }
      
      private function sorryClick(e:MouseEvent) : void
      {
         if(gc.hasgetsorry == 0 || gc.hasgetsorry == 1)
         {
            gc.hasgetsorry = 2;
            if(gc.player1.roleid > 0)
            {
               gc.putQhsInBackPack(gc.player1,"mpyj");
               gc.putQhsInBackPack(gc.player1,"mpyj");
               gc.putQhsInBackPack(gc.player1,"wpcsd");
               gc.putQhsInBackPack(gc.player1,"wpcsd");
               gc.putQhsInBackPack(gc.player1,"wphhd");
               gc.putQhsInBackPack(gc.player1,"wphhd");
               gc.putQhsInBackPack(gc.player1,"wpqhs2");
               gc.putQhsInBackPack(gc.player1,"wpqhs2");
               gc.putQhsInBackPack(gc.player1,"wpqhs2");
            }
            if(gc.player2.roleid > 0)
            {
               gc.putQhsInBackPack(gc.player2,"mpyj");
               gc.putQhsInBackPack(gc.player2,"mpyj");
               gc.putQhsInBackPack(gc.player2,"wpcsd");
               gc.putQhsInBackPack(gc.player2,"wpcsd");
               gc.putQhsInBackPack(gc.player2,"wphhd");
               gc.putQhsInBackPack(gc.player2,"wphhd");
               gc.putQhsInBackPack(gc.player2,"wpqhs2");
               gc.putQhsInBackPack(gc.player2,"wpqhs2");
               gc.putQhsInBackPack(gc.player2,"wpqhs2");
            }
            gc.ts.setTxt("领取成功，已经放入您的背包");
            gc.stage.addChild(gc.ts);
            this.sorrybag.visible = false;
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
      }
      
      private function inToKunLunMountains(e:MouseEvent) : void
      {
         kls.removeEventListener("click",inToKunLunMountains);
         gc.curStageAndCurLevel.curStage = 41;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function inToLingLongTower(e:MouseEvent) : void
      {
         if(gc.curBigStage < 4)
         {
            gc.ts.setTxt("请先通过凌霄宝殿的考验！");
            gc.stage.addChild(gc.ts);
            return;
         }
         llbt.removeEventListener("click",inToLingLongTower);
         gc.gameinwhere = "玲珑宝塔";
         GMain.getInstance().switchSence("OpenAnimation");
      }
      
      private function nmgClick(e:MouseEvent) : void
      {
         var lshChanllenge:LiuShaHeChanllengeBossAssets = new LiuShaHeChanllengeBossAssets();
         this.addChild(lshChanllenge);
      }
      
      private function chanllenge(e:CommonEvent) : void
      {
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function showOtherScene(e:CommonEvent) : void
      {
         trace("remove--selectplace");
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function ssslClick(e:MouseEvent) : void
      {
         sssl.removeEventListener("click",ssslClick);
         gc.curStageAndCurLevel.curStage = 9;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function fulClick(e:MouseEvent) : void
      {
         var fulushou:Fulushou = new Fulushou();
         this.addChild(fulushou);
      }
      
      private function sssdClick(e:MouseEvent) : void
      {
         var mysteriousshop:MysteriousShopAssets = new MysteriousShopAssets();
         this.addChild(mysteriousshop);
      }
      
      private function dsgClick(e:MouseEvent) : void
      {
         if(gc.playNum == 2)
         {
            if(gc.player1.getCurLevel() < 38 || gc.player2.getCurLevel() < 38)
            {
               gc.ts.setTxt("玩家的等级必须都大于38级才可以进入");
               gc.stage.addChild(gc.ts);
               return;
            }
         }
         else if(gc.player1.getCurLevel() < 38)
         {
            gc.ts.setTxt("玩家的等级必须都大于38级才可以进入");
            gc.stage.addChild(gc.ts);
            return;
         }
         dsgbtn.removeEventListener("click",dsgClick);
         gc.curStageAndCurLevel.curStage = 12;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
      }
      
      private function stzClick(e:MouseEvent) : void
      {
         if(gc.playNum == 2)
         {
            if(gc.player1.getCurLevel() < 20 || gc.player2.getCurLevel() < 20)
            {
               gc.ts.setTxt("玩家的等级必须都大于20级才可以进入");
               gc.stage.addChild(gc.ts);
               return;
            }
         }
         else if(gc.player1.getCurLevel() < 20)
         {
            gc.ts.setTxt("玩家的等级必须都大于20级才可以进入");
            gc.stage.addChild(gc.ts);
            return;
         }
         var tb:ThreeBrothers = new ThreeBrothers();
         this.addChild(tb);
      }
      
      private function moonClick(e:MouseEvent) : void
      {
         var mai:MidAutumnInterfaceAssets = new MidAutumnInterfaceAssets();
         this.addChild(mai);
      }
      
      private function sunClick(e:MouseEvent) : void
      {
         if(int(gc.dataObject.houyifb2) <= 0)
         {
            this.currentPayPart = "houyiFuben";
            this.addRenewalseMask("您确定花200点券再进一次吗？");
            return;
         }
         this.goIntoHouYiSheRi();
      }
      
      private function goIntoHouYiSheRi() : void
      {
         gc.dataObject.houyifb2--;
         gc.stage531Type = 1;
         gc.curStageAndCurLevel.curStage = 53;
         gc.curStageAndCurLevel.curLevel = 1;
         gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         if(this.parent)
         {
            this.parent.removeChild(this);
         }
         if(gc.isHideDebug)
         {
            gc.memory.setStorage(gc.luanqibazaojiami.saveId);
         }
      }
      
      private function hyClick(e:MouseEvent) : void
      {
         if(int(gc.dataObject.houyifb) == 0)
         {
            if(gc.dataObject.houyiTimes > 0)
            {
               gc.ts.setTxt("后羿已助战，还有" + gc.dataObject.houyiTimes + "次，赶紧挑战吧~");
               gc.stage.addChild(gc.ts);
            }
            else
            {
               this.currentPayPart = "houyiTimes";
               this.addRenewalseMask("您确定花300点券邀请后羿助战吗？");
            }
         }
         else
         {
            hymc.removeEventListener("click",hyClick);
            gc.curStageAndCurLevel.curStage = 53;
            gc.curStageAndCurLevel.curLevel = 1;
            gc.stage531Type = 0;
            gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
            if(this.parent)
            {
               this.parent.removeChild(this);
            }
         }
      }
      
      private function yuelaoClick(e:MouseEvent) : void
      {
         if(int(gc.dataObject.yuelaoFB) == 0)
         {
            if(gc.dataObject.yuelaoTimes > 0)
            {
               gc.ts.setTxt("月老已助战，还有" + gc.dataObject.yuelaoTimes + "次，赶紧挑战吧~");
               gc.stage.addChild(gc.ts);
            }
            else
            {
               this.currentPayPart = "yeulaoTimes";
               this.addRenewalseMask("您确定花300点券邀请月老助战吗？");
            }
         }
         else
         {
            yuelaomc.removeEventListener("click",yuelaoClick);
            gc.curStageAndCurLevel.curStage = 58;
            gc.curStageAndCurLevel.curLevel = 1;
            gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
            if(this.parent)
            {
               this.parent.removeChild(this);
            }
         }
      }
      
      private function ziShuActivity(e:MouseEvent) : void
      {
         gc.openUrl("http://127.0.0.1:43999/_a/0002ryyyyyyyyyyyyy");
      }
      
      private function changeLsxs(e:MouseEvent) : void
      {
         if(gc.dataObject.lsxxFB >= 1)
         {
            gc.dataObject.lsxxFB--;
            gc.curStageAndCurLevel.curStage = 65;
            gc.curStageAndCurLevel.curLevel = 1;
            gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
            if(this.parent)
            {
               this.parent.removeChild(this);
            }
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
         else
         {
            gc.ts.setTxt("您今天已经挑战过该副本");
            gc.stage.addChild(gc.ts);
         }
      }
      
      private function changeDaJi(e:MouseEvent) : void
      {
         var arr:Array = null;
         if(gc.dataObject.dajiFB >= 1)
         {
            gc.dataObject.dajiFB--;
            arr = [42,47,60];
            gc.guoqingMonsterId = arr[int(RandomControllor.getIns().getRandom() * arr.length)];
            gc.stage461Type = 0;
            gc.curStageAndCurLevel.curStage = 46;
            gc.curStageAndCurLevel.curLevel = 1;
            gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
            if(this.parent)
            {
               this.parent.removeChild(this);
            }
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
         else
         {
            gc.ts.setTxt("您今天已经挑战过该副本");
            gc.stage.addChild(gc.ts);
         }
      }
      
      private function newWorldClick(e:MouseEvent) : void
      {
         var aned:HundunDoor = new HundunDoor();
         this.addChild(aned);
      }
      
      public function addRenewalseMask(inst:String) : void
      {
         renewalse["txt"].text = inst;
         this.addChild(renewalse);
         renewalse["okbtn"].addEventListener("click",renewalseOk);
         renewalse["nobtn"].addEventListener("click",renewalseChange);
      }
      
      private function renewalseOk(e:MouseEvent) : void
      {
         renewalse["okbtn"].removeEventListener("click",renewalseOk);
         renewalse["nobtn"].removeEventListener("click",renewalseChange);
         var enough:Boolean = true;
         var __pay:int = int(HeroSkillProtect.getInstance().payConst.yibai);
         if(this.currentPayPart == "houyiTimes" || this.currentPayPart == "yeulaoTimes")
         {
            __pay = int(HeroSkillProtect.getInstance().payConst.sanbai);
         }
         else if(this.currentPayPart == "changemoon" || this.currentPayPart == "houyiFuben")
         {
            __pay = int(HeroSkillProtect.getInstance().payConst.erbai);
         }
         if(gc.gameVip.dataObject.lastMoney < __pay)
         {
            gc.ts.setTxt("账户余额不足");
            gc.stage.addChild(gc.ts);
            enough = false;
         }
         gc.pay.money = __pay;
         if(enough)
         {
            if(GMain.serviceHold)
            {
               PayManager.getInstance().pay(23,"道具23",gc.pay.money,1);
               gc.lastPaymentString = "SelectPlace";
               gc.addActionMask("renewalse");
            }
         }
         if(renewalse && this.contains(renewalse))
         {
            this.removeChild(renewalse);
         }
      }
      
      private function renewalseChange(e:MouseEvent) : void
      {
         renewalse["okbtn"].removeEventListener("click",renewalseOk);
         renewalse["nobtn"].removeEventListener("click",renewalseChange);
         if(renewalse && this.contains(renewalse))
         {
            this.removeChild(renewalse);
         }
      }
      
      private function paymentSuccess(e:CommonEvent) : void
      {
         if(gc.lastPaymentString != "SelectPlace")
         {
            return;
         }
         var exictmask:Sprite = gc.stage.getChildByName("execuitionActionrenewalse") as Sprite;
         if(exictmask)
         {
            gc.stage.removeChild(exictmask);
            exictmask = null;
         }
         gc.gameVip.dataObject.lastMoney = e.data[0];
         gc.lastPaymentString = "";
         if(this.currentPayPart == "houyiTimes")
         {
            gc.dataObject.houyiTimes++;
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
         else if(this.currentPayPart == "yeulaoTimes")
         {
            gc.dataObject.yuelaoTimes++;
            if(gc.isHideDebug)
            {
               gc.memory.setStorage(gc.luanqibazaojiami.saveId);
            }
         }
         else if(this.currentPayPart == "changemoon")
         {
            gc.curStageAndCurLevel.curStage = 46;
            gc.curStageAndCurLevel.curLevel = 1;
            gc.stage461Type = 1;
            gc.guoqingMonsterId = 130;
            gc.eventManger.dispatchEvent(new CommonEvent("selectStageOver"));
         }
         else if(this.currentPayPart == "houyiFuben")
         {
            gc.dataObject.houyifb2++;
            this.goIntoHouYiSheRi();
         }
      }
      
      private function paymentFail(e:CommonEvent) : void
      {
         var exictmask:Sprite = gc.stage.getChildByName("execuitionActionrenewalse") as Sprite;
         if(exictmask)
         {
            gc.stage.removeChild(exictmask);
            exictmask = null;
         }
      }
      private function localText(value:String,size:int,color:uint,bold:Boolean,width:Number,height:Number) : TextField
      {
         var field:TextField = new TextField();
         field.defaultTextFormat = new TextFormat("Microsoft YaHei",size,color,bold);
         field.text = value;
         field.width = width;
         field.height = height;
         field.wordWrap = true;
         field.selectable = false;
         field.mouseEnabled = false;
         return field;
      }

      private function initLocalActivityControls() : void
      {
         if(!this.localActivityToggle)
         {
            this.localActivityToggle = new Sprite();
            this.localActivityToggle.name = "offlineActivitiesToggle";
            this.localActivityToggle.x = 7;
            this.localActivityToggle.y = 3;
            this.localActivityToggle.buttonMode = true;
            this.localActivityToggle.mouseChildren = false;
            this.addChild(this.localActivityToggle);
            moonmc.y += 10;
            sunmc.y += 10;
            dajimc.y += 10;
            lsxsmc.y += 10;
         }
         this.initStarIdleIcon();
         this.localActivityToggle.addEventListener("click",this.toggleLocalActivities);
         this.localActivityToggle.addEventListener("rollOver",this.localToggleOver);
         this.localActivityToggle.addEventListener("rollOut",this.localToggleOut);
         this.refreshLocalActivityControls();
      }

      private function refreshLocalActivityControls() : void
      {
         for each(var icon:MovieClip in [moonmc,sunmc,dajimc,lsxsmc]) icon.gotoAndStop(1);
         this.setStarIdle(true);
         moonmc.visible = !localActivitiesCollapsed;
         sunmc.visible = !localActivitiesCollapsed;
         dajimc.visible = !localActivitiesCollapsed;
         lsxsmc.visible = !localActivitiesCollapsed;
         while(this.localActivityToggle.numChildren > 0) this.localActivityToggle.removeChildAt(0);
         var ink:Sprite = this.localActivityToggle;
         ink.graphics.clear();
         ink.graphics.lineStyle(1,0x9E6A28,.8);
         ink.graphics.beginFill(0x1A120A,.94);
         ink.graphics.moveTo(3,9);ink.graphics.lineTo(11,5);ink.graphics.lineTo(22,6);ink.graphics.lineTo(30,3);
         ink.graphics.lineTo(98,4);ink.graphics.lineTo(111,7);ink.graphics.lineTo(121,6);ink.graphics.lineTo(117,13);
         ink.graphics.lineTo(123,17);ink.graphics.lineTo(116,22);ink.graphics.lineTo(119,28);ink.graphics.lineTo(106,29);
         ink.graphics.lineTo(99,32);ink.graphics.lineTo(24,31);ink.graphics.lineTo(13,33);ink.graphics.lineTo(5,28);
         ink.graphics.lineTo(9,22);ink.graphics.lineTo(2,20);ink.graphics.lineTo(7,15);ink.graphics.lineTo(3,9);ink.graphics.endFill();
         ink.graphics.lineStyle(2,0x50351A,.8);ink.graphics.moveTo(0,13);ink.graphics.lineTo(16,9);ink.graphics.moveTo(106,30);ink.graphics.lineTo(123,27);
         ink.graphics.lineStyle(3,0xF8CE69,1);
         var arrow:Number = localActivitiesCollapsed ? 106 : 112;
         var point:Number = localActivitiesCollapsed ? 113 : 105;
         ink.graphics.moveTo(arrow,12);ink.graphics.lineTo(point,18);ink.graphics.lineTo(arrow,24);
         var title:TextField = new TextField();
         title.defaultTextFormat=new TextFormat("FZCuYuan-M03S",17,0xFFD56B,false,null,null,null,null,"center");
         title.embedFonts=true;title.selectable=false;title.mouseEnabled=false;title.width=94;title.height=29;
         title.text=localActivitiesCollapsed ? "打开活动" : "收起活动";
         title.x=7;title.y=(35-title.textHeight)*.5-2;
         title.filters=[new GlowFilter(0x140700,1,3,3,8,1),new GlowFilter(0xFFF0BA,.7,1,1,1,1)];
         ink.addChild(title);
      }

      private function localToggleOver(e:MouseEvent):void
      {
         this.localActivityToggle.filters=[new GlowFilter(0xFFD56B,.8,6,6,1.4,1)];
      }

      private function localToggleOut(e:MouseEvent):void
      {
         this.localActivityToggle.filters=[];
      }

      private function toggleLocalActivities(e:MouseEvent) : void
      {
         e.stopPropagation();
         localActivitiesCollapsed = !localActivitiesCollapsed;
         this.refreshLocalActivityControls();
         trace("[offline-activities] folded=" + localActivitiesCollapsed);
      }

      private function initStarIdleIcon() : void
      {
         if(!this.localStarIdleData)
         {
            // The original idle bitmap includes its caption. Keep its upper
            // orb pixels and reflect the clean upper cap below the equator,
            // preserving a round silhouette instead of cropping the circle.
            lsxsmc.gotoAndStop(1);
            var original:BitmapData = new BitmapData(156,97,true,0);
            original.draw(lsxsmc);
            this.localStarIdleData = original.clone();
            this.localStarIdleData.fillRect(new Rectangle(0,64,156,33),0);
            for(var y:int = 64;y < 90;y++)
               this.localStarIdleData.copyPixels(original,new Rectangle(0,90-y,156,1),new Point(0,y));
            original.dispose();
            this.localStarIdle = new Bitmap(this.localStarIdleData,"auto",true);
            this.localStarIdle.name = "offlineStarIconOnly";
            lsxsmc.addChild(this.localStarIdle);
         }
         for each(var icon:MovieClip in [moonmc,sunmc,dajimc,lsxsmc])
         {
            icon.removeEventListener("rollOver",mOver);
            icon.removeEventListener("rollOut",mOut);
            icon.mouseChildren = false;
            icon.addEventListener("mouseOver",this.localActivityHoverIn);
            icon.addEventListener("mouseOut",this.localActivityHoverOut);
         }
         this.setStarIdle(true);
      }

      private function localActivityHoverIn(e:MouseEvent) : void
      {
         for each(var icon:MovieClip in [moonmc,sunmc,dajimc,lsxsmc])
            if(icon != e.currentTarget) icon.gotoAndStop(1);
         MovieClip(e.currentTarget).gotoAndStop(3);
         this.setStarIdle(e.currentTarget != lsxsmc);
      }

      private function localActivityHoverOut(e:MouseEvent) : void
      {
         MovieClip(e.currentTarget).gotoAndStop(1);
         if(e.currentTarget == lsxsmc) this.setStarIdle(true);
      }

      private function setStarIdle(idle:Boolean) : void
      {
         if(!this.localStarIdle) return;
         for(var i:int = 0;i < lsxsmc.numChildren;i++)
         {
            var child:DisplayObject = lsxsmc.getChildAt(i);
            child.visible = child == this.localStarIdle ? idle : !idle;
         }
      }

      private function giftButton(label:String,xpos:Number,ypos:Number,width:Number,handler:Function,primary:Boolean = false) : Sprite
      {
         var button:Sprite = new Sprite();button.x=xpos;button.y=ypos;
         if(label=="×")
         {
            return this.giftCloseButton(xpos,ypos,handler);
         }
         var matrix:Matrix=new Matrix();matrix.createGradientBox(width,39,Math.PI/2);
         button.graphics.lineStyle(2,0x442007);button.graphics.beginGradientFill("linear",[0xFFF4AD,0xFFC127,0xEF8B08],[1,1,1],[0,155,255],matrix);
         button.graphics.drawRoundRect(0,0,width,39,12,12);button.graphics.endFill();
         button.buttonMode=true;button.mouseChildren=false;
         var text:TextField=this.giftText(label,18,0xFFFFFF,width,39);text.wordWrap=false;this.placeGiftText(text,0,0,width,39);button.addChild(text);
         button.addEventListener(MouseEvent.CLICK,handler);
         button.addEventListener(MouseEvent.ROLL_OVER,function(e:MouseEvent):void{Sprite(e.currentTarget).alpha=.84;});
         button.addEventListener(MouseEvent.ROLL_OUT,function(e:MouseEvent):void{Sprite(e.currentTarget).alpha=1;});
         return button;
      }

      private function giftCloseButton(xpos:Number,ypos:Number,handler:Function) : Sprite
      {
         var button:Sprite=new Sprite();button.name="giftCloseButton";button.x=xpos+26;button.y=ypos+26;
         button.buttonMode=true;button.useHandCursor=true;button.mouseChildren=false;
         var hit:Sprite=new Sprite();hit.graphics.beginFill(0,0);hit.graphics.drawRect(-32,-32,64,64);hit.graphics.endFill();
         hit.visible=false;hit.mouseEnabled=false;button.addChild(hit);button.hitArea=hit;
         var face:Sprite=new Sprite();face.mouseEnabled=false;button.addChild(face);
         var matrix:Matrix=new Matrix();matrix.createGradientBox(52,52,Math.PI/2,-26,-26);
         face.graphics.lineStyle(2,0x602A08);face.graphics.beginGradientFill("linear",[0xFFF7B5,0xFFD64A,0xF3A019],[1,1,1],[0,150,255],matrix);
         face.graphics.drawRoundRect(-26,-26,52,52,15,15);face.graphics.endFill();
         face.graphics.lineStyle(1,0xFFF5B0);face.graphics.drawRoundRect(-23,-23,46,46,11,11);
         face.graphics.lineStyle(9,0x652008,1,false,"normal","round","round");
         face.graphics.moveTo(-10,-10);face.graphics.lineTo(10,10);face.graphics.moveTo(10,-10);face.graphics.lineTo(-10,10);
         face.graphics.lineStyle(5,0xF32D19,1,false,"normal","round","round");
         face.graphics.moveTo(-10,-10);face.graphics.lineTo(10,10);face.graphics.moveTo(10,-10);face.graphics.lineTo(-10,10);
         var change:Function=function(scale:Number,highlight:Boolean):void
         {
            face.scaleX=face.scaleY=scale;
            face.filters=highlight?[new GlowFilter(0xFFF18A,.95,10,10,2)]:[];
         };
         button.addEventListener(MouseEvent.ROLL_OVER,function(e:MouseEvent):void{change(1.14,true);});
         button.addEventListener(MouseEvent.ROLL_OUT,function(e:MouseEvent):void{change(1,false);});
         button.addEventListener(MouseEvent.MOUSE_DOWN,function(e:MouseEvent):void{change(.96,true);});
         button.addEventListener(MouseEvent.MOUSE_UP,function(e:MouseEvent):void{change(1.14,true);});
         button.addEventListener(MouseEvent.CLICK,handler);return button;
      }

      private function giftText(label:String,size:int,color:uint,width:Number,height:Number) : TextField
      {
         var field:TextField=this.localText(label,size,color,true,width,height);
         var format:TextFormat=field.defaultTextFormat;format.align="center";field.defaultTextFormat=format;field.setTextFormat(format);
         field.filters=[new GlowFilter(0x301400,1,3,3,5),new GlowFilter(0xFFFFFF,1,2,2,2)];return field;
      }

      private function placeGiftText(field:TextField,xpos:Number,ypos:Number,width:Number,height:Number) : void
      {
         field.width=width;field.height=Math.ceil(field.textHeight)+4;
         field.x=xpos;field.y=ypos+(height-field.height)/2;
      }

      private function giftInput(parent:Sprite,xpos:Number,ypos:Number,width:Number,height:Number,size:int) : TextField
      {
         var frame:Sprite=new Sprite();frame.graphics.lineStyle(1,0xA76110);frame.graphics.beginFill(0xFFF7BB);
         frame.graphics.drawRoundRect(xpos,ypos,width,height,7,7);frame.graphics.endFill();parent.addChild(frame);
         var field:TextField=new TextField();field.type="input";field.multiline=false;field.wordWrap=false;field.maxChars=64;
         field.defaultTextFormat=new TextFormat("Microsoft YaHei",size,0x442306,false,null,null,null,null,"center");
         field.text="国M";var lineHeight:Number=Math.ceil(field.textHeight)+4;field.text="";
         field.x=xpos+8;field.y=ypos+(height-lineHeight)/2;field.width=width-16;field.height=lineHeight;
         parent.addChild(field);frame.addEventListener(MouseEvent.CLICK,function(e:MouseEvent):void{if(field.stage)field.stage.focus=field;});return field;
      }

      private function giftResourceIcon(parent:Sprite,kind:String,xpos:Number,ypos:Number,size:Number) : void
      {
         var icon:Sprite=new Sprite();icon.x=xpos;icon.y=ypos;icon.scaleX=icon.scaleY=size/48;icon.mouseEnabled=false;parent.addChild(icon);
         var g:* = icon.graphics;g.lineStyle(2,0x4C290E);
         if(kind=="points")
         {
            g.beginFill(0xF9C62E);g.drawCircle(24,24,20);g.endFill();g.lineStyle(2,0xFFF3A4);g.drawCircle(24,24,15);
            g.lineStyle(2,0xA6530D);g.beginFill(0xFFEB7C);g.moveTo(24,11);g.lineTo(35,24);g.lineTo(24,37);g.lineTo(13,24);g.lineTo(24,11);g.endFill();
         }
         else if(kind=="soul")
         {
            g.beginFill(0x2BA9ED);g.moveTo(26,3);g.curveTo(42,17,40,31);g.curveTo(34,49,17,43);g.curveTo(3,37,9,23);g.curveTo(18,25,18,11);g.curveTo(23,18,26,3);g.endFill();
            g.lineStyle(1,0xA8EEFF);g.beginFill(0xBCF7FF);g.moveTo(25,20);g.curveTo(35,31,29,38);g.curveTo(15,43,18,29);g.curveTo(24,33,25,20);g.endFill();
         }
         else if(kind=="resource")
         {
            g.beginFill(0xFFF1A3);g.drawRoundRect(6,8,36,32,8,8);g.endFill();g.lineStyle(4,0xC17E18);g.moveTo(8,8);g.lineTo(40,8);g.moveTo(8,40);g.lineTo(40,40);
            g.lineStyle(4,0x199FA3);g.moveTo(13,24);g.lineTo(34,24);g.moveTo(27,17);g.lineTo(34,24);g.lineTo(27,31);
         }
         else
         {
            g.beginFill(0xEC4D27);g.drawRoundRect(5,18,38,25,4,4);g.endFill();g.beginFill(0xFFB526);g.drawRoundRect(3,13,42,10,4,4);g.drawRect(20,15,8,28);g.endFill();
            g.lineStyle(3,0xFFDA57);g.moveTo(24,14);g.curveTo(4,13,10,5);g.curveTo(18,0,24,14);g.curveTo(30,0,38,5);g.curveTo(44,13,24,14);
         }
      }

      private function giftArt(parent:Sprite,path:String,xpos:Number,ypos:Number,width:Number,height:Number,owners:Array,complete:Function=null) : void
      {
         var frame:Sprite=new Sprite();frame.x=xpos;frame.y=ypos;frame.mouseEnabled=false;frame.mouseChildren=false;parent.addChild(frame);
         var key:String=path;
         var paint:Function=function(data:BitmapData):void
         {
            if(frame.parent)
            {
               while(frame.numChildren)frame.removeChildAt(0);
               if(data)
               {
                  var bitmap:Bitmap=new Bitmap(data,"auto",true);
                  var ratio:Number=Math.min(width/data.width,height/data.height);
                  bitmap.scaleX=bitmap.scaleY=ratio;
                  bitmap.x=(width-data.width*ratio)/2;bitmap.y=(height-data.height*ratio)/2;
                  frame.addChild(bitmap);
               }
               else if(path.indexOf("icon/")==0)giftResourceIcon(frame,"gift",0,0,Math.min(width,height));
            }
            if(complete!=null)complete();
         };
         if(this.localGiftImageCache[key]){paint(this.localGiftImageCache[key]);return;}
         if(path.indexOf("icon/")==0)this.giftResourceIcon(frame,"gift",0,0,Math.min(width,height));
         if(this.localGiftImageWaiting[key]){this.localGiftImageWaiting[key].push(paint);return;}
         this.localGiftImageWaiting[key]=[paint];
         var loader:Loader=new Loader();
         var finished:Boolean=false;
         var finish:Function=function(data:BitmapData):void
         {
            if(finished)return;finished=true;
            if(data)
            {
               localGiftImageCache[key]=data;localGiftImageOrder.push(key);localGiftImageBytes+=data.width*data.height*4;
               while(localGiftImageOrder.length>64 || localGiftImageBytes>33554432)
               {
                  var evicted:String=localGiftImageOrder.shift();var oldData:BitmapData=localGiftImageCache[evicted];
                  if(oldData)localGiftImageBytes-=oldData.width*oldData.height*4;
                  delete localGiftImageCache[evicted];
               }
            }
            var waiting:Array=localGiftImageWaiting[key];delete localGiftImageWaiting[key];
            for each(var done:Function in waiting)done(data);
            try{loader.unloadAndStop(false);}catch(ignored:Error){}
         };
         loader.contentLoaderInfo.addEventListener(Event.COMPLETE,function(e:Event):void
         {
            try
            {
               var image:Bitmap=loader.content as Bitmap;
               if(!image || !image.bitmapData)throw new Error("Gift image is not a bitmap");
               finish(image.bitmapData.clone());
            }
            catch(error:Error){finish(null);}
         });
         loader.contentLoaderInfo.addEventListener("ioError",function(e:Event):void{finish(null);});
         loader.contentLoaderInfo.addEventListener("securityError",function(e:Event):void{finish(null);});
         loader.load(new URLRequest("http://127.0.0.1:43999/local-gifts/"+path));
      }

      private function clearGiftArt(owners:Array) : void
      {
         owners.length=0;
      }



      private function loadGiftCatalog() : void
      {
         var loader:URLLoader=new URLLoader();
         loader.addEventListener(Event.COMPLETE,function(e:Event):void
         {
            try{var data:Object=JSON.parse(String(loader.data));if(data.ok){localGiftCatalog=data;if(localActivityOverlay)renderGiftHall();}}
            catch(error:Error){}
         });
         loader.addEventListener("ioError",function(e:Event):void{});loader.addEventListener("securityError",function(e:Event):void{});
         loader.load(new URLRequest("http://127.0.0.1:43999/local-gifts/catalog"));
      }

      public function openLocalActivities() : void
      {
         if(this.localGiftBusy || this.localGiftUncertain)return;
         this.closeLocalActivities();this.localActivityOverlay=new Sprite();this.localActivityOverlay.name="offlineGiftActivities";
         this.addChild(this.localActivityOverlay);this.renderGiftHall();this.loadGiftCatalog();
      }

      private function renderGiftHall() : void
      {
         var panel:Sprite=this.localActivityOverlay;if(!panel)return;
         if(!this.localGiftHallPager)
         {
            panel.graphics.beginFill(0,.35);panel.graphics.drawRect(0,0,940,590);panel.graphics.endFill();
            panel.graphics.lineStyle(3,0x401908);panel.graphics.beginFill(0xD5170B);panel.graphics.drawRoundRect(89,26,762,528,17,17);panel.graphics.endFill();
            this.giftArt(panel,"art/hall.png",90,27,760,525,this.localGiftHallArt);
            panel.addChild(this.giftButton("×",785,32,52,this.closeLocalActivities));
            this.localGiftHallPrevious=this.giftButton("上一页",111,505,105,this.previousGiftPage);panel.addChild(this.localGiftHallPrevious);
            this.localGiftHallNext=this.giftButton("下一页",720,505,105,this.nextGiftPage);panel.addChild(this.localGiftHallNext);
            this.localGiftHallPager=this.giftText("正在读取礼包…",15,0xFFEBA8,450,28);this.placeGiftText(this.localGiftHallPager,245,510,450,28);panel.addChild(this.localGiftHallPager);
         }
         var posters:Array=this.localGiftCatalog?this.localGiftCatalog.posters:[];
         var pages:int=Math.max(1,Math.ceil(posters.length/6));this.localGiftHallPage=Math.min(this.localGiftHallPage,pages-1);
         var layer:Sprite=new Sprite();var ready:int=0;var total:int=Math.min(6,posters.length-this.localGiftHallPage*6);
         var page:int=this.localGiftHallPage;var generation:int=++this.localGiftHallGeneration;
         var commit:Function=function():void
         {
            if(generation!=localGiftHallGeneration || !localActivityOverlay || ready<total)return;
            if(localGiftHallCards && localGiftHallCards.parent)localGiftHallCards.parent.removeChild(localGiftHallCards);
            localGiftHallCards=layer;panel.addChildAt(layer,Math.min(1,panel.numChildren));
            localGiftHallPager.text=(page+1)+" / "+pages+"　　活动礼包";
            localGiftHallPrevious.mouseEnabled=page>0;localGiftHallPrevious.alpha=page>0?1:.45;
            localGiftHallNext.mouseEnabled=page+1<pages;localGiftHallNext.alpha=page+1<pages?1:.45;
            var warm:Sprite=new Sprite();for(var n:int=(page+1)*6;n<Math.min(posters.length,(page+2)*6);n++)if(posters[n].id)giftArt(warm,"art/"+posters[n].id+".png",0,0,224,170,localGiftHallArt);
         };
         var loaded:Function=function():void{ready++;commit();};
         for(var i:int=0;i<6 && i+this.localGiftHallPage*6<posters.length;i++)
         {
            var entry:Object=posters[i+this.localGiftHallPage*6];var card:Sprite=new Sprite();card.name="giftActivity:"+entry.id;
            card.x=118+(i%3)*240;card.y=137+int(i/3)*179;card.buttonMode=true;card.mouseChildren=false;
            card.graphics.beginFill(0,0);card.graphics.drawRect(0,0,224,170);card.graphics.endFill();
            layer.addChild(card);
            if(entry.id)this.giftArt(card,"art/"+entry.id+".png",0,0,224,170,this.localGiftHallArt,loaded);
            else
            {
               this.giftResourceIcon(card,"gift",76,15,72);
               var custom:TextField=this.giftText("全部礼包",26,0xFFE287,224,42);
               this.placeGiftText(custom,0,96,224,42);card.addChild(custom);loaded();
            }
            card.addEventListener(MouseEvent.CLICK,this.chooseGiftActivity);
            card.addEventListener(MouseEvent.ROLL_OVER,function(e:MouseEvent):void{Sprite(e.currentTarget).filters=[new GlowFilter(0xFFE47A,1,8,8,2)];});
            card.addEventListener(MouseEvent.ROLL_OUT,function(e:MouseEvent):void{Sprite(e.currentTarget).filters=[];});
         }
         if(!total)commit();
      }

      private function previousGiftPage(e:MouseEvent) : void {this.localGiftHallPage=Math.max(0,this.localGiftHallPage-1);this.renderGiftHall();}
      private function nextGiftPage(e:MouseEvent) : void {if(!this.localGiftCatalog)return;this.localGiftHallPage=Math.max(0,Math.min(Math.ceil(this.localGiftCatalog.posters.length/6)-1,this.localGiftHallPage+1));this.renderGiftHall();}
      private function giftActivityName(id:int) : String
      {
         if(this.localGiftCatalog)for each(var item:Object in this.localGiftCatalog.posters)if(item.id==id)return String(item.name);
         return "活动礼包";
      }

      private function codesForActivity() : Array
      {
         var rows:Array=[];if(!this.localGiftCatalog)return rows;
         var wanted:Array=[];for each(var poster:Object in this.localGiftCatalog.posters)if(poster.id==this.localGiftActivity)wanted=poster.codes;
         for each(var code:Object in this.localGiftCatalog.codes)if(code.enabled && (this.localGiftActivity==0 || wanted.indexOf(code.id)>=0))rows.push(code);
         return rows;
      }

      private function openGiftCodes(e:MouseEvent) : void
      {
         if(this.localGiftBusy || this.localGiftUncertain)return;
         this.localGiftCodePage=0;this.localGiftCodePicker=new Sprite();
         this.localGiftCodePicker.graphics.beginFill(0,.45);this.localGiftCodePicker.graphics.drawRect(100,28,740,526);this.localGiftCodePicker.graphics.endFill();
         this.localGiftCodePicker.graphics.lineStyle(3,0x69320B);this.localGiftCodePicker.graphics.beginFill(0xFFF098);this.localGiftCodePicker.graphics.drawRoundRect(122,132,696,375,14,14);this.localGiftCodePicker.graphics.endFill();
         var title:TextField=this.giftText("选择礼包兑换码",23,0xFFD15E,645,40);this.placeGiftText(title,146,142,645,40);this.localGiftCodePicker.addChild(title);
         this.localGiftCodeSearch=this.giftInput(this.localGiftCodePicker,146,191,645,31,15);
         this.localGiftCodeSearch.addEventListener(Event.CHANGE,function(e:Event):void{localGiftCodePage=0;renderGiftCodeRows();});
         this.localGiftCodeRows=new Sprite();this.localGiftCodePicker.addChild(this.localGiftCodeRows);
         this.localGiftCodePicker.addChild(this.giftButton("关闭",714,456,77,this.closeGiftCodes));
         this.localGiftCodePicker.addChild(this.giftButton("上一页",146,456,100,this.previousGiftCodePage));this.localGiftCodePicker.addChild(this.giftButton("下一页",258,456,100,this.nextGiftCodePage));
         this.localGiftPanel.addChild(this.localGiftCodePicker);this.renderGiftCodeRows();gc.stage.focus=this.localGiftCodeSearch;
      }

      private function renderGiftCodeRows() : void
      {
         while(this.localGiftCodeRows.numChildren)this.localGiftCodeRows.removeChildAt(0);
         var term:String=this.localGiftCodeSearch.text.toLowerCase();var source:Array=term?this.localGiftCatalog.codes:this.codesForActivity();if(!source.length)source=this.localGiftCatalog.codes;this.localGiftCodeMatches=[];
         for each(var data:Object in source)if(data.enabled && (!term || (data.name+" "+data.code+" "+data.description).toLowerCase().indexOf(term)>=0))this.localGiftCodeMatches.push(data);
         var pages:int=Math.max(1,Math.ceil(this.localGiftCodeMatches.length/4));this.localGiftCodePage=Math.min(this.localGiftCodePage,pages-1);
         for(var i:int=0;i<4 && i+this.localGiftCodePage*4<this.localGiftCodeMatches.length;i++)
         {
            var index:int=i+this.localGiftCodePage*4;var item:Object=this.localGiftCodeMatches[index];var button:Sprite=this.giftButton(item.code+"　"+String(item.name).substr(0,16),146,237+i*50,645,this.chooseGiftCode);button.name=String(index);this.localGiftCodeRows.addChild(button);
            TextField(button.getChildAt(0)).width=498;
            var examples:Array=item.examples || [];for(var n:int=0;n<Math.min(3,examples.length);n++)this.giftRewardIcon(button,examples[n],513+n*42,3,32);
         }
         var count:TextField=this.localText((this.localGiftCodePage+1)+" / "+pages+"　共 "+this.localGiftCodeMatches.length+" 项",13,0x703C12,false,320,27);count.x=380;count.y=467;this.localGiftCodeRows.addChild(count);
      }
      private function closeGiftCodes(e:MouseEvent) : void {if(this.localGiftCodePicker && this.localGiftCodePicker.parent)this.localGiftCodePicker.parent.removeChild(this.localGiftCodePicker);this.localGiftCodePicker=null;}
      private function chooseGiftCode(e:MouseEvent) : void {this.localGiftCode.text=this.localGiftCodeMatches[int(e.currentTarget.name)].code;this.closeGiftCodes(null);this.localGiftCodeChanged(null);this.verifyLocalGift(null);}
      private function previousGiftCodePage(e:MouseEvent) : void {this.localGiftCodePage=Math.max(0,this.localGiftCodePage-1);this.renderGiftCodeRows();}
      private function nextGiftCodePage(e:MouseEvent) : void {this.localGiftCodePage=Math.max(0,Math.min(Math.ceil(this.localGiftCodeMatches.length/4)-1,this.localGiftCodePage+1));this.renderGiftCodeRows();}

      private function simulateLegacyGift(id:String,target:String="all") : Array
      {
         var before:Object=this.captureLocalGiftSnapshot();var original:* = GMain.serviceHold;var after:Object;
         try
         {
            GMain.serviceHold={"isLog":original.isLog,"saveData":function(title:String,data:*,extra:Boolean,index:int):void{}};
            var native:Object=this.localGiftCatalog.nativeRewards?this.localGiftCatalog.nativeRewards[id]:null;
            if(native && native.travel)
            {
               gc.dataObject.worldtransfromTimes+=int(native.travel);
               if(gc.player1.roleid>0)gc.putQhsInBackPack(gc.player1,"sbjyd",1);
               if(gc.player2.roleid>0)gc.putQhsInBackPack(gc.player2,"sbjyd",1);
            }
            else
            {
               var className:String=native?String(native.className):id=="GBX"?"export.huodong.gamebox.GameBoxActivity":id=="YYL"?"export.huodong.gamebox.YaoYaoLeActivity3":"export.huodong.miaomiaoBase.MiaoMiaoBaseInterface";
               var rewardClass:Class=getDefinitionByName(className) as Class;
               var rewardObject:Object=new rewardClass();if(native)rewardObject[String(native.method)]();else if(id=="GBX" || id=="YYL")rewardObject["getGameBoxPresent"]();else rewardObject["getPresent"](uint(id));
            }
            after=this.captureLocalGiftSnapshot();
         }
         finally
         {
            GMain.serviceHold=original;
            var restored:Object=JSON.parse(JSON.stringify(before));
            for each(var restoredKey:String in ["player1_obj","player2_obj"])
            {
               var saved:Object=restored[restoredKey];if(!saved)continue;
               // Player.setSaveObj reverses these two serialized lists on read.
               for each(var reversedField:String in ["curSaveString","petSave"])if(saved[reversedField])saved[reversedField]=String(saved[reversedField]).split("}").reverse().join("}");
            }
            gc.memory.getStorage(restored);if(gc.ts.parent)gc.ts.parent.removeChild(gc.ts);
         }
         this.localGiftLegacyBefore=before;this.localGiftLegacyAfter=after;
         var rewards:Array=[];
         for each(var key:String in ["player1_obj","player2_obj"])
         {
            if(target!="all" && key!=target+"_obj")continue;
            if(!before[key] || !after[key] || !before[key].roleid)continue;
            var a:Object=before[key];var b:Object=after[key];
            for each(var bag:String in ["bagSaveString","bagdjSaveString","bagszSaveString","bagjnsSaveString","petEquipSaveString","petSave"])
            {
               var old:Object={};var fresh:Object={};var idKey:int=bag=="petSave" || bag=="petEquipSaveString"?0:2;
               for each(var record:String in String(a[bag] || "").split("}"))if(record){var f:Array=record.split("&")[0].split("|");var code:String=f[idKey];old[code]=(old[code] || 0)+(bag=="bagdjSaveString"?int(f[22]):1);}
               for each(record in String(b[bag] || "").split("}"))if(record){f=record.split("&")[0].split("|");code=f[idKey];fresh[code]=(fresh[code] || 0)+(bag=="bagdjSaveString"?int(f[22]):1);}
               for(code in fresh)if(fresh[code]>(old[code] || 0))
               {
                  var label:String=code;
                  try{label=bag=="petSave"?(this.localGiftCatalog.petNames[code] || code):(this.localGiftCatalog.itemNames[code] || code);}catch(error:Error){}
                  rewards.push({"kind":bag=="petSave"?"pet":"item","id":code,"name":label,"count":fresh[code]-(old[code] || 0)});
               }
            }
            if(int(b.lhValue)>int(a.lhValue))rewards.push({"kind":"soul","name":"灵魂","count":int(b.lhValue)-int(a.lhValue)});
            break;
         }
         if(int(after.worldtransfromTimes)>int(before.worldtransfromTimes))rewards.push({"kind":"resource","name":"世界传送次数","count":int(after.worldtransfromTimes)-int(before.worldtransfromTimes)});
         if(!rewards.length)rewards.push({"kind":"resource","name":"原版礼包奖励","count":1});
         return rewards;
      }

      private function closeLocalActivities(e:MouseEvent=null) : void
      {
         if(e)e.stopPropagation();
         if(this.localActivityOverlay && this.contains(this.localActivityOverlay))this.removeChild(this.localActivityOverlay);
         this.localActivityOverlay=null;this.localGiftHallCards=null;this.localGiftHallPager=null;this.localGiftHallGeneration++;this.clearGiftArt(this.localGiftHallArt);
      }



      private function chooseGiftActivity(e:MouseEvent) : void
      {
         this.localGiftActivity=int(String(e.currentTarget.name).split(":")[1]);this.openLocalGiftExchange(null);
      }

      private function openLocalGiftExchange(e:MouseEvent) : void
      {
         this.closeLocalActivities();this.localGiftToken="";this.localGiftUncertain=false;this.localGiftBusy=false;
         this.localGiftSlot=int(gc.luanqibazaojiami.saveId);this.localGiftRewardPage=0;this.localGiftRewardList=[];
         this.localGiftPanel=new Sprite();this.localGiftPanel.name="offlineGiftExchange";
         this.localGiftPanel.graphics.beginFill(0,.35);this.localGiftPanel.graphics.drawRect(0,0,940,590);this.localGiftPanel.graphics.endFill();
         this.localGiftPanel.graphics.lineStyle(3,0x3A1605);this.localGiftPanel.graphics.beginFill(0xEE2719);
         this.localGiftPanel.graphics.drawRoundRect(99,28,742,526,18,18);this.localGiftPanel.graphics.endFill();
         this.localGiftPanel.graphics.lineStyle(2,0x6B3508);this.localGiftPanel.graphics.beginFill(0xFFF07D);
         this.localGiftPanel.graphics.drawRect(111,108,718,432);this.localGiftPanel.graphics.endFill();
         this.localGiftPanel.graphics.lineStyle(0,0,0);this.localGiftPanel.graphics.beginFill(0xFFD74B);
         this.localGiftPanel.graphics.moveTo(748,30);this.localGiftPanel.graphics.lineTo(836,30);this.localGiftPanel.graphics.lineTo(836,118);this.localGiftPanel.graphics.endFill();
         this.localGiftTitle=this.giftText(this.giftActivityName(this.localGiftActivity),30,0xFFD345,620,55);this.localGiftTitle.x=151;this.localGiftTitle.y=51;this.localGiftPanel.addChild(this.localGiftTitle);
         this.localGiftPanel.addChild(this.giftButton("×",777,36,52,this.closeLocalGiftExchange));
         var heading:TextField=this.giftText(this.localGiftActivity?"礼包兑换码":"请输入兑换码",22,0x6B3508,409,35);this.placeGiftText(heading,129,146,409,35);this.localGiftPanel.addChild(heading);
         if(this.localGiftActivity)this.giftArt(this.localGiftPanel,"art/"+this.localGiftActivity+".png",566,144,240,178,this.localGiftDetailArt);
         else{var symbol:TextField=this.giftText("礼",90,0xFFD238,240,160);symbol.x=566;symbol.y=155;this.localGiftPanel.addChild(symbol);}
         this.localGiftCode=this.giftInput(this.localGiftPanel,129,193,409,48,22);this.localGiftCode.name="localGiftCode";
         this.localGiftCode.addEventListener(Event.CHANGE,this.localGiftCodeChanged);
         this.localGiftVerify=this.giftButton("查看奖励",130,259,156,this.verifyLocalGift);
         this.localGiftPanel.addChild(this.giftButton("选择礼包码",129,259,194,this.openGiftCodes));this.localGiftClaim=this.giftButton("领取礼包",337,259,201,this.claimLocalGift,true);this.localGiftClaim.alpha=.45;this.localGiftClaim.mouseEnabled=false;this.localGiftPanel.addChild(this.localGiftClaim);
         var contentTitle:TextField=this.giftText("礼包内容",20,0x6B3508,200,33);this.placeGiftText(contentTitle,128,319,200,33);this.localGiftPanel.addChild(contentTitle);
         this.localGiftPanel.graphics.lineStyle(1,0x8D5112);this.localGiftPanel.graphics.beginFill(0x775016);this.localGiftPanel.graphics.drawRoundRect(128,357,678,119,7,7);this.localGiftPanel.graphics.endFill();
         this.localGiftRewardIcons=new Sprite();this.localGiftPanel.addChild(this.localGiftRewardIcons);
         this.localGiftRewards=this.localText("选择礼包码即可查看奖励图标，验证后领取。",14,0xFFF1AF,false,640,28);this.localGiftRewards.x=148;this.localGiftRewards.y=380;this.localGiftPanel.addChild(this.localGiftRewards);
         this.localGiftStatus=this.localText("在修改器「设置 → 本地兑换码」中配置礼包与兑换码。",12,0x703C12,false,440,44);this.localGiftStatus.x=129;this.localGiftStatus.y=487;this.localGiftPanel.addChild(this.localGiftStatus);
         this.localGiftPanel.addChild(this.giftButton("返回",721,490,84,this.returnLocalActivities));
         this.addChild(this.localGiftPanel);gc.stage.focus=this.localGiftCode;
         var choices:Array=this.codesForActivity();if(choices.length)this.localGiftCode.text=choices[0].code;
         this.showGiftExamples();if(choices.length)this.verifyLocalGift(null);
      }

      private function drawGiftRewards() : void
      {
         this.clearGiftArt(this.localGiftRewardArt);
         while(this.localGiftRewardIcons.numChildren)this.localGiftRewardIcons.removeChildAt(0);
         var start:int=this.localGiftRewardPage*6;
         for(var i:int=0;i<6 && start+i<this.localGiftRewardList.length;i++)
         {
            var reward:Object=this.localGiftRewardList[start+i];var x:int=139+i*110;
            this.giftRewardIcon(this.localGiftRewardIcons,reward,x+27,366,48);
            var label:TextField=this.localText(String(reward.name),12,0xFFF4BC,true,103,35);label.x=x;label.y=418;
            var format:TextFormat=label.defaultTextFormat;format.align="center";label.defaultTextFormat=format;label.setTextFormat(format);this.localGiftRewardIcons.addChild(label);
            var count:TextField=this.giftText("× "+reward.count,13,0xFFFFFF,103,24);count.x=x;count.y=451;this.localGiftRewardIcons.addChild(count);
         }
         if(this.localGiftRewardList.length>6)
         {
            var pager:Sprite=this.giftButton("更多奖励  ›",580,490,130,this.nextGiftRewards);this.localGiftRewardIcons.addChild(pager);
         }
      }

      private function giftRewardIcon(parent:Sprite,reward:Object,xpos:Number,ypos:Number,size:Number) : void
      {
         if(reward.kind=="item" || reward.kind=="pet")this.giftArt(parent,"icon/"+(reward.kind=="pet"?"pets":"items")+"/"+reward.id+".png",xpos,ypos,size,size,this.localGiftRewardArt);
         else this.giftResourceIcon(parent,String(reward.kind),xpos,ypos,size);
      }

      private function showGiftExamples() : void
      {
         var code:String=this.localGiftCode.text.replace(/^\s+|\s+$/g,"").toUpperCase();var chosen:Object=null;
         if(this.localGiftCatalog)for each(var row:Object in this.localGiftCatalog.codes)if(String(row.code).toUpperCase()==code){chosen=row;break;}
         if(!chosen){var choices:Array=this.codesForActivity();if(choices.length)chosen=choices[0];}
         this.localGiftRewardList=chosen && chosen.examples?chosen.examples:[];this.localGiftRewardPage=0;
         this.drawGiftRewards();this.localGiftRewards.visible=!this.localGiftRewardList.length;
         if(!this.localGiftRewardList.length){this.giftResourceIcon(this.localGiftRewardIcons,"gift",439,366,48);this.localGiftRewards.text="选择礼包码，查看具体奖励";this.localGiftRewards.width=640;var format:TextFormat=this.localGiftRewards.defaultTextFormat;format.align="center";this.localGiftRewards.defaultTextFormat=format;this.localGiftRewards.setTextFormat(format);this.localGiftRewards.y=427;}
         this.localGiftStatus.text=this.localGiftRewardList.length?"奖励示例 · 点击「查看奖励」验证当前兑换码后领取。":"点击「礼包码」选择奖励，或输入自己设置的兑换码。";
      }

      private function nextGiftRewards(e:MouseEvent) : void
      {
         this.localGiftRewardPage=(this.localGiftRewardPage+1)%Math.ceil(this.localGiftRewardList.length/6);this.drawGiftRewards();
      }

      private function localGiftCodeChanged(e:Event) : void
      {
         if(this.localGiftBusy || this.localGiftUncertain) return;
         this.localGiftLegacyBefore=null;this.localGiftLegacyAfter=null;
         this.localGiftToken = "";this.localGiftClaim.alpha = 0.4;this.localGiftClaim.mouseEnabled = false;
         var resetCaption:TextField=this.localGiftClaim.getChildAt(0) as TextField;resetCaption.text="领取礼包";this.placeGiftText(resetCaption,0,0,resetCaption.width,39);
         this.localGiftStatus.defaultTextFormat=new TextFormat("Microsoft YaHei",12,0x703C12,false);
         this.showGiftExamples();
         if(!this.localGiftAutoPreviewTimer){this.localGiftAutoPreviewTimer=new Timer(300,1);this.localGiftAutoPreviewTimer.addEventListener("timerComplete",function(e:Event):void{if(localGiftPanel && localGiftCode.text.length>=4)verifyLocalGift(null);});}
         this.localGiftAutoPreviewTimer.reset();this.localGiftAutoPreviewTimer.start();
      }

      private function gameLog(name:String,message:String="",error:Error=null):void
      {
         try
         {
            var row:Object={event:name,level:error?"error":"info",phase:localGiftPhase,slot:localGiftSlot,activity:localGiftActivity,message:message.substr(0,3000)};
            if(error)row.stack=error.getStackTrace() || error.toString();
            trace("[GameLog] "+JSON.stringify(row));
            var request:URLRequest=new URLRequest("http://127.0.0.1:43999/game-log");request.method="POST";request.contentType="application/json";request.data=JSON.stringify(row);
            var logger:URLLoader=new URLLoader();logger.addEventListener("ioError",function(e:Event):void{});logger.addEventListener("securityError",function(e:Event):void{});logger.load(request);
         }
         catch(ignored:Error){trace("[GameLog] logging unavailable: "+ignored.toString());}
      }
      private function installGameLog():void
      {
         if(gameLogInstalled)return;gameLogInstalled=true;
         var self:SelectPLace=this;
         this.loaderInfo.uncaughtErrorEvents.addEventListener("uncaughtError",function(e:*):void
         {
            if(gameLogCount++>200)return;
            var error:Error=e.error as Error;self.gameLog("game.uncaught",String(e.error),error || new Error(String(e.error)));
         });
         this.gameLog("game.map.ready","diagnostics revision 5");
      }
      private function resolveLocalGift():void
      {
         if(this.localGiftBusy || this.localGiftToken=="")return;
         this.localGiftPhase="resolve";this.sendLocalGift("resolve",{token:this.localGiftToken});
      }
      private function localGiftTimeout(e:Event):void
      {
         this.gameLog("gift.timeout","request exceeded 15 seconds",new Error("request timeout"));
         this.localGiftError(e);
      }
      private function setLocalGiftBusy(value:Boolean) : void
      {
         this.localGiftBusy = value;
         this.localGiftCode.type = value || this.localGiftUncertain || this.localGiftActivity!=0 ? "dynamic" : "input";
         this.localGiftVerify.mouseEnabled = !value && !this.localGiftUncertain;this.localGiftVerify.alpha = this.localGiftVerify.mouseEnabled ? 1 : 0.4;
         this.localGiftClaim.mouseEnabled = !value && this.localGiftToken != "";this.localGiftClaim.alpha = this.localGiftClaim.mouseEnabled ? 1 : 0.4;
      }

      private function verifyLocalGift(e:MouseEvent) : void
      {
         if(this.localGiftBusy || this.localGiftUncertain) return;
         if(this.localGiftSlot < 0 || this.localGiftSlot > 5)
         {
            this.localGiftStatus.text = "请先正常保存到角色存档，再兑换礼包。";return;
         }
         if(this.localGiftAutoPreviewTimer)this.localGiftAutoPreviewTimer.stop();
         this.localGiftToken = "";this.localGiftPhase = "preview";
         this.sendLocalGift("preview",{"code":this.localGiftCode.text,"slot":this.localGiftSlot,"activity":this.localGiftActivity});
      }

      private function captureLocalGiftSnapshot() : Object
      {
         var original:* = GMain.serviceHold;
         var captured:Object = null;
         try
         {
            GMain.serviceHold = {
               "isLog":original.isLog,
               "saveData":function(title:String,data:*,extra:Boolean,index:int):void
               {
                  captured = JSON.parse(JSON.stringify(data));
               }
            };
            gc.memory.setStorage(this.localGiftSlot);
         }
         finally
         {
            GMain.serviceHold = original;
         }
         if(captured == null) throw new Error("无法读取当前游戏进度，请先正常保存游戏。");
         return captured;
      }

      private function claimLocalGift(e:MouseEvent) : void
      {
         if(this.localGiftBusy || this.localGiftToken == "") return;
         if(this.localGiftUncertain){this.resolveLocalGift();return;}
         this.localGiftCommitted=false;
         try
         {
            if(!this.localGiftUncertain) this.localGiftSnapshot = this.localGiftLegacyAfter || this.captureLocalGiftSnapshot();
            this.localGiftUncertain = true;this.localGiftPhase = "redeem";
            this.sendLocalGift("redeem",{"token":this.localGiftToken,"save":this.localGiftSnapshot,"baseSave":this.localGiftLegacyBefore});
         }
         catch(error:Error)
         {
            this.gameLog("gift.submit.failed",error.message,error);this.setLocalGiftBusy(false);
            if(this.localGiftUncertain)this.resolveLocalGift();else this.localGiftStatus.text=error.message;
         }
      }

      private function sendLocalGift(action:String,data:Object) : void
      {
         this.setLocalGiftBusy(true);this.localGiftStatus.text=action=="preview"?"正在验证兑换码…":action=="resolve"?"正在核对领取结果…":"正在发放并保存，请稍候…";
         this.gameLog("gift.request",action);
         if(!this.localGiftWatchdog){this.localGiftWatchdog=new Timer(15000,1);this.localGiftWatchdog.addEventListener("timerComplete",this.localGiftTimeout);}
         this.localGiftWatchdog.reset();this.localGiftWatchdog.start();
         var request:URLRequest = new URLRequest("http://127.0.0.1:43999/local-gifts/" + action);
         request.method = "POST";request.contentType = "application/json";request.data = JSON.stringify(data);
         this.localGiftLoader = new URLLoader();this.localGiftLoader.dataFormat = "text";
         this.localGiftLoader.addEventListener("complete",this.localGiftComplete);
         this.localGiftLoader.addEventListener("ioError",this.localGiftError);
         this.localGiftLoader.addEventListener("securityError",this.localGiftError);
         this.localGiftLoader.load(request);
      }

      private function detachLocalGiftLoader() : void
      {
         if(this.localGiftWatchdog)this.localGiftWatchdog.stop();
         if(!this.localGiftLoader) return;
         try{this.localGiftLoader.close();}catch(ignored:Error){}
         this.localGiftLoader.removeEventListener("complete",this.localGiftComplete);
         this.localGiftLoader.removeEventListener("ioError",this.localGiftError);
         this.localGiftLoader.removeEventListener("securityError",this.localGiftError);
         this.localGiftLoader = null;
      }

      private function localGiftComplete(e:Event) : void
      {
         var text:String = String(this.localGiftLoader.data);this.detachLocalGiftLoader();
         try
         {
            var data:Object = JSON.parse(text);
            if(!data.ok)
            {
               this.gameLog("gift.rejected",String(data.error));
               this.localGiftUncertain = Boolean(data.unresolved);
               if(!this.localGiftUncertain && this.localGiftPhase == "redeem") this.localGiftToken = "";
               this.localGiftStatus.text = String(data.error);this.setLocalGiftBusy(false);
               if(this.localGiftUncertain && this.localGiftPhase!="resolve")this.resolveLocalGift();return;
            }
            if(this.localGiftPhase=="resolve" && !data.completed)
            {
               this.gameLog("gift.resolved.cancelled","No reward committed; request fenced");
               this.localGiftUncertain=false;this.localGiftToken="";this.localGiftSnapshot=null;
               this.localGiftStatus.text=data.otherSave?"本次礼包已保存到原存档，当前存档未修改。":"本次未领取，未消耗次数。请重新选择礼包码后领取。";
               this.localGiftPhase="";this.setLocalGiftBusy(false);return;
            }
            if(this.localGiftPhase == "preview")
            {
               this.localGiftRewards.visible=false;
               this.localGiftLegacyBefore=null;this.localGiftLegacyAfter=null;
               this.localGiftRewardList=data.rewards;
               if(data.rewards.length==1 && data.rewards[0].kind=="legacy")this.localGiftRewardList=this.simulateLegacyGift(String(data.rewards[0].id),String(data.target));
               this.localGiftRewardPage=0;this.drawGiftRewards();this.localGiftToken=String(data.token);
               var rule:String=data.rule=="once"?"每存档一次":data.rule=="daily"?"每存档每天一次":"每存档最多 "+data.perSaveLimit+" 次";
               this.localGiftStatus.text=String(data.name).substr(0,28)+" · "+rule+"\n"+(data.expires?"有效至 "+data.expires:"永久有效")+" · 领取后自动保存";
               this.setLocalGiftBusy(false);
            }
            else
            {
               // The server receipt is definitive even if refreshing the display fails.
               this.localGiftCommitted=true;this.localGiftUncertain=false;this.localGiftToken="";
               this.gameLog("gift.committed","receipt="+String(data.receipt));
               var hold:* = GMain.serviceHold;
               var saveTitle:String = "";
               if(gc.player1.roleid > 0) saveTitle += gc.player1.getRoleName() + " ";
               if(gc.player2.roleid > 0) saveTitle += gc.player2.getRoleName();
               hold.gmPutSlot(this.localGiftSlot,saveTitle,JSON.stringify(data.save));
               gc.memory.storageValue(this.localGiftSlot,data.save);gc.memory.getStorage(data.save);
               if(data.vip && data.vip.lastMoney != undefined) gc.gameVip.dataObject.lastMoney = data.vip.lastMoney;
               hold.getBalance();this.localGiftBusy = false;this.localGiftUncertain = false;
               this.localGiftToken="";this.localGiftSnapshot=null;this.localGiftLegacyBefore=null;this.localGiftLegacyAfter=null;
               if(this.localGiftAutoPreviewTimer)this.localGiftAutoPreviewTimer.stop();
               this.setLocalGiftBusy(false);
               var caption:TextField=this.localGiftClaim.getChildAt(0) as TextField;caption.text="已领取";this.placeGiftText(caption,0,0,caption.width,39);this.localGiftClaim.alpha=.65;
               var hasItems:Boolean=false;var hasPets:Boolean=false;
               for each(var reward:Object in this.localGiftRewardList){if(reward.kind=="item")hasItems=true;if(reward.kind=="pet")hasPets=true;}
               var notice:String="领取成功，"+(hasItems?"物品已加入背包":"")+(hasItems && hasPets?"，":"")+(hasPets?"宠物已加入宠物栏":"")+(!hasItems && !hasPets?"奖励已到账":"")+"。";
               this.localGiftStatus.defaultTextFormat=new TextFormat("Microsoft YaHei",14,0x1D6B3B,true);
               this.localGiftStatus.text=notice+"\n已自动保存，可继续选择其他礼包。";
               this.localGiftPhase="";this.gameLog("gift.receipt.shown",notice);
               gc.ts.setTxt(notice,3,21);gc.stage.addChild(gc.ts);
            }
         }
         catch(error:Error)
         {
            this.gameLog("gift.response.failed",error.message,error);
            if(this.localGiftCommitted)
            {
               this.localGiftUncertain=false;this.localGiftToken="";this.setLocalGiftBusy(false);
               this.localGiftStatus.text="奖励已领取并保存。画面刷新失败，请关闭此页并重新读档；详情已记录。";return;
            }
            this.localGiftUncertain = this.localGiftPhase == "redeem" || this.localGiftPhase == "resolve";
            if(!this.localGiftUncertain)this.localGiftToken="";
            this.localGiftStatus.text = this.localGiftUncertain ? "兑换结果尚未确认，请点击领取重试同一次请求。" : "无法读取预览，请重新验证兑换码。";
            this.setLocalGiftBusy(false);
            if(this.localGiftUncertain && this.localGiftPhase!="resolve")this.resolveLocalGift();
         }
      }

      private function localGiftError(e:Event) : void
      {
         this.gameLog("gift.network.failed",e.toString(),new Error(e.toString()));
         this.detachLocalGiftLoader();this.setLocalGiftBusy(false);
         if(this.localGiftUncertain && this.localGiftPhase!="resolve"){this.resolveLocalGift();return;}
         if(!this.localGiftUncertain)this.localGiftToken="";
         this.localGiftStatus.text=this.localGiftUncertain?"本地服务暂不可用。点击领取或关闭可重新核对结果，避免重复发奖。":"请求失败或超时，未领取。请重新选择礼包码；详情见 logs/game-events.log。";
         this.setLocalGiftBusy(false);
      }

      private function scrollLocalGiftRewards(e:MouseEvent) : void
      {
         this.localGiftRewards.scrollV -= e.delta;e.stopPropagation();
      }

      private function closeLocalGiftExchange(e:MouseEvent = null) : void
      {
         if(e) e.stopPropagation();
         if(this.localGiftBusy || this.localGiftUncertain)
         {
            if(!this.localGiftBusy)this.resolveLocalGift();
            else this.localGiftStatus.text="正在核对领取结果，请稍候（最长15秒）。";return;
         }
         if(this.localGiftAutoPreviewTimer)this.localGiftAutoPreviewTimer.stop();
         this.detachLocalGiftLoader();
         if(this.localGiftPanel && this.contains(this.localGiftPanel)) this.removeChild(this.localGiftPanel);
         this.localGiftPanel = null;this.localGiftSnapshot = null;this.localGiftLegacyBefore=null;this.localGiftLegacyAfter=null;this.clearGiftArt(this.localGiftDetailArt);this.clearGiftArt(this.localGiftRewardArt);
      }

      private function returnLocalActivities(e:MouseEvent) : void
      {
         this.closeLocalGiftExchange(e);if(!this.localGiftPanel) this.openLocalActivities();
      }

   }
}

