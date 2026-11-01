namespace MortalFastCombat
{
    // Require a release while eligible before accepting a fresh press. Holding a key through
    // loading, pause, a modal, or another encounter must never trigger a newly available skip.
    public sealed class SkipHotkeyLatch
    {
        private bool armed;
        public void Reset() { armed=false; }
        public bool TakePress(bool eligible,bool held,bool pressedThisFrame)
        {
            if(!eligible){armed=false;return false;}
            if(pressedThisFrame){bool take=armed;armed=false;return take;}
            if(!held)armed=true;
            return false;
        }
    }
}
