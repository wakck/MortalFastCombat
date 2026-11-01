using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace MortalFastCombat
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> skipHotkeys,skipPadEnabled;
        private ConfigEntry<Key> skipKeyboardKey;
        private ConfigEntry<GamepadButton> skipPadButton;
        private readonly SkipHotkeyLatch skipHotkeyLatch=new SkipHotkeyLatch();
        private int skipHotkeyConsumedFrame=-1;
        private readonly System.Collections.Generic.List<Func<bool>> assistantInputGuards=new System.Collections.Generic.List<Func<bool>>();

        private void SetupSkipHotkeys()
        {
            skipHotkeys=Config.Bind("Hotkeys","EnableSkipHotkeys",true,"启用跳过快捷键；只有本场跳过按钮可用时才生效。");
            skipKeyboardKey=Config.Bind("Hotkeys","SkipKeyboardKey",Key.F10,"键盘跳过键，默认 F10；None 禁用键盘快捷键。使用 Unity Input System 键名。");
            skipPadEnabled=Config.Bind("Hotkeys","EnableSkipGamepadHotkey",true,"启用手柄跳过快捷键。");
            skipPadButton=Config.Bind("Hotkeys","SkipGamepadButton",GamepadButton.RightStick,"手柄跳过键，默认 RightStick（按下右摇杆，R3）。如与助手面板键冲突，请修改其中一方。");
            skipHotkeys.SettingChanged+=ResetSkipHotkeys;
            skipKeyboardKey.SettingChanged+=ResetSkipHotkeys;
            skipPadEnabled.SettingChanged+=ResetSkipHotkeys;
            skipPadButton.SettingChanged+=ResetSkipHotkeys;
            Patch(typeof(Mortal.Core.ToggleSelectedInputAction),"SelectedAction","BeforeSkipSelectedInput",null);
            // ModWindowOpen also includes the always-visible combat forecast HUD.
            // Only actual modal panels, text focus and key capture suppress shortcuts.
            AddAssistantInputGuard("LOMAssistant.InputGuard","BlockKeyboard");
            AddAssistantInputGuard("LOMAssistant.InputGuard","CapturingKey");
            AddAssistantInputGuard("LOMAssistant.Features.Story.StoryReviewPanel","IsOpen");
            AddAssistantInputGuard("LOMAssistant.Features.Extras.SaveNoteEditor","IsOpen");
            AddAssistantInputGuard("LOMAssistant.EventPicker","OwnsEscape");
        }
        private void AddAssistantInputGuard(string typeName,string property)
        {
            Type type=AccessTools.TypeByName(typeName);
            if(type==null)return;
            Func<bool> getter=ReadBooleanGuard(type,property);
            if(getter!=null)assistantInputGuards.Add(getter);
        }
        private static Func<bool> ReadBooleanGuard(Type type,string property)
        {
            MethodInfo getter=AccessTools.PropertyGetter(type,property);
            if(getter==null || !getter.IsStatic || getter.ReturnType!=typeof(bool))return null;
            return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),getter);
        }
        private void ResetSkipHotkeys(object sender,EventArgs args) { skipHotkeyLatch.Reset(); }
        [HarmonyPriority(Priority.First)]
        private static bool BeforeSkipSelectedInput()
        {
            return instance==null || !instance.ConsumeSkipHotkey();
        }
        private bool SkipHotkeyContext()
        {
            if(!isActiveAndEnabled || !Application.isFocused || showSettings || !skipHotkeys.Value || !CanSkip())return false;
            foreach(Func<bool> guard in assistantInputGuards)if(guard())return false;
            if(GUIUtility.hotControl!=0 || GUIUtility.keyboardControl!=0)return false;
            GameObject selected=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;
            if(selected!=null && (selected.GetComponentInParent<InputField>()!=null || selected.GetComponent("TMP_InputField")!=null))return false;
            return true;
        }
        private bool ConsumeSkipHotkey()
        {
            // Native input callbacks can run before Update, or share one press with another action.
            // Consume the accepted press for the rest of this frame as well as locking settlement.
            if(skipHotkeyConsumedFrame==Time.frameCount)return true;
            ButtonControl key=Keyboard.current==null || skipKeyboardKey.Value==Key.None?null:Keyboard.current[skipKeyboardKey.Value];
            ButtonControl pad=!skipPadEnabled.Value || Gamepad.current==null?null:Gamepad.current[skipPadButton.Value];
            bool held=(key!=null && key.isPressed) || (pad!=null && pad.isPressed);
            bool pressed=(key!=null && key.wasPressedThisFrame) || (pad!=null && pad.wasPressedThisFrame);
            if(!skipHotkeyLatch.TakePress(SkipHotkeyContext(),held,pressed))return false;
            if(!TrySkip())return false;
            skipHotkeyConsumedFrame=Time.frameCount;
            Logger.LogInfo("Repeat battle skipped by "+(key!=null && key.wasPressedThisFrame?"keyboard":"gamepad")+" shortcut.");
            return true;
        }
        private string SkipHotkeyHint()
        {
            if(skipHotkeys==null || !skipHotkeys.Value)return "快捷键关闭";
            string key=skipKeyboardKey.Value==Key.None?"":skipKeyboardKey.Value.ToString();
            string pad=!skipPadEnabled.Value?"":skipPadButton.Value==GamepadButton.RightStick?"R3":skipPadButton.Value==GamepadButton.LeftStick?"L3":skipPadButton.Value.ToString();
            return key.Length==0?pad:pad.Length==0?key:key+" / "+pad;
        }
    }
}
