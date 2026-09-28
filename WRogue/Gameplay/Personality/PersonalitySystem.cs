using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class PersonalitySystem
    {
        static readonly PersonalityRegistry s_Registry = PersonalityContent.Create();
        public static PersonalityRegistry Registry { get { return s_Registry; } }

        public static void Initialize(Actor actor, DiceRoller dice)
        {
            if (actor == null || dice == null || actor.IsPlayer || actor.Model.Abilities.IsUndead ||
                !actor.Model.Abilities.IsIntelligent ||
                !Session.Get.GamePreset.NpcPersonalitiesEnabled || actor.Personality != null) return;

            PersonalityState state = new PersonalityState();
            actor.Personality = state;
            List<TraitDefinition> available = new List<TraitDefinition>(s_Registry.StartingTraits);
            for (int i = 0; i < 3 && available.Count > 0; i++)
            {
                available.RemoveAll(candidate => !candidate.Eligible(actor));
                if (available.Count == 0) break;
                int choice = dice.Roll(0, available.Count);
                TraitDefinition definition = available[choice];
                available.RemoveAt(choice);
                int itemModelId = definition.ItemParameter ? RollItemModel(dice) : -1;
                state.AddTrait(new TraitInstance(definition.Id, itemModelId));
            }

            List<MemoryDefinition> memories = new List<MemoryDefinition>(s_Registry.StartingMemories);
            int count = 1 + dice.Roll(0, 2);
            for (int i = 0; i < count && memories.Count > 0; i++)
            {
                int choice = dice.Roll(0, memories.Count);
                MemoryDefinition definition = memories[choice];
                memories.RemoveAt(choice);
                AddMemory(actor, definition, actor.SpawnTime, null, dice);
            }
        }

        static int RollItemModel(DiceRoller dice)
        {
            if (Models.Items == null) return -1;
            for (int i = 0; i < 16; i++)
            {
                // Backstory preferences should usually refer to loot the NPC can encounter.
                // Explicit TraitInstance parameters can still target any item model.
                int id = dice.Roll(0, (int)GameItems.IDs.UNIQUE_SUBWAY_BADGE);
                if (id == (int)GameItems.IDs.UNIQUE_JASON_MYERS_AXE ||
                    id == (int)GameItems.IDs.EXPLOSIVE_GRENADE_PRIMED) continue;
                if (Models.Items[id] != null) return id;
            }
            return -1;
        }

        static void AddMemory(Actor actor, MemoryDefinition definition, int turn, string subject,
            DiceRoller dice, bool relatedToSubject = false, Guid subjectId = default(Guid),
            Actor relatedPerson = null, Actor relatedGroupLeader = null, int impact = 0)
        {
            int days = dice.Roll(definition.MinDays, definition.MaxDays + 1);
            MemoryInstance memory = new MemoryInstance(definition.Id, turn,
                turn + days * WorldTime.TURNS_PER_DAY, subject, relatedToSubject, subjectId,
                relatedPerson == null ? Guid.Empty : relatedPerson.PersonalityIdentity);
            if (!actor.Personality.AddMemory(memory)) return;
            if (relatedPerson != null)
            {
                actor.Personality.RememberPerson(relatedPerson.PersonalityIdentity,
                    relatedPerson.UnmodifiedName, memory, impact);
                if (relatedGroupLeader == null && relatedPerson.HasLeader)
                    relatedGroupLeader = relatedPerson.Leader;
                else if (relatedGroupLeader == null && relatedPerson.CountFollowers > 0)
                    relatedGroupLeader = relatedPerson;
                if (definition.RelationFactionId < 0 && relatedPerson.Faction != null &&
                    relatedPerson.Faction.ID != (int)GameFactions.IDs.TheCivilians)
                    actor.Personality.RememberFaction(relatedPerson.Faction.ID,
                        relatedPerson.Faction.Name, memory, impact / 4);
            }
            if (relatedGroupLeader != null)
                actor.Personality.RememberGroup(relatedGroupLeader.PersonalityIdentity,
                    relatedGroupLeader.UnmodifiedName, memory, impact / 3);
            if (definition.RelationFactionId >= 0)
                actor.Personality.RememberFaction(definition.RelationFactionId,
                    Models.Factions[definition.RelationFactionId].Name, memory, definition.FactionFeelingChange);
            if (actor.Location.Map != null)
                Session.Get.ResidentRecords.MemoryStarted(actor, memory);
        }

        static Actor RelationActor(MemoryRelationRole role, Actor observer, SignificantEvent lifeEvent)
        {
            Actor target;
            switch (role)
            {
                case MemoryRelationRole.Subject:
                    target = lifeEvent.Subject;
                    break;
                case MemoryRelationRole.OtherOrSubject:
                    target = lifeEvent.Other ?? lifeEvent.Subject;
                    break;
                case MemoryRelationRole.Other:
                    target = lifeEvent.Other;
                    break;
                default:
                    target = null;
                    break;
            }
            return target == observer ? null : target;
        }

        public static int Attitude(Actor observer, Actor target)
        {
            if (observer == null || target == null || observer == target ||
                observer.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled)
                return 0;
            int total = 0;
            RelationshipRecord person = observer.Personality.Person(target.PersonalityIdentity);
            if (person != null) total += person.Feeling;
            Actor groupLeader = target.HasLeader ? target.Leader :
                target.CountFollowers > 0 ? target : null;
            if (groupLeader != null)
            {
                RelationshipRecord group = observer.Personality.Group(groupLeader.PersonalityIdentity);
                if (group != null) total += group.Feeling;
            }
            if (target.Faction != null)
            {
                RelationshipRecord faction = observer.Personality.Faction(target.Faction.ID);
                if (faction != null) total += faction.Feeling;
                foreach (TraitInstance trait in observer.Personality.Traits)
                {
                    TraitDefinition definition = s_Registry.Trait(trait.Id);
                    if (definition != null && definition.RelationFactionId == target.Faction.ID)
                        total += definition.RelationBias;
                }
            }
            return Math.Max(-100, Math.Min(100, total));
        }

        public static int Bias(Actor actor, DecisionKind decision, Item item = null)
        {
            if (actor == null || actor.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled)
                return 0;
            int total = 0;
            foreach (TraitInstance instance in actor.Personality.Traits)
            {
                TraitDefinition definition = s_Registry.Trait(instance.Id);
                if (definition == null) continue;
                if (definition.ItemParameter && (item == null || item.Model.ID != instance.ItemModelId))
                    continue;
                foreach (TraitEffect effect in definition.Effects)
                    if (effect.Decision == decision && (effect.Applies == null || effect.Applies(actor, item)))
                        total += effect.Amount;
            }
            return Math.Max(-100, Math.Min(100, total));
        }

        public static bool HasTrait(Actor actor, string id)
        {
            return actor != null && actor.Personality != null &&
                Session.Get.GamePreset.NpcPersonalitiesEnabled && actor.Personality.HasTrait(id);
        }

        public static void Report(RogueGame game, SignificantEvent lifeEvent)
        {
            if (game == null || lifeEvent == null || lifeEvent.Map == null ||
                !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            // Make a snapshot: resolving a death may remove actors from this map.
            List<Actor> actors = new List<Actor>(lifeEvent.Map.Actors);
            Guid subjectId = lifeEvent.Subject == null ? Guid.Empty : lifeEvent.Subject.PersonalityIdentity;
            Guid otherId = lifeEvent.Other == null ? Guid.Empty : lifeEvent.Other.PersonalityIdentity;
            ObservedEvent directEvent = new ObservedEvent(lifeEvent.Kind, lifeEvent.Turn,
                lifeEvent.Subject == null ? null : lifeEvent.Subject.UnmodifiedName,
                lifeEvent.Other == null ? null : lifeEvent.Other.UnmodifiedName,
                true, false, subjectId, otherId);
            if (lifeEvent.SubjectIsDirect) Session.Get.ResidentRecords.Observe(lifeEvent.Subject, directEvent);
            if (lifeEvent.OtherIsDirect) Session.Get.ResidentRecords.Observe(lifeEvent.Other, directEvent);
            foreach (Actor observer in actors)
            {
                if ((!observer.IsPlayer && observer.Personality == null) || observer.IsDead || observer.Model.Abilities.IsUndead ||
                    !observer.Model.Abilities.IsIntelligent)
                    continue;
                bool direct = (lifeEvent.SubjectIsDirect && observer == lifeEvent.Subject) ||
                    (lifeEvent.OtherIsDirect && observer == lifeEvent.Other);
                bool saw = !observer.IsSleeping &&
                    game.Rules.GridDistance(observer.Location.Position, lifeEvent.Position) <=
                        game.Rules.ActorFOV(observer, lifeEvent.Map.LocalTime, game.Session.World.Weather) &&
                    LOS.CanTraceViewLine(observer.Location, lifeEvent.Position);
                if (!direct && !saw) continue;
                if (observer.IsPlayer && observer.Personality == null)
                    observer.Personality = new PersonalityState();
                if (lifeEvent.Kind.StartsWith("met_unique:"))
                {
                    if (observer == lifeEvent.Subject) continue;
                    bool known = false;
                    RelationshipRecord person = observer.Personality.Person(subjectId);
                    if (person != null)
                        foreach (MemoryDefinition definition in s_Registry.ForEvent(lifeEvent.Kind))
                            if (definition.OncePerPerson)
                                foreach (MemoryInstance old in person.Memories)
                                    if (old.Id == definition.Id) known = true;
                    if (known) continue;
                }
                string subject = lifeEvent.Subject == null ? null : lifeEvent.Subject.UnmodifiedName;
                string other = lifeEvent.Other == null ? null : lifeEvent.Other.UnmodifiedName;
                bool relatedToSubject = lifeEvent.Subject != null &&
                    (observer.Leader == lifeEvent.Subject || lifeEvent.Subject.Leader == observer);
                ObservedEvent observation = new ObservedEvent(lifeEvent.Kind, lifeEvent.Turn,
                    subject, other, direct, relatedToSubject, subjectId, otherId);
                observer.Personality.Remember(observation);
                Session.Get.ResidentRecords.Observe(observer, observation);
                foreach (MemoryInstance memory in observer.Personality.Memories)
                {
                    MemoryDefinition pendingDefinition = s_Registry.Memory(memory.Id);
                    if (pendingDefinition != null && lifeEvent.Turn >= memory.StartTurn &&
                        pendingDefinition.TracksEvidence(lifeEvent.Kind))
                        memory.RememberEvidence(lifeEvent.Kind, lifeEvent.Turn);
                }
                foreach (MemoryDefinition definition in s_Registry.ForEvent(lifeEvent.Kind))
                    foreach (MemoryTrigger trigger in definition.Triggers)
                        if (trigger.EventKind == lifeEvent.Kind && trigger.Applies(observer, lifeEvent))
                        {
                            Actor relatedPerson = RelationActor(definition.PersonRole, observer, lifeEvent);
                            Actor relatedGroupLeader = RelationActor(definition.GroupRole, null, lifeEvent);
                            int impact = definition.PersonRole == MemoryRelationRole.OtherOrSubject &&
                                lifeEvent.Other == null ? definition.FallbackFeelingChange :
                                definition.FeelingChange;
                            AddMemory(observer, definition, lifeEvent.Turn, subject,
                                game.Session.GameDiceRoller, relatedToSubject, subjectId,
                                relatedPerson, relatedGroupLeader, impact);
                            break;
                        }
            }
        }

        public static void ResolveDue(RogueGame game, Map map)
        {
            if (game == null || map == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            foreach (Actor actor in map.Actors)
            {
                if (actor.Personality == null || actor.IsDead || actor.Model.Abilities.IsUndead ||
                    !actor.Model.Abilities.IsIntelligent)
                    continue;
                // Copy because successful resolution removes the pending memory.
                List<MemoryInstance> pending = new List<MemoryInstance>(actor.Personality.Memories);
                foreach (MemoryInstance memory in pending)
                {
                    if (map.LocalTime.TurnCounter < memory.ResolveTurn) continue;
                    MemoryDefinition definition = s_Registry.Memory(memory.Id);
                    if (definition != null && !actor.IsPlayer)
                        foreach (MemoryOutcome outcome in definition.Outcomes)
                        {
                            if (outcome.Applies != null && !outcome.Applies(actor, memory)) continue;
                            if (outcome.TraitId != null)
                            {
                                TraitDefinition trait = s_Registry.Trait(outcome.TraitId);
                                if (trait == null || !trait.Eligible(actor)) continue;
                                actor.Personality.AddTrait(new TraitInstance(trait.Id));
                                memory.OutcomeId = "trait:" + trait.Id;
                                break;
                            }
                            if (outcome.SkillId.HasValue && actor.Sheet.SkillTable.GetSkillLevel((int)outcome.SkillId.Value) <
                                Skills.MaxSkillLevel(outcome.SkillId.Value))
                            {
                                game.SkillUpgrade(actor, outcome.SkillId.Value);
                                memory.OutcomeId = "skill:" + outcome.SkillId.Value;
                                break;
                            }
                        }
                    if (memory.OutcomeId == null) memory.OutcomeId = "none";
                    memory.ResolvedTurn = map.LocalTime.TurnCounter;
                    Session.Get.ResidentRecords.MemoryResolved(actor, memory);
                    actor.Personality.RemoveMemory(memory);
                }
            }
        }

        public static void ObserveEncounters(RogueGame game, Map map)
        {
            if (game == null || map == null || !game.Session.GamePreset.NpcPersonalitiesEnabled) return;
            UniqueActors uniques = game.Session.UniqueActors;
            if (uniques == null) return;
            foreach (PersonalityWorldContent.Experience source in PersonalityWorldContent.Uniques)
            {
                UniqueActor unique = source.Unique(uniques);
                Actor actor = unique == null ? null : unique.TheActor;
                if (actor == null || actor.IsDead || actor.Location.Map != map || !map.HasActor(actor)) continue;
                Report(game, new SignificantEvent(source.Kind, actor, null, map,
                    actor.Location.Position, map.LocalTime.TurnCounter, false, false));
            }
        }
    }
}
