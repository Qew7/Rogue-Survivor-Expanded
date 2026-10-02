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
            foreach (var faction in PersonalityWorldContent.Factions) catalog.FactionPolicy((int)faction.Id, new NpcFactionPolicy(faction.Supply, faction.Shelter, faction.Care, faction.Security));
            catalog.Event(new NpcEventDefinition("craps", NpcRecordCategory.World));
            catalog.Event(new NpcEventDefinition("floods", NpcRecordCategory.World));
            catalog.OnReport("death", c => {
                NpcKnownPerson person = c.Listener.Personality.Knowledge.Person(c.Fact.SubjectId);
                bool accepted = person != null && person.Dead && person.Source == NpcKnowledgeSource.Told && person.SeenTurn == c.Fact.EventTurn;
                if (accepted) NpcGoalLifecycle.KnownDeath(c.Listener, c.Fact.SubjectId, "learned of death through a report", c.Catalog);
            });
            catalog.Event(new NpcEventDefinition("attack", NpcRecordCategory.Combat, true, e => (e.Other ?? "someone") + " attacked " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("murder", NpcRecordCategory.Combat, true, e => (e.Other ?? "someone") + " murdered " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("death", NpcRecordCategory.Combat | NpcRecordCategory.Life, true, e => (e.Subject ?? "Someone") + " died" + (e.Other == null ? "." : "; killed by " + (e.Other ?? "someone") + "."), f => f.ReportSubject + " died" + (f.ReportOther == null ? "" : "; killed by " + f.ReportOther)) { ProvesDeath = true });
            catalog.Event(new NpcEventDefinition("kill_human", NpcRecordCategory.Combat, false, e => (e.Other ?? "someone") + " killed " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("starvation", NpcRecordCategory.Life, false, e => (e.Subject ?? "Someone") + " faced starvation.", null));
            catalog.Event(new NpcEventDefinition("zombified", NpcRecordCategory.Life, false, e => (e.Other ?? "someone") + " turned into " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("base_loss", NpcRecordCategory.World, true, e => (e.Subject ?? "Someone") + " lost a base.", null));
            catalog.Event(new NpcEventDefinition("raid", NpcRecordCategory.World, true, e => "A raid occurred.", f => "there was a raid"));
            catalog.Event(new NpcEventDefinition("spawn", NpcRecordCategory.Life, false, null, null) { CanObserve = (a, e) => a != e.Subject });
            catalog.Event(new NpcEventDefinition("unique_arrival", NpcRecordCategory.World, false, e => (e.Subject ?? "Someone") + " arrived.", null) { CanObserve = (a, e) => a != e.Subject });
            catalog.Event(new NpcEventDefinition("met_unique", NpcRecordCategory.Encounters, false, null, null));
            catalog.Event(new NpcEventDefinition("chat", NpcRecordCategory.Encounters, false,
                e => (e.Subject ?? "Someone") + " talked with " + (e.Other ?? "someone") + "."));
            catalog.Event(new NpcEventDefinition("traded", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " traded with " + (e.Other ?? "someone") + "."));
            catalog.On("chat", NpcObservationPhase.Relationships, RememberContact);
            catalog.On("traded", NpcObservationPhase.Relationships, RememberContact);
            catalog.On("death", NpcObservationPhase.Goals, OnGoals);
            foreach (var source in PersonalityWorldContent.Uniques)
                catalog.Event(new NpcEventDefinition(source.Kind, NpcRecordCategory.Encounters, describe: e => source.Name + (e.Subject == null ? "." : ": " + e.Subject + ".")) { OncePerSubject = true, CanObserve = (a, e) => a != e.Subject });
            foreach (var source in PersonalityWorldContent.WorldEvents)
            {
                Func<NpcFact, string> report = null;
                if (source.Kind.EndsWith("_raid"))
                    report = f => "there was a " + source.Name.ToLowerInvariant() +
                        (f.ReportSubject == null ? "" : " involving " + f.ReportSubject);
                else if (source.Kind == "army_supplies")
                    report = f => "there was an army relief drop" +
                        (f.ReportSubject == null ? "" : " involving " + f.ReportSubject);
                catalog.Event(new NpcEventDefinition(source.Kind, WorldCategory(source.Kind),
                    source.Kind == "army_supplies" || source.Kind.EndsWith("_raid"),
                    e => source.Name + (e.Subject == null ? "." : ": " + e.Subject + "."), report));
            }
        }
        static NpcRecordCategory WorldCategory(string kind)
        { return kind.EndsWith("_raid") || kind.EndsWith("_arrival") || kind == "zombie_invasion" || kind == "army_supplies" ? NpcRecordCategory.World : NpcRecordCategory.None; }
        static void RememberContact(NpcObservation observation)
        {
            Actor partner = observation.Owner == observation.Source.Subject ? observation.Source.Other :
                observation.Owner == observation.Source.Other ? observation.Source.Subject : null;
            if (partner == null || !observation.Direct) return;
            RelationshipRecord record = observation.Owner.Personality.Opinion(partner.PersonalityIdentity,
                partner.UnmodifiedName);
            if (observation.Source.Kind == "traded") record.AdjustSocial(trust: 2);
        }
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
                        {
                            if (source.Subject == owner || intent.CoordinatorId == source.Subject.PersonalityIdentity)
                                NpcIntentSystem.Finish(observation.Game.NpcContent, owner, intent,
                                    NpcIntentStatus.Failed, source.Subject == owner ? "owner died" : "coordinator died");
                            else NpcGoalLifecycle.TargetDied(owner, intent, source.Other == owner, "learned that the target died", observation.Game.NpcContent);
                        }
                if (source.Subject == owner) state.Reactions.Clear();
            }
        }
    }
}
