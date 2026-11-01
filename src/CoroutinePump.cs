using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace MortalFastCombat
{
    public enum RoutinePolicy { Unknown, Structural, SkipNull }

    // Shared by all pumps: the bound is per Unity frame, not per effect or wrapper.
    public sealed class FrameBudget
    {
        private readonly Func<int> frame;
        private readonly Func<long> clock;
        private readonly long maxTicks;
        private readonly int maxSteps;
        private int lastFrame = int.MinValue, steps;
        private long start;
        public FrameBudget(Func<int> frame, int maxSteps, double milliseconds)
            : this(frame, delegate { return Stopwatch.GetTimestamp(); }, maxSteps,
                   (long)(milliseconds * Stopwatch.Frequency / 1000.0)) { }
        public FrameBudget(Func<int> frame, Func<long> clock, int maxSteps, long maxTicks)
        { this.frame = frame; this.clock = clock; this.maxSteps = maxSteps; this.maxTicks = maxTicks; }
        public bool TryAdvance()
        {
            int now = frame();
            if (now != lastFrame) { lastFrame = now; steps = 0; start = clock(); }
            if (steps >= maxSteps || (steps > 0 && clock() - start >= maxTicks)) return false;
            ++steps;
            return true;
        }
    }

    public sealed class CoroutinePump : IEnumerator, IDisposable
    {
        private sealed class Entry
        {
            internal readonly IEnumerator Routine;
            internal readonly RoutinePolicy Policy;
            internal Entry(IEnumerator routine, RoutinePolicy policy) { Routine = routine; Policy = policy; }
        }
        private readonly Stack<Entry> stack = new Stack<Entry>();
        private readonly Func<Type, RoutinePolicy> classify;
        private readonly Func<bool> active;
        private readonly Func<bool> paused;
        private readonly FrameBudget budget;
        private readonly Action skipped;
        private object current;
        public CoroutinePump(IEnumerator root, Func<Type, RoutinePolicy> classify, Func<bool> active,
                             Func<bool> paused, FrameBudget budget, Action skipped)
        {
            this.classify = classify; this.active = active; this.paused = paused;
            this.budget = budget; this.skipped = skipped;
            stack.Push(new Entry(root, classify(root.GetType())));
        }
        public object Current { get { return current; } }
        public bool MoveNext()
        {
            current = null;
            try
            {
                while (stack.Count > 0)
                {
                    // Even null-only iterators must not continue modifying stats while paused.
                    if (paused()) return true;
                    bool fast = active();
                    if (fast && !budget.TryAdvance()) return true;
                    Entry top = stack.Peek();
                    if (!top.Routine.MoveNext())
                    {
                        stack.Pop();
                        IDisposable done = top.Routine as IDisposable;
                        if (done != null) done.Dispose();
                        continue;
                    }
                    object value = top.Routine.Current;
                    if (fast && value == null && top.Policy == RoutinePolicy.SkipNull)
                    { if (skipped != null) skipped(); continue; }
                    IEnumerator child = value as IEnumerator;
                    RoutinePolicy policy = child == null ? RoutinePolicy.Unknown : classify(child.GetType());
                    // Unknown enumerators include CustomYieldInstruction, tween completion and input waits.
                    // They are returned unchanged; never MoveNext them ourselves.
                    if (fast && child != null && policy != RoutinePolicy.Unknown)
                    { stack.Push(new Entry(child, policy)); continue; }
                    current = value;
                    return true;
                }
                return false;
            }
            catch { try { Dispose(); } catch { } throw; }
        }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            Exception first = null;
            while (stack.Count > 0)
            {
                IDisposable routine = stack.Pop().Routine as IDisposable;
                if (routine != null) try { routine.Dispose(); } catch (Exception e) { if (first == null) first = e; }
            }
            current = null;
            if (first != null) throw first;
        }
    }
}
