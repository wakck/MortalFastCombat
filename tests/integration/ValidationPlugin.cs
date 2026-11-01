// Test-only plugin. Installed temporarily for -fastcombat-test-profile launches.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx;
#if BEPINEX6
using BaseUnityPlugin = BepInEx.Unity.Mono.BaseUnityPlugin;
#endif
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using MortalFastCombat;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("local.legendofmortal.fastcombat.validation", "Fast Combat Validation (test only)", "1.0.0")]
#if BEPINEX6
[BepInDependency("LOMAssistant")]
[BepInDependency(Plugin.Guid)]
#else
[BepInDependency(Plugin.Guid)]
#endif
public sealed partial class ValidationPlugin : BaseUnityPlugin
{
    private static string root, profile;
    private Harmony patches;
    private Plugin plugin;
    private Func<Type,RoutinePolicy> pluginPolicy;
    private static int sequence;
    private float nextPoll;
    private void Awake()
    {
        root = Path.Combine(Paths.GameRootPath, "validation");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fastcombat-test-profile") < 0 && !File.Exists(Path.Combine(root,"test-mode.flag"))) { enabled = false; return; }
        profile = Path.Combine(Paths.GameRootPath, "sandbox/persistent/coexist-test");
        if (!Directory.Exists(profile)) throw new DirectoryNotFoundException("Validation save copy missing");
        patches = new Harmony("local.legendofmortal.fastcombat.validation");
        foreach (string method in new string[] { "GetSaveDataFile", "GetLegendDataFile", "GetBackupDataFile" })
            patches.Patch(AccessTools.Method(typeof(SaveSystem), method), postfix: new HarmonyMethod(typeof(ValidationPlugin), "RedirectPath"));
        foreach(string method in new string[]{"ExecuteSaveData","ExecuteSaveJsonData"})
            patches.Patch(AccessTools.Method(typeof(SaveSystem),method),prefix:new HarmonyMethod(typeof(ValidationPlugin),"GuardWrite"));
        patches.Patch(AccessTools.Method(typeof(SaveSystem),"GetUserFolder"),prefix:new HarmonyMethod(typeof(ValidationPlugin),"TestUser"));
        #if BEPINEX6
        plugin = (Plugin)BepInEx.Unity.Mono.Bootstrap.UnityChainloader.Instance.Plugins[Plugin.Guid].Instance;
#else
        plugin = (Plugin)BepInEx.Bootstrap.Chainloader.PluginInfos[Plugin.Guid].Instance;
#endif
        if(plugin==null)throw new InvalidOperationException("Production MFC plugin was not loaded");
        SetupCoexist();
        pluginPolicy=(Func<Type,RoutinePolicy>)Delegate.CreateDelegate(typeof(Func<Type,RoutinePolicy>),AccessTools.Method(typeof(Plugin),"Classify"));
        Application.logMessageReceived += CaptureErrors;
        Log("HOST name="+gameObject.name+" active="+gameObject.activeInHierarchy+" enabled="+enabled);
        Application.runInBackground=true;
        QualitySettings.vSyncCount=0;
        Application.targetFrameRate=60;
        Log("ISOLATED SAVE PROFILE: " + profile);
        foreach (string method in new string[] { "ApplyEffectBeforeAction", "ApplyEffectInitAction", "ApplyEffectAfterAction", "ApplyEffectBeforeResult", "ApplyEffectAfterResult" })
            patches.Patch(AccessTools.Method(typeof(CombatActionController), method), postfix: new HarmonyMethod(typeof(ValidationPlugin), "MeasureStage"));
        SetupReplayHooks();
        SetupTailProbe();
        SetupV110();
        StartCoroutine(StartupFixtures());
    }
    private void OnEnable() { if(root!=null)Log("VALIDATION OnEnable"); }
    private void Start() { if(patches!=null)Log("VALIDATION Start"); }
    private void OnDisable() { if(patches!=null)Log("VALIDATION OnDisable"); }
    private static void RedirectPath(ref string __result) { __result = Path.Combine(profile, Path.GetFileName(__result)); }
    private static bool TestUser(ref string __result) {__result="coexist-test";return false;}
    private static void GuardWrite(string __0) {
        string prefix=Path.GetFullPath(profile).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(__0).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Blocked test write outside isolated profile: "+__0);
    }
    private void CheckIsolation() {
        if(!gameObject.activeInHierarchy || !enabled)throw new InvalidOperationException("Validation host inactive");
        foreach(string name in new string[]{"GetSaveDataFile","GetLegendDataFile","GetBackupDataFile"}) {
            object arg=name=="GetLegendDataFile"?(object)1:"auto_battle";
            string path=(string)AccessTools.Method(typeof(SaveSystem),name).Invoke(SaveSystem.Instance,new object[]{arg});
            GuardWrite(path);
        }
        Log("ISOLATION VERIFIED before replay/load");
    }
    private static void Log(string text)
    { File.AppendAllText(Path.Combine(root, "validation.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine); }
    private static void CaptureErrors(string text,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception) Log("UNITY ERROR "+text+" "+stack); }
    private static void MeasureStage(CombatActionController __instance, MethodBase __originalMethod, ref IEnumerator __result)
    { if (__result != null) __result = Measured(__result, __instance.name+"/"+__originalMethod.Name); }
    private static IEnumerator Measured(IEnumerator routine, string label)
    {
        int frame = Time.frameCount; float time = Time.realtimeSinceStartup;
        yield return routine;
        Log("STAGE " + label + " frames=" + (Time.frameCount-frame) + " seconds=" + (Time.realtimeSinceStartup-time).ToString("F4"));
        RecordPhase(label);
        NoteAfterResults(label);
    }
    private static void Set(object value, string name, object fieldValue) { AccessTools.Field(value.GetType(), name).SetValue(value, fieldValue); }
    private static T Get<T>(object value, string name) { return (T)AccessTools.Field(value.GetType(), name).GetValue(value); }
    private sealed class Fixture : IDisposable
    {
        internal GameObject go;
        internal CombatActionController action;
        internal CombatStatController stat;
        internal List<ScriptableObject> assets = new List<ScriptableObject>();
        internal List<CombatStateEffectParameter> parameters = new List<CombatStateEffectParameter>();
        internal List<CombatStateEffectUnitScriptable> units = new List<CombatStateEffectUnitScriptable>();
        internal List<CombatStateEffect> effects = new List<CombatStateEffect>();
        internal Fixture(int count)
        {
            go = new GameObject("FastCombat-isolated-fixture"); go.SetActive(false);
            stat = go.AddComponent<CombatStatController>();
            action = go.AddComponent<CombatActionController>();
            Set(action, "<Stat>k__BackingField", stat);
            CombatStat data = new CombatStat();
            data.CurrentHealth = 700; data.CurrentStamina = 80; data.CurrentPoisonValue = 3; data.CurrentParalyzedValue = 2;
            Set(stat, "_statData", data);
            foreach (FieldInfo f in typeof(CombatStatController).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if (f.FieldType == typeof(CombatStatItem)) f.SetValue(stat, new CombatStatItem(100));
            Set(stat, "<AddDice>k__BackingField", new CombatAddDice());
            for (int i=0;i<count;++i)
            {
                CombatStateEffectScriptable descriptor = ScriptableObject.CreateInstance<CombatStateEffectScriptable>();
                Set(descriptor, "_key", "validation-" + i); assets.Add(descriptor);
                CombatStateEffect effect = (CombatStateEffect)FormatterServices.GetUninitializedObject(typeof(CombatStateEffect));
                Set(effect, "_data", descriptor); Set(effect, "<RemainRounds>k__BackingField", 3);
                CombatStateEffectUnitScriptable unit;
                if (i%3 == 0) unit = ScriptableObject.CreateInstance<B_Action_002>();
                else if (i%3 == 1) unit = ScriptableObject.CreateInstance<B_Defence_001>();
                else unit = ScriptableObject.CreateInstance<B_Damage_001>();
                FieldInfo rate=unit.GetType().GetField("_rate",BindingFlags.Instance|BindingFlags.NonPublic);
                if (rate != null) rate.SetValue(unit,0.2f);
                assets.Add(unit); units.Add(unit);
                Set(effect,"_sourceAction",action);Set(effect,"_targetAction",action);
                Set(effect,"<Effects>k__BackingField",new List<CombatStateEffectUnitScriptable>{unit});
                effects.Add(effect);
                parameters.Add(new CombatStateEffectParameter(null, action, action, null, effect));
            }
            Set(action,"_effects",effects);
        }
        internal IEnumerator Stage(bool remove)
        {
            for (int i=0;i<units.Count;++i) yield return remove ? units[i].Remove(parameters[i]) : units[i].Init(parameters[i]);
        }
        internal string Snapshot()
        {
            List<string> entries = new List<string>();
            foreach (FieldInfo f in typeof(CombatStatController).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(CombatStatItem)) {
                    CombatStatItem item = (CombatStatItem)f.GetValue(stat);
                    entries.Add(f.Name+"="+item.FinalValue+":"+item.ModifyList.Count);
                }
            }
            entries.Add(JsonUtility.ToJson(Get<CombatStat>(stat, "_statData")));
            foreach (CombatStateEffectParameter p in parameters) entries.Add(p.Effect.RemainRounds.ToString());
            return String.Join("|", entries.ToArray());
        }
        public void Dispose() { UnityEngine.Object.Destroy(go); foreach (ScriptableObject asset in assets) UnityEngine.Object.Destroy(asset); }
    }
    private RoutinePolicy Classify(Type type)
    {
        if (type.DeclaringType == typeof(Fixture)) return RoutinePolicy.Structural;
        return pluginPolicy(type);
    }
    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext()) { IEnumerator child = routine.Current as IEnumerator; if (child != null) Drain(child); }
        IDisposable d = routine as IDisposable; if (d != null) d.Dispose();
    }
    private IEnumerator FixtureTests()
    {
        yield return new WaitForSecondsRealtime(3);
        try { VerifyFixtures(); } catch (Exception e) { Log("FIXTURE FAILED " + e); }
        Fixture baseline = null, fast = null;
        try { baseline = new Fixture(80); fast = new Fixture(80); } catch (Exception e) { Log("BENCHMARK SETUP FAILED " + e); }
        if (baseline != null && fast != null)
        {
            int frame = Time.frameCount; float time = Time.realtimeSinceStartup;
            yield return baseline.Stage(false);
            int normalFrames = Time.frameCount-frame; float normalTime = Time.realtimeSinceStartup-time;
            frame=Time.frameCount; time=Time.realtimeSinceStartup;
            yield return new CoroutinePump(fast.Stage(false), Classify, delegate { return true; }, delegate { return false; },
                new FrameBudget(delegate { return Time.frameCount; },256,2.0), null);
            Log("NATIVE BUFF FIXTURE 80 effects: baselineFrames=" + normalFrames + " fastFrames=" + (Time.frameCount-frame) +
                " baselineSeconds="+normalTime.ToString("F4")+" fastSeconds="+(Time.realtimeSinceStartup-time).ToString("F4")+
                " equalStats="+(baseline.Snapshot()==fast.Snapshot())+" (controlled fixture, not a full battle)");
        }
        if(baseline!=null)baseline.Dispose(); if(fast!=null)fast.Dispose();
    }
    private void VerifyFixtures()
    {
        UnityEngine.Random.State old = UnityEngine.Random.state;
        using (Fixture normal = new Fixture(80))
        using (Fixture fast = new Fixture(80))
        {
            foreach (bool remove in new bool[] {false,true}) {
                UnityEngine.Random.InitState(12345); Drain(normal.Stage(remove));
                string random = JsonUtility.ToJson(UnityEngine.Random.state);
                UnityEngine.Random.InitState(12345);
                Drain(new CoroutinePump(fast.Stage(remove), Classify, delegate { return true; }, delegate { return false; },
                    new FrameBudget(delegate { return ++sequence; },256,2.0), null));
                if (normal.Snapshot()!=fast.Snapshot() || random!=JsonUtility.ToJson(UnityEngine.Random.state)) throw new Exception("Stat/RNG mismatch");
            }
        }
        UnityEngine.Random.state = old;
        Log("FIXTURE PASS: actual game Buff Init/Remove; HP/stamina/poison/paralysis/stat modifiers/effect rounds/RNG identical.");
    }
    private void Update()
    {
        if(replaying){QualitySettings.vSyncCount=0;Application.targetFrameRate=30;}
        if (patches == null || !startupFixturesReady || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.1f;
        string path = Path.Combine(root,"command.txt");
        if(!File.Exists(path)) return;
        string command;
        try {command=File.ReadAllText(path).Trim();File.Delete(path);} catch(IOException) {return;}
        try {
            CombatManager manager=CombatManager.Instance;
            if(command=="quit") { Application.Quit(); return; }
            if(command=="hotkey-tests") {StartCoroutine(HotkeyTests());}
            else if(command.StartsWith("global")) {SetGlobal(Int32.Parse(command.Substring(6)));}
            else if(command=="handoff-tests") {StartCoroutine(HandoffTests());}
            else if(command=="coexist") {ReportCoexist();}
            else if(command=="switch-tests") {StartCoroutine(SwitchTests());}
            else if(command=="persistence-check") {StartCoroutine(PersistenceCheck());}
            else if(command=="fresh-history") {FreshHistory();}
            else if(command=="history-status") {HistoryStatus();}
            else if(command=="v110-tests") {StartCoroutine(V110Tests());}
            else if(command=="ordinary") {ordinaryFixture=true;}
            else if(command=="late") {ordinaryFixture=false;}
            else if(command=="presentation-on" || command=="presentation-off") {FieldInfo field=AccessTools.Field(typeof(Plugin),"presentation");if(field!=null)((ConfigEntry<bool>)field.GetValue(plugin)).Value=command=="presentation-on";}
            else if(command=="replay-compare") {StartCoroutine(CompareBoth());}
            else
            if(command=="replay-original" || command=="replay-fast") {StartCoroutine(Replay(command=="replay-fast"));}
            else
            if(command=="load-battle") {
                CheckIsolation();
                SaveSystem.Instance.SetSlot(SaveSystem.Instance.GetAutoSaveData("auto_battle").Slot);
                SoundManager.Instance.StopMusic();
                SaveSystem.Instance.AutoLoadGameData("auto_battle");
                SceneController.Instance.LoadCurrentScene();
            }
            else if(command=="next" && manager!=null) Set(manager,"_triggerNextInput",true);
            else if(command=="attack" && manager!=null) manager.Action((CombatActionType)2);
            else if(command=="fast" && manager!=null) Get<UnityEngine.UI.Toggle>(CombatUIManager.Instance,"_speedButtonToggle").isOn=true;
            else if(command=="slow" && manager!=null) Get<UnityEngine.UI.Toggle>(CombatUIManager.Instance,"_speedButtonToggle").isOn=false;
            else if(command=="pause") Time.timeScale=0;
            else if(command=="resume") Time.timeScale=1;
            else if(command=="baseline" || command=="enhanced") Get<ConfigEntry<bool>>(plugin,"enhanced").Value=command=="enhanced";
            else if(command=="compact-on" || command=="compact-off") Get<ConfigEntry<bool>>(plugin,"compress").Value=command=="compact-on";
            else if(command.StartsWith("speed")) {benchmarkSpeed=Int32.Parse(command.Substring(5));Get<ConfigEntry<int>>(plugin,"multiplier").Value=benchmarkSpeed;}
            else if(command=="snapshot") Snapshot();
            else if(command=="defeat" && manager!=null) manager.StartCoroutine((IEnumerator)AccessTools.Method(typeof(CombatManager),"GameOver").Invoke(manager,new object[]{false}));
            else if(command=="fingerprint") {
                MethodInfo match=AccessTools.Method(typeof(Plugin),"Matches");
                string expected=(string)AccessTools.Field(AccessTools.TypeByName("MortalFastCombat.Whitelist"),"CombatHash").GetRawConstantValue();
                bool correct=(bool)match.Invoke(plugin,new object[]{"Mortal.Combat.dll",expected});
                bool wrong=(bool)match.Invoke(plugin,new object[]{"Mortal.Combat.dll","intentionally-wrong-fingerprint"});
                Log("FINGERPRINT CHECK valid="+correct+" mismatchRejected="+!wrong);
            }
            Log("COMMAND "+command+" scale="+Time.timeScale);
        } catch(Exception e) {Log("COMMAND FAILED "+command+" "+e);}
    }
    private void Snapshot()
    {
        CombatManager m=CombatManager.Instance;
        string value="scene="+SceneManager.GetActiveScene().name+" scale="+Time.timeScale;
        if(m!=null) {
            value+=" round="+m.CurrentRounds;
            foreach(CombatActionController a in new CombatActionController[]{m.PlayerAction,m.EnemyAction}) {
                value+="\n"+a.name+" data="+JsonUtility.ToJson(a.Stat.Data);
                foreach(CombatStateEffect effect in Get<List<CombatStateEffect>>(a,"_effects")) value+=" "+effect.Key+":"+effect.RemainRounds;
            }
        }
        Log("SNAPSHOT "+value);
    }
    // Keep static save redirection and write guards until process exit, even if a scene destroys this host.
    private void OnDestroy() {if(patches!=null)Log("VALIDATION OnDestroy; process-lifetime save guards retained");}
}
