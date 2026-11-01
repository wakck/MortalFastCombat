using System;
using System.Collections;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using MortalFastCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed partial class ValidationPlugin
{
    private Keyboard virtualKeyboard;
    private Gamepad virtualPad;
    private void Neutral(){InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState());InputSystem.QueueStateEvent(virtualPad,new GamepadState());}
    private void KeyDown(Key key){InputSystem.QueueStateEvent(virtualKeyboard,new KeyboardState(key));}
    private void PadDown(GamepadButton button){InputSystem.QueueStateEvent(virtualPad,new GamepadState().WithButton(button,true));}
    private static bool Skipping(){return Get<bool>(validation.plugin,"skipping") || Get<bool>(validation.plugin,"settling");}
    private IEnumerator InputFrames(){yield return null;yield return null;yield return null;}
    private void BeginResultCount(){resultApplications=0;countResults=true;expectedResults=CombatManager.Instance.CombatLevel.WinResult.Length;}
    private IEnumerator CheckHotkeySettlement(string device)
    {
        yield return new WaitForSecondsRealtime(6);
        Check(resultApplications==expectedResults,device+" executes current native victory results exactly once");
        Check(SceneManager.GetActiveScene().name=="Story",device+" returns to Story");
        Check(Time.timeScale==Convert.ToSingle(AssistantValue("GameSpeed")),device+" restores global speed");
        Check(!Get<bool>(plugin,"actionIssued"),device+" does not issue a normal battle command");
        countResults=false;
    }
    private void HotkeyDiagnostics()
    {
        Log("HOTKEY STATE canSkip="+AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null)+
            " context="+AccessTools.Method(typeof(Plugin),"SkipHotkeyContext").Invoke(plugin,null)+
            " ready="+Get<bool>(plugin,"firstReady")+" focused="+Application.isFocused+
            " guiKey="+GUIUtility.keyboardControl+" guiHot="+GUIUtility.hotControl+" armed="+Get<bool>(Get<object>(plugin,"skipHotkeyLatch"),"armed")+" keyboardId="+Keyboard.current.deviceId+" virtualId="+virtualKeyboard.deviceId+" keyEnabled="+virtualKeyboard.enabled+" pressed="+virtualKeyboard.f10Key.wasPressedThisFrame+" held="+virtualKeyboard.f10Key.isPressed);
        Type guard=AccessTools.TypeByName("LOMAssistant.InputGuard");
        foreach(Func<bool> inputGuard in Get<System.Collections.Generic.List<Func<bool>>>(plugin,"assistantInputGuards"))Log("HOTKEY MODAL "+inputGuard.Method.DeclaringType.FullName+" "+inputGuard.Method.Name+"="+inputGuard());
        Log("HOTKEY GUARD active="+plugin.isActiveAndEnabled+" settings="+Get<bool>(plugin,"showSettings")+" enabled="+Get<ConfigEntry<bool>>(plugin,"skipHotkeys").Value+" window="+AccessTools.PropertyGetter(guard,"ModWindowOpen").Invoke(null,null)+" blocked="+AccessTools.PropertyGetter(guard,"BlockKeyboard").Invoke(null,null)+" selected="+(UnityEngine.EventSystems.EventSystem.current==null?"none":Convert.ToString(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject)));
    }
    private IEnumerator HotkeyTests()
    {
        assertions=0;
        CheckIsolation();
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        virtualKeyboard=InputSystem.AddDevice<Keyboard>();virtualPad=InputSystem.AddDevice<Gamepad>();
        Neutral();yield return InputFrames();
        Check(Get<ConfigEntry<Key>>(plugin,"skipKeyboardKey").Value==Key.F10,"Default keyboard shortcut is F10");
        Check(Get<ConfigEntry<GamepadButton>>(plugin,"skipPadButton").Value==GamepadButton.RightStick,"Default gamepad shortcut is R3");
        FreshHistory();ordinaryFixture=true;yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);
        int collisions=0;
        foreach(InputAction action in InputSystem.ListEnabledActions())foreach(InputBinding binding in action.bindings){
            string path=(binding.effectivePath??"").ToLowerInvariant();
            if(path=="<keyboard>/f10" || (path=="<gamepad>/rightstickpress" && action.name!="ToggleSelected")){collisions++;Log("DEFAULT KEY COLLISION "+action.name+" "+path);}
        }
        Check(collisions==0,"Default keys have no unknown native binding collision");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Mortal.Core.ToggleSelectedInputAction),"SelectedAction")).Prefixes.Any(p=>p.owner==Plugin.Guid),"Native R3 selection action has scoped shortcut interception");
        Check(!(bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Unwon enemy is not eligible");
        KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"F10 cannot skip an unrecorded enemy");
        VictoryHistory h=Get<VictoryHistory>(plugin,"history");h.Record(Get<string>(plugin,"enemyKey"));
        yield return InputFrames();
        Check(!Skipping(),"Holding F10 through becoming eligible does not skip");
        Neutral();yield return InputFrames();
        Check((bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Recorded ordinary enemy permits shortcut");
        Time.timeScale=0;KeyDown(Key.F10);yield return InputFrames();
        Check(Time.timeScale==0 && !Skipping(),"F10 preserves pause and cannot skip paused battle");
        Neutral();yield return InputFrames();Time.timeScale=1;yield return InputFrames();
        Get<ConfigEntry<bool>>(plugin,"skipHotkeys").Value=false;KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"Global shortcut toggle disables F10");
        Neutral();Get<ConfigEntry<bool>>(plugin,"skipHotkeys").Value=true;yield return InputFrames();
        Set(plugin,"showSettings",true);KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"Settings panel blocks F10");
        Neutral();Set(plugin,"showSettings",false);yield return InputFrames();
        Type review=AccessTools.TypeByName("LOMAssistant.Features.Story.StoryReviewPanel");
        object reviewPanel=AccessTools.PropertyGetter(review,"Instance").Invoke(null,null);
        Set(reviewPanel,"_visible",true);KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"Assistant settings popup blocks F10");
        Set(reviewPanel,"_visible",false);Neutral();yield return InputFrames();
        var eventSystem=UnityEngine.EventSystems.EventSystem.current;
        GameObject previousSelection=eventSystem.currentSelectedGameObject;
        GameObject textInput=new GameObject("Shortcut text input fixture",typeof(RectTransform),typeof(UnityEngine.UI.InputField));
        eventSystem.SetSelectedGameObject(textInput);Neutral();yield return InputFrames();
        KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"Selected text input blocks F10");
        eventSystem.SetSelectedGameObject(previousSelection);Destroy(textInput);Neutral();yield return InputFrames();
        HotkeyDiagnostics();BeginResultCount();KeyDown(Key.F10);yield return InputFrames();
        HotkeyDiagnostics();
        Check(Skipping(),"F10 starts native skip through actual Input System keyboard event");
        KeyDown(Key.F10);yield return InputFrames();
        yield return CheckHotkeySettlement("Keyboard F10");
        Check(h.Run(h.Current).victories.Count==1,"Keyboard skip adds no victory record");

        Neutral();ordinaryFixture=true;yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);Neutral();yield return InputFrames();
        Get<ConfigEntry<Key>>(plugin,"skipKeyboardKey").Value=Key.F11;KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"Keyboard remapping stops the old F10 key");
        Neutral();Get<ConfigEntry<Key>>(plugin,"skipKeyboardKey").Value=Key.None;KeyDown(Key.F10);yield return InputFrames();
        Check(!Skipping(),"None disables keyboard shortcut");
        Neutral();Get<ConfigEntry<bool>>(plugin,"skipPadEnabled").Value=false;PadDown(GamepadButton.RightStick);yield return InputFrames();
        Check(!Skipping(),"Gamepad shortcut toggle disables R3");
        Neutral();Get<ConfigEntry<bool>>(plugin,"skipPadEnabled").Value=true;yield return InputFrames();
        Set(plugin,"showSettings",true);PadDown(GamepadButton.RightStick);yield return InputFrames();
        Check(!Skipping(),"Settings panel blocks R3");
        Neutral();Set(plugin,"showSettings",false);yield return InputFrames();
        HotkeyDiagnostics();BeginResultCount();PadDown(GamepadButton.RightStick);yield return InputFrames();
        Check(Skipping(),"R3 starts native skip through actual Input System gamepad event");
        PadDown(GamepadButton.RightStick);yield return InputFrames();
        yield return CheckHotkeySettlement("Gamepad R3");
        Check(h.Run(h.Current).victories.Count==1,"Gamepad skip adds no victory record");

        Neutral();yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);
        Get<ConfigEntry<GamepadButton>>(plugin,"skipPadButton").Value=GamepadButton.South;Neutral();yield return InputFrames();
        BeginResultCount();PadDown(GamepadButton.South);yield return InputFrames();
        Check(Skipping(),"Remapped gamepad South/A skip wins over normal input callback");
        yield return CheckHotkeySettlement("Remapped gamepad");

        Neutral();yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);
        Get<ConfigEntry<Key>>(plugin,"skipKeyboardKey").Value=Key.F11;Get<ConfigEntry<GamepadButton>>(plugin,"skipPadButton").Value=GamepadButton.RightStick;Neutral();yield return InputFrames();
        BeginResultCount();KeyDown(Key.F11);yield return InputFrames();
        Check(Skipping(),"Remapped F11 starts skip");
        yield return CheckHotkeySettlement("Remapped keyboard");
        Neutral();Get<ConfigEntry<Key>>(plugin,"skipKeyboardKey").Value=Key.F10;

        ordinaryFixture=false;yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);Neutral();yield return InputFrames();
        KeyDown(Key.F10);PadDown(GamepadButton.RightStick);yield return InputFrames();
        Check(!Skipping(),"Both shortcuts reject actual scripted combat");
        Neutral();ordinaryFixture=true;yield return LoadReady();yield return new WaitForSecondsRealtime(.3f);Neutral();yield return InputFrames();
        CombatManager.Instance.Action((CombatActionType)2);KeyDown(Key.F10);PadDown(GamepadButton.RightStick);yield return InputFrames();
        Check(!Get<bool>(plugin,"skipping") && Get<bool>(plugin,"actionIssued"),"First normal command permanently closes shortcut window");
        Neutral();InputSystem.RemoveDevice(virtualKeyboard);InputSystem.RemoveDevice(virtualPad);
        Log("HOTKEY TESTS COMPLETE assertions="+assertions);
    }
}
