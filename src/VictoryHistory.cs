using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MortalFastCombat
{
    [Serializable,DataContract] public sealed class VictoryRun
    {
        [DataMember(IsRequired=true)] public string id;
        [DataMember] public string label;
        [DataMember(IsRequired=true)] public List<string> victories=new List<string>();
    }
    [Serializable,DataContract] public sealed class SaveRunLink {[DataMember(IsRequired=true)] public string save;[DataMember(IsRequired=true)] public string run;}
    [Serializable,DataContract] public sealed class VictoryHistoryData
    {
        [DataMember(IsRequired=true)] public int schema=1;
        [DataMember(IsRequired=true)] public string user;
        [DataMember(IsRequired=true)] public string game;
        [DataMember(IsRequired=true)] public List<VictoryRun> runs=new List<VictoryRun>();
        [DataMember(IsRequired=true)] public List<SaveRunLink> saves=new List<SaveRunLink>();
    }
    public static class HistoryJson
    {
        // Managed serializer: Unity's native type tree omits plugin-defined generic list fields.
        public static string Encode(VictoryHistoryData data)
        {using(MemoryStream stream=new MemoryStream()){new DataContractJsonSerializer(typeof(VictoryHistoryData)).WriteObject(stream,data);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static VictoryHistoryData Decode(string json)
        {using(MemoryStream stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (VictoryHistoryData)new DataContractJsonSerializer(typeof(VictoryHistoryData)).ReadObject(stream);}
    }
    public sealed class VictoryHistory
    {
        public VictoryHistoryData Data {get;private set;}
        public string Current {get;private set;}
        public string Error {get;private set;}
        public bool Healthy {get {return Error==null;}}
        private readonly string path,user,game;
        private readonly Func<VictoryHistoryData,string> encode;
        private readonly Func<string,VictoryHistoryData> decode;
        public VictoryHistory(string path,string user,string game,Func<VictoryHistoryData,string> encode,Func<string,VictoryHistoryData> decode)
        {
            this.path=path;this.user=user;this.game=game;this.encode=encode;this.decode=decode;
            try {
                Data=File.Exists(path)?decode(File.ReadAllText(path)):new VictoryHistoryData{user=user,game=game};
                Validate();
            }catch(Exception e){Error="胜利记录不可用："+e.Message;Data=new VictoryHistoryData{user=user,game=game};}
        }
        private void Validate()
        {
            if(Data==null || Data.schema!=1 || Data.user!=user || Data.game!=game || Data.runs==null || Data.saves==null)throw new InvalidDataException("记录格式、用户或游戏版本不匹配");
            HashSet<string> ids=new HashSet<string>();
            foreach(VictoryRun run in Data.runs) {
                Guid parsed;
                if(run==null || !Guid.TryParse(run.id,out parsed) || !ids.Add(run.id) || run.victories==null)throw new InvalidDataException("周目记录无效");
                foreach(string victory in run.victories)if(!Digest(victory))throw new InvalidDataException("胜利指纹无效");
            }
            HashSet<string> saves=new HashSet<string>();
            foreach(SaveRunLink link in Data.saves)if(link==null || !Digest(link.save) || !ids.Contains(link.run) || !saves.Add(link.save))throw new InvalidDataException("存档归属无效");
        }
        public static string Hash(string text)
        {using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
        private static bool Digest(string value)
        {if(value==null || value.Length!=64)return false;foreach(char c in value)if(!Uri.IsHexDigit(c))return false;return true;}
        public VictoryRun Run(string id) {return Data.runs.Find(delegate(VictoryRun r){return r.id==id;});}
        public string NewRun()
        {
            if(!Healthy)return null;
            Current=Guid.NewGuid().ToString("N");
            Data.runs.Add(new VictoryRun{id=Current,label=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")});Persist();return Current;
        }
        public void Load(string save)
        {
            if(!Healthy)return;
            SaveRunLink link=Data.saves.Find(delegate(SaveRunLink s){return s.save==save;});
            if(link!=null){Current=link.run;return;}
            NewRun();Associate(save);
        }
        public void Associate(string save)
        {
            if(!Healthy)return;
            if(Current==null)NewRun();
            SaveRunLink link=Data.saves.Find(delegate(SaveRunLink s){return s.save==save;});
            if(link==null)Data.saves.Add(new SaveRunLink{save=save,run=Current});else link.run=Current;
            Persist();
        }
        public bool Won(string key) {VictoryRun run=Run(Current);return Healthy && run!=null && run.victories.Contains(key);}
        public void Record(string key)
        {
            if(!Healthy || !Digest(key) || Current==null)return;
            VictoryRun run=Run(Current);if(run!=null && !run.victories.Contains(key)){run.victories.Add(key);Persist();}
        }
        public void MergeInto(string target)
        {
            if(!Healthy || Current==target)return;
            VictoryRun from=Run(Current),to=Run(target);if(from==null || to==null)return;
            foreach(string key in from.victories)if(!to.victories.Contains(key))to.victories.Add(key);
            foreach(SaveRunLink link in Data.saves)if(link.run==Current)link.run=target;
            Data.runs.Remove(from);Current=target;Persist();
        }
        private void Persist()
        {
            if(!Healthy)return;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp=path+".tmp",backup=path+".bak";
                using(FileStream stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                    byte[] bytes=Encoding.UTF8.GetBytes(encode(Data));stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                if(File.Exists(path))File.Replace(temp,path,backup);else File.Move(temp,path);
            }catch(Exception e){Error="胜利记录写入失败："+e.Message;}
        }
    }
}
