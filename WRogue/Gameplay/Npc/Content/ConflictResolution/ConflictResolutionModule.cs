using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ConflictResolutionModule : INpcContentModule, INpcGoalSource
    {
        internal const string PolicePursuitId = "police_pursuit";
        internal const int PolicePursuitTurns = 2 * WorldTime.TURNS_PER_DAY;
        public string Id { get { return "conflict-resolution"; } }
        public void Register(NpcCatalogBuilder c)
        {
            c.GoalSource(this);
            NpcIntentDefinition defense = RegisterMechanic(c, "Protection", "Defend someone I value", "defend_person", "Defended",
                m => 25 + m.Compassion + m.Courage + m.Group / 2 + m.CommunitySecurity + m.HumanLife / 2, NpcConflictActions.Defend, true);
            NpcIntentDefinition retaliation = RegisterMechanic(c, "Retaliation", "Retaliate for remembered harm", "retaliate", "Retaliated",
                m => m.Courage - m.Compassion - m.Law + m.Grievance / 2 - m.HumanLife - m.Patience / 2, NpcConflictActions.Retaliate, true);
            RegisterMechanic(c, "Intimidation", "Deter remembered misconduct", "intimidate", "ThreatCommunicated",
                m => m.Courage - m.Law - m.Compassion + m.Grievance / 3 - m.HumanLife / 3 - m.Patience / 2, NpcConflictActions.Threaten);
            RegisterMechanic(c, "RepairTrust", "Attempt to repair a relationship", "apologize", "ApologyCommunicated",
                m => 20 + m.Compassion + m.Law + m.Attachment / 2 + m.Patience / 2, NpcConflictActions.Apologize);
            RegisterMechanic(c, "GroupSecurity", "Exclude a dangerous companion", "expel_member", "MemberExpelled",
                m => 15 + m.Group + m.Law + m.Courage + m.Grievance / 3, NpcConflictActions.Expel);
            defense.OnTargetDeath = killed => new NpcIntentOutcome(NpcIntentStatus.Completed, "the known threat died");
            c.Capability(new NpcIntentDefinition(PolicePursuitId, "Pursue a reported armed suspect", PolicePursuitTurns, 360)
            { ResultFacts = defense.ResultFacts, DirectAction = defense.DirectAction,
              AssignedScore = defense.AssignedScore, AllowHostile = true, ActDuringDanger = true,
              AllowQuestions = true, OnTargetDeath = defense.OnTargetDeath });
            retaliation.OnTargetDeath = killed => new NpcIntentOutcome(killed ? NpcIntentStatus.Completed : NpcIntentStatus.Abandoned,
                killed ? "actually retaliated against the attacker" : "someone else ended the threat");
            RegisterEvents(c);
        }
        static NpcIntentDefinition RegisterMechanic(NpcCatalogBuilder c, string value, string description, string id, string fact,
            Func<NpcMotivation, int> importance, Func<NpcActionContext, djack.RogueSurvivor.Data.ActorAction> action, bool combat = false)
        {
            c.Fact(fact); c.Value(new NpcValueDefinition(value, description, null, importance));
            var capability = new NpcIntentDefinition(id, description, 180, 360) { ResultFacts = (catalog, g) => catalog.Facts.Mask(fact),
                DirectAction = action, AssignedScore = (catalog, owner, target) =>
                    importance(new NpcMotivation(catalog, owner, target)),
                AllowHostile = combat, ActDuringDanger = combat, AllowQuestions = true };
            c.Capability(capability);
            c.Operator(new NpcOperatorDefinition(id, null, context => action(new NpcActionContext(context))));
            c.OperatorSource(new NpcOperatorSource(id, catalog => catalog.Facts.Mask(fact), d => {
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                d.Add(id, d.Goal.LastKnown, d.Goal.TargetId, at, 0, d.Catalog.Facts.Mask(fact), default(NpcPlanningState), 1);
            }));
            return capability;
        }
        public void Evaluate(NpcGoalContext c, NpcGoalOffers offers)
        {
            Actor owner = c.Owner;
            foreach (NpcKnownPerson person in c.People)
            {
                if (person.Dead || person.Id == c.Self.Id) continue;
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                bool recent = c.Turn - person.ThreatTurn < 180;
                if (recent && person.Danger > 0 && opinion != null && opinion.Grievance >= 20)
                    offers.Add(person, "Retaliation", "retaliate", 0, 1, person.Danger, person.ThreatConfidence, person.ThreatCause);
                if (!person.Hostile && person.Violation > 0 && person.ViolationConfidence >= 60)
                    offers.Add(person, "Intimidation", "intimidate", 0, 1, person.Violation, person.ViolationConfidence, person.ViolationCause);
                if (owner.SocialGroup != null && owner.SocialGroup.LeaderId == c.Self.Id &&
                    owner.SocialGroup.Members.Contains(person.Id) && person.Violation >= 80 && person.ViolationConfidence >= 80)
                    offers.Add(person, "GroupSecurity", "expel_member", 0, 1, person.Violation, person.ViolationConfidence, person.ViolationCause);
            }
            foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
            {
                if (fact.Kind == "pursuit_announced" && fact.NamesSubject && fact.NamesOther &&
                    fact.Confidence >= 40 && c.Turn - fact.EventTurn < PolicePursuitTurns)
                {
                    bool target = c.Self.Id == fact.OtherId;
                    bool follower = owner.Leader != null && owner.Leader.PersonalityIdentity == fact.OtherId;
                    NpcKnownPerson pursuer = c.FindPerson(p => p.Id == fact.SubjectId);
                    if (pursuer != null && !pursuer.Dead)
                    {
                        string response = PursuitResponse(owner, target, follower);
                        bool near = pursuer.Place.Map == owner.Location.Map &&
                            NpcGoalContext.Distance(owner.Location, pursuer.Place) < 5;
                        if (response != null && (response != "avoid_reported_threat" || near))
                            offers.Add(pursuer, response == "avoid_reported_threat" ? "Safety" :
                                response == "retaliate" ? "Retaliation" : "Protection", response,
                                0, 1, 100, fact.Confidence, fact.EventId, fact.StoryId);
                    }
                }
                if (fact.Kind == "police_alert" && owner.Faction != null &&
                    owner.Faction.ID == (int)djack.RogueSurvivor.Gameplay.GameFactions.IDs.ThePolice &&
                    fact.NamesOther && fact.Confidence >= 40 && c.Turn - fact.EventTurn < PolicePursuitTurns)
                {
                    NpcKnownPerson suspect = c.FindPerson(p => p.Id == fact.OtherId);
                    if (suspect != null && !suspect.Dead)
                        offers.Add(suspect, "Protection", PolicePursuitId, 0, 1, 100, fact.Confidence,
                            fact.EventId, fact.StoryId, obligation: fact.EventId);
                }
                if (fact.Kind != "attack" || fact.Source == NpcKnowledgeSource.Told && (!fact.NamesSubject || !fact.NamesOther) ||
                    c.Turn - fact.EventTurn >= 180 || fact.SubjectId == c.Self.Id) continue;
                NpcKnownPerson victim = c.FindPerson(p => p.Id == fact.SubjectId), aggressor = c.FindPerson(p => p.Id == fact.OtherId);
                if (victim == null || victim.Dead || aggressor == null || aggressor.Dead) continue;
                RelationshipRecord attachment = owner.Personality.Person(victim.Id);
                bool close = attachment != null && attachment.Attachment >= 20 || victim.GroupId != Guid.Empty && owner.SocialGroup != null && victim.GroupId == owner.SocialGroup.Identity;
                if (close || owner.Faction != null && victim.FactionId == owner.Faction.ID && c.Catalog.FactionPolicy(owner.Faction.ID).Security > 0) offers.Add(aggressor, "Protection", "defend_person", 0, 1, 100, fact.Confidence, fact.EventId,
                    fact.StoryId, obligation: fact.EventId);
            }
        }

        internal static ActorAction AnnouncePursuit(RogueGame game, Actor cop, IList<Actor> visible = null)
        {
            if (!NpcIntentSystem.Enabled(cop) || cop.Faction == null ||
                cop.Faction.ID != (int)djack.RogueSurvivor.Gameplay.GameFactions.IDs.ThePolice ||
                PersonalitySystem.Bias(cop, NpcPersonalityAxis.Group) < 15 &&
                PersonalitySystem.Bias(cop, NpcPersonalityAxis.Law) < 20) return null;
            if (visible != null) foreach (Actor actor in visible)
                if (game.Rules.AreEnemies(cop, actor)) return null;
            NpcIntent goal = NpcIntentSystem.Select(game.NpcContent, cop);
            if (goal == null || goal.DefinitionId != PolicePursuitId || goal.CauseId == 0) return null;
            NpcFact alert = cop.Personality.Knowledge.Facts.Find(f => f.EventId == goal.CauseId &&
                f.Kind == "police_alert" && f.NamesOther && f.OtherId == goal.TargetId);
            NpcKnownPerson suspect = cop.Personality.Knowledge.Person(goal.TargetId);
            if (alert == null || suspect == null || suspect.Dead ||
                cop.Personality.Knowledge.Facts.Exists(f => f.Kind == "pursuit_announced" && f.CauseId == alert.EventId &&
                    f.SubjectId == cop.PersonalityIdentity)) return null;
            Actor listener = null;
            foreach (Actor actor in cop.Location.Map.Actors)
                if (actor != cop && !actor.IsDead && !actor.IsSleeping && actor.Model.Abilities.IsIntelligent &&
                    !actor.Model.Abilities.IsUndead && game.Rules.StdDistance(actor.Location.Position, cop.Location.Position) <= actor.AudioRange &&
                    (listener == null || game.Rules.StdDistance(actor.Location.Position, cop.Location.Position) <
                     game.Rules.StdDistance(listener.Location.Position, cop.Location.Position))) listener = actor;
            if (listener == null) return null;
            var context = new NpcActionContext(game, cop, goal);
            return context.Action(() => !listener.IsDead && !listener.IsSleeping && listener.Location.Map == cop.Location.Map &&
                game.Rules.StdDistance(listener.Location.Position, cop.Location.Position) <= listener.AudioRange &&
                !suspect.Dead && !cop.Personality.Knowledge.Facts.Exists(f => f.Kind == "pursuit_announced" && f.CauseId == alert.EventId &&
                    f.SubjectId == cop.PersonalityIdentity), () => {
                string speech = "I'm pursuing " + alert.ReportOther + ".";
                long speechId = game.DoSay(cop, listener, speech, RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR,
                    alert.EventId, goal.StoryId, deferNpcArchive: true);
                var report = new SignificantEvent("pursuit_announced", cop, null, cop.Location.Map,
                    cop.Location.Position, cop.Location.Map.LocalTime.TurnCounter, causeId: alert.EventId, storyId: goal.StoryId)
                    { Resource = alert.ReportOther };
                PersonalitySystem.Report(game, report);
                NpcFact fact = cop.Personality.Knowledge.Facts.Find(f => f.EventId == report.Id && f.Kind == report.Kind);
                if (fact == null) return;
                fact.OtherId = alert.OtherId; fact.OtherName = alert.OtherName;
                fact.OtherReportName = alert.OtherReportName; fact.OtherFactionId = alert.OtherFactionId;
                foreach (Actor hearer in cop.Location.Map.Actors)
                {
                    if (hearer == cop || hearer.IsDead || hearer.IsSleeping || !hearer.Model.Abilities.IsIntelligent ||
                        hearer.Model.Abilities.IsUndead || game.Rules.StdDistance(hearer.Location.Position, cop.Location.Position) > hearer.AudioRange) continue;
                    if (hearer.Personality == null) hearer.Personality = new PersonalityState();
                    bool learned = NpcKnowledgeSystem.Hear(game, hearer, cop, fact);
                    cop.Personality.Knowledge.Told(fact.EventId, hearer.PersonalityIdentity, report.Turn);
                    if (!hearer.IsPlayer && (learned || hearer == listener))
                        game.RecordHeardSpeech(cop, listener, hearer, speech, "heard_rumor", speechId,
                            report.Id, goal.StoryId);
                }
            });
        }

        internal static ActorAction AnnouncePursuitResponse(RogueGame game, Actor suspect, IList<Actor> visible)
        {
            if (!NpcIntentSystem.Enabled(suspect) || visible == null || !suspect.Personality.HasIntentState) return null;
            bool hasResponseGoal = false;
            foreach (NpcIntent intent in suspect.Personality.IntentList)
                if (!intent.Finished && (intent.DefinitionId == "avoid_reported_threat" ||
                    intent.DefinitionId == "retaliate")) { hasResponseGoal = true; break; }
            if (!hasResponseGoal) return null;
            NpcIntent goal = NpcIntentSystem.Select(game.NpcContent, suspect);
            if (goal == null || goal.DefinitionId != "avoid_reported_threat" && goal.DefinitionId != "retaliate" ||
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Group) < 15 &&
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Trade) < 15) return null;
            foreach (Actor actor in visible)
                if (game.Rules.AreEnemies(suspect, actor)) return null;
            NpcFact pursuit = suspect.Personality.Knowledge.Facts.Find(f => f.EventId == goal.CauseId &&
                f.Kind == "pursuit_announced" && f.NamesSubject && f.OtherId == suspect.PersonalityIdentity);
            if (pursuit == null || suspect.Personality.Knowledge.Facts.Exists(f =>
                f.Kind == "pursuit_response_announced" && f.CauseId == pursuit.EventId &&
                f.Resource == goal.DefinitionId)) return null;
            Actor listener = null;
            foreach (Actor actor in visible)
                if (actor != suspect && !actor.IsSleeping && actor.Model.Abilities.IsIntelligent &&
                    !actor.Model.Abilities.IsUndead && !game.Rules.AreEnemies(suspect, actor) &&
                    game.Rules.GridDistance(suspect.Location.Position, actor.Location.Position) <= 4 &&
                    NpcIntentSystem.CanSee(game, suspect, actor))
                { listener = actor; break; }
            if (listener == null) return null;
            var context = new NpcActionContext(game, suspect, goal);
            return context.Action(() => !listener.IsDead && !listener.IsSleeping &&
                listener.Location.Map == suspect.Location.Map &&
                game.Rules.GridDistance(suspect.Location.Position, listener.Location.Position) <= 4 &&
                !game.Rules.AreEnemies(suspect, listener) && NpcIntentSystem.CanSee(game, suspect, listener) &&
                !suspect.Personality.Knowledge.Facts.Exists(f => f.Kind == "pursuit_response_announced" &&
                    f.CauseId == pursuit.EventId && f.Resource == goal.DefinitionId), () => {
                var report = new SignificantEvent("pursuit_response_announced", suspect, null,
                    suspect.Location.Map, suspect.Location.Position, suspect.Location.Map.LocalTime.TurnCounter,
                    causeId: pursuit.EventId, storyId: pursuit.StoryId) { Resource = goal.DefinitionId };
                PersonalitySystem.Report(game, report);
                NpcFact fact = suspect.Personality.Knowledge.Facts.Find(f =>
                    f.EventId == report.Id && f.Kind == report.Kind);
                if (fact == null) return;
                fact.OtherId = pursuit.SubjectId; fact.OtherName = pursuit.SubjectName;
                fact.OtherReportName = pursuit.SubjectReportName; fact.OtherFactionId = pursuit.SubjectFactionId;
                NpcConversation.ShareRumor(game, suspect, listener, fact, false);
            });
        }

        static void HearPursuit(NpcReportContext c)
        {
            Actor listener = c.Listener; NpcFact fact = c.Fact;
            if (c.Improvement <= 0 || fact.Confidence < 40 || !fact.NamesSubject || !fact.NamesOther ||
                fact.SubjectId == listener.PersonalityIdentity ||
                fact.EventTurn + PolicePursuitTurns <= listener.Location.Map.LocalTime.TurnCounter) return;
            bool target = listener.PersonalityIdentity == fact.OtherId;
            bool follower = listener.Leader != null && listener.Leader.PersonalityIdentity == fact.OtherId;
            if (!target && !follower) return;
            NpcKnownPerson cop = listener.Personality.Knowledge.Person(fact.SubjectId);
            if (cop == null || cop.Dead || cop.Place.Map == null) return;
            string response = PursuitResponse(listener, target, follower);
            if (response == null) return;
            NpcStorySystem.StartKnown(c.Catalog, listener, cop, c.Catalog.Capability(response), fact.EventId, fact.StoryId);
        }

        static string PursuitResponse(Actor listener, bool target, bool follower)
        {
            if (!target && !follower) return null;
            int courage = PersonalitySystem.Bias(listener, NpcPersonalityAxis.Courage);
            if (courage <= -15) return "avoid_reported_threat";
            if (target && courage >= 15 && PersonalitySystem.Bias(listener, NpcPersonalityAxis.Law) < 0)
                return "retaliate";
            return follower && courage + PersonalitySystem.Bias(listener, NpcPersonalityAxis.Group) >= 20 ?
                "defend_person" : null;
        }
    }
}
