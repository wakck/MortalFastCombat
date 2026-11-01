using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;

namespace MortalFastCombat
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> repeatWins;
        private VictoryHistory history;
        private CombatManager encounter;
        private string enemyKey,skipReason="战斗尚未初始化",encounterRun,mergeTarget;
        private bool captured,firstReady,actionIssued,skipping,settling,pendingVictory;
        private int runPage;
        private void SetupRepeatWins()
        {
            repeatWins=Config.Bind("General","EnableRepeatWin",true,"同一周目真实击败过的相同普通敌人，提供手动跳过并获胜。");
            Patch(typeof(CombatManager),"StartRound","BeforeRound",null);
            Patch(typeof(CombatManager),"Action","BeforeAction",null);
            Patch(typeof(CombatManager),"LoadNextScene","BeforeNextScene",null);
            Patch(typeof(CombatUIManager),"ShowAction",null,"WrapReady");
            Patch(typeof(SaveSystem),"ExecuteLoadGameData",null,"AfterLoadSave");
            Patch(typeof(SaveSystem),"NewGameData","BeforeNewRun",null);
            Patch(typeof(SaveSystem),"NewActGameData","BeforeNewRun",null);
            Patch(typeof(SaveSystem),"ExecuteSaveData","BeforeSaveFile","AfterSaveFile");
        }
        private VictoryHistory History()
        {
            if(history!=null)return history;
            string user=(string)AccessTools.Method(typeof(SaveSystem),"GetUserFolder").Invoke(SaveSystem.Instance,null);
            if(String.IsNullOrEmpty(user) || user.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || user=="." || user=="..")throw new InvalidOperationException("存档用户标识无效");
            string game=Whitelist.CoreHash+Whitelist.CombatHash;
            string path=Path.Combine(Paths.GameRootPath,"MortalFastCombat/data/"+user+"/history-"+VictoryHistory.Hash(game).Substring(0,16)+".json");
            history=new VictoryHistory(path,user,game,HistoryJson.Encode,HistoryJson.Decode);
            if(!history.Healthy)Logger.LogError(history.Error);
            return history;
        }
        private void HistoryFailure(Exception e) {skipReason="胜利记录不可用";Logger.LogError("Repeat win history: "+e);}
        private static void AfterLoadSave(GameSave __0)
        {
            Plugin p=instance;if(p==null || !p.compatible || __0==null)return;
            try {p.History().Load(VictoryHistory.Hash(JsonUtility.ToJson(__0)));}catch(Exception e){p.HistoryFailure(e);}
        }
        private static void BeforeNewRun()
        {Plugin p=instance;if(p==null || !p.compatible)return;try{p.History().NewRun();}catch(Exception e){p.HistoryFailure(e);}}
        private static void BeforeSaveFile(string __0,out string __state)
        {__state=FileDigest(__0);}
        private static string FileDigest(string path)
        {
            try {if(!File.Exists(path))return null;using(System.Security.Cryptography.SHA256 sha=System.Security.Cryptography.SHA256.Create())using(FileStream stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream));}
            catch(IOException){return null;}
        }
        private static void AfterSaveFile(string __0,object __1,string __state)
        {
            Plugin p=instance;if(p==null || !p.compatible)return;
            GameSave save=__1 as GameSave;AutoGameSave auto=__1 as AutoGameSave;if(auto!=null)save=auto.GameSave;
            if(save==null)return;
            try {
                string digest=FileDigest(__0);
                // ExecuteSaveData catches its own failures. Only associate a newly written file.
                if(digest!=null && digest!=__state)p.History().Associate(VictoryHistory.Hash(JsonUtility.ToJson(save)));
            }catch(Exception e){p.HistoryFailure(e);}
        }
        private void BeginEncounter(CombatManager manager)
        {
            encounter=manager;enemyKey=null;encounterRun=null;captured=false;firstReady=false;actionIssued=false;
            skipHotkeyLatch.Reset();
            skipping=false;settling=false;pendingVictory=false;skipReason="等待首回合准备";mergeTarget=null;
        }
        private void CancelEncounter(CombatManager manager)
        {if(encounter==manager){encounter=null;pendingVictory=false;firstReady=false;skipHotkeyLatch.Reset();}}
        private static void BeforeRound(CombatManager __instance)
        {
            Plugin p=instance;if(p==null || !p.compatible || p.encounter!=__instance || p.captured)return;
            p.captured=true;
            try {
                p.skipReason=EnemyFingerprint.Unsupported(__instance.CombatLevel);
                if(p.skipReason!=null)return;
                p.enemyKey=EnemyFingerprint.Capture(__instance);
                VictoryHistory h=p.History();if(h.Current==null)h.NewRun();p.encounterRun=h.Current;
                p.skipReason=h.Healthy?"本周目尚未记录击败此敌人":h.Error;
                p.Logger.LogInfo("Repeat fingerprint: "+p.enemyKey+" run="+p.encounterRun);
            }catch(Exception e){p.skipReason="敌人配置无法完整识别，不能跳过";p.Logger.LogWarning("Repeat fingerprint rejected: "+e.Message);}
        }
        private static void WrapReady(ref IEnumerator __result)
        {if(instance!=null && instance.compatible && __result!=null)__result=Ready(__result);}
        private static IEnumerator Ready(IEnumerator original)
        {
            try {while(original.MoveNext())yield return original.Current;}
            finally {IDisposable d=original as IDisposable;if(d!=null)d.Dispose();}
            Plugin p=instance;
            if(p!=null && p.encounter!=null && p.encounter.CurrentRounds==1 && !p.actionIssued && !p.settling)p.firstReady=true;
        }
        private static bool BeforeAction(CombatManager __instance)
        {
            Plugin p=instance;if(p==null || !p.compatible || p.encounter!=__instance)return true;
            if(p.ConsumeSkipHotkey())return false;
            if(p.settling || p.skipping)return false;
            p.actionIssued=true;p.firstReady=false;return true;
        }
        private bool EnterSettlement(CombatManager manager,bool win)
        {
            if(encounter!=manager)return true;
            if(settling)return false;
            settling=true;firstReady=false;
            pendingVictory=win && !skipping && actionIssued && enemyKey!=null && enhanced.Value &&
                manager.EnemyAction!=null && manager.EnemyAction.IsDead;
            return true;
        }
        private static void BeforeNextScene(CombatManager __instance)
        {
            Plugin p=instance;
            if(p==null || !p.compatible || p.encounter!=__instance || !p.pendingVictory)return;
            p.pendingVictory=false;
            try {
                // Reached only after the original GameOver applied every current result successfully.
                VictoryHistory h=p.History();
                if(h.Current==p.encounterRun) {
                    h.Record(p.enemyKey);
                    if(h.Healthy)p.Logger.LogInfo("Recorded played victory: "+p.enemyKey+" run="+p.encounterRun);
                }
            }catch(Exception e){p.HistoryFailure(e);}
        }
        private bool CanSkip()
        {
            return compatible && enhanced.Value && repeatWins.Value && inCombat && encounter!=null && owner==encounter &&
                firstReady && !actionIssued && !skipping && !settling && enemyKey!=null && history!=null &&
                history.Healthy && history.Current==encounterRun && history.Won(enemyKey) && Time.timeScale>0;
        }
        private bool TrySkip()
        {
            if(!CanSkip())return false;
            // This state is committed before invoking native code, so input/GUI cannot race settlement.
            skipping=true;firstReady=false;pendingVictory=false;
            CombatManager manager=encounter;
            manager.EnableHotKey=false;
            CombatUIManager.Instance.HideAction();
            IEnumerator result=(IEnumerator)AccessTools.Method(typeof(CombatManager),"GameOver").Invoke(manager,new object[]{true});
            manager.StartCoroutine(result);
            return true;
        }
        private void DrawRepeatButton(float x)
        {
            if(!enhanced.Value || !repeatWins.Value || !firstReady || actionIssued || settling)return;
            if(CanSkip()) {
                if(GUI.Button(new Rect(x,46,380,32),"已击败 · 跳过并获胜  ["+SkipHotkeyHint()+"]",buttonStyle))TrySkip();
            }else {
                Color previous=GUI.color;GUI.color=new Color(0.04f,0.05f,0.07f,0.94f);
                GUI.DrawTexture(new Rect(x,46,380,38),Texture2D.whiteTexture);GUI.color=previous;
                GUI.Label(new Rect(x+8,50,364,34),Time.timeScale==0?"暂停中":skipReason,labelStyle);
            }
        }
        private void DrawFeatureSettings(float x)
        {
            bool value=GUI.Toggle(new Rect(x+12,238,350,26),presentation.Value,"精简战斗提示（保留出招）",toggleStyle);
            if(value!=presentation.Value)presentation.Value=value;
            value=GUI.Toggle(new Rect(x+12,272,350,26),repeatWins.Value,"重复敌人跳过（"+SkipHotkeyHint()+"）",toggleStyle);
            if(value!=repeatWins.Value)repeatWins.Value=value;
            if(history==null){GUI.Label(new Rect(x+12,310,350,56),"首次读取存档后建立周目记录。",labelStyle);return;}
            if(!history.Healthy){GUI.Label(new Rect(x+12,310,350,90),history.Error,labelStyle);return;}
            VictoryRun current=history.Run(history.Current);
            GUI.Label(new Rect(x+12,310,350,48),current==null?"尚未建立周目记录":"当前周目 "+current.id.Substring(0,8)+" · 已胜 "+current.victories.Count,labelStyle);
            if(history.Data.runs.Count<=1){GUI.Label(new Rect(x+12,365,350,70),"旧存档默认独立记录；读取其他旧档后可在此关联同一周目。",labelStyle);return;}
            GUI.Label(new Rect(x+12,360,350,28),"手动关联：仅选择同一周目的记录",labelStyle);
            int count=history.Data.runs.Count;
            if(runPage>=count)runPage=0;
            VictoryRun candidate=history.Data.runs[runPage];
            if(GUI.Button(new Rect(x+12,394,350,32),candidate.id.Substring(0,8)+" · "+candidate.label+"  ›",buttonStyle)){runPage=(runPage+1)%count;mergeTarget=null;}
            bool enabled=GUI.enabled;GUI.enabled=enhanced.Value && candidate.id!=history.Current && !actionIssued && !settling;
            string label=mergeTarget==candidate.id?"确认关联并合并胜利记录":"关联到上方周目";
            if(GUI.Button(new Rect(x+12,436,350,32),label,buttonStyle)) {
                if(mergeTarget!=candidate.id)mergeTarget=candidate.id;
                else {history.MergeInto(candidate.id);encounterRun=history.Current;mergeTarget=null;}
            }
            GUI.enabled=enabled;
            GUI.Label(new Rect(x+12,480,350,68),"关联后共享胜利记录，不修改游戏存档。新游戏自动建立新周目。",labelStyle);
        }
    }
}
