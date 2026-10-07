using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine.Items
{
    [Serializable]
    class ItemRadio : ItemTracker
    {
        public bool IsOn;
        public int Station { get { return (Model as ItemTrackerModel).RadioStation; } }

        public ItemRadio(ItemTrackerModel model) : base(model)
        {
            if (model.RadioStation < 0 || model.RadioStation > 3)
                throw new ArgumentException("Radio station must be 0..3", "model");
        }
    }
}
