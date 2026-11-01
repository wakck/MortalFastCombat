using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using MortalFastCombat;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class ValidationPlugin
{
    private bool startupFixturesReady;
    private IEnumerator StartupFixtures()
    {
        yield return NotificationFixtures();
        startupFixturesReady=true;Log("HANDOFF STARTUP FIXTURES READY");
    }
    private static void SetGlobal(float speed)
    {
        Type t=AccessTools.TypeByName("LOMAssistant.Config.ConfigManager");
        object setting=AccessTools.Field(t,"GameSpeed").GetValue(null);
        setting.GetType().GetProperty("Value").SetValue(setting,speed,null);
        Log("GLOBAL CHOICE "+speed);
    }
    private IEnumerator HandoffTests()
    {
        int initial=assertions;
        CheckIsolation();
        Type speed=AccessTools.TypeByName("LOMAssistant.Features.Extras.GameSpeed");
        Check(AccessTools.Field(typeof(Plugin),"assistantBase").GetValue(plugin)!=null,"Assistant handoff patch installed");
        AccessTools.Field(speed,"AutoSay").SetValue(null,false);
        foreach(int value in new int[]{2,4}){
            SetGlobal(value);Time.timeScale=1;
            yield return new WaitForSecondsRealtime(.25f);
            Check(Time.timeScale==value,"Outside Combat uses assistant global "+value);
            yield return new WaitForSecondsRealtime(.5f);
            Check(Time.timeScale==value,"Outside global speed does not compound "+value);
        }
        Time.timeScale=0;SetGlobal(2);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==0,"Changing global speed keeps outside pause");
        Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==2,"Outside resume restores global 2x");
        Scene previous=SceneManager.GetActiveScene();
        Scene free=SceneManager.CreateScene("Free");SceneManager.SetActiveScene(free);Time.timeScale=1;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==2,"Free scene probe retains global 2x");
        SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(free);
        Scene battle=SceneManager.CreateScene("Battle");SceneManager.SetActiveScene(battle);Time.timeScale=1;SetGlobal(4);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==1,"Native Battle exemption unchanged");
        SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(battle);
        Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Leaving exempt scene restores global 4x");
        // Exercise a paused scene exit without unpausing or destroying the running test scene.
        AccessTools.Method(typeof(Plugin),"AcquireAssistantSpeed").Invoke(plugin,null);
        Time.timeScale=0;
        AccessTools.Method(typeof(Plugin),"ReleaseAssistantSpeed").Invoke(plugin,null);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==0,"Paused ownership release stays paused");
        Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Resume after paused release restores global 4x");
        AccessTools.Method(typeof(Plugin),"AcquireAssistantSpeed").Invoke(plugin,null);
        Time.timeScale=.5f;
        AccessTools.Method(typeof(Plugin),"ReleaseAssistantSpeed").Invoke(plugin,null);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==2,"External native .5x transition preserved with global 4x");
        SetGlobal(2);Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        ordinaryFixture=true;yield return LoadReady();
        CombatManager manager=CombatManager.Instance;
        Check(Get<bool>(plugin,"assistantHeld"),"Real Combat acquires multiplier ownership");
        foreach(int value in new int[]{2,4,8}){
            Get<ConfigEntry<int>>(plugin,"multiplier").Value=value;manager.EnableSpeedTime(true);
            yield return new WaitForSecondsRealtime(.25f);
            Check(Time.timeScale==value,"Global 2x does not multiply combat "+value);
        }
        SetGlobal(4);yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==8,"Global setting changes during combat do not change combat speed");
        Time.timeScale=0;SetGlobal(2);Get<ConfigEntry<int>>(plugin,"multiplier").Value=4;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==0,"Changing both settings keeps combat paused");
        Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Combat resume restores selected 4x without global multiplication");
        manager.EnableSpeedTime(false);yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==1,"Quick combat off uses 1x despite global 2x");
        manager.EnableSpeedTime(true);Get<ConfigEntry<bool>>(plugin,"enhanced").Value=false;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==2,"Mod toggle off preserves native quick 2x without global multiplication");
        Get<ConfigEntry<bool>>(plugin,"enhanced").Value=true;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Mod toggle on restores combat 4x");
        SetGlobal(4);
        Set(manager.CombatLevel,"_deadEnd",false);
        manager.StartCoroutine((IEnumerator)AccessTools.Method(typeof(CombatManager),"GameOver").Invoke(manager,new object[]{false}));
        yield return new WaitForSecondsRealtime(6);
        Check(SceneManager.GetActiveScene().name=="Story" && Time.timeScale==4,"Defeat restores latest global 4x in Story");
        Check(!Get<bool>(plugin,"assistantHeld"),"Leaving Combat releases ownership");
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==0,"Story pause remains functional after combat");
        Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Story resume reapplies global without compounding");
        AccessTools.Field(speed,"AutoSay").SetValue(null,true);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==1,"Assistant Story auto-dialogue exemption preserved");
        AccessTools.Field(speed,"AutoSay").SetValue(null,false);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Time.timeScale==4,"Ending auto-dialogue exemption restores global 4x");
        SetGlobal(2);Time.timeScale=1;yield return new WaitForSecondsRealtime(.25f);
        ReportCoexist();Log("HANDOFF TESTS COMPLETE assertions="+(assertions-initial));
    }
}
