using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcHomeObservation
    {
        public static void RememberHome(Actor owner)
        {
            if (!owner.Location.Map.GetTileAt(owner.Location.Position).IsInside) return;
            XpdBase claim = owner.Location.Map.XpdBaseAt(owner.Location.Position);
            if (claim != null && claim.Owns(owner) && !owner.Personality.Attachments.Exists(a => a.Kind == "place" &&
                a.Place.Map == owner.Location.Map && claim.Contains(a.Place.Position)))
                owner.Personality.Attach(new NpcAttachment { Kind = "place", Place = owner.Location, Name = owner.Location.Map.Name, Weight = 40 });
        }
    }
}
