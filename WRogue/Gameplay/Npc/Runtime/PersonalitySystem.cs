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
            NpcMemoryProcessor.Add(actor, definition, turn, subject, dice, relatedToSubject, subjectId,
                NpcMemoryRelations.Observed(relatedPerson, relatedGroupLeader), impact);
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
            Actor leader = target.HasLeader ? target.Leader : target.CountFollowers > 0 ? target : null;
            Guid group = leader == null ? Guid.Empty : target.SocialGroup == null ? leader.PersonalityIdentity : target.SocialGroup.Identity;
            return NpcRelationshipValue.Calculate(observer, target.PersonalityIdentity, group, target.Faction == null ? -1 : target.Faction.ID, false);
        }

        public static int Bias(Actor actor, DecisionKind decision, Item item = null, PersonalityRegistry registry = null)
        {
            if (actor == null || actor.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled)
                return 0;
            int total = 0;
            foreach (TraitInstance instance in actor.Personality.Traits)
            {
                TraitDefinition definition = (registry ?? s_Registry).Trait(instance.Id);
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
            NpcEventDefinition eventDefinition = game.NpcContent.Event(lifeEvent.Kind);
            if (eventDefinition != null) eventDefinition.Validate(lifeEvent);
            if (lifeEvent.Id == 0) lifeEvent.Id = Session.Get.NextPersonalityEventId();
            // Make a snapshot: resolving a death may remove actors from this map.
            Actor privateOwner = eventDefinition == null || !eventDefinition.Private ? null : eventDefinition.PrivateAudience == null ? lifeEvent.Subject : eventDefinition.PrivateAudience(lifeEvent);
            if (eventDefinition != null && eventDefinition.Private && privateOwner == null) return;
            List<Actor> actors = privateOwner == null ? new List<Actor>(lifeEvent.Map.Actors) : new List<Actor> { privateOwner };
            Guid subjectId = lifeEvent.Subject == null ? Guid.Empty : lifeEvent.Subject.PersonalityIdentity;
            Guid otherId = lifeEvent.Other == null ? Guid.Empty : lifeEvent.Other.PersonalityIdentity;
            ObservedEvent directEvent = new ObservedEvent(lifeEvent.Kind, lifeEvent.Turn,
                lifeEvent.Subject == null ? null : lifeEvent.Subject.UnmodifiedName,
                lifeEvent.Other == null ? null : lifeEvent.Other.UnmodifiedName,
                true, false, subjectId, otherId, lifeEvent.Id, lifeEvent.CauseId, lifeEvent.StoryId);
            NpcRecordDescriptions.Annotate(directEvent, eventDefinition, game.NpcContent);
            if (privateOwner == null && lifeEvent.SubjectIsDirect || privateOwner == lifeEvent.Subject) Session.Get.ResidentRecords.Observe(lifeEvent.Subject, directEvent);
            if (privateOwner == null && lifeEvent.OtherIsDirect || privateOwner == lifeEvent.Other) Session.Get.ResidentRecords.Observe(lifeEvent.Other, directEvent);
            foreach (Actor observer in actors)
            {
                if ((!observer.IsPlayer && observer.Personality == null) || observer.IsDead || observer.Model.Abilities.IsUndead ||
                    !observer.Model.Abilities.IsIntelligent)
                    continue;
                bool direct = (lifeEvent.SubjectIsDirect && observer == lifeEvent.Subject) ||
                    (lifeEvent.OtherIsDirect && observer == lifeEvent.Other) || observer == privateOwner;
                bool saw = !observer.IsSleeping &&
                    game.Rules.GridDistance(observer.Location.Position, lifeEvent.Position) <=
                        game.Rules.ActorFOV(observer, lifeEvent.Map.LocalTime, game.Session.World.Weather) &&
                    LOS.CanTraceViewLine(observer.Location, lifeEvent.Position);
                if (!direct && !saw) continue;
                if (eventDefinition != null && eventDefinition.CanObserve != null && !eventDefinition.CanObserve(observer, lifeEvent)) continue;
                if (!direct && eventDefinition != null && (eventDefinition.Private || eventDefinition.CanWitness != null && !eventDefinition.CanWitness(observer, lifeEvent))) continue;
                if (observer.IsPlayer && observer.Personality == null)
                    observer.Personality = new PersonalityState();
                if (eventDefinition != null && eventDefinition.OncePerSubject)
                {
                    if (observer == lifeEvent.Subject) continue;
                    bool known = false;
                    RelationshipRecord person = observer.Personality.Person(subjectId);
                    if (person != null)
                        foreach (MemoryDefinition definition in game.NpcContent.Personalities.ForEvent(lifeEvent.Kind))
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
                    subject, other, direct, relatedToSubject, subjectId, otherId,
                    lifeEvent.Id, lifeEvent.CauseId, lifeEvent.StoryId);
                NpcRecordDescriptions.Annotate(observation, eventDefinition, game.NpcContent);
                if (!observer.Personality.Remember(observation)) continue;
                var eventObservation = new NpcObservation(game, observer, lifeEvent, direct);
                if (direct && lifeEvent.StoryId != null)
                    foreach (NpcIntent goal in observer.Personality.Intents)
                        if (!goal.Finished && goal.Plan != null && goal.StoryId == lifeEvent.StoryId && lifeEvent.Id > goal.Plan.LastEventId) goal.Plan.LastEventId = lifeEvent.Id;
                NpcEventPipeline.BeforeMemories(eventObservation);
                Session.Get.ResidentRecords.Observe(observer, observation);
                NpcMemoryProcessor.Evidence(observer, game.NpcContent.Personalities, lifeEvent.Kind, lifeEvent.Turn);
                foreach (MemoryDefinition definition in game.NpcContent.Personalities.ForEvent(lifeEvent.Kind))
                    foreach (MemoryTrigger trigger in definition.Triggers)
                        if (trigger.EventKind == lifeEvent.Kind && trigger.Applies(observer, lifeEvent))
                        {
                            Actor relatedPerson = RelationActor(definition.PersonRole, observer, lifeEvent);
                            // Attribute collective history to the permanent group behind the observed leader.
                            Actor relatedGroupLeader = RelationActor(definition.GroupRole, null, lifeEvent);
                            int impact = definition.PersonRole == MemoryRelationRole.OtherOrSubject &&
                                lifeEvent.Other == null ? definition.FallbackFeelingChange :
                                definition.FeelingChange;
                            AddMemory(observer, definition, lifeEvent.Turn, subject,
                                game.Session.GameDiceRoller, relatedToSubject, subjectId,
                                relatedPerson, relatedGroupLeader, impact);
                            break;
                        }
                NpcEventPipeline.AfterMemories(eventObservation);
            }
            NpcStorySystem.EventFinished(game, lifeEvent);
            if (eventDefinition != null) eventDefinition.Completed(game, lifeEvent);
            NpcConversation.BroadcastReport(game, lifeEvent, eventDefinition);
            NpcConversation.OfferPlayerReply(game, lifeEvent, eventDefinition);
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
                    MemoryDefinition definition = game.NpcContent.Personalities.Memory(memory.Id);
                    if (definition != null && !actor.IsPlayer)
                        foreach (MemoryOutcome outcome in definition.Outcomes)
                        {
                            if (outcome.Applies != null && !outcome.Applies(actor, memory)) continue;
                            if (outcome.TraitId != null)
                            {
                                TraitDefinition trait = game.NpcContent.Personalities.Trait(outcome.TraitId);
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
                    Session.Get.ResidentRecords.MemoryResolved(actor, memory, game.NpcContent.Personalities);
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
