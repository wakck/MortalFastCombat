using System;
using System.Collections;
using System.Collections.Generic;
using MortalFastCombat;

internal static class PumpTests
{
    private static int assertions, frame;
    private static bool enabled = true, paused;
    private static readonly object realWait = new object();
    private static List<string> trace = new List<string>();
    private static void Check(bool ok, string name) { ++assertions; if (!ok) throw new Exception(name); }
    private static RoutinePolicy Policy(Type type)
    {
        if (type.Name.Contains("Leaf") || type.Name.Contains("Throwing")) return RoutinePolicy.SkipNull;
        if (type.Name.Contains("Root") || type.Name.Contains("Waits") || type.Name.Contains("NotificationHandle")) return RoutinePolicy.Structural;
        return RoutinePolicy.Unknown;
    }
    private static CoroutinePump Pump(IEnumerator root, FrameBudget budget)
    { return new CoroutinePump(root, Policy, delegate { return enabled; }, delegate { return paused; }, budget, null); }
    private static FrameBudget Budget(int steps) { return new FrameBudget(delegate { return frame; }, steps, 100000); }
    private static IEnumerator Leaf(int i)
    {
        try { trace.Add("add" + i); yield return null; trace.Add("remove" + i); yield return null; }
        finally { trace.Add("dispose" + i); }
    }
    private static IEnumerator Root(int count) { for (int i = 0; i < count; ++i) yield return Leaf(i); }
    private static IEnumerator Unknown() { trace.Add("unknown"); yield return null; }
    private static IEnumerator Waits()
    { yield return realWait; yield return Unknown(); trace.Add("after wait"); yield return null; }
    private static IEnumerator Throwing()
    { try { yield return null; throw new InvalidOperationException("original error"); } finally { trace.Add("throw cleanup"); } }
    private static IEnumerator ThrowRoot() { try { yield return Throwing(); } finally { trace.Add("root cleanup"); } }
    private static IEnumerator NotificationRoot(List<int> listeners)
    { yield return NotificationHandle(listeners); yield return NotificationHandle(listeners); }
    private static IEnumerator NotificationHandle(List<int> listeners)
    {
        int[] snapshot=listeners.ToArray();
        foreach(int key in snapshot) if(listeners.Contains(key)) yield return NotificationLeaf(listeners,key);
    }
    private static IEnumerator NotificationLeaf(List<int> listeners,int key)
    {
        trace.Add("notify"+key);
        if(key==0){listeners.Remove(2);if(!listeners.Contains(3))listeners.Add(3);}
        yield return null;
    }
    private static void Drain(IEnumerator root)
    {
        while (root.MoveNext())
        {
            IEnumerator nested = root.Current as IEnumerator;
            if (nested != null) Drain(nested); else ++frame;
        }
        IDisposable d = root as IDisposable; if (d != null) d.Dispose();
    }
    private static void Main()
    {
        frame = 0; Drain(Root(80)); string expected = String.Join(",", trace.ToArray()); int normalFrames = frame;
        trace.Clear(); frame = 0; Drain(Pump(Root(80), Budget(256)));
        Check(String.Join(",", trace.ToArray()) == expected, "Order/count/disposal differs");
        Check(frame < normalFrames / 2, "Synthetic Buff waits did not fall by 50%");
        Console.WriteLine("Synthetic scheduler: baseline=" + normalFrames + " null-yield frames, compressed=" + frame + "; not an in-game battle benchmark.");
        trace.Clear(); frame = 0;
        CoroutinePump waits = Pump(Waits(), Budget(256));
        Check(waits.MoveNext() && Object.ReferenceEquals(waits.Current, realWait), "Real wait was changed");
        Check(waits.MoveNext() && waits.Current is IEnumerator && trace.Count == 0, "Unknown iterator was advanced");
        Drain((IEnumerator)waits.Current); Check(waits.MoveNext() && waits.Current == null, "Structural null was removed");
        waits.Dispose();
        trace.Clear(); frame = 0; paused = true;
        CoroutinePump p = Pump(Root(2), Budget(256));
        Check(p.MoveNext() && trace.Count == 0, "Advanced while paused");
        paused = false; enabled = false;
        Check(p.MoveNext() && p.Current is IEnumerator, "Disabled pump did not preserve nested wait");
        Drain((IEnumerator)p.Current); enabled = true; ++frame; Drain(p);
        Check(trace.Count == 6, "Toggle duplicated or lost work");
        trace.Clear(); frame = 0;
        FrameBudget shared = Budget(1);
        CoroutinePump a = Pump(Leaf(1), shared), b = Pump(Leaf(2), shared);
        Check(a.MoveNext() && trace.Count == 1, "Step bound failed");
        Check(b.MoveNext() && trace.Count == 1, "Budget was not shared per frame");
        enabled = false; ++frame;
        Check(a.MoveNext() && a.Current == null && trace.Count == 2, "Disabling after compression lost next null wait");
        a.Dispose(); b.Dispose(); enabled = true;
        long ticks = 0; frame = 0;
        FrameBudget time = new FrameBudget(delegate { return frame; }, delegate { return ticks; }, 256, 2);
        Check(time.TryAdvance(), "Initial budget rejected"); ticks = 2;
        Check(!time.TryAdvance(), "Time bound failed"); ++frame;
        Check(time.TryAdvance(), "Budget did not reset next frame");
        trace.Clear(); frame = 0; bool caught = false;
        try { Drain(Pump(ThrowRoot(), Budget(256))); } catch (InvalidOperationException e) { caught = e.Message == "original error"; }
        Check(caught && String.Join(",", trace.ToArray()) == "throw cleanup,root cleanup", "Exception cleanup/order changed");
        trace.Clear(); frame = 0;
        CoroutinePump stop = Pump(Root(3), Budget(2)); stop.MoveNext(); stop.Dispose(); stop.Dispose();
        Check(trace.FindAll(delegate(string s) { return s.StartsWith("dispose"); }).Count == 1, "Disposal ran twice");
        trace.Clear();frame=0;
        Drain(NotificationRoot(new List<int>{0,1,2}));
        string notifications=String.Join(",",trace.ToArray());
        Check(notifications=="notify0,notify1,notify0,notify1,notify3", "Notification fixture snapshot semantics incorrect");
        trace.Clear();frame=0;
        Drain(Pump(NotificationRoot(new List<int>{0,1,2}),Budget(256)));
        Check(String.Join(",",trace.ToArray())==notifications,"Notification add/remove order differs after flattening");
        trace.Clear();frame=0;
        CoroutinePump notification=Pump(NotificationRoot(new List<int>{0,1,2}),Budget(2));
        notification.MoveNext();paused=true;++frame;notification.MoveNext();
        Check(trace.Count==0,"Notification callback advanced during pause");
        paused=false;enabled=false;++frame;Drain(notification);enabled=true;
        Check(String.Join(",",trace.ToArray())==notifications,"Disabling compression mid-notification lost or duplicated callbacks");
        Console.WriteLine("PASS " + assertions + " assertions: order, count, real waits, unknown iterators, pause, toggles, shared step/time budget, exceptions and disposal.");
    }
}
