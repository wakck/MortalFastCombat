using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using MortalFastCombat;
using UnityEngine;

public sealed partial class ValidationPlugin
{
#if BEPINEX6
    private const string CaseName="handoff-bep6-";
    private const string CaseVersion="1.1.2";
#else
    private const string CaseName="baseline-bep5-";
    private const string CaseVersion="1.1.0";
#endif
    private int scaleSamples,scaleErrors;
    private static object AssistantValue(string key)
    {
        Type t=AccessTools.TypeByName("LOMAssistant.Config.ConfigManager");
        if(t==null)return null;
        object setting=AccessTools.Field(t,key).GetValue(null);
        return setting.GetType().GetProperty("Value").GetValue(setting,null);
    }
    private void SetupCoexist()
    {
        string expected=Path.GetFullPath(Path.Combine(Paths.GameRootPath,"sandbox/persistent"));
        if(Path.GetFullPath(Application.persistentDataPath)!=expected)Environment.FailFast("Persistent path isolation failed");
        if(!File.Exists(Path.Combine(Paths.GameRootPath,"isolation-root.txt")))Environment.FailFast("Isolation marker missing");
        if(!Get<bool>(plugin,"compatible"))throw new Exception("MFC fingerprint guard rejected test installation");
        Log("NATIVE PERSISTENT PATH VERIFIED: "+Application.persistentDataPath);
        ReportCoexist();
    }
    private void ReportCoexist()
    {
        Log("COEXIST STATUS case="+CaseName+" mfcActive="+plugin.isActiveAndEnabled+" mfcObject="+plugin.gameObject.name+" hide="+plugin.gameObject.hideFlags+
            " samples="+scaleSamples+" scaleErrors="+scaleErrors);
#if BEPINEX6
        foreach(string key in new string[]{"GameSpeed","EnableFlowSpeedup","EnableCombatAssist"})Log("ASSISTANT CONFIG "+key+"="+AssistantValue(key));
        if(Convert.ToBoolean(AssistantValue("EnableFlowSpeedup")) || Convert.ToBoolean(AssistantValue("EnableCombatAssist")))throw new Exception("Overlapping assistant features not disabled");
#endif
    }
    private void LateUpdate()
    {
        if(plugin==null || !replaying || !Get<bool>(plugin,"inCombat") || Time.timeScale==0)return;
        ++scaleSamples;
        bool enabled=Get<ConfigEntry<bool>>(plugin,"enhanced").Value;
        bool fast=Get<bool>(plugin,"fastRequested");
        float expected=!fast?1f:enabled?Get<ConfigEntry<int>>(plugin,"multiplier").Value:2f;
        if(!Mathf.Approximately(Time.timeScale,expected)){
            ++scaleErrors;
            if(scaleErrors<=5)Log("SCALE MISMATCH actual="+Time.timeScale+" expected="+expected);
        }
    }
    private IEnumerator SwitchTests()
    {
        ordinaryFixture=true;
        yield return LoadReady();
        CombatManager manager=CombatManager.Instance;
        foreach(int speed in new int[]{2,4,8}) {
            Get<ConfigEntry<int>>(plugin,"multiplier").Value=speed;manager.EnableSpeedTime(true);
            yield return new WaitForSecondsRealtime(.15f);
            Check(Mathf.Approximately(Time.timeScale,speed),"Coexist selected multiplier stable "+speed);
        }
        Time.timeScale=0;
        Get<ConfigEntry<int>>(plugin,"multiplier").Value=4;
        yield return new WaitForSecondsRealtime(.15f);
        Check(Time.timeScale==0,"Multiplier change respects pause");
        Time.timeScale=1;
        yield return new WaitForSecondsRealtime(.15f);
        Check(Time.timeScale==4,"Pause recovery restores selected multiplier");
        manager.EnableSpeedTime(false);
        yield return new WaitForSecondsRealtime(.15f);
        Check(Time.timeScale==1,"Closing quick combat restores 1x");
        manager.EnableSpeedTime(true);
        Get<ConfigEntry<bool>>(plugin,"compress").Value=false;
        Check(!(bool)AccessTools.Method(typeof(Plugin),"FastBuffs").Invoke(plugin,null),"Compression can be disabled during battle");
        Get<ConfigEntry<bool>>(plugin,"compress").Value=true;
        Check((bool)AccessTools.Method(typeof(Plugin),"FastBuffs").Invoke(plugin,null),"Compression can be reenabled during battle");
        Get<ConfigEntry<bool>>(plugin,"enhanced").Value=false;
        yield return new WaitForSecondsRealtime(.15f);
        Check(Time.timeScale==2,"Disabled mod preserves native quick combat");
        Get<ConfigEntry<bool>>(plugin,"enhanced").Value=true;
        yield return new WaitForSecondsRealtime(.15f);
        Check(Time.timeScale==4,"Reenabled mod restores enhanced combat");
        Set(manager.CombatLevel,"_deadEnd",false);
        manager.StartCoroutine((IEnumerator)AccessTools.Method(typeof(CombatManager),"GameOver").Invoke(manager,new object[]{false}));
        yield return new WaitForSecondsRealtime(6);
        Check(!Get<bool>(plugin,"inCombat") && Time.timeScale==Convert.ToSingle(AssistantValue("GameSpeed")),"Defeat settlement clears speed and returns to story");
        Check(plugin.isActiveAndEnabled,"Production plugin remains active across scenes");
        ReportCoexist();Log("SWITCH TESTS COMPLETE");
    }
}
