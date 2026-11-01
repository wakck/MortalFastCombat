using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Unity.Mono;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MortalFastCombat
{
    [BepInPlugin(Guid, "Mortal Fast Combat", "1.1.3")]
    [BepInDependency("LOMAssistant", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Mortal.exe")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.legendofmortal.fastcombat";
        private static Plugin instance;
        private Harmony harmony;
        private ConfigEntry<bool> enhanced, compress;
        private ConfigEntry<int> multiplier;
        private FieldInfo currentSpeed, nativeSpeed;
        private FrameBudget budget;
        private CombatManager owner;
        private bool compatible, inCombat, fastRequested, ownsScale, wasPaused, showSettings;
        private float entryScale = 1f, lastScale = 1f;
        private long skipped, shortenedTweens;
        private GUIStyle buttonStyle, labelStyle, toggleStyle;
        private Font guiFont;
        private static readonly string[] RootMethods = {
            "ApplyEffectBeforeAction", "ApplyEffectInitAction", "ApplyEffectAfterAction",
            "ApplyEffectBeforeResult", "ApplyEffectAfterResult", "UpdateRemainEffect",
            "TriggerEffectAfterAttacked", "TriggerEffectDuringAttack", "AddStateEffect", "InstantlyRemoveEffectByKey"
        };
        // These are children of the already wrapped removal paths, not additional Harmony roots.
        // Returning the original iterators here lets one pump preserve the game's notification order.
        private static readonly string[] NestedControllerMethods = {
            "NotifyEffectRemoved", "HandleStateEffectRemoved"
        };

        private void Awake()
        {
            instance = this;
            enhanced = Config.Bind("General", "Enabled", true, "增强战斗加速；false 时保留游戏原版行为。");
            multiplier = Config.Bind("General", "Multiplier", 4,
                new ConfigDescription("相对于游戏原速的倍率。", new AcceptableValueList<int>(2, 4, 8)));
            compress = Config.Bind("General", "CompressBuffWaits", true, "仅合并白名单 Buff 的无依赖空帧等待。");
            budget = new FrameBudget(delegate { return Time.frameCount; }, 256, 2.0);
            try
            {
                if (!Matches("Mortal.Combat.dll", Whitelist.CombatHash) || !Matches("Mortal.Core.dll", Whitelist.CoreHash))
                    throw new InvalidOperationException("游戏程序集指纹不匹配。保留原版快速战斗；请使用适配该游戏版本的插件。");
                currentSpeed = AccessTools.Field(typeof(CombatManager), "_currentSpeedTime");
                nativeSpeed = AccessTools.Field(typeof(CombatManager), "_speedTime");
                if (currentSpeed == null || currentSpeed.FieldType != typeof(float) || nativeSpeed == null || nativeSpeed.FieldType != typeof(float)) throw new MissingFieldException("Combat speed fields");
                harmony = new Harmony(Guid);
                Patch(typeof(CombatManager), "Awake", "BeforeCombat", null);
                Patch(typeof(CombatManager), "EnableSpeedTime", "BeforeSpeed", null);
                Patch(typeof(CombatManager), "GameOver", "BeforeGameOver", null);
                Patch(typeof(CombatManager), "OnDisable", "BeforeCombatDisabled", null);
                Patch(typeof(CombatManager), "NextInputAction", "BeforeNextInput", null);
                foreach (string name in RootMethods) Patch(typeof(CombatActionController), name, null, "WrapBuffRoutine");
                Patch(typeof(Mortal.Core.MovePanel), "LocalMoveTo", "BeforeLocalMove", null);
                MethodInfo originMove = null;
                foreach (MethodInfo method in typeof(Mortal.Core.MovePanel).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                {
                    ParameterInfo[] args=method.GetParameters();
                    if(method.Name=="MoveToOrigin" && args.Length==3 && args[0].ParameterType==typeof(float)) originMove=method;
                }
                if(originMove==null)throw new MissingMethodException("MovePanel.MoveToOrigin(duration,ease,ignoreTimeScale)");
                harmony.Patch(originMove,prefix:new HarmonyMethod(AccessTools.Method(typeof(Plugin),"BeforeOriginMove")));
                SetupPresentation();
                SetupRepeatWins();
                SetupSkipHotkeys();
                SetupAssistantSpeed();
                compatible = true;
                enhanced.SettingChanged += SettingsChanged;
                multiplier.SettingChanged += SettingsChanged;
                Logger.LogInfo("Ready: game fingerprints match; " + Whitelist.Pure.Count + " audited stat iterators; default " + multiplier.Value + "x.");
            }
            catch (Exception e)
            {
                if (harmony != null) harmony.UnpatchSelf();
                Logger.LogError("Enhancements disabled: " + e);
            }
        }

        private bool Matches(string file, string expected)
        {
            string path = Path.Combine(Paths.ManagedPath, file);
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                Logger.LogInfo(file + " SHA256=" + actual);
                return String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            }
        }
        private void Patch(Type type, string name, string prefix, string postfix)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            if (postfix == "WrapBuffRoutine" && method.ReturnType != typeof(IEnumerator)) throw new InvalidOperationException("Unexpected coroutine signature: " + name);
            harmony.Patch(method, prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), prefix)),
                          postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), postfix)));
        }
        private static void BeforeCombat(CombatManager __instance)
        {
            Plugin p = instance;
            if (p == null || !p.compatible) return;
            p.AcquireAssistantSpeed();
            p.owner = __instance;
            p.entryScale = p.assistantHeld ? 1f : Time.timeScale > 0 ? Time.timeScale : 1f;
            if(p.assistantHeld && Time.timeScale > 0) Time.timeScale = 1f;
            p.inCombat = true;
            p.fastRequested = false;
            p.ownsScale = false;
            p.skipped = 0;
            p.shortenedTweens = 0;
            p.wasPaused = Time.timeScale == 0;
            p.BeginEncounter(__instance);
        }
        private static bool BeforeSpeed(CombatManager __instance, bool __0)
        {
            Plugin p = instance;
            if (p == null || !p.compatible || !p.inCombat || p.owner != __instance) return true;
            p.fastRequested = __0;
            if (!p.enhanced.Value)
            {
                p.ownsScale = false;
                if (Time.timeScale == 0)
                {
                    p.currentSpeed.SetValue(__instance, __0 ? (float)p.nativeSpeed.GetValue(__instance) : 1f);
                    p.wasPaused = true;
                    return false;
                }
                return true;
            }
            float speed = __0 ? p.multiplier.Value : 1f;
            p.currentSpeed.SetValue(__instance, speed);
            // Changing the speed while paused records it without unpausing the game.
            if (Time.timeScale > 0) p.SetScale(speed);
            else p.wasPaused = true;
            return false;
        }
        private void SetScale(float speed) { Time.timeScale = speed; lastScale = speed; ownsScale = true; }
        private void SettingsChanged(object sender, EventArgs e)
        {
            if (compatible && inCombat && owner != null) owner.EnableSpeedTime(fastRequested);
        }
        private void Update()
        {
            if (!compatible || !inCombat) return;
            bool paused = Time.timeScale == 0;
            if (wasPaused && !paused && owner != null) owner.EnableSpeedTime(fastRequested);
            wasPaused = paused;
            ConsumeSkipHotkey();
        }
        private static bool BeforeGameOver(CombatManager __instance, bool __0, ref IEnumerator __result)
        {
            Plugin p=instance;
            if(p!=null && p.compatible && !p.EnterSettlement(__instance,__0)){__result=EmptyRoutine();return false;}
            EndCombat(__instance); return true;
        }
        private static IEnumerator EmptyRoutine() {yield break;}
        private static bool BeforeNextInput(InputAction.CallbackContext __0)
        {
            Plugin p = instance;
            if(p!=null && p.ConsumeSkipHotkey())return false;
            if (p == null || !p.compatible || !p.inCombat || __0.control == null || !(__0.control.device is Mouse) || Mouse.current == null) return true;
            float scale = OverlayScale();
            Vector2 position = Mouse.current.position.ReadValue();
            position = new Vector2(position.x / scale, (Screen.height-position.y) / scale);
            // Clicking mod settings must not also advance an underlying battle dialog.
            return !new Rect(8,8,380,80).Contains(position) && !(p.showSettings && new Rect(8,90,380,470).Contains(position));
        }
        private static void BeforeCombatDisabled(CombatManager __instance) { if(instance!=null)instance.CancelEncounter(__instance); EndCombat(__instance); }
        private static void EndCombat(CombatManager manager)
        {
            Plugin p = instance;
            if (p == null || p.owner != manager) return;
            p.ReleaseScale();
            p.Logger.LogInfo("Combat ended. Removed redundant Buff frame waits: " + p.skipped + "; shortened Buff UI tweens: " + p.shortenedTweens);
            p.inCombat = false;
            p.owner = null;
            p.showSettings = false;
        }
        private void ReleaseScale()
        {
            // Leave externally set pause/transition scales intact.
            if (ownsScale && Mathf.Approximately(Time.timeScale, lastScale)) Time.timeScale = entryScale;
            ownsScale = false;
        }
        private bool FastBuffs()
        {
            return compatible && inCombat && owner != null && enhanced.Value && fastRequested && compress.Value;
        }
        private static void BeforeLocalMove(Mortal.Core.MovePanel __instance, ref float __1) { CapBuffTween(__instance,ref __1); }
        private static void BeforeOriginMove(Mortal.Core.MovePanel __instance, ref float __0) { CapBuffTween(__instance,ref __0); }
        private static void CapBuffTween(Mortal.Core.MovePanel panel,ref float duration)
        {
            Plugin p=instance;
            if(p==null)return;
            bool buff=p.FastBuffs() && panel.GetComponentInParent<CombatStateEffectUI>()!=null;
            bool menu=p.FastPresentation() && p.IsCombatMenu(panel);
            if((!buff && !menu) || duration<=0.06f)return;
            // Preserve the original tween, easing, completion callback and engine wait.
            // Only Buff card travel is shortened; character and narrative animations are unchanged here.
            duration=0.06f;
            ++p.shortenedTweens;
        }
        private static RoutinePolicy Classify(Type type)
        {
            if (type.Assembly != typeof(CombatManager).Assembly) return RoutinePolicy.Unknown;
            if (Whitelist.Pure.Contains(type.FullName)) return RoutinePolicy.SkipNull;
            Type parent = type.DeclaringType;
            if (parent == typeof(CombatActionController))
            {
                foreach (string method in RootMethods)
                    if (type.Name.StartsWith("<" + method + ">", StringComparison.Ordinal)) return RoutinePolicy.Structural;
                foreach (string method in NestedControllerMethods)
                    if (type.Name.StartsWith("<" + method + ">", StringComparison.Ordinal)) return RoutinePolicy.Structural;
            }
            if (parent == typeof(CombatStateEffect))
            {
                string n = type.Name;
                if (n.StartsWith("<Apply", StringComparison.Ordinal) || n.StartsWith("<Init>", StringComparison.Ordinal) ||
                    n.StartsWith("<Remove>", StringComparison.Ordinal) || n.StartsWith("<Trigger", StringComparison.Ordinal) ||
                    n.StartsWith("<OnStateEffectRemoved>", StringComparison.Ordinal))
                    return RoutinePolicy.Structural;
            }
            // Dispatch wrappers: retain every explicit yield; only descend into already reviewed children.
            if (parent == typeof(C_Effect_Base) && (type.Name.StartsWith("<Apply",StringComparison.Ordinal) || type.Name.StartsWith("<Trigger",StringComparison.Ordinal)))
                return RoutinePolicy.Structural;
            return RoutinePolicy.Unknown;
        }
        private static void WrapBuffRoutine(ref IEnumerator __result)
        {
            Plugin p = instance;
            if (p == null || !p.compatible || !p.inCombat || __result == null || __result is CoroutinePump) return;
            __result = new CoroutinePump(__result, Classify, p.FastBuffs,
                delegate { return p.inCombat && Time.timeScale == 0; }, p.budget, delegate { ++p.skipped; });
        }
        private void OnGUI()
        {
            if (!compatible || !inCombat || owner == null) return;
            if (buttonStyle == null)
            {
                guiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
                buttonStyle = new GUIStyle(GUI.skin.button) { font = guiFont, fontSize = 16 };
                labelStyle = new GUIStyle(GUI.skin.label) { font = guiFont, fontSize = 16, wordWrap = true };
                toggleStyle = new GUIStyle(GUI.skin.toggle) { font = guiFont, fontSize = 16 };
            }
            // A compact, resolution-scaled overlay; all controls are mouse accessible.
            float scale = OverlayScale();
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float x = 8;
            string state = Time.timeScale == 0 ? "已暂停" : !enhanced.Value ? "原版" : fastRequested ? multiplier.Value + "x" : "原速";
            if (GUI.Button(new Rect(x, 8, 380, 32), "战斗加速 " + state + "  ·  设置", buttonStyle)) showSettings = !showSettings;
            DrawRepeatButton(x);
            if (showSettings)
            {
                Color color = GUI.color;
                GUI.color = new Color(0.04f,0.05f,0.07f,0.94f);
                GUI.DrawTexture(new Rect(x,90,380,470),Texture2D.whiteTexture);
                GUI.color = color;
                GUI.Box(new Rect(x, 90, 380, 470), GUIContent.none);
                GUI.Label(new Rect(x + 12, 98, 350, 24), "加速由游戏「快速战斗」按钮控制", labelStyle);
                bool enable = GUI.Toggle(new Rect(x + 12, 130, 350, 26), enhanced.Value, "启用模组功能", toggleStyle);
                if (enable != enhanced.Value) enhanced.Value = enable;
                int[] values = { 2, 4, 8 };
                for (int i = 0; i < values.Length; ++i)
                {
                    int value = values[i];
                    string text = (multiplier.Value == value ? "● " : "") + value + " 倍";
                    if (GUI.Button(new Rect(x + 12 + i * 116, 164, 108, 30), text, buttonStyle)) multiplier.Value = value;
                }
                bool compact = GUI.Toggle(new Rect(x + 12, 204, 350, 26), compress.Value, "压缩 Buff 空等待", toggleStyle);
                if (compact != compress.Value) compress.Value = compact;
                DrawFeatureSettings(x);
            }
            GUI.matrix = saved;
        }
        private static float OverlayScale() {return Mathf.Max(0.65f,Mathf.Min(Screen.width/1280f,Screen.height/720f));}
        private void OnDisable()
        {
            skipHotkeyLatch.Reset();
            if (owner != null && compatible)
            {
                ReleaseScale();
                if (currentSpeed != null) currentSpeed.SetValue(owner, 1f);
            }
            inCombat = false;
        }
        private void OnDestroy()
        {
            OnDisable();
            ReleaseAssistantSpeed();
            if (harmony != null) harmony.UnpatchSelf();
            if (guiFont != null) UnityEngine.Object.Destroy(guiFont);
            if (instance == this) instance = null;
        }
    }
}
