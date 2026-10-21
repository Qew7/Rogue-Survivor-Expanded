using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        public void DoTakeFromContainer(Actor actor, Point position)
        {
            Map map = actor.Location.Map;
            StorageContainer storage = map.GetMapObjectAt(position) as StorageContainer;
            if (storage != null)
            {
                DoTakeFromStorage(actor, storage, storage.Contents.TopItem);
                return;
            }

            // get topmost item.
            Item it = map.GetItemsAt(position).TopItem;

            // take it.
            DoTakeItem(actor, position, it);
        }

        public void DoTakeStorageItem(Actor actor, StorageContainer storage, Item item)
        { DoTakeFromStorage(actor, storage, item); }

        void DoTakeFromStorage(Actor actor, StorageContainer storage, Item item)
        {
            if (storage == null || !storage.IsOpen || item == null ||
                !storage.Contents.Contains(item) ||
                storage.Location.Map != actor.Location.Map ||
                m_Rules.GridDistance(actor.Location.Position, storage.Location.Position) > 1 ||
                !m_Rules.CanActorGetItem(actor, item)) return;
            Map map = storage.Location.Map;
            Point position = storage.Location.Position;
            bool alreadyStolen = item.IsStolen;
            Guid claimant = item.StorageClaimantId;
            bool personal = claimant != Guid.Empty && claimant != actor.PersonalityIdentity && !alreadyStolen;
            XpdBase baseClaim = map.XpdBaseAt(position);
            bool territorial = !alreadyStolen && baseClaim != null &&
                !baseClaim.Owns(actor) && item.LastDroppedBy != actor;
            Actor personalVictim = null;
            if (personal)
                foreach (Actor candidate in map.Actors)
                    if (candidate.PersonalityIdentity == claimant) { personalVictim = candidate; break; }
            Actor baseVictim = territorial ? baseClaim.GroupLeader : null;
            Actor victim = baseVictim ?? personalVictim;
            Guid victimId = territorial ? baseVictim.PersonalityIdentity : personal ? claimant : Guid.Empty;
            string resource = item is ItemFood ? "food" : item is ItemMedicine ? "medicine" : "item";
            int personalPermission = !personal ? int.MaxValue :
                actor.Personality == null || !m_Session.GamePreset.NpcPersonalitiesEnabled ? 0 :
                actor.Personality.AvailablePermission(claimant, storage.Location, resource, map.LocalTime.TurnCounter);
            int basePermission = !territorial || claimant == victimId && personal ? int.MaxValue :
                actor.Personality == null || !m_Session.GamePreset.NpcPersonalitiesEnabled ? 0 :
                actor.Personality.AvailablePermission(victimId, storage.Location, resource, map.LocalTime.TurnCounter);
            int permitted = personal || territorial ? Math.Min(personalPermission, basePermission) : 0;
            int total = 0;
            if (permitted > 0 && permitted < item.Quantity)
            {
                Item lawful = item.CopyQuantity(permitted);
                int moved;
                actor.Inventory.AddAsMuchAsPossible(lawful, out moved);
                if (moved > 0)
                {
                    item.Quantity -= moved;
                    total += moved;
                    if (personal) UseStoragePermission(actor, personalVictim, claimant, storage.Location, resource, item.Model.ID, moved);
                    if (territorial && (!personal || claimant != victimId))
                        UseStoragePermission(actor, baseVictim, victimId, storage.Location, resource, item.Model.ID, moved);
                }
                if (moved < permitted) goto finished;
                permitted = 0;
            }
            if (item.Quantity > 0)
            {
                int before = item.Quantity;
                bool theft = (personal || territorial) && permitted < before;
                string storyId = null;
                if (theft)
                {
                    storyId = (territorial ? "base-theft:" : "cache-theft:") + victimId.ToString("N") +
                        ":" + map.LocalTime.TurnCounter;
                    if (!territorial)
                        item.MarkStolen(item.StorageClaimantGroupId, claimant,
                            item.StorageClaimantName, storyId, 0);
                    else
                    {
                        Actor baseOwner = baseClaim.GroupLeader;
                        item.MarkStolen(baseOwner.SocialGroup == null ? baseOwner.PersonalityIdentity :
                            baseOwner.SocialGroup.Identity, victimId, baseOwner.UnmodifiedName, storyId, 0);
                    }
                }
                int moved;
                actor.Inventory.AddAsMuchAsPossible(item, out moved);
                if (moved == before) storage.Contents.RemoveAllQuantity(item);
                else if (theft) item.ClearTheft();
                total += moved;
                if (moved > 0)
                {
                    if (permitted >= before)
                    {
                        if (personal) UseStoragePermission(actor, personalVictim, claimant, storage.Location, resource, item.Model.ID, moved);
                        if (territorial && (!personal || claimant != victimId))
                            UseStoragePermission(actor, baseVictim, victimId, storage.Location, resource, item.Model.ID, moved);
                    }
                    if (theft)
                    {
                        long eventId = territorial ? NoticeXpdBaseTheft(actor, baseClaim, position, item, 0, storyId, moved, true) :
                            NoticeCacheTheft(actor, victim, storage, item, resource, moved, storyId);
                        if (eventId > 0)
                            foreach (Item carried in actor.Inventory.Items)
                                if (carried.StolenFromLeaderId == victimId && carried.TheftStoryId == storyId)
                                    carried.RememberTheftEvent(eventId);
                    }
                    if (alreadyStolen) ReportStolenGoods(actor, null, item, "stolen_goods_found", position);
                }
                else if (theft) item.ClearTheft();
            }
        finished:
            if (total == 0) return;
            RememberStorageContents(actor, storage, NpcKnowledgeSource.Participant);
            ItemTrap trap = item as ItemTrap;
            if (trap != null) trap.Desactivate();
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(storage))
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_TAKE), item));
            if (actor.Inventory.Contains(item) && !item.Model.DontAutoEquip &&
                m_Rules.CanActorEquipItem(actor, item) && actor.GetEquippedItem(item.Model.EquipmentPart) == null)
                DoEquipItem(actor, item);
        }

        void UseStoragePermission(Actor actor, Actor victim, Guid victimId, Location place,
            string resource, int modelId, int units)
        {
            if (victimId == Guid.Empty || actor.Personality == null || units <= 0) return;
            int used;
            long cause = actor.Personality.UsePermission(victimId, place, resource,
                place.Map.LocalTime.TurnCounter, units, out used);
            if (cause != 0 && used > 0)
                Gameplay.Personality.PersonalitySystem.Report(this,
                    new Gameplay.Personality.SignificantEvent("permission_used", actor, victim,
                        place.Map, place.Position, place.Map.LocalTime.TurnCounter, false,
                        causeId: cause) { Units = used, ModelId = modelId, Resource = resource });
        }

        long NoticeCacheTheft(Actor thief, Actor victim, StorageContainer storage,
            Item item, string resource, int units, string storyId)
        {
            var eventData = new Gameplay.Personality.SignificantEvent("cache_theft", thief, victim,
                storage.Location.Map, storage.Location.Position,
                storage.Location.Map.LocalTime.TurnCounter, false, storyId: storyId)
            {
                ClaimantId = item.StorageClaimantId,
                ClaimantGroupId = item.StorageClaimantGroupId,
                ClaimantName = item.StorageClaimantName,
                ItemId = item.StoryIdentity, Units = units, Resource = resource, ModelId = item.Model.ID
            };
            Gameplay.Personality.PersonalitySystem.Report(this, eventData);
            return eventData.Id;
        }

    }
}
