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
         if(this.localActivityToggle) this.localActivityToggle.removeEventListener("click",this.toggleLocalActivities);
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
         this.localActivityToggle.graphics.clear();
         this.localActivityToggle.graphics.lineStyle(1,0x8D5E26);
         this.localActivityToggle.graphics.beginFill(0xFFF1BC);
         this.localActivityToggle.graphics.drawRoundRect(0,0,96,25,10,10);
         this.localActivityToggle.graphics.endFill();
         var title:TextField = this.localText(localActivitiesCollapsed ? "展开活动 ＞" : "收起活动 ＜",13,0x65401B,true,94,24);
         title.x = 5;
         title.y = 2;
         this.localActivityToggle.addChild(title);
      }

      private function toggleLocalActivities(e:MouseEvent) : void
      {
         e.stopPropagation();
         localActivitiesCollapsed = !localActivitiesCollapsed;
         this.refreshLocalActivityControls();
         trace("[offline-activities] folded=" + localActivitiesCollapsed);
      }

      public function openLocalActivities() : void
      {
         this.closeLocalActivities();
         this.localActivityOverlay = new Sprite();
         this.localActivityOverlay.name = "offlineActivities";
         this.localActivityOverlay.graphics.beginFill(0x000000,0.38);
         this.localActivityOverlay.graphics.drawRect(0,0,940,590);
         this.localActivityOverlay.graphics.endFill();
         this.localActivityOverlay.graphics.lineStyle(3,0x98652B);
         this.localActivityOverlay.graphics.beginFill(0xFFF6DE);
         this.localActivityOverlay.graphics.drawRoundRect(120,34,700,516,20,20);
         this.localActivityOverlay.graphics.endFill();
         var title:TextField = this.localText("本地活动与副本",24,0x5E3B1D,true,500,38);
         title.x = 144;title.y = 49;this.localActivityOverlay.addChild(title);
         var intro:TextField = this.localText("使用原有关卡与规则，保留挑战次数、等级和进入条件。",13,0x7C664A,false,600,25);
         intro.x = 145;intro.y = 89;this.localActivityOverlay.addChild(intro);
         var close:Sprite = new Sprite();
         close.name = "closeOfflineActivities";close.x = 765;close.y = 49;
         close.graphics.beginFill(0xEAD4AA);close.graphics.drawRoundRect(0,0,32,32,8,8);close.graphics.endFill();
         close.buttonMode = true;close.mouseChildren = false;
         var cross:TextField = this.localText("×",25,0x6F431E,true,31,32);cross.x = 3;cross.y = -2;close.addChild(cross);
         close.addEventListener("click",this.closeLocalActivities);this.localActivityOverlay.addChild(close);
         var titles:Array = ["广寒宫","射日挑战","乱世月宫","乱世星宿","斗部群星","八仙过海","福禄寿","挑战三太子"];
         var notes:Array = ["嫦娥挑战 · 月兔与桂花","后羿射日 · 使用原有挑战次数","妲己与凶兽 · 使用原有挑战次数","邪星宿挑战 · 使用原有挑战次数","星宿挑战 · 原版副本入口","八仙挑战 · 保留喜好品规则","三仙挑战 · 原版进入条件","三太子挑战 · 保留等级条件"];
         var icons:Array = [moonmc,sunmc,dajimc,lsxsmc,this.localMenu.kaixuebtn,this.localMenu.bxghbtn,flsbtn,stzmc];
         for(var i:int = 0;i < 8;i++) this.addLocalActivityCard(i,titles[i],notes[i],icons[i]);
         var tip:TextField = this.localText("已移除仅跳转网页或依赖联网兑换的宣传入口。",12,0x806C50,false,635,24);
         tip.x = 145;tip.y = 510;this.localActivityOverlay.addChild(tip);
         this.addChild(this.localActivityOverlay);
         trace("[offline-activities] menu-open count=8");
      }

      private function addLocalActivityCard(id:int,title:String,note:String,source:DisplayObject) : void
      {
         var card:Sprite = new Sprite();
         card.name = "offlineActivity:" + id;
         card.x = 145 + (id % 2) * 337;
         card.y = 126 + int(id / 2) * 93;
         card.graphics.lineStyle(1,0xD9C29A);card.graphics.beginFill(0xF4E5C5);
         card.graphics.drawRoundRect(0,0,315,79,10,10);card.graphics.endFill();
         card.buttonMode = true;card.mouseChildren = false;
         if(source && source.width > 0 && source.height > 0)
         {
            var bounds:Rectangle = source.getBounds(source);
            var scale:Number = Math.min(48 / bounds.width,48 / bounds.height);
            var matrix:Matrix = new Matrix();matrix.translate(-bounds.x,-bounds.y);matrix.scale(scale,scale);
            var data:BitmapData = new BitmapData(48,48,true,0);
            data.draw(source,matrix,null,null,null,true);
            var icon:Bitmap = new Bitmap(data,"auto",true);icon.x = 12;icon.y = 15;card.addChild(icon);
            this.localActivityBitmaps.push(data);
         }
         var label:TextField = this.localText(title,18,0x59391B,true,235,29);label.x = 70;label.y = 10;card.addChild(label);
         var hint:TextField = this.localText(note,12,0x7D6749,false,235,35);hint.x = 70;hint.y = 41;card.addChild(hint);
         card.addEventListener("click",this.chooseLocalActivity);
         card.addEventListener("rollOver",this.localActivityOver);
         card.addEventListener("rollOut",this.localActivityOut);
         this.localActivityOverlay.addChild(card);
      }

      private function localActivityOver(e:MouseEvent) : void
      {
         Sprite(e.currentTarget).alpha = 0.78;
      }

      private function localActivityOut(e:MouseEvent) : void
      {
         Sprite(e.currentTarget).alpha = 1;
      }

      private function closeLocalActivities(e:MouseEvent = null) : void
      {
         if(e) e.stopPropagation();
         if(this.localActivityOverlay && this.contains(this.localActivityOverlay)) this.removeChild(this.localActivityOverlay);
         this.localActivityOverlay = null;
         for each(var data:BitmapData in this.localActivityBitmaps) data.dispose();
         this.localActivityBitmaps = [];
      }

      private function chooseLocalActivity(e:MouseEvent) : void
      {
         e.stopPropagation();
         var id:int = int(String(e.currentTarget.name).split(":")[1]);
         if((id == 6 && !gc.playerGuide.checkPlayerIsAttach(5)) || (id == 7 && !gc.playerGuide.checkPlayerIsAttach(7)))
         {
            gc.ts.setTxt(id == 6 ? "福禄寿需要角色平均等级达到10级" : "挑战三太子需要角色平均等级达到30级");
            gc.stage.addChild(gc.ts);
            trace("[offline-activities] locked=" + id);
            return;
         }
         this.closeLocalActivities();
         trace("[offline-activities] choose=" + id);
         switch(id)
         {
            case 0: moonClick(null);break;
            case 1: sunClick(null);break;
            case 2: changeDaJi(null);break;
            case 3: changeLsxs(null);break;
            case 4:
            case 5: this.localMenu.openLocalActivityMenuItem(id);break;
            case 6: fulClick(null);break;
            case 7: stzClick(null);break;
         }
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

   }
}

