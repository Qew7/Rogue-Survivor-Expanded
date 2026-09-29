using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcGroupTasks
    {
        public static void AcceptSupply(RogueGame game, Actor owner, SignificantEvent source, string resource, string capability)
        {
            NpcGroupPlan plan = source.Task;
            NpcCollectiveDefinition definition = game.NpcContent.Collective(plan.Kind);
            if (definition == null || plan.CollectorId != owner.PersonalityIdentity ||
                (definition.Scope == NpcCollectiveScope.Group && (owner.SocialGroup == null || source.Subject.SocialGroup != owner.SocialGroup)) ||
                (definition.Scope == NpcCollectiveScope.Faction && owner.Faction.ID != source.Subject.Faction.ID)) return;
            NpcKnownPerson beneficiary = source.Subject.Personality.Knowledge.Person(plan.BeneficiaryId);
            // The leader actually told the collector the beneficiary and observed destination.
            if (beneficiary == null) return;
            NpcKnownPlace cache = source.Subject.Personality.Knowledge.Places.Find(p => p.Kind == resource && p.Place == plan.Destination);
            if (cache != null) owner.Personality.Knowledge.RememberPlace(new NpcKnownPlace(cache.Place, cache.Kind, cache.SeenTurn, cache.Units, cache.Risk));
            NpcKnownPerson target = new NpcKnownPerson { Id = beneficiary.Id, Name = beneficiary.Name, Place = beneficiary.Place, SeenTurn = beneficiary.SeenTurn };
            NpcIntent goal = NpcStorySystem.StartKnown(owner, target, game.NpcContent.Capability(capability), source.Id, plan.StoryId, plan.Destination, definition.Scope == NpcCollectiveScope.Group ? owner.SocialGroup.Identity : Guid.Empty);
            if (goal != null) { goal.CoordinatorId = source.Subject.PersonalityIdentity; goal.CoordinatorPlace = source.Subject.Location; }
            if (goal == null && owner.Personality.Reactions.Count < 4)
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I won't take that task.", source.Id, source.Turn, "task_declined", plan.StoryId));
            else if (goal != null) plan.Stage = "fetching";
        }
        public static void AcceptShelter(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (!NpcIntentSystem.Enabled(owner)) return;
            NpcIntent goal = NpcStorySystem.StartKnown(owner, new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location },
                NpcIntentContent.Shelter, source.Id, source.Task.StoryId, source.Task.Destination, owner.SocialGroup.Identity);
            if (goal == null && owner != source.Subject && owner.Personality.Reactions.Count < 4)
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I'm not coming to that shelter.", source.Id,
                    source.Turn, "shelter_declined", source.StoryId));
        }
    }
}
