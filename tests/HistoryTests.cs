using System;
using System.IO;
using MortalFastCombat;

class HistoryTests
{
    static int count;
    static void Assert(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static VictoryHistory Open(string path,string game="game")
    {return new VictoryHistory(path,"test",game,HistoryJson.Encode,HistoryJson.Decode);}
    static void Main(string[] args)
    {
        string path=Path.Combine(args[0],"history.json");Directory.CreateDirectory(args[0]);
        string s1=VictoryHistory.Hash("save1"),s2=VictoryHistory.Hash("save2"),win=VictoryHistory.Hash("enemy"),win2=VictoryHistory.Hash("enemy2");
        VictoryHistory h=Open(path);h.Load(s1);string run=h.Current;h.Record(win);h.Associate(s2);
        Assert(h.Healthy && h.Won(win),"Records real victory");
        h=Open(path);h.Load(s2);Assert(h.Current==run && h.Won(win),"Restart and cross-slot save preserve run");
        h.Load(s1);Assert(h.Current==run,"Loading earlier save reuses run history");
        h.NewRun();string next=h.Current;Assert(!h.Won(win),"New game does not inherit victories");
        h.Associate(s2);h=Open(path);h.Load(s2);Assert(h.Current==next && !h.Won(win),"Overwritten slot follows content association");
        h.Load(VictoryHistory.Hash("unknown"));string unknown=h.Current;Assert(unknown!=next && unknown!=run,"Unknown old save gets independent run");
        h.Record(win2);h.MergeInto(run);Assert(h.Current==run && h.Won(win) && h.Won(win2),"Manual association merges victories");
        h.Load(VictoryHistory.Hash("unknown"));Assert(h.Current==run,"Merged save association survives reload");
        Assert(File.Exists(path+".bak") && !File.Exists(path+".tmp"),"Atomic write leaves backup and no temp");
        h.Record(win);Assert(h.Run(run).victories.Count==2,"Duplicate victory stays idempotent");
        Assert(!Open(path,"updated-game").Healthy,"Game fingerprint mismatch disables history");
        File.WriteAllText(path,"{broken");h=Open(path);Assert(!h.Healthy && !h.Won(win),"Corruption disables skipping without replacing file");
        Assert(File.ReadAllText(path)=="{broken","Corrupt record retained for recovery");
        Console.WriteLine("PASS "+count+" history assertions");
    }
}
