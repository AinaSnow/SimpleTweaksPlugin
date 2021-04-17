﻿using Dalamud.Hooking;
using System.Numerics;
using Dalamud.Game.ClientState;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public DisableClickTargeting.Configs DisableClickTargeting = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class DisableClickTargeting : Tweak {

        public class Configs {
            public bool DisableRightClick = true;
            public bool DisableLeftClick;
            public bool OnlyDisableInCombat;
        }

        public Configs Config => PluginConfig.DisableClickTargeting;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("禁用右键选中", ref Config.DisableRightClick);
            hasChanged |= ImGui.Checkbox("禁用左键选中", ref Config.DisableLeftClick);

            if (!(Config.DisableLeftClick || Config.DisableRightClick)) {
                ImGui.Text("都不禁用你开这个选项干什么...");
            }

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.Checkbox("仅在战斗中禁用", ref Config.OnlyDisableInCombat);
            
            if (hasChanged && Enabled) {
                Disable();
                Enable();
            }
        };

        public override string Name => "禁用目标选中";
        public override string Description => "可以禁用鼠标点击选中目标的功能";

        private delegate void* ClickTarget(void** a1, void* a2, bool a3);
        private Hook<ClickTarget> rightClickTargetHook;
        private Hook<ClickTarget> leftClickTargetHook;
        
        public override void Enable() {
            rightClickTargetHook ??= new Hook<ClickTarget>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B"), new ClickTarget(RightClickTargetDetour));
            leftClickTargetHook ??= new Hook<ClickTarget>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 16"), new ClickTarget(LeftClickTargetDetour));
            if (Config.DisableRightClick) rightClickTargetHook?.Enable();
            if (Config.DisableLeftClick) leftClickTargetHook?.Enable();
            base.Enable();
        }
        
        public override void Disable() {
            rightClickTargetHook?.Disable();
            leftClickTargetHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            rightClickTargetHook?.Dispose();
            leftClickTargetHook?.Dispose();
            base.Dispose();
        }

        private void* RightClickTargetDetour(void** a1, void* a2, bool a3) {
            if (a2 != null && a2 == a1[16]) return rightClickTargetHook.Original(a1, a2, a3);
            if (Config.OnlyDisableInCombat && !PluginInterface.ClientState.Condition[ConditionFlag.InCombat]) {
                return rightClickTargetHook.Original(a1, a2, a3);
            }
            return null;
        }
        
        private void* LeftClickTargetDetour(void** a1, void* a2, bool a3) {
            if (a2 != null && a2 == a1[16]) return leftClickTargetHook.Original(a1, a2, a3);
            if (Config.OnlyDisableInCombat && !PluginInterface.ClientState.Condition[ConditionFlag.InCombat]) {
                return leftClickTargetHook.Original(a1, a2, a3);
            }
            return null;
        }
    }
}
