using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using HarmonyLib;
using Mortal.Combat;
using MortalFastCombat;
using UnityEngine;

public sealed class NotificationReactiveUnit : CombatStateEffectUnitScriptable
{
    internal CombatStateEffect Added;
    public override IEnumerator OnStateEffectRemoved(CombatStateEffectParameter p,CombatActionController removedOwner,string key)
    {
        ValidationPlugin.NotificationTrace.Add(p.Source.name+"/"+p.Effect.Key+"/"+key+"/begin");
        if(key=="root") {
            List<CombatStateEffect> effects=ValidationPlugin.EffectsOf(p.Source);
            effects.RemoveAt(effects.Count-1);
            effects.Add(Added);
        }
        yield return new WaitForSecondsRealtime(0.025f);
        p.Source.Stat.Data.CurrentStamina-=1;
        if(key=="root")yield return ValidationPlugin.Notify(p.Source,"nested");
        ValidationPlugin.NotificationTrace.Add(p.Source.name+"/"+p.Effect.Key+"/"+key+"/end");
    }
}

public sealed partial class ValidationPlugin
{
    internal static List<string> NotificationTrace=new List<string>();
    internal static List<CombatStateEffect> EffectsOf(CombatActionController action) {return Get<List<CombatStateEffect>>(action,"_effects");}
    internal static IEnumerator Notify(CombatActionController action,string key) {return (IEnumerator)AccessTools.Method(typeof(CombatActionController),"NotifyEffectRemoved").Invoke(action,new object[]{key});}
    private void Connect(Fixture a,Fixture b) {
        Set(a.action,"<TargetAction>k__BackingField",b.action);
        foreach(CombatStateEffect e in a.effects)Set(e,"_targetAction",b.action);
    }
    private CombatStateEffect NotificationEffect(Fixture fixture,string key,CombatStateEffectUnitScriptable unit) {
        CombatStateEffectScriptable data=ScriptableObject.CreateInstance<CombatStateEffectScriptable>();
        Set(data,"_key",key);fixture.assets.Add(data);
        CombatStateEffect effect=(CombatStateEffect)FormatterServices.GetUninitializedObject(typeof(CombatStateEffect));
        Set(effect,"_data",data);Set(effect,"_sourceAction",fixture.action);
        Set(effect,"_targetAction",fixture.action.TargetAction);
        Set(effect,"<Effects>k__BackingField",new List<CombatStateEffectUnitScriptable>{unit});
        Set(effect,"<RemainRounds>k__BackingField",2);
        return effect;
    }
    private void Reactive(Fixture fixture) {
        NotificationReactiveUnit unit=ScriptableObject.CreateInstance<NotificationReactiveUnit>();fixture.assets.Add(unit);
        unit.Added=NotificationEffect(fixture,fixture.go.name+"-added",unit);
        fixture.effects.Insert(0,NotificationEffect(fixture,fixture.go.name+"-reactive",unit));
    }
    private IEnumerator NotificationFixtures() {
        yield return new WaitForSecondsRealtime(4);
        List<string> rows=new List<string>();
        for(int scenario=0;scenario<2;++scenario) {
            Fixture normal=new Fixture(scenario==0?40:8),target=new Fixture(scenario==0?40:8);
            Fixture fast=new Fixture(scenario==0?40:8),fastTarget=new Fixture(scenario==0?40:8);
            normal.go.name=fast.go.name="FixturePlayer";target.go.name=fastTarget.go.name="FixtureEnemy";
            Connect(normal,target);Connect(target,normal);Connect(fast,fastTarget);Connect(fastTarget,fast);
            if(scenario==1){Reactive(normal);Reactive(target);Reactive(fast);Reactive(fastTarget);}
            NotificationTrace.Clear();int frame=Time.frameCount;float time=Time.realtimeSinceStartup;
            string key=scenario==0?"empty":"root";
            yield return Notify(normal.action,key);
            int originalFrames=Time.frameCount-frame;float originalTime=Time.realtimeSinceStartup-time;
            string trace=String.Join("|",NotificationTrace.ToArray());
            string expected=NotificationSnapshot(normal,target);
            NotificationTrace.Clear();frame=Time.frameCount;time=Time.realtimeSinceStartup;
            yield return new CoroutinePump(Notify(fast.action,key),Classify,delegate{return true;},delegate{return false;},
                new FrameBudget(delegate{return Time.frameCount;},256,2.0),null);
            bool equal=trace==String.Join("|",NotificationTrace.ToArray()) && expected==NotificationSnapshot(fast,fastTarget);
            string row="NOTIFICATION FIXTURE "+scenario+" originalFrames="+originalFrames+" fastFrames="+(Time.frameCount-frame)+
                " originalSeconds="+originalTime.ToString("F6")+" fastSeconds="+(Time.realtimeSinceStartup-time).ToString("F6")+" equal="+equal+" callbacks="+NotificationTrace.Count;
            rows.Add(row);Log(row);
            normal.Dispose();target.Dispose();fast.Dispose();fastTarget.Dispose();
            if(!equal)throw new Exception("Notification fixture state/order mismatch");
        }
        File.WriteAllLines(Path.Combine(root,"v"+CaseVersion+"-notification-fixtures.txt"),rows.ToArray());
        Log("NOTIFICATION FIXTURES COMPLETE "+CaseVersion);
    }
    private static string NotificationSnapshot(Fixture a,Fixture b) {
        List<string> items=new List<string>();
        foreach(Fixture f in new Fixture[]{a,b}){
            items.Add(f.stat.Data.CurrentStamina.ToString());
            foreach(CombatStateEffect e in f.effects)items.Add(e.Key+":"+e.RemainRounds+":"+e.Disable);
        }
        return String.Join("|",items.ToArray());
    }
}
