﻿using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class OldNameplatesTweak : UiAdjustments.SubTweak {
        public override string Name => "老版本姓名版(不要和JobIcons一起开,炸给你看哦)";
        public override string Description => "显示5.5版本前的姓名版样式.";
        protected override string Author => "aers";

        private delegate void AddonNameplateOnUpdateDelegate(AddonNamePlate* thisPtr, NumberArrayData** numberData,
            StringArrayData** stringData);
        private Hook<AddonNameplateOnUpdateDelegate> addonNameplateOnUpdateHook;

        public override void Enable() {
            addonNameplateOnUpdateHook ??= new Hook<AddonNameplateOnUpdateDelegate>(Common.Scanner.ScanText("48 8B C4 41 56 48 81 EC ?? ?? ?? ?? 48 89 58 F0"), new AddonNameplateOnUpdateDelegate(AddonNameplateOnUpdateDetour));
            addonNameplateOnUpdateHook?.Enable();
            base.Enable();
        }
        
        public override void Disable() {
            addonNameplateOnUpdateHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            addonNameplateOnUpdateHook?.Dispose();
            base.Dispose();
        }

        private void AddonNameplateOnUpdateDetour(AddonNamePlate* thisPtr, NumberArrayData** numberData,
            StringArrayData** stringData) {
            //PluginLog.Error((((long)numberData[5]->IntArray)+0xC).ToString("X")+"@"+numberData[5]->IntArray[3]);
            numberData[5]->IntArray[3] = 1;
            this.addonNameplateOnUpdateHook.Original(thisPtr, numberData, stringData);
        }
    }
}
