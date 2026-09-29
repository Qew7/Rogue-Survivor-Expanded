using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcGoalGenerator
    {
        static void SocialGoals(RogueGame game, Actor owner, List<NpcGoalCandidate> result, NpcKnownPerson self, List<NpcKnownPerson> people)
        {
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead || person.Hostile) continue;
                if ((person.MedicalNeed > 0 || Pending(owner, NpcGoalValue.MedicalCare, person.Id)) && turn - person.MedicalTurn <= 60)
                    SocialAdd(result, owner, person, NpcGoalValue.MedicalCare, NpcIntentContent.MedicalAid,
                        100 - person.MedicalNeed, 100, person.MedicalNeed, person.MedicalConfidence, "medicine", person.MedicalCause, person.MedicalStory);
                if (person.LossUnits > 0 || Pending(owner, NpcGoalValue.Restitution, person.Id))
                    SocialAdd(result, owner, person, NpcGoalValue.Restitution, NpcIntentContent.Restitution,
                        person.LossUnits, 0, Math.Min(100, person.LossUnits * 40), 100, "food", person.LossCause, objectPlace: person.LossPlace);
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                bool attached = opinion != null && opinion.Attachment >= 20 || owner.Personality.HasAttachments && owner.Personality.Attachments.Exists(a => a.Kind == "person" && a.Person == person.Id);
                if (attached && turn - person.SeenTurn >= 30 && !Pending(owner, NpcGoalValue.Belonging, person.Id))
                    Add(result, owner, person, NpcGoalValue.Belonging, NpcIntentContent.Seek, 0, 100, 100, person.Confidence, person.SocialCause);
            }
            if (owner.Personality.HasCommitments) foreach (NpcCommitment promise in owner.Personality.Commitments)
            {
                if (promise.Promisor != self.Id || promise.Status != NpcCommitmentStatus.Active || turn >= promise.DueTurn) continue;
                NpcKnownPerson recipient = people.Find(p => p.Id == promise.Beneficiary);
                if (recipient == null || recipient.Hostile) continue;
                SocialAdd(result, owner, recipient, NpcGoalValue.Commitment, NpcIntentContent.Promise,
                    0, promise.Units, 100, 100, promise.Resource, promise.Id, promise.StoryId, promise.Id);
            }
            foreach (TraitInstance trait in owner.Personality.Traits)
                if (trait.Id == "likes_items" && trait.ItemModelId >= 0)
                {
                    bool owned = false;
                    foreach (Item item in owner.Inventory.Items)
                        if (item.Model.ID == trait.ItemModelId && !item.Model.IsStackable)
                        { owned = true; owner.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = trait.ItemModelId, ItemId = item.StoryIdentity, Weight = 35, Name = item.TheName }); }
                    if (!owned && !owner.Personality.Attachments.Exists(a => a.Kind == "item" && a.ModelId == trait.ItemModelId))
                        owner.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = trait.ItemModelId, Weight = 35, Name = "preferred item" });
                }
            if (owner.Personality.HasAttachments) foreach (NpcAttachment attachment in owner.Personality.Attachments)
            {
                if (attachment.Kind == "item")
                {
                    bool owned = false; foreach (Item item in owner.Inventory.Items) if (item.Model.ID == attachment.ModelId && (attachment.ItemId == Guid.Empty || item.StoryIdentity == attachment.ItemId)) owned = true;
                    string kind = attachment.ItemId == Guid.Empty ? "item:" + attachment.ModelId : "item:" + attachment.ItemId.ToString("N");
                    bool known = owner.Personality.Knowledge.Places.Exists(p => p.Kind == kind && p.Units > 0 && turn - p.SeenTurn <= 180);
                    if (!owned && known) SocialAdd(result, owner, self, NpcGoalValue.Possession, NpcIntentContent.ValuedItem,
                        0, 1, 100, 100, "item", attachment.CauseId, model: attachment.ModelId, itemId: attachment.ItemId);
                }
                if (attachment.Kind == "place" && attachment.Place.Map != null && (attachment.MissingUnits > 0 || people.Exists(p =>
                    p.Danger > 0 && p.Place.Map == attachment.Place.Map && turn - p.ThreatTurn < 180 && game.Rules.GridDistance(p.Place.Position, attachment.Place.Position) < 5)) &&
                    owner.Location.Map == attachment.Place.Map && (game.Rules.GridDistance(owner.Location.Position, attachment.Place.Position) > 1 || Pending(owner, NpcGoalValue.ProtectHome, self.Id)))
                    SocialAdd(result, owner, self, NpcGoalValue.ProtectHome, NpcIntentContent.Shelter,
                        0, 1, 100, 100, "home", attachment.CauseId, objectPlace: attachment.Place);
            }
        }
        static void SocialAdd(List<NpcGoalCandidate> result, Actor owner, NpcKnownPerson person, NpcGoalValue value,
            NpcIntentDefinition capability, int current, int desired, int deficit, int confidence, string resource,
            long cause = 0, string story = null, long obligation = 0, int model = -1, Location objectPlace = default(Location), Guid itemId = default(Guid))
        {
            int count = result.Count; Add(result, owner, person, value, capability, current, desired, deficit, confidence, cause, story);
            if (result.Count == count) return;
            NpcGeneratedGoal state = result[count].State;
            state.Resource = resource; state.ObligationId = obligation; state.ModelId = model; state.ObjectPlace = objectPlace;
            state.ItemId = itemId;
            if (value == NpcGoalValue.ProtectHome) state.Result = (ulong)NpcPlanFact.Sheltered;
        }
    }
}
