using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class ItemPreferenceInterest : INpcTraitInterest
    {
        public void Prepare(Actor owner, TraitInstance trait)
        {
            if (trait.ItemModelId < 0 || owner.Inventory == null) return;
            bool owned = false;
            foreach (Item item in owner.Inventory.Items)
                if (item.Model.ID == trait.ItemModelId && !item.Model.IsStackable)
                { owned = true; owner.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = trait.ItemModelId, ItemId = item.StoryIdentity, Weight = 35, Name = item.TheName }); }
            if (!owned && !owner.Personality.Attachments.Exists(a => a.Kind == "item" && a.ModelId == trait.ItemModelId))
                owner.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = trait.ItemModelId, Weight = 35, Name = "preferred item" });
        }
        public void Observe(Actor owner, TraitInstance trait, Inventory items, Location place, int risk)
        {
            if (trait.ItemModelId < 0) return;
            int count = 0; foreach (Item item in items.Items) if (item.Model.ID == trait.ItemModelId) count += item.Quantity;
            owner.Personality.Knowledge.RememberPlace(new NpcKnownPlace(place, "item:" + trait.ItemModelId, place.Map.LocalTime.TurnCounter, count, risk));
        }
    }
}
