using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Mortal.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace MortalFastCombat
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> presentation;
        private static readonly FieldInfo BarNormal=AccessTools.Field(typeof(CombatStatBar),"_normalImage"),
            BarBack=AccessTools.Field(typeof(CombatStatBar),"_backImage"),
            BarFull=AccessTools.Field(typeof(CombatStatBar),"_progressFullImage"),
            BarText=AccessTools.Field(typeof(CombatStatBar),"_valueText"),
            PopupDuration=AccessTools.Field(typeof(CombatDisplayValue),"_duration"),
            PopupFade=AccessTools.Field(typeof(CombatDisplayValue),"_fadeDuration");
        private void SetupPresentation()
        {
            presentation=Config.Bind("General","FastPresentation",true,"快速战斗时精简血条、数字和操作面板过渡，保留出招及剧情。");
            Patch(typeof(CombatStatBar),"Setup",null,"WrapBar");
            Patch(typeof(CombatDisplayValue),"StartMove",null,"WrapPopup");
        }
        private bool FastPresentation() {return compatible && inCombat && owner!=null && enhanced.Value && fastRequested && presentation.Value;}
        private bool IsCombatMenu(Mortal.Core.MovePanel panel)
        {
            // Only the two command menus. In particular, exclude dialog/narrative and character panels.
            CombatUIManager ui=CombatUIManager.Instance;
            if(ui==null)return false;
            foreach(string name in new string[]{"_actionPanel","_menuPanel"}) {
                FieldInfo field=AccessTools.Field(typeof(CombatUIManager),name);
                Component component=field==null?null:field.GetValue(ui) as Component;
                if(component!=null && (panel.transform==component.transform || panel.transform.IsChildOf(component.transform)))return true;
            }
            return false;
        }
        private static void WrapBar(CombatStatBar __instance,int __0,int __1,ref IEnumerator __result)
        {
            Plugin p=instance;
            if(p!=null && p.FastPresentation() && __1>0)__result=SnapBar(__instance,__0,__1,__result);
        }
        private static IEnumerator SnapBar(CombatStatBar bar,int value,int max,IEnumerator original)
        {
            while(instance!=null && instance.inCombat && Time.timeScale==0)yield return null;
            if(instance==null || !instance.FastPresentation()) {yield return original;yield break;}
            try {
                // Same text, fraction and full-state predicate as the audited Setup iterator.
                // Image.fillAmount performs the game's own clamping.
                float amount=value*1f/max;
                ((Text)BarText.GetValue(bar)).text=value.ToString()+"/"+max.ToString();
                ((Image)BarNormal.GetValue(bar)).fillAmount=amount;
                ((Image)BarBack.GetValue(bar)).fillAmount=amount;
                GameObject full=((Image)BarFull.GetValue(bar)).gameObject;
                if(full.activeSelf!=(amount>=1f))full.SetActive(amount>=1f);
            } finally {IDisposable disposable=original as IDisposable;if(disposable!=null)disposable.Dispose();}
        }
        private static void WrapPopup(CombatDisplayValue __instance,ref IEnumerator __result)
        {
            if(instance!=null && instance.FastPresentation())__result=CompactPopup(__instance,__result);
        }
        private static IEnumerator CompactPopup(CombatDisplayValue popup,IEnumerator original)
        {
            try {
                while(true) {
                    while(instance!=null && instance.inCombat && Time.timeScale==0)yield return null;
                    float duration=(float)PopupDuration.GetValue(popup),fade=(float)PopupFade.GetValue(popup);
                    bool more;
                    try {
                        if(instance!=null && instance.FastPresentation()) {
                            PopupDuration.SetValue(popup,Math.Min(duration,0.35f));
                            PopupFade.SetValue(popup,Math.Min(fade,0.15f));
                        }
                        more=original.MoveNext();
                    } finally {PopupDuration.SetValue(popup,duration);PopupFade.SetValue(popup,fade);}
                    if(!more)yield break;
                    yield return original.Current;
                }
            } finally {IDisposable disposable=original as IDisposable;if(disposable!=null)disposable.Dispose();}
        }
    }
}
