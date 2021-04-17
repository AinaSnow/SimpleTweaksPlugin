﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public NotificationToastAdjustments.Configs NotificationToastAdjustments = new NotificationToastAdjustments.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class NotificationToastAdjustments : UiAdjustments.SubTweak {
        public override string Name => "弹出通知修改";
        public override string Description => "移动或隐藏屏幕中央显示的弹出通知内容";
        protected override string Author => "Aireil";

        public class Configs {
            public bool Hide = false;
            public bool ShowInCombat = false;
            public int OffsetXPosition = 0;
            public int OffsetYPosition = 0;
            public List<string> Exceptions = new List<string>();
        }

        public Configs Config => PluginConfig.UiAdjustments.NotificationToastAdjustments;

        private string newException = String.Empty;
        private bool isPreviewing = false;
        private Task previewDelayTask;
        private CancellationTokenSource tokenSource;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("隐藏", ref Config.Hide);
            if (Config.Hide) {
                ImGui.SameLine();
                hasChanged |= ImGui.Checkbox("战斗中显示", ref Config.ShowInCombat);
            }

            if (!Config.Hide || Config.ShowInCombat) {
                var offsetChanged = false;
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                offsetChanged |= ImGui.InputInt("水平偏移##offsetPosition", ref Config.OffsetXPosition, 1);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                offsetChanged |= ImGui.InputInt("垂直偏移##offsetPosition", ref Config.OffsetYPosition, 1);
                if (offsetChanged) {
                    if (!isPreviewing) {
                        isPreviewing = true;
                        tokenSource = new CancellationTokenSource();
                        previewDelayTask = Task.Delay(5000, tokenSource.Token).ContinueWith(t => {
                            isPreviewing = false;
                            UpdateNotificationToastText(true);
                        });
                    }
                    hasChanged = true;
                }
            }

            if (!Config.Hide) {
                ImGui.Text("如果通知含有以下内容则隐藏:");
                for (int i = 0; i < Config.Exceptions.Count; i++) {
                    ImGui.PushID($"Exception_{i}");
                    var exception = Config.Exceptions[i];
                    if (ImGui.InputText("##ToastTextException", ref exception, 500)) {
                        Config.Exceptions[i] = exception;
                        hasChanged = true;
                    }
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString())) {
                        Config.Exceptions.RemoveAt(i--);
                        hasChanged = true;
                    }
                    ImGui.PopFont();
                    ImGui.PopID();
                    if (i < 0) break;
                }
                ImGui.InputText("##NewToastTextException", ref newException, 500);
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString())) {
                    Config.Exceptions.Add(newException);
                    newException = String.Empty;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }
        };

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            if (tokenSource != null && previewDelayTask != null) {
                tokenSource.Cancel();
                while (!previewDelayTask.IsCompleted) Thread.Sleep(1);
            }
            UpdateNotificationToastText(true);
            base.Disable();
        }

        private void FrameworkOnUpdate(Framework framework) {
            try {
                UpdateNotificationToastText();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        public void UpdateNotificationToastText(bool reset = false) {
            var toastUnitBase = Common.GetUnitBase("_WideText", 2);
            if (toastUnitBase == null) return;
            if (toastUnitBase->UldManager.NodeList == null || toastUnitBase->UldManager.NodeListCount < 4) return;

            var toastNode1 = toastUnitBase->UldManager.NodeList[0];
            var toastNode2 = toastUnitBase->UldManager.NodeList[1];
            var toastBackgroundNode = toastUnitBase->UldManager.NodeList[2];
            var toastTextNode = (AtkTextNode*)toastUnitBase->UldManager.NodeList[3];

            if (reset) {
                isPreviewing = false;

                UiHelper.Show(toastBackgroundNode);
                UiHelper.Show(toastTextNode);

                SetOffsetPosition(toastNode1, 0.0f, 0.0f);

                toastNode1->Color.A = 0;
                toastNode2->Color.A = 0;

                UiHelper.Hide(toastNode1);
                UiHelper.Show(toastNode2);

                toastUnitBase->Flags |= 0x10;
                toastUnitBase->Flags &= unchecked((byte)~0x20);
                toastUnitBase->Flags |= 0x40;

                return;
            }

            if (!isPreviewing && !toastUnitBase->IsVisible) return; // no point continuing

            var hide = Config.Hide;

            if (Config.Exceptions.Any() && !Config.Hide && !isPreviewing) {
                // var text = Marshal.PtrToStringAnsi(new IntPtr(toastTextNode->NodeText.StringPtr));
                // fix text tranfer problem
                byte[] buffer1 = System.Text.Encoding.Default.GetBytes(Marshal.PtrToStringAnsi(new IntPtr(toastTextNode->NodeText.StringPtr)));
                byte[] buffer2 = System.Text.Encoding.Convert(System.Text.Encoding.UTF8, System.Text.Encoding.Default, buffer1, 0, buffer1.Length);
                string text = System.Text.Encoding.Default.GetString(buffer2, 0, buffer2.Length);
                hide = Config.Exceptions.Any(x => text.Contains(x));
            }

            if (Config.Hide && Config.ShowInCombat && !isPreviewing) {
                var inCombat = PluginInterface.ClientState.Condition[Dalamud.Game.ClientState.ConditionFlag.InCombat];
                if (inCombat) {
                    hide = false;
                }
            }

            if (hide && !isPreviewing) {
                UiHelper.Hide(toastBackgroundNode);
                UiHelper.Hide(toastTextNode);
            }
            else {
                UiHelper.Show(toastBackgroundNode);
                UiHelper.Show(toastTextNode);

                SetOffsetPosition(toastNode1, Config.OffsetXPosition, Config.OffsetYPosition);

                if (isPreviewing) {
                    var text = Marshal.PtrToStringAnsi(new IntPtr(toastTextNode->NodeText.StringPtr));
                    if (text == String.Empty) {
                        UiHelper.SetText(toastTextNode, "这只是一个预览，不是游戏通知");
                    }

                    toastNode1->Color.A = 255;
                    toastNode2->Color.A = 255;

                    UiHelper.Show(toastNode1);
                    UiHelper.Show(toastNode2);

                    toastUnitBase->Flags &= unchecked((byte)~0x10);
                    toastUnitBase->Flags |= 0x20;
                    toastUnitBase->Flags &= unchecked((byte)~0x40);
                }
            }
        }

        private void SetOffsetPosition(AtkResNode* node, float offsetX, float offsetY) {
            var defaultXPos = (ImGui.GetIO().DisplaySize.X * 1 / 2) - 512;
            var defaultYPos = (ImGui.GetIO().DisplaySize.Y * 3 / 5) - 20;

            UiHelper.SetPosition(node, defaultXPos + offsetX, defaultYPos - offsetY);
        }
    }
}
