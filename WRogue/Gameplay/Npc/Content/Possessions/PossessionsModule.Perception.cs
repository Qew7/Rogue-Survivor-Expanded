using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class PossessionsModule
    {
        static void PerceiveItems(NpcPerceptionContext c)
        {
            Actor actor = c.Owner; Inventory items = c.Items;
            Location place = c.Place; int turn = c.Turn, risk = c.Risk;
            NpcKnowledge knowledge = actor.Personality.Knowledge;
                    if (actor.Personality.HasAttachments) foreach (NpcAttachment attachment in actor.Personality.Attachments)
                        if (attachment.Kind == "item" && attachment.ItemId != Guid.Empty)
                        { int count = 0; foreach (Item item in items.Items) if (item.Model.ID == attachment.ModelId && item.StoryIdentity == attachment.ItemId) count += item.Quantity;
                            string kind = "item:" + attachment.ItemId.ToString("N");
                            if (count > 0 || knowledge.Places.Exists(p => p.Kind == kind && p.Place == place))
                                knowledge.RememberPlace(new NpcKnownPlace(place, kind, turn, count, risk)); }
            foreach (Item item in items.Items)
                if (item.IsStolen)
                    c.Game.ReportStolenGoods(actor, null, item, "stolen_goods_found", place.Position);
            NpcHomeObservation.RememberHome(actor);
        }
    }
}
