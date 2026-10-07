using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine.MapObjects
{
    [Serializable]
    class RadioReceiver : StateMapObject
    {
        // Off, survivor, military, local, gang. Bumping cycles through them.
        public int Station { get { return State - 1; } }
        public bool IsOn { get { return State != 0; } }

        public RadioReceiver(string imageID) : base("radio", imageID)
        {
            IsMovable = true;
            Weight = 4;
        }

        public void TuneNext() { SetState((State + 1) % 5); }
    }
}
