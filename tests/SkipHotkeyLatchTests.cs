using System;
using MortalFastCombat;
public static class SkipHotkeyLatchTests
{
    private static int checks;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    public static int Main()
    {
        SkipHotkeyLatch gate=new SkipHotkeyLatch();
        Check(!gate.TakePress(false,true,true),"Unavailable battle must ignore a press");
        Check(!gate.TakePress(true,true,false),"Holding through readiness must not skip");
        Check(!gate.TakePress(true,false,false),"Eligible release only arms");
        Check(gate.TakePress(true,true,true),"Fresh eligible press accepted");
        Check(!gate.TakePress(true,true,true),"Two input routes in one held press cannot repeat");
        Check(!gate.TakePress(true,true,false),"Held key cannot repeat");
        gate.TakePress(true,false,false);
        Check(gate.TakePress(true,true,true),"A later release and fresh press accepted");
        gate.TakePress(true,false,false);gate.Reset();
        Check(!gate.TakePress(true,true,true),"New encounter resets arming");
        gate.TakePress(true,false,false);gate.TakePress(false,false,false);
        Check(!gate.TakePress(true,true,true),"Pause/focus/modal/disabled state disarms");
        Check(!gate.TakePress(true,true,false),"Holding after blocked context stays ignored");
        gate.TakePress(true,false,false);
        Check(gate.TakePress(true,true,true),"Releasing after blocked context restores shortcut");
        gate.Reset();
        Check(!gate.TakePress(false,false,false),"An ineligible release does not arm");
        Check(!gate.TakePress(true,true,true),"Release must occur while eligible");
        gate.TakePress(true,false,false);
        Check(gate.TakePress(true,false,true),"A press and release within one frame still triggers");
        Check(!gate.TakePress(true,false,true),"Same-frame quick tap cannot repeat");
        gate.TakePress(true,false,false);
        Check(gate.TakePress(true,true,true),"A later fresh press works after a quick tap");
        Console.WriteLine("PASS "+checks+" skip shortcut latch checks");return 0;
    }
}
