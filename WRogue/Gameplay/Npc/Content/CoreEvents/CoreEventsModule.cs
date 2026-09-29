using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class CoreEventsModule : INpcContentModule
    {
        public string Id { get { return "core-events"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            foreach (var faction in PersonalityWorldContent.Factions) catalog.FactionPolicy((int)faction.Id, new NpcFactionPolicy(faction.Supply, faction.Shelter));
            catalog.Event(new NpcEventDefinition("craps", NpcRecordCategory.World));
            catalog.Event(new NpcEventDefinition("floods", NpcRecordCategory.World));
            catalog.OnReport("death", c => {
                NpcKnownPerson person = c.Listener.Personality.Knowledge.Person(c.Fact.SubjectId);
                bool accepted = person != null && person.Dead && person.Source == NpcKnowledgeSource.Told && person.SeenTurn == c.Fact.EventTurn;
                if (accepted) NpcGoalLifecycle.KnownDeath(c.Listener, c.Fact.SubjectId, "learned of death through a report");
            });
            catalog.Event(new NpcEventDefinition("attack", NpcRecordCategory.Combat, true, e => (e.Other ?? "someone") + " attacked " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("murder", NpcRecordCategory.Combat, true, e => (e.Other ?? "someone") + " murdered " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("death", NpcRecordCategory.Combat | NpcRecordCategory.Life, true, e => (e.Subject ?? "Someone") + " died" + (e.Other == null ? "." : "; killed by " + (e.Other ?? "someone") + "."), f => f.SubjectName + " died") { ProvesDeath = true });
            catalog.Event(new NpcEventDefinition("kill_human", NpcRecordCategory.Combat, false, e => (e.Other ?? "someone") + " killed " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("starvation", NpcRecordCategory.Life, false, e => (e.Subject ?? "Someone") + " faced starvation.", null));
            catalog.Event(new NpcEventDefinition("zombified", NpcRecordCategory.Life, false, e => (e.Other ?? "someone") + " turned into " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("base_loss", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " lost a base.", null));
            catalog.Event(new NpcEventDefinition("raid", NpcRecordCategory.World, false, e => "A raid occurred.", null));
            catalog.Event(new NpcEventDefinition("spawn", NpcRecordCategory.Life, false, null, null));
            catalog.Event(new NpcEventDefinition("unique_arrival", NpcRecordCategory.World, false, e => (e.Subject ?? "Someone") + " arrived.", null));
            catalog.Event(new NpcEventDefinition("met_unique", NpcRecordCategory.Encounters, false, null, null));
            catalog.On("death", NpcObservationPhase.Goals, OnGoals);
            foreach (var source in PersonalityWorldContent.Uniques)
                catalog.Event(new NpcEventDefinition(source.Kind, NpcRecordCategory.Encounters, describe: e => source.Name + (e.Subject == null ? "." : ": " + e.Subject + ".")) { OncePerSubject = true, CanObserve = (a, e) => a != e.Subject });
            foreach (var source in PersonalityWorldContent.WorldEvents)
                catalog.Event(new NpcEventDefinition(source.Kind, WorldCategory(source.Kind), source.Kind == "army_supplies" || source.Kind.EndsWith("_raid"),
                    e => source.Name + (e.Subject == null ? "." : ": " + e.Subject + ".")));
        }
        static NpcRecordCategory WorldCategory(string kind)
        { return kind.EndsWith("_raid") || kind.EndsWith("_arrival") || kind == "zombie_invasion" || kind == "army_supplies" ? NpcRecordCategory.World : NpcRecordCategory.None; }
        static void OnGoals(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            if (source.Kind == "death")
            {
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && source.Subject != null &&
                        (source.Subject == owner || (intent.Generated != null ? intent.Generated.SubjectId == source.Subject.PersonalityIdentity :
                            intent.TargetId == source.Subject.PersonalityIdentity || intent.CoordinatorId == source.Subject.PersonalityIdentity)))
                        NpcIntentSystem.Finish(owner, intent, NpcIntentStatus.Failed, source.Subject == owner ? "owner died" : "learned that the target died");
                if (source.Subject == owner) state.Reactions.Clear();
            }
        }
    }
}
