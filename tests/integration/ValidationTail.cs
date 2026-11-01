using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Mortal.Combat;
using UnityEngine;

public sealed partial class ValidationPlugin
{
    private sealed class Probe {
        internal int Frame,Round;
        internal float Time;
        internal string Label,Actor;
    }
    private static Dictionary<object,Probe> probes=new Dictionary<object,Probe>();
    private static List<string> probeRows,callbackTrace,tailRows;
    private static float tailStart=-1;
    private static int tailFrame,tailRound;
    private void SetupTailProbe() {
        foreach(Type parent in new Type[]{typeof(CombatActionController),typeof(CombatStateEffect),typeof(CombatUIManager)}) {
            foreach(Type t in parent.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) {
                string n=t.Name;
                bool selected=n.StartsWith("<UpdateRemainEffect>") || n.StartsWith("<NotifyEffectRemoved>") ||
                    n.StartsWith("<HandleStateEffectRemoved>") || n.StartsWith("<ResetAttackCombo>") ||
                    n.StartsWith("<OnStateEffectRemoved>") || n.StartsWith("<ShowAction>");
                if(selected)patches.Patch(AccessTools.Method(t,"MoveNext"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"ProbeBefore"),
                    postfix:new HarmonyMethod(typeof(ValidationPlugin),"ProbeAfter"));
            }
        }
    }
    private static object Field(object instance,string name) {return AccessTools.Field(instance.GetType(),name).GetValue(instance);}
    private static void ProbeBefore(object __instance) {
        if(!replaying || probes.ContainsKey(__instance))return;
        Type t=__instance.GetType();
        object owner=Field(__instance,"<>4__this");
        CombatStateEffect effect=owner as CombatStateEffect;
        string actor;
        if(effect!=null)actor=((CombatActionController)Field(effect,"_sourceAction")).name;
        else actor=((Component)owner).name;
        string label=t.Name.Substring(1,t.Name.IndexOf('>')-1);
        probes.Add(__instance,new Probe{Frame=Time.frameCount,Time=Time.realtimeSinceStartup,Round=CombatManager.Instance.CurrentRounds,Label=label,Actor=actor});
        if(effect!=null) {
            string key=(string)Field(__instance,"removedKey");
            CombatActionController removed=(CombatActionController)Field(__instance,"removedOwner");
            callbackTrace.Add(CombatManager.Instance.CurrentRounds+"|"+actor+"|"+removed.name+"|"+key+"|"+effect.Key+"|disabled="+effect.Disable);
        }
    }
    private static void ProbeAfter(object __instance,bool __result) {
        if(!replaying || __result)return;
        Probe p;
        if(!probes.TryGetValue(__instance,out p))return;
        probes.Remove(__instance);
        probeRows.Add(p.Round+","+p.Label+","+p.Actor+","+(Time.frameCount-p.Frame)+","+(Time.realtimeSinceStartup-p.Time).ToString("F6"));
    }
    private static void BeginTailProbe() {
        probes.Clear();tailStart=-1;
        probeRows=new List<string>{"round,phase,actor,frames,seconds"};
        callbackTrace=new List<string>();tailRows=new List<string>{"round,frames,seconds"};
    }
    private static void NoteAfterResults(string label) {
        if(replaying && label=="Enemy/ApplyEffectAfterResult") {
            tailStart=Time.realtimeSinceStartup;tailFrame=Time.frameCount;tailRound=CombatManager.Instance.CurrentRounds;
        }
    }
    private static void TailReady() {
        if(tailStart>=0)tailRows.Add(tailRound+","+(Time.frameCount-tailFrame)+","+(Time.realtimeSinceStartup-tailStart).ToString("F6"));
        tailStart=-1;
    }
    private static void SaveTailProbe() {
        File.WriteAllLines(Path.Combine(root,replayName+"-tail.csv"),tailRows.ToArray());
        File.WriteAllLines(Path.Combine(root,replayName+"-probe.csv"),probeRows.ToArray());
        File.WriteAllLines(Path.Combine(root,replayName+"-callbacks.txt"),callbackTrace.ToArray());
        Log("PASSIVE TAIL PROBE "+replayName+" callbacks="+callbackTrace.Count+" samples="+probeRows.Count);
        probes.Clear();
    }
}
