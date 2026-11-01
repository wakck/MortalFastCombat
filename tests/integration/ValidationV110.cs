using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using MortalFastCombat;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ValidationPlugin
{
    private static int assertions,resultApplications,expectedResults;
    private static bool countResults;
    private void FreshHistory()
    {
        CheckIsolation();
        File.Copy(Path.Combine(root,"battle-original.dat"),Path.Combine(profile,"Save_auto_battle.dat"),true);
        VictoryHistory h=(VictoryHistory)AccessTools.Method(typeof(Plugin),"History").Invoke(plugin,null);
        h.NewRun();h.Associate(VictoryHistory.Hash(JsonUtility.ToJson(SaveSystem.Instance.GetAutoSaveData("auto_battle").GameSave)));
        HistoryStatus();
    }
    private void HistoryStatus()
    {VictoryHistory h=(VictoryHistory)AccessTools.Method(typeof(Plugin),"History").Invoke(plugin,null);VictoryRun run=h.Run(h.Current);Log("HISTORY STATUS current="+h.Current+" healthy="+h.Healthy+" victories="+(run==null?-1:run.victories.Count));}
    private IEnumerator PersistenceCheck()
    {
        ordinaryFixture=true;
        yield return LoadReady();
        VictoryHistory h=Get<VictoryHistory>(plugin,"history");string run=h.Current;
        Check(h.Healthy && h.Run(run).victories.Count==1,"Real played victory survives process restart");
        Check((bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Reloaded identical enemy enables skip before any command");
        Log("PERSISTENCE run="+run+" victories="+h.Run(run).victories.Count);
        Check((bool)AccessTools.Method(typeof(Plugin),"TrySkip").Invoke(plugin,null),"Persisted victory can start native skip");
        yield return new WaitForSecondsRealtime(6);
        Check(h.Run(run).victories.Count==1,"Skipped victory does not add a history entry");
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Story" && Mathf.Approximately(Time.timeScale,Convert.ToSingle(AssistantValue("GameSpeed"))),"Restarted skip restores current global speed in Story");
        Log("PERSISTENCE COMPLETE run="+run+" victories="+h.Run(run).victories.Count);
    }
    private void SetupV110()
    {
        patches.Patch(AccessTools.Method(typeof(CombatManager),"InitCombatRound"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"FixtureLevel"));
        patches.Patch(AccessTools.Method(typeof(CombatResultFlag),"Apply"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"CountResult"));
        foreach(Type t in typeof(CombatStatBar).GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public))
            if(t.Name.StartsWith("<Setup>"))patches.Patch(AccessTools.Method(t,"MoveNext"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"BarProbeBefore"),postfix:new HarmonyMethod(typeof(ValidationPlugin),"BarProbeAfter"));
        foreach(Type t in typeof(Plugin).GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public))
            if(t.Name.StartsWith("<SnapBar>"))patches.Patch(AccessTools.Method(t,"MoveNext"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"BarProbeBefore"),postfix:new HarmonyMethod(typeof(ValidationPlugin),"BarProbeAfter"));
    }
    private static Dictionary<object,Probe> bars=new Dictionary<object,Probe>();
    private static void BarProbeBefore(object __instance)
    {if(replaying && !bars.ContainsKey(__instance))bars.Add(__instance,new Probe{Frame=Time.frameCount,Time=Time.realtimeSinceStartup});}
    private static void BarProbeAfter(object __instance,bool __result)
    {if(replaying && !__result && bars.ContainsKey(__instance)){Probe b=bars[__instance];bars.Remove(__instance);Log("BAR frames="+(Time.frameCount-b.Frame)+" seconds="+(Time.realtimeSinceStartup-b.Time).ToString("F6"));}}
    private static void CountResult() {if(countResults)resultApplications++;}
    private static void FixtureLevel(CombatManager __instance)
    {if(ordinaryFixture)PrepareOrdinary(__instance);}
    private static void PrepareOrdinary(CombatManager manager)
    {
        CombatLevel level=UnityEngine.Object.Instantiate(manager.CombatLevel);
        level.Events=new List<CombatEventData>();
        Set(level,"_conditionResult",new CombatConditionResultFlag[0]);Set(level,"_talkingEvent",null);
        Set(manager,"_combatLevel",level);
        // Keep actual character/skills. This is an explicitly artificial ordinary-rule fixture.
        CombatStat stat=(CombatStat)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager.EnemyAction.Stat.Data,null);
        stat.TalentItems=new List<CombatTalentItem>();
        Set(manager.EnemyAction.Stat,"_statData",stat);Set(level,"_enemyStat",stat);
        Log("ORDINARY FIXTURE: removed scripted level events and enemy talents in isolated runtime only");
    }
    private static void Check(bool ok,string label)
    {if(!ok)throw new Exception("V110 ASSERT: "+label);assertions++;Log("V110 PASS "+label);}
    private IEnumerator LoadReady()
    {
        CheckIsolation();
        File.Copy(Path.Combine(root,"battle-original.dat"),Path.Combine(profile,"Save_auto_battle.dat"),true);
        CombatManager previousManager=CombatManager.Instance;
        SaveSystem.Instance.SetSlot(SaveSystem.Instance.GetAutoSaveData("auto_battle").Slot);
        SoundManager.Instance.StopMusic();SaveSystem.Instance.AutoLoadGameData("auto_battle");SceneController.Instance.LoadCurrentScene();
        float until=Time.realtimeSinceStartup+45;
        bool found=false;
        while(Time.realtimeSinceStartup<until) {
            CombatManager m=CombatManager.Instance;
            if(m==previousManager)m=null;
            if(m!=null){Set(m,"_triggerNextInput",true);if(CombatUIManager.Instance!=null && m.CurrentRounds==1 && Get<CanvasGroup>(Get<CombatActionButtonPanel>(CombatUIManager.Instance,"_actionPanel"),"_canvasGroup").interactable){found=true;break;}}
            yield return null;
        }
        Check(found,"Reached first command panel");
        yield return null;
    }
    private IEnumerator V110Tests()
    {
        assertions=0;
        ordinaryFixture=true;
        yield return LoadReady();
        CombatManager manager=CombatManager.Instance;
        yield return BarTests(manager);
        try {
            string key=EnemyFingerprint.Capture(manager);
            Check(key.Length==64,"Full enemy configuration serializes");
            Check(EnemyFingerprint.Unsupported(manager.CombatLevel)==null,"Ordinary-rule fixture eligible");
            manager.EnemyAction.Stat.Data.Strength++;
            Check(EnemyFingerprint.Capture(manager)!=key,"One stat change rejects match");manager.EnemyAction.Stat.Data.Strength--;
            string old=manager.EnemyAction.Stat.Data.Name;manager.EnemyAction.Stat.Data.Name=old+"-different-internal-character";
            Check(EnemyFingerprint.Capture(manager)!=key,"Different internal character rejects match");manager.EnemyAction.Stat.Data.Name=old;
            old=manager.EnemyAction.Stat.Data.UltimateOne;manager.EnemyAction.Stat.Data.UltimateOne=old+"-different";
            Check(EnemyFingerprint.Capture(manager)!=key,"Different skill rejects match");manager.EnemyAction.Stat.Data.UltimateOne=old;
            List<CombatStateEffect> effects=Get<List<CombatStateEffect>>(manager.EnemyAction,"_effects");
            using(Fixture fixture=new Fixture(2)) {
                CombatStateEffect effect=fixture.effects[1];Set(effect,"_sourceAction",manager.EnemyAction);Set(effect,"_targetAction",manager.PlayerAction);
                effects.Add(effect);
                string withBuff=EnemyFingerprint.Capture(manager);
                Check(withBuff!=key,"Initial buff presence rejects match");
                Set(effect,"<RemainRounds>k__BackingField",4);Check(EnemyFingerprint.Capture(manager)!=withBuff,"Buff duration rejects match");Set(effect,"<RemainRounds>k__BackingField",3);
                Set(effect,"<DisplayCount>k__BackingField",2);Check(EnemyFingerprint.Capture(manager)!=withBuff,"Buff stack count rejects match");Set(effect,"<DisplayCount>k__BackingField",0);
                FieldInfo rate=AccessTools.Field(fixture.units[1].GetType(),"_rate");float oldRate=(float)rate.GetValue(fixture.units[1]);rate.SetValue(fixture.units[1],oldRate+0.1f);
                Check(EnemyFingerprint.Capture(manager)!=withBuff,"Buff skill parameter rejects match");rate.SetValue(fixture.units[1],oldRate);
                effects.Remove(effect);
            }
            if(effects.Count>0){Set(effects[0],"<RemainRounds>k__BackingField",effects[0].RemainRounds+1);Check(EnemyFingerprint.Capture(manager)!=key,"Buff round change rejects match");Set(effects[0],"<RemainRounds>k__BackingField",effects[0].RemainRounds-1);}
            manager.PlayerAction.Stat.Data.Strength--;
            Check(EnemyFingerprint.Capture(manager)==key,"Weaker player does not change enemy key");manager.PlayerAction.Stat.Data.Strength++;
            Set(manager.CombatLevel,"_conditionResult",new CombatConditionResultFlag[]{new CombatConditionResultFlag()});
            Check(EnemyFingerprint.Unsupported(manager.CombatLevel)!=null,"Conditional settlement refused");Set(manager.CombatLevel,"_conditionResult",new CombatConditionResultFlag[0]);
            CombatEventData ev=new CombatEventData();((IList)EnemyFingerprint.Field(ev,"_initAction")).Add(ScriptableObject.CreateInstance<CombatEventActionDebug>());
            manager.CombatLevel.Events.Add(ev);Check(EnemyFingerprint.Unsupported(manager.CombatLevel)!=null,"Mid-battle event refused");manager.CombatLevel.Events.Clear();
            VictoryHistory history=Get<VictoryHistory>(plugin,"history");
            Check(history!=null && history.Healthy,"History available in isolated user");
            // Synthetic history is confined to validation-v110. Actual played-victory path is also tested by Replay.
            history.Record(Get<string>(plugin,"enemyKey"));
            Check((bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Recorded identical enemy enables skip");
            Time.timeScale=0;
            Check(!(bool)AccessTools.Method(typeof(Plugin),"TrySkip").Invoke(plugin,null),"Paused battle cannot skip");Time.timeScale=4;
            Get<ConfigEntry<bool>>(plugin,"repeatWins").Value=false;
            Check(!(bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Skip toggle respected");Get<ConfigEntry<bool>>(plugin,"repeatWins").Value=true;
            resultApplications=0;countResults=true;
            expectedResults=manager.CombatLevel.WinResult.Length;
            Check((bool)AccessTools.Method(typeof(Plugin),"TrySkip").Invoke(plugin,null),"Manual skip starts native victory");
            Check(!(bool)AccessTools.Method(typeof(Plugin),"TrySkip").Invoke(plugin,null),"Repeated click refused");
            manager.Action((CombatActionType)2);Check(!Get<bool>(plugin,"actionIssued"),"Combat action refused during skip");
            Log("V110 expected current result applications="+expectedResults);
        }catch(Exception e){Log("V110 FAILED "+e);yield break;}
        yield return new WaitForSecondsRealtime(6);
        try {
            Check(resultApplications==expectedResults,"Current native victory results executed exactly once");
            Log("V110 result applications="+resultApplications);
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Combat","Skip returns to story");
            Check(Mathf.Approximately(Time.timeScale,Convert.ToSingle(AssistantValue("GameSpeed"))),"Global speed restored after skip");
        }catch(Exception e){Log("V110 FAILED "+e);}
        countResults=false;
        Log("V110 COMPLETE assertions="+assertions);
        // Real late encounter remains protected by its scripted compatibility gate.
        ordinaryFixture=false;
        yield return LoadReady();
        try {
            CombatManager late=CombatManager.Instance;
            List<CombatStateEffect> list=Get<List<CombatStateEffect>>(late.EnemyAction,"_effects");
            Check(list.Count>0,"Late fixture has initial buffs");
            Check(EnemyFingerprint.Unsupported(late.CombatLevel)!=null,"Actual scripted encounter refused");
            Check(!(bool)AccessTools.Method(typeof(Plugin),"CanSkip").Invoke(plugin,null),"Scripted late battle has no skip button");
            late.Action((CombatActionType)2);
            Check(Get<bool>(plugin,"actionIssued") && !Get<bool>(plugin,"firstReady"),"First command permanently closes skip window");
        }catch(Exception e){Log("V110 FAILED late "+e);}
        Log("V110 ALL COMPLETE assertions="+assertions);
    }
    private IEnumerator BarTests(CombatManager manager)
    {
        manager.EnableSpeedTime(true);
        ConfigEntry<bool> setting=Get<ConfigEntry<bool>>(plugin,"presentation");bool saved=setting.Value;
        GameObject rootBar=new GameObject("isolated-bar-fixture");rootBar.SetActive(false);
        CombatStatBar bar=rootBar.AddComponent<CombatStatBar>();
        foreach(string name in new string[]{"_normalImage","_backImage","_progressFullImage","_valueText"}) {
            GameObject child=new GameObject(name,typeof(RectTransform));child.transform.SetParent(rootBar.transform,false);
            Set(bar,name,name=="_valueText"?(object)child.AddComponent<Text>():child.AddComponent<Image>());
        }
        string[] baseline=new string[3];int i=0;
        setting.Value=false;
        foreach(int value in new int[]{100,100,200}){yield return bar.Setup(value,200);baseline[i++]=BarState(bar);}
        Get<Image>(bar,"_normalImage").fillAmount=0;Get<Image>(bar,"_backImage").fillAmount=0;
        setting.Value=true;i=0;
        foreach(int value in new int[]{100,100,200}){yield return bar.Setup(value,200);Check(baseline[i++]==BarState(bar),"Bar final state matches original: "+value);}
        IEnumerator paused=bar.Setup(50,200);string oldState=BarState(bar);Time.timeScale=0;
        Check(paused.MoveNext() && BarState(bar)==oldState,"Bar respects pause before update");
        setting.Value=false;Time.timeScale=4;
        yield return paused;
        Check(Get<Text>(bar,"_valueText").text=="50/200","Turning presentation off while paused resumes native bar");
        setting.Value=saved;UnityEngine.Object.Destroy(rootBar);
    }
    private static string BarState(CombatStatBar bar)
    {return Get<Text>(bar,"_valueText").text+"|"+Get<Image>(bar,"_normalImage").fillAmount+"|"+Get<Image>(bar,"_backImage").fillAmount+"|"+Get<Image>(bar,"_progressFullImage").gameObject.activeSelf;}
}
