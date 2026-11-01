using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;

namespace MortalFastCombat
{
    // Deterministic data traversal. No property evaluation, Unity instance IDs, RNG or game methods.
    public sealed class CanonicalCombatData
    {
        private readonly StringBuilder text=new StringBuilder();
        private readonly Dictionary<object,int> visited=new Dictionary<object,int>(new ReferenceComparer());
        private int nodes;
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {public new bool Equals(object a,object b){return System.Object.ReferenceEquals(a,b);}public int GetHashCode(object o){return RuntimeHelpers.GetHashCode(o);}}
        public string Result {get{return text.ToString();}}
        private void Token(string value) {text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);}
        public void Add(string label,object value) {Token(label);Write(value,0);}
        private void Write(object value,int depth)
        {
            if(++nodes>30000 || depth>64)throw new InvalidOperationException("配置图过大，不能可靠识别");
            if(value==null){Token("null");return;}
            Type t=value.GetType();Token(t.FullName);
            if(t==typeof(string)){Token((string)value);return;}
            if(t==typeof(float)){Token(BitConverter.ToString(BitConverter.GetBytes((float)value)));return;}
            if(t==typeof(double)){Token(BitConverter.ToString(BitConverter.GetBytes((double)value)));return;}
            if(t.IsPrimitive || t.IsEnum || t==typeof(decimal)){Token(Convert.ToString(value,CultureInfo.InvariantCulture));return;}
            // Audited CombatStatConvertData uses only GetResult(int), whose implementation is input * _convertRate.
            // _statData and _testValue are editor/test inputs, and traversing them would include unrelated player data.
            if(t==typeof(GameStatRateConvertData)){Token("rate");Write(EnemyFingerprint.Field(value,"_convertRate"),depth+1);return;}
            if(value is Delegate || value is IDictionary)throw new InvalidOperationException("未知动态配置："+t.FullName);
            if(!t.IsValueType) {
                int id;if(visited.TryGetValue(value,out id)){Token("ref"+id);return;}visited.Add(value,visited.Count);
            }
            UnityEngine.Object asset=value as UnityEngine.Object;
            if(asset!=null && !(asset is ScriptableObject)) {
                // These are presentation references only. Combat identity is the internal Name + AvatarAddressKey,
                // and all behavioral ScriptableObjects below are expanded, never replaced by their display names.
                if(asset is GameObject || asset is Sprite || asset is AudioClip || asset is Texture || asset is Material) {Token(asset.name);return;}
                throw new InvalidOperationException("未知资源类型："+t.FullName);
            }
            IList list=value as IList;
            if(list!=null){Token(list.Count.ToString());foreach(object child in list)Write(child,depth+1);return;}
            if(t.Assembly!=typeof(CombatManager).Assembly && t.Assembly!=typeof(SaveSystem).Assembly &&
                !(t.IsValueType && t.Namespace=="UnityEngine"))throw new InvalidOperationException("未知扩展类型："+t.FullName);
            bool serialized=asset is ScriptableObject;
            foreach(FieldInfo field in Fields(t)) {
                if(field.IsStatic || field.IsNotSerialized)continue;
                if(serialized && !field.IsPublic && !Attribute.IsDefined(field,typeof(SerializeField)))continue;
                Token(field.DeclaringType.FullName+"."+field.Name);Write(field.GetValue(value),depth+1);
            }
        }
        public static List<FieldInfo> Fields(Type type)
        {
            List<FieldInfo> fields=new List<FieldInfo>();
            for(Type t=type;t!=null && t!=typeof(UnityEngine.Object) && t!=typeof(ScriptableObject) && t!=typeof(MonoBehaviour) && t!=typeof(Behaviour) && t!=typeof(Component);t=t.BaseType)
                fields.AddRange(t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly));
            fields.Sort(delegate(FieldInfo a,FieldInfo b){return String.CompareOrdinal(a.DeclaringType.FullName+"."+a.Name,b.DeclaringType.FullName+"."+b.Name);});return fields;
        }
    }
    public static class EnemyFingerprint
    {
        public static object Field(object owner,string name)
        {FieldInfo f=AccessTools.Field(owner.GetType(),name);if(f==null)throw new MissingFieldException(owner.GetType().FullName,name);return f.GetValue(owner);}
        private static bool NonEmpty(object value) {ICollection collection=value as ICollection;return collection!=null && collection.Count>0;}
        public static string Unsupported(CombatLevel level)
        {
            if(level==null)return "战斗尚未初始化";
            if(level.ConditionResult!=null && level.ConditionResult.Length>0)return "本场有条件结算，不能跳过";
            if(level.Events!=null)foreach(CombatEventData ev in level.Events) {
                if(ev==null)return "本场包含未知战斗事件";
                if(NonEmpty(Field(ev,"_initAction")) || NonEmpty(Field(ev,"_conditionAction")))return "本场有战斗剧情事件，不能跳过";
            }
            if(level.TalkingEvent!=null && NonEmpty(Field(level.TalkingEvent,"_conditionAction")))return "本场有条件对话，不能跳过";
            return null;
        }
        public static string Capture(CombatManager manager)
        {
            CombatActionController enemy=manager.EnemyAction;
            if(enemy==null || enemy.Stat==null || enemy.Stat.Data==null || String.IsNullOrEmpty(enemy.Stat.Data.Name))throw new InvalidOperationException("缺少敌人内部标识");
            CanonicalCombatData data=new CanonicalCombatData();
            data.Add("format",1);data.Add("game",Whitelist.CoreHash+Whitelist.CombatHash);
            data.Add("brave",SystemSettings.BraveMode);data.Add("enemy",enemy.Stat.Data);
            foreach(FieldInfo field in CanonicalCombatData.Fields(typeof(CombatStatController))) {
                Type t=field.FieldType;
                if(t.IsPrimitive || t.IsEnum || t==typeof(CombatStatItem) || t==typeof(CombatAddDice) || t==typeof(int[]))data.Add("stat/"+field.Name,field.GetValue(enemy.Stat));
                if(typeof(Delegate).IsAssignableFrom(t) && field.GetValue(enemy.Stat)!=null)throw new InvalidOperationException("本场有动态属性提供器");
            }
            foreach(FieldInfo field in CanonicalCombatData.Fields(typeof(CombatActionController)))
                if(field.FieldType==typeof(int[]) || field.Name=="_combatStatConvert" || field.Name=="_actionRate" || field.FieldType==typeof(CombatActionItem))data.Add("action/"+field.Name,field.GetValue(enemy));
            IList effects=(IList)Field(enemy,"_effects");
            data.Add("effectCount",effects.Count);
            foreach(CombatStateEffect effect in effects) {
                data.Add("definition",Field(effect,"_data"));data.Add("units",effect.Effects);
                data.Add("rounds",effect.RemainRounds);data.Add("count",effect.DisplayCount);
                data.Add("apply",effect.IsApply);data.Add("remove",effect.IsRemove);data.Add("disabled",effect.Disable);
                CombatActionController source=(CombatActionController)Field(effect,"_sourceAction"),target=(CombatActionController)Field(effect,"_targetAction");
                if(source!=manager.PlayerAction && source!=enemy || target!=manager.PlayerAction && target!=enemy)throw new InvalidOperationException("未知 Buff 来源");
                data.Add("source",source==enemy?"enemy":"player");data.Add("target",target==enemy?"enemy":"player");
            }
            return VictoryHistory.Hash(data.Result);
        }
    }
}
