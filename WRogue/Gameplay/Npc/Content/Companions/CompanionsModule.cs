using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class CompanionsModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "companions"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Belonging", "Restore contact", NpcGoalValue.Belonging, m => 20 + m.Group + m.Compassion / 2 + m.Attachment / 2 + m.Feeling / 2, false));
            var seek = new NpcIntentDefinition("seek_companion", "Find a missing companion",
            NpcIntentMethod.SeekPerson, 20, 35, WorldTime.TURNS_PER_DAY, 180, 1, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Compassion, 1, 2));
            seek.BuildPlan = d => { CompanionPlanOperators.Build(d, false); };
            seek.Result = (c, g) => (ulong)(NpcPlanFact.Contact);
            seek.SocialPriority = r => r.Attachment / 2;
            catalog.Capability(seek);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead || person.Hostile) continue;
                if (!person.Hostile && owner.SocialGroup != null && owner.SocialGroup.Members.Contains(person.Id) &&
                    (turn - person.SeenTurn >= 30 || context.Pending(NpcGoalValue.Belonging, person.Id)))
                    offers.Add(person, NpcGoalValue.Belonging, context.Catalog.Capability("seek_companion"), 0, 100, 100, person.Confidence);
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                bool attached = opinion != null && opinion.Attachment >= 20 || owner.Personality.HasAttachments && owner.Personality.Attachments.Exists(a => a.Kind == "person" && a.Person == person.Id);
                if (attached && turn - person.SeenTurn >= 30 && !context.Pending(NpcGoalValue.Belonging, person.Id))
                    offers.Add(person, NpcGoalValue.Belonging, context.Catalog.Capability("seek_companion"), 0, 100, 100, person.Confidence, person.SocialCause);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("person.ask_location", NpcPlanAction.AskLocation, c => c.PlanAction()));
            catalog.Operator(new NpcOperatorDefinition("person.reunite", NpcPlanAction.Reunite, c => c.PlanAction()));
            catalog.Memory(new MemoryDefinition("found_a_companion", "Found a missing companion", 2, 5,
                new[] { new MemoryTrigger("reunited", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Other, 5, MemoryRelationRole.Other), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("asked_location", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " about a missing companion.", null));
            catalog.Event(new NpcEventDefinition("location_reported", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " answered " + (e.Other ?? "someone") + " about a companion's last known location.", null));
            catalog.Event(new NpcEventDefinition("reunited", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " found " + (e.Other ?? "someone") + " after searching.", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("joined_group", NpcRecordCategory.Encounters, false, e => (e.Subject ?? "Someone") + " joined " + (e.Other ?? "someone") + "'s group.", null));
            catalog.Event(new NpcEventDefinition("abandoned", NpcRecordCategory.Encounters, false, e => (e.Subject ?? "Someone") + " was abandoned by " + (e.Other ?? "someone") + ".", null));
        }
    }
}
