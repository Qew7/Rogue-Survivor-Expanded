using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay;

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

        internal void RestoreImage()
        {
            if (ImageID == GameImages.ITEM_POLICE_RADIO) ImageID = GameImages.OBJ_RADIO;
            if (HiddenImageID == GameImages.ITEM_POLICE_RADIO) HiddenImageID = GameImages.OBJ_RADIO;
        }

        public void TuneNext() { SetState((State + 1) % 5); }
    }
}
