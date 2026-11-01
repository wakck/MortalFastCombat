using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MortalFastCombat
{
    public sealed partial class Plugin
    {
        private FieldInfo assistantBase,assistantApplied;
        private bool assistantHeld;
        private float assistantResumeBase=1f;
        private float assistantLastCombatScale=1f;
        private const string AssistantHash="8504ADE709EE4343F0C2FDD134B12A0E2CC6D62D642A4E60E6306CD97A910660";

        private void SetupAssistantSpeed()
        {
            Type type=AccessTools.TypeByName("LOMAssistant.Features.Extras.GameSpeed");
            if(type==null)return; // The mod also works without the optional assistant.
            string actual;
            using(SHA256 sha=SHA256.Create())
            using(FileStream input=File.OpenRead(type.Assembly.Location))
                actual=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
            if(actual!=AssistantHash)throw new InvalidOperationException("LOMAssistant 指纹不匹配，拒绝使用未经验证的倍率交接。");
            MethodInfo tick=AccessTools.DeclaredMethod(type,"Tick",Type.EmptyTypes);
            assistantBase=AccessTools.DeclaredField(type,"_base");
            assistantApplied=AccessTools.DeclaredField(type,"_applied");
            if(tick==null || !tick.IsStatic || tick.ReturnType!=typeof(void) ||
               assistantBase==null || assistantApplied==null || !assistantBase.IsStatic || !assistantApplied.IsStatic ||
               assistantBase.FieldType!=typeof(float) || assistantApplied.FieldType!=typeof(float))
                throw new MissingMemberException("Assistant speed state does not match the reviewed version");
            harmony.Patch(tick,prefix:new HarmonyMethod(typeof(Plugin),"BeforeAssistantSpeed"));
            Logger.LogInfo("LOMAssistant 3.3.2 speed handoff ready: global speed outside Combat; native/selected speed inside Combat.");
        }

        private void AcquireAssistantSpeed()
        {
            if(assistantBase==null || assistantHeld)return;
            float original=(float)assistantBase.GetValue(null);
            assistantResumeBase=original>0 && !Single.IsInfinity(original)?original:1f;
            assistantHeld=true;
            assistantLastCombatScale=Time.timeScale;
        }

        private void ReleaseAssistantSpeed()
        {
            if(!assistantHeld)return;
            assistantHeld=false;
            // Restore the unmultiplied base, never the combat scale or the old global product.
            // The original Tick then applies the CURRENT global choice and its own scene exemptions.
            // A zero scale stays zero; the assistant observes a later native resume normally.
            float value=Time.timeScale==0?0:!Mathf.Approximately(Time.timeScale,assistantLastCombatScale)?Time.timeScale:assistantResumeBase;
            Time.timeScale=value;
            assistantBase.SetValue(null,value);
            assistantApplied.SetValue(null,value);
        }

        [HarmonyPriority(Priority.First)]
        private static bool BeforeAssistantSpeed()
        {
            Plugin p=instance;
            if(p==null || !p.compatible || p.assistantBase==null)return true;
            // Keep ownership through the victory/defeat coroutine until the scene actually exits.
            // inCombat alone ends at GameOver, while the Combat scene can still be settling.
            if(p.inCombat || SceneManager.GetActiveScene().name=="Combat"){
                p.AcquireAssistantSpeed();
                p.assistantLastCombatScale=Time.timeScale;
                return false;
            }
            p.ReleaseAssistantSpeed();
            return true;
        }
    }
}
