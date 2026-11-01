using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using MortalFastCombat;
using UnityEngine;

public sealed partial class ValidationPlugin
{
    private static ValidationPlugin validation;
    private static bool replaying, ended;
    private static int turns;
    private static float actionTime;
    private static int actionFrame;
    private static List<string> phases, durations;
    private static string replayName;
    private static bool ordinaryFixture;
    private static int benchmarkSpeed=4;
    private void SetupReplayHooks()
    {
        validation=this;
        patches.Patch(AccessTools.Method(typeof(CombatManager),"StartRound"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"SeedRound"));
        patches.Patch(AccessTools.Method(typeof(CombatManager),"GameOver"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"Outcome"));
        // Test instrumentation only: the game's frame-dependent camera shake consumes gameplay's global RNG.
        // Keep those visual draws local during controlled A/B replays. Production Plugin.cs never patches RNG.
        Type shake=AccessTools.Inner(typeof(CombatCharacterController),"<Shake>d__47");
        patches.Patch(AccessTools.Method(shake,"MoveNext"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"BeforeVisualStep"),
            postfix:new HarmonyMethod(typeof(ValidationPlugin),"AfterVisualStep"));
    }
    private static void BeforeVisualStep(out UnityEngine.Random.State __state) {__state=UnityEngine.Random.state;}
    private static void AfterVisualStep(UnityEngine.Random.State __state) {if(replaying)UnityEngine.Random.state=__state;}
    private static void SeedRound()
    { if(replaying) UnityEngine.Random.InitState(73119 + CombatManager.Instance.CurrentRounds); }
    private static void Outcome(bool __0)
    {
        if(!replaying)return;
        RecordPhase("outcome="+__0);
        Log("REPLAY OUTCOME "+replayName+" win="+__0+" turns="+turns);
        ended=true;
    }
    private static void RecordPhase(string label)
    {
        if(!replaying || CombatManager.Instance==null)return;
        try {
        CombatManager m=CombatManager.Instance;
        StringBuilder s=new StringBuilder(label+"|round="+m.CurrentRounds);
        foreach(CombatActionController a in new CombatActionController[]{m.PlayerAction,m.EnemyAction}){
            s.Append("|"+a.name);
            foreach(FieldInfo f in typeof(CombatStat).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType==typeof(string))s.Append(";"+f.Name+"="+f.GetValue(a.Stat.Data));
            foreach(FieldInfo f in typeof(CombatStatController).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
                if(f.FieldType==typeof(CombatStatItem)){
                    CombatStatItem item=(CombatStatItem)f.GetValue(a.Stat);
                    s.Append(";"+f.Name+"="+(item==null?"null":item.FinalValue+":"+(item.ModifyList==null?0:item.ModifyList.Count)));
                }
            }
            foreach(CombatStateEffect e in Get<List<CombatStateEffect>>(a,"_effects"))s.Append(";buff="+e.Key+":"+e.RemainRounds+":"+e.DisplayCount+":"+e.Disable);
        }
        s.Append("|rng="+JsonUtility.ToJson(UnityEngine.Random.state));
        phases.Add(s.ToString());
        } catch(Exception e) {Log("PHASE SNAPSHOT ERROR "+label+" "+e);}
    }
    private IEnumerator Replay(bool fast)
    {
        if(replaying)yield break;
        CheckIsolation();
        Get<ConfigEntry<bool>>(plugin,"enhanced").Value=fast;
        Get<ConfigEntry<int>>(plugin,"multiplier").Value=benchmarkSpeed;
        Get<ConfigEntry<bool>>(plugin,"compress").Value=true;
        SystemSettings.SetCombatSpeedup(true);
        ended=false; turns=0; actionTime=0;
        phases=new List<string>(); durations=new List<string>(); replayName=fast?CaseName+"v"+CaseVersion+"-"+benchmarkSpeed+"x"+(ordinaryFixture?"-ordinary":"-late"):"original-controlled";
        replayName+="-global"+AssistantValue("GameSpeed");
        replaying=true;
        BeginTailProbe();
        QualitySettings.vSyncCount=0; Application.targetFrameRate=30;
        Screen.SetResolution(1280,720,false);
        File.Copy(Path.Combine(root,"battle-original.dat"),Path.Combine(profile,"Save_auto_battle.dat"),true);
        SaveSystem.Instance.SetSlot(SaveSystem.Instance.GetAutoSaveData("auto_battle").Slot);
        SaveSystem.Instance.AutoLoadGameData("auto_battle");
        CombatManager previousManager=CombatManager.Instance;
        SceneController.Instance.LoadCurrentScene();
        float deadline=Time.realtimeSinceStartup+360;
        CombatManager speedOwner=null;
        Log("REPLAY START "+replayName);
        while(!ended && Time.realtimeSinceStartup<deadline) {
            CombatManager m=CombatManager.Instance;
            if(m==previousManager)m=null; // Never issue an action into the outgoing scene during asynchronous reload.
            if(m!=null && m!=speedOwner) {m.EnableSpeedTime(true);speedOwner=m;Log("REPLAY SPEED actual="+Time.timeScale+" requested="+benchmarkSpeed);}
            if(m!=null)Set(m,"_triggerNextInput",true);
            bool canAct=false;
            if(m!=null && CombatUIManager.Instance!=null && m.CurrentRounds>turns){
                CombatActionButtonPanel panel=Get<CombatActionButtonPanel>(CombatUIManager.Instance,"_actionPanel");
                CanvasGroup group=Get<CanvasGroup>(panel,"_canvasGroup");
                canAct=group.interactable && group.alpha>0.99f;
            }
            if(canAct){
                TailReady();
                if(actionTime>0)durations.Add(turns+","+(Time.frameCount-actionFrame)+","+(Time.realtimeSinceStartup-actionTime).ToString("F4"));
                RecordPhase("turn-ready");
                ++turns;
                actionFrame=Time.frameCount; actionTime=Time.realtimeSinceStartup;
                m.Action((CombatActionType)2); // CombatActionButtonPanel.AttackButtonClick uses enum value 2.
            }
            yield return null;
        }
        Log("REPLAY COMPLETE "+replayName+" ended="+ended+" turns="+turns+" phases="+phases.Count);
        File.WriteAllLines(Path.Combine(root,replayName+"-phases.txt"),phases.ToArray());
        durations.Insert(0,"turn,frames,seconds");
        File.WriteAllLines(Path.Combine(root,replayName+"-durations.csv"),durations.ToArray());
        SaveTailProbe();
        replaying=false;
        yield return new WaitForSecondsRealtime(3);
        Log("POST-REPLAY scale="+Time.timeScale+" scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        Get<ConfigEntry<bool>>(plugin,"enhanced").Value=true;
    }
    private IEnumerator CompareBoth() {yield return Replay(false);yield return Replay(true);Log("CONTROLLED COMPARE FINISHED");}
}
