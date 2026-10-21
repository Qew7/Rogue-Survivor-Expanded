using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // An attempted robbery is a social encounter. Ownership changes only when
    // the robber actually picks up the item that the victim dropped.
    sealed class RobberyModule : INpcContentModule, INpcGoalSource
    {
        const string DemandFact = "robbery.demanded";
        const string DropFact = "robbery.dropped";
        const string TakeFact = "robbery.taken";
        const string PoliceFact = "robbery.police_contacted";
        public string Id { get { return "robbery"; } }

        public void Register(NpcCatalogBuilder c)
        {
            c.GoalSource(this);
            c.Fact(DemandFact); c.Fact(DropFact); c.Fact(TakeFact); c.Fact(PoliceFact);
            c.Value(new NpcValueDefinition("Robbery", "Demand a useful item by threat", null,
                m => 35 + m.Scarcity / 2 + m.Supplies / 2 + m.Courage + m.Grievance / 2 -
                    m.Law - m.Compassion - m.HumanLife / 3 - m.Fear / 2 - m.Attachment / 2, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            c.Value(new NpcValueDefinition("RobberyLoot", "Collect an item surrendered under threat", null,
                m => 100 + m.Supplies / 4, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            c.Value(new NpcValueDefinition("RobberyPolice", "Intervene in a reported robbery", null,
                m => 90 + m.Law / 2 + m.Courage / 4, true)
                { IdentitySuffix = g => ":" + g.ObligationId });
            c.Capability(new NpcIntentDefinition("robbery_demand", "Demand a carried item", 40, 240)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(DemandFact), AllowQuestions = true,
                  Assess = (game, actor, goal) => goal.Generated != null &&
                    Carried(Here(actor, goal.TargetId), goal.Generated.ItemId) == null ?
                    new NpcIntentOutcome(NpcIntentStatus.Abandoned, "the intended victim no longer carries the item") : null });
            c.Capability(new NpcIntentDefinition("robbery_surrender", "Drop the demanded item", 30, 0)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(DropFact), AssignedScore = (catalog, owner, target) => 120,
                  UrgentStory = true });
            c.Capability(new NpcIntentDefinition("robbery_collect", "Take the surrendered item", 40, 0)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(TakeFact),
                  Assess = (game, actor, goal) => goal.Generated != null &&
                    Ground(goal.Generated.ObjectPlace, goal.Generated.ItemId) == null ?
                    new NpcIntentOutcome(NpcIntentStatus.Abandoned, "the surrendered item is gone") : null });
            c.Capability(new NpcIntentDefinition("robbery_police_contact", "Stop a reported robbery", 60, 90)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(PoliceFact), AllowQuestions = true,
                  UrgentStory = true });
            Contact(c, DemandFact, "robbery.demand", Demand);
            Contact(c, DropFact, "robbery.surrender", Surrender);
            Contact(c, PoliceFact, "robbery.police", PoliceContact);
            c.OperatorSource(new NpcOperatorSource("robbery.collect", catalog => catalog.Facts.Mask(TakeFact), d => {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place); if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var done = d.Catalog.Facts.Mask(TakeFact);
                d.Add("robbery.collect", place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, done, done, default(NpcPlanningState), 1);
            }));
            c.Operator(new NpcOperatorDefinition("robbery.collect", null, Collect,
                archiveText: step => "pick up the surrendered item"));
            Events(c);
        }

        static void Contact(NpcCatalogBuilder c, string factName, string id,
            Func<NpcExecutionContext, ActorAction> action)
        {
            c.OperatorSource(new NpcOperatorSource(id, catalog => catalog.Facts.Mask(factName), d => {
                if (d.Goal.LastKnown.Map == null) return;
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                var done = d.Catalog.Facts.Mask(factName);
                d.Add(id, d.Goal.LastKnown, d.Goal.TargetId,
                    (NpcPlanningState)at, done, done, default(NpcPlanningState), 1);
            }));
            c.Operator(new NpcOperatorDefinition(id, null, action));
        }

        static Item Carried(Actor actor, Guid id)
        {
            if (actor == null || actor.Inventory == null) return null;
            foreach (Item item in actor.Inventory.Items)
                if (item.StoryIdentity == id) return item;
            return null;
        }

        static Item Ground(Location place, Guid id)
        {
            if (place.Map == null) return null;
            Inventory ground = place.Map.GetItemsAt(place.Position);
            if (ground == null) return null;
            foreach (Item item in ground.Items)
                if (item.StoryIdentity == id) return item;
            return null;
        }

        static bool PendingDemand(Actor victim, Guid itemId, int turn)
        {
            if (victim.Personality == null) return false;
            foreach (NpcFact demand in victim.Personality.Knowledge.Facts)
            {
                if (demand.Kind != "robbery_demanded" || demand.ItemId != itemId ||
                    turn - demand.EventTurn >= 30) continue;
                bool answered = victim.Personality.Knowledge.Facts.Exists(f => f.CauseId == demand.EventId &&
                    (f.Kind == "robbery_refused" || f.Kind == "robbery_fled" ||
                     f.Kind == "robbery_resisted" || f.Kind == "robbery_dropped"));
                if (!answered) return true;
            }
            return false;
        }

        internal static bool AvoidAutomaticPickup(Actor actor, Item item)
        {
            if (actor == null || actor.Personality == null || actor.Inventory == null || item == null || actor.Location.Map == null) return false;
            int turn = actor.Location.Map.LocalTime.TurnCounter;
            foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
                if (fact.Kind == "robbery_dropped" && fact.ItemId == item.StoryIdentity &&
                    turn - fact.EventTurn < 35 &&
                    (fact.OtherId != actor.PersonalityIdentity || !actor.Inventory.CanAddAll(item))) return true;
            return false;
        }

        internal static NpcFact SurrenderedTo(Actor actor, Item item, Location place)
        {
            if (actor == null || actor.Personality == null || item == null || place.Map == null) return null;
            int turn = place.Map.LocalTime.TurnCounter;
            return actor.Personality.Knowledge.Facts.Find(f => f.Kind == "robbery_dropped" &&
                f.OtherId == actor.PersonalityIdentity && f.ItemId == item.StoryIdentity &&
                f.Place == place && turn - f.EventTurn < 35);
        }

        internal static bool RecordTake(RogueGame game, Actor actor, Item item, NpcFact drop, int units)
        {
            if (drop == null || units <= 0) return false;
            Actor victim = Here(actor, drop.SubjectId);
            item.MarkStolen(victim == null || victim.SocialGroup == null ? drop.SubjectId : victim.SocialGroup.Identity,
                drop.SubjectId, drop.SubjectName, drop.StoryId, 0);
            var result = new SignificantEvent("robbery_taken", actor, victim,
                actor.Location.Map, actor.Location.Position, actor.Location.Map.LocalTime.TurnCounter,
                causeId: drop.EventId, storyId: drop.StoryId)
                { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = units,
                  ClaimantId = drop.SubjectId, ClaimantName = drop.SubjectName };
            PersonalitySystem.Report(game, result);
            item.RememberTheftEvent(result.Id);
            return true;
        }

        internal static ActorAction NearbyAction(RogueGame game, Actor actor)
        {
            if (!NpcIntentSystem.Enabled(actor) || actor.Inventory == null) return null;
            int turn = actor.Location.Map.LocalTime.TurnCounter;
            foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
            {
                if (fact.Kind != "robbery_dropped" || fact.OtherId != actor.PersonalityIdentity ||
                    fact.Place.Map != actor.Location.Map || turn - fact.EventTurn >= 35 ||
                    game.Rules.GridDistance(actor.Location.Position, fact.Place.Position) > 1) continue;
                Item item = Ground(fact.Place, fact.ItemId);
                if (item == null || !actor.Inventory.CanAddAll(item)) continue;
                var take = new ActionTakeItem(actor, game, fact.Place.Position, item);
                if (take.IsLegal()) return take;
            }
            return null;
        }

        static bool Desirable(RogueGame game, Actor robber, Item item)
        {
            if (item == null || item.IsEquipped || item.IsStolen || item.IsUnique) return false;
            if (item is ItemFood) return game.Rules.IsActorHungry(robber) ||
                !robber.Inventory.HasItemMatching(it => it is ItemFood);
            if (item is ItemMedicine) return robber.HitPoints < game.Rules.ActorMaxHPs(robber);
            if (item is ItemWeapon) return robber.GetEquippedWeapon() == null;
            return PersonalitySystem.Bias(robber, NpcPersonalityAxis.Item) >= 20 &&
                !(item is ItemTrap) && !(item is ItemNote);
        }

        static Item Wanted(RogueGame game, Actor robber, Actor victim)
        {
            if (victim.Inventory == null) return null;
            foreach (Item item in victim.Inventory.Items)
                if (Desirable(game, robber, item)) return item;
            return null;
        }

        static Actor Here(Actor owner, Guid id)
        {
            foreach (Actor actor in owner.Location.Map.Actors)
                if (actor.PersonalityIdentity == id && !actor.IsDead) return actor;
            return null;
        }

        static bool Suitable(RogueGame game, Actor robber, Actor victim)
        {
            if (victim == null || victim == robber || victim.IsDead || victim.IsSleeping ||
                victim.Model.Abilities.IsUndead || !victim.Model.Abilities.IsIntelligent ||
                game.Rules.AreEnemies(robber, victim) || !NpcIntentSystem.CanSee(game, robber, victim) ||
                robber.Leader == victim || victim.Leader == robber ||
                robber.SocialGroup != null && robber.SocialGroup == victim.SocialGroup) return false;
            if (robber.HitPoints * 3 < victim.HitPoints * 2) return false;
            if (victim.CountFollowers > robber.CountFollowers + 1) return false;
            return true;
        }

        public void Evaluate(NpcGoalContext c, NpcGoalOffers offers)
        {
            Actor owner = c.Owner;
            if (owner.IsPlayer || owner.Model.Abilities.IsUndead || !owner.Model.Abilities.IsIntelligent) return;
            if (owner.Inventory != null && !owner.Inventory.IsFull)
            foreach (NpcKnownPerson person in c.People)
            {
                if (person.Id == c.Self.Id || person.Dead || person.Place.Map != owner.Location.Map ||
                    c.Turn - person.SeenTurn > 1 || NpcGoalContext.Distance(owner.Location, person.Place) > 5) continue;
                Actor target = Here(owner, person.Id);
                if (!Suitable(c.Game, owner, target)) continue;
                Item item = Wanted(c.Game, owner, target);
                if (item == null) continue;
                if (PendingDemand(target, item.StoryIdentity, c.Turn)) continue;
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                int grievance = opinion == null ? 0 : opinion.Grievance;
                int fear = opinion == null ? 0 : opinion.Fear;
                int attachment = opinion == null ? 0 : opinion.Attachment;
                int urge = 35 + c.Scarcity / 2 + c.SuppliesBias / 2 + c.CourageBias + grievance / 2 -
                    c.LawBias - c.CompassionBias - c.HumanLife / 3 - fear / 2 - attachment / 2;
                if (urge < 75) continue;
                bool pending = false;
                foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
                    if (fact.Kind == "robbery_demanded" && fact.ItemId == item.StoryIdentity &&
                        fact.OtherId == person.Id && c.Turn - fact.EventTurn < 30) { pending = true; break; }
                if (!pending)
                    offers.Add(person, "Robbery", "robbery_demand", 0, 1, 100, 100,
                        itemId: item.StoryIdentity, resource: item.Model.SingleName);
            }
            var offeredSuspects = new HashSet<Guid>();
            foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
            {
                if (fact.Kind == "robbery_dropped" && fact.OtherId == owner.PersonalityIdentity &&
                    c.Turn - fact.EventTurn < 35 && Ground(fact.Place, fact.ItemId) != null)
                    offers.Add(c.Self, "RobberyLoot", "robbery_collect", 0, 1, 100, 100,
                        fact.EventId, fact.StoryId, itemId: fact.ItemId, objectPlace: fact.Place, self: true);
                bool offenderIsOther = fact.Kind == "robbery_resisted" || fact.Kind == "robbery_fled" ||
                    fact.Kind == "robbery_refused" || fact.Kind == "robbery_dropped";
                Guid offenderId = offenderIsOther ? fact.OtherId : fact.SubjectId;
                if (owner.Faction == null || owner.Faction.ID != (int)GameFactions.IDs.ThePolice ||
                    (fact.Kind != "robbery_demanded" && fact.Kind != "robbery_taken" &&
                     fact.Kind != "robbery_resisted" && fact.Kind != "robbery_fled" &&
                     fact.Kind != "robbery_refused" && fact.Kind != "robbery_dropped") ||
                    !(offenderIsOther ? fact.NamesOther : fact.NamesSubject) || offenderId == owner.PersonalityIdentity ||
                    fact.Confidence < 50 || c.Turn - fact.EventTurn > 180) continue;
                NpcKnownPerson suspect = c.FindPerson(p => p.Id == offenderId);
                if (suspect == null || suspect.Dead || suspect.Place.Map == null) continue;
                bool handled = false;
                foreach (NpcFact known in owner.Personality.Knowledge.Facts)
                    if (known.Kind == "robbery_police_confronted" &&
                        known.SubjectId == owner.PersonalityIdentity && known.OtherId == offenderId &&
                        (known.CauseId == fact.EventId || c.Turn - known.EventTurn < 30))
                    { handled = true; break; }
                if (!handled && offeredSuspects.Add(offenderId))
                    offers.Add(suspect, "RobberyPolice", "robbery_police_contact", 0, 1, 100,
                        fact.Confidence, fact.EventId, fact.StoryId, obligation: fact.EventId);
            }
        }

        static ActorAction Demand(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            Item item = c.Goal.Generated == null ? null : Carried(c.Target, c.Goal.Generated.ItemId);
            if (item == null || !c.NearPerson(c.Goal.TargetId) ||
                PendingDemand(c.Target, item.StoryIdentity, c.Owner.Location.Map.LocalTime.TurnCounter)) return null;
            string speech = "Drop your " + item.Model.SingleName + " and step away.";
            int turn = c.Owner.Location.Map.LocalTime.TurnCounter;
            if (!c.Owner.Personality.Knowledge.CanRequest(c.Goal.Sequence, c.Target.PersonalityIdentity, speech, turn)) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) && Carried(c.Target, item.StoryIdentity) == item &&
                !PendingDemand(c.Target, item.StoryIdentity, c.Owner.Location.Map.LocalTime.TurnCounter) &&
                c.Owner.Personality.Knowledge.CanRequest(c.Goal.Sequence, c.Target.PersonalityIdentity, speech,
                    c.Owner.Location.Map.LocalTime.TurnCounter), () => {
                c.Game.DoSay(c.Owner, c.Target, speech, RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                    c.Goal.CauseId, c.Goal.StoryId);
                c.Owner.Personality.Knowledge.Requested(c.Goal.Sequence, c.Target.PersonalityIdentity, speech,
                    c.Owner.Location.Map.LocalTime.TurnCounter);
                PersonalitySystem.Report(c.Game, new SignificantEvent("robbery_demanded", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: c.Goal.CauseId, storyId: c.Goal.StoryId)
                    { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
                c.Done(c.Game.NpcContent.Facts.Mask(DemandFact));
            });
        }

        static NpcFact DemandFor(Actor actor, long responseId)
        {
            NpcFact response = actor.Personality.Knowledge.Facts.Find(f => f.EventId == responseId && f.Kind == "robbery_accepted");
            return response == null ? null : actor.Personality.Knowledge.Facts.Find(f =>
                f.EventId == response.CauseId && f.Kind == "robbery_demanded");
        }

        static ActorAction Surrender(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            NpcFact demand = DemandFor(c.Owner, c.Goal.CauseId);
            Item item = demand == null ? null : Carried(c.Owner, demand.ItemId);
            if (item == null || !c.NearPerson(c.Goal.TargetId)) return null;
            StorageContainer storage = c.Owner.Location.Map.GetMapObjectAt(c.Owner.Location.Position) as StorageContainer;
            if (storage != null && storage.IsOpen)
            {
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Point next = new Point(c.Owner.Location.Position.X + dx, c.Owner.Location.Position.Y + dy);
                    if (!c.Owner.Location.Map.IsInBounds(next) ||
                        c.Owner.Location.Map.GetMapObjectAt(next) is StorageContainer) continue;
                    var move = new ActionMoveStep(c.Owner, c.Game, next);
                    if (move.IsLegal()) return move;
                }
                return null;
            }
            var drop = new ActionDropItem(c.Owner, c.Game, item);
            if (!drop.IsLegal()) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) && Carried(c.Owner, item.StoryIdentity) == item &&
                drop.IsLegal(), () => {
                drop.Perform();
                if (Ground(c.Owner.Location, item.StoryIdentity) == null) return;
                NpcFact accepted = c.Owner.Personality.Knowledge.Facts.Find(f => f.EventId == c.Goal.CauseId);
                PersonalitySystem.Report(c.Game, new SignificantEvent("robbery_dropped", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: accepted == null ? demand.EventId : accepted.EventId, storyId: c.Goal.StoryId)
                    { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
                c.Done(c.Game.NpcContent.Facts.Mask(DropFact));
            });
        }

        static ActorAction Collect(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            if (c.Goal.Generated == null) return null;
            Location place = c.Goal.Generated.ObjectPlace;
            Item item = Ground(place, c.Goal.Generated.ItemId);
            if (item == null || c.Owner.Location != place || !c.Owner.Inventory.CanAddAll(item)) return null;
            var take = new ActionTakeItem(c.Owner, c.Game, place.Position, item);
            if (!take.IsLegal()) return null;
            return c.Action(() => c.Owner.Location == place && Ground(place, item.StoryIdentity) == item &&
                c.Owner.Inventory.CanAddAll(item) && take.IsLegal(), () => {
                c.Game.DoTakeItem(c.Owner, place.Position, item, noticeTheft: false);
                if (c.Owner.Inventory.Contains(item)) c.Done(c.Game.NpcContent.Facts.Mask(TakeFact));
            });
        }

        static ActorAction PoliceContact(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            NpcFact report = c.Goal.Generated == null ? null : c.Owner.Personality.Knowledge.Facts.Find(f =>
                f.EventId == c.Goal.Generated.ObligationId);
            if (report == null || !c.NearPerson(c.Goal.TargetId)) return null;
            string speech = "I have a report that you threatened someone for their property. Stop and explain yourself.";
            if (!c.Owner.Personality.Knowledge.CanRequest(c.Goal.Sequence, c.Target.PersonalityIdentity,
                speech, c.Owner.Location.Map.LocalTime.TurnCounter)) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) && c.Owner.Personality.Knowledge.CanRequest(
                c.Goal.Sequence, c.Target.PersonalityIdentity, speech, c.Owner.Location.Map.LocalTime.TurnCounter), () => {
                c.Game.DoSay(c.Owner, c.Target, speech, RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                    report.EventId, report.StoryId);
                c.Owner.Personality.Knowledge.Requested(c.Goal.Sequence, c.Target.PersonalityIdentity,
                    speech, c.Owner.Location.Map.LocalTime.TurnCounter);
                PersonalitySystem.Report(c.Game, new SignificantEvent("robbery_police_confronted", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: report.EventId, storyId: report.StoryId) { ItemId = report.ItemId });
                c.Done(c.Game.NpcContent.Facts.Mask(PoliceFact));
            });
        }

        static void Events(NpcCatalogBuilder c)
        {
            c.Event(new NpcEventDefinition("robbery_demanded", NpcRecordCategory.Combat | NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " demanded property from " + (e.Other ?? "someone") + " by threat.",
                f => (f.ReportSubject ?? "Someone") + " threatened " + (f.ReportOther ?? "someone") + " for " + (f.Resource ?? "an item"))
                { PlayerReply = new NpcPlayerReply("drop the demanded item", "robbery_accepted",
                    "robbery_refused", "I'll drop it.", "No.", directOnly: true,
                    yesReply: "Drop it and step away.", noReply: "Then stay away from me."),
                  SelfReportTone = NpcSelfReportTone.Harmful });
            Event(c, "robbery_accepted", NpcRecordCategory.Encounters,
                e => (e.Subject ?? "Someone") + " agreed to surrender property under threat.",
                f => (f.ReportSubject ?? "Someone") + " agreed to surrender property to " + (f.ReportOther ?? "someone"));
            Event(c, "robbery_refused", NpcRecordCategory.Encounters,
                e => (e.Subject ?? "Someone") + " refused a robber's demand.",
                f => (f.ReportSubject ?? "Someone") + " refused " + (f.ReportOther ?? "someone") + "'s demand");
            Event(c, "robbery_fled", NpcRecordCategory.Combat,
                e => (e.Subject ?? "Someone") + " fled a robbery threat.",
                f => (f.ReportSubject ?? "Someone") + " fled from " + (f.ReportOther ?? "someone") + "'s threat");
            Event(c, "robbery_resisted", NpcRecordCategory.Combat,
                e => (e.Subject ?? "Someone") + " fought back against a robbery threat.",
                f => (f.ReportSubject ?? "Someone") + " fought back against " + (f.ReportOther ?? "someone") + "'s threat");
            Event(c, "robbery_dropped", NpcRecordCategory.Encounters,
                e => (e.Subject ?? "Someone") + " dropped property under threat.",
                f => (f.ReportSubject ?? "Someone") + " dropped " + (f.Resource ?? "an item") + " under " + (f.ReportOther ?? "someone") + "'s threat");
            Event(c, "robbery_taken", NpcRecordCategory.Encounters,
                e => (e.Subject ?? "Someone") + " took property surrendered by " + (e.Other ?? "someone") + ".",
                f => (f.ReportSubject ?? "Someone") + " robbed " + (f.ReportOther ?? f.ClaimantName ?? "someone") + " of " + (f.Resource ?? "an item"));
            Event(c, "robbery_police_confronted", NpcRecordCategory.Encounters,
                e => (e.Subject ?? "An officer") + " confronted " + (e.Other ?? "someone") + " about a robbery.",
                f => (f.ReportSubject ?? "An officer") + " confronted " + (f.ReportOther ?? "someone") + " about a robbery");
            c.On("robbery_demanded", NpcObservationPhase.Replies, VictimReply);
            c.On("robbery_demanded", NpcObservationPhase.Knowledge, Witness);
            c.On("robbery_demanded", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_accepted", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_refused", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_fled", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_resisted", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_dropped", NpcObservationPhase.Relationships, Observe);
            c.On("robbery_taken", NpcObservationPhase.Relationships, Observe);
            c.AfterEvent("robbery_accepted", StartSurrender);
            c.AfterEvent("robbery_dropped", StartRetreat);
            c.AfterEvent("robbery_fled", StartRetreat);
            foreach (string kind in new[] { "robbery_demanded", "robbery_accepted", "robbery_refused", "robbery_fled",
                "robbery_resisted", "robbery_dropped", "robbery_taken" })
                c.OnReport(kind, Hear);
            c.Memory(new MemoryDefinition("robbed_under_threat", "Someone threatened me for my property", 2, 2,
                new[] { new MemoryTrigger("robbery_demanded", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -12), false);
        }

        static void Event(NpcCatalogBuilder c, string kind, NpcRecordCategory category,
            Func<ObservedEvent, string> description, Func<NpcFact, string> report)
        { c.Event(new NpcEventDefinition(kind, category, true, description, report)
            { ReportPriority = kind == "robbery_taken" ? 5 : 0,
              SelfReportTone = kind == "robbery_taken" ? NpcSelfReportTone.Harmful : NpcSelfReportTone.Neutral }); }

        static void VictimReply(NpcObservation o)
        {
            Actor victim = o.Owner, robber = o.Source.Subject;
            if (victim != o.Source.Other || robber == null || !NpcIntentSystem.Enabled(victim) ||
                victim.Personality.Reactions.Count >= 4) return;
            Item item = Carried(victim, o.Source.ItemId);
            int courage = PersonalitySystem.Bias(victim, NpcPersonalityAxis.Courage);
            int patience = PersonalitySystem.Disposition(victim, NpcPersonalityAxis.Patience, o.Game.NpcContent);
            RelationshipRecord opinion = victim.Personality.Person(robber.PersonalityIdentity);
            int fear = opinion == null ? 0 : opinion.Fear;
            int threat = Math.Max(0, 30 * robber.CurrentMeleeAttack.DamageValue / Math.Max(1, victim.HitPoints));
            int resolve = courage + patience / 3 + victim.CountFollowers * 15 - fear - threat -
                (victim.HitPoints < o.Game.Rules.ActorMaxHPs(victim) / 2 ? 20 : 0);
            string kind, words;
            bool attack = false;
            if (item == null) { kind = "robbery_refused"; words = "I no longer have it."; }
            else if (resolve <= -15) { kind = "robbery_accepted"; words = "I'll drop it. Please don't hurt me."; }
            else if (resolve >= 35 && o.Game.Rules.CanActorMeleeAttack(victim, robber))
            { kind = "robbery_resisted"; words = "You won't take it!"; attack = true; }
            else if (resolve < 10) { kind = "robbery_fled"; words = "Stay away from me!"; }
            else { kind = "robbery_refused"; words = "No. Leave me alone."; }
            victim.Personality.Reactions.Add(new NpcReaction(robber, words, o.Source.Id, o.Source.Turn,
                kind, o.Source.StoryId, attackTarget: attack));
        }

        static void StartSurrender(RogueGame game, SignificantEvent accepted)
        {
            Actor victim = accepted.Subject, robber = accepted.Other;
            if (victim == null || robber == null) return;
            NpcFact demand = victim.Personality.Knowledge.Facts.Find(f =>
                f.EventId == accepted.CauseId && f.Kind == "robbery_demanded");
            if (demand == null) return;
            if (victim.IsPlayer)
            {
                Item item = Carried(victim, demand.ItemId);
                if (item == null || item.IsEquipped || !game.Rules.CanActorDropItem(victim, item) ||
                    victim.Location.Map.GetMapObjectAt(victim.Location.Position) is StorageContainer) return;
                game.DoDropItem(victim, item);
                if (Ground(victim.Location, item.StoryIdentity) != null)
                    PersonalitySystem.Report(game, new SignificantEvent("robbery_dropped", victim, robber,
                        victim.Location.Map, victim.Location.Position, victim.Location.Map.LocalTime.TurnCounter,
                        causeId: accepted.Id, storyId: accepted.StoryId)
                        { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
                return;
            }
            if (!NpcIntentSystem.Enabled(victim)) return;
            NpcKnownPerson known = victim.Personality.Knowledge.Person(robber.PersonalityIdentity);
            if (known == null || known.Place.Map == null)
                known = new NpcKnownPerson { Id = robber.PersonalityIdentity, Name = robber.UnmodifiedName,
                    Place = robber.Location, SeenTurn = accepted.Turn };
            NpcStory story = accepted.StoryId == null ? null : Session.Get.NpcDirector.Find(accepted.StoryId);
            NpcStorySystem.StartKnown(game.NpcContent, victim, known,
                game.NpcContent.Capability("robbery_surrender"), accepted.Id,
                story != null && !story.Finished ? accepted.StoryId : null);
        }

        static void StartRetreat(RogueGame game, SignificantEvent source)
        {
            Actor victim = source.Subject, robber = source.Other;
            if (victim == null || robber == null || !NpcIntentSystem.Enabled(victim)) return;
            NpcKnownPerson known = victim.Personality.Knowledge.Person(robber.PersonalityIdentity);
            if (known == null || known.Place.Map == null) return;
            known.Danger = 100; known.ThreatTurn = source.Turn;
            known.ThreatConfidence = 100; known.ThreatCause = source.Id;
            NpcStorySystem.StartKnown(game.NpcContent, victim, known,
                game.NpcContent.Capability("avoid_reported_threat"), source.Id, source.StoryId);
        }

        static void Witness(NpcObservation o)
        {
            Actor robber = o.Source.Subject;
            if (robber == null || o.Owner == robber) return;
            NpcKnownPerson known = o.Owner.Personality.Knowledge.Person(robber.PersonalityIdentity);
            if (known == null) return;
            known.Violation = 100; known.ViolationTurn = o.Source.Turn;
            known.ViolationConfidence = o.Direct ? 100 : 70; known.ViolationCause = o.Source.Id;
            if (o.Owner == o.Source.Other)
            {
                known.Danger = 100; known.ThreatTurn = o.Source.Turn;
                known.ThreatConfidence = 100; known.ThreatCause = o.Source.Id;
            }
            o.Owner.Personality.Knowledge.Revision++;
        }

        static void Observe(NpcObservation o)
        {
            Actor robber = o.Source.Kind == "robbery_demanded" || o.Source.Kind == "robbery_taken" ?
                o.Source.Subject : o.Source.Other;
            Actor victim = robber == o.Source.Subject ? o.Source.Other : o.Source.Subject;
            if (robber == null || o.Owner == robber) return;
            int law = PersonalitySystem.Bias(o.Owner, NpcPersonalityAxis.Law);
            int compassion = PersonalitySystem.Bias(o.Owner, NpcPersonalityAxis.Compassion);
            bool close = victim == o.Owner || victim != null && o.Owner.Personality.Person(victim.PersonalityIdentity) != null &&
                o.Owner.Personality.Person(victim.PersonalityIdentity).Attachment >= 20;
            int judgment = law + compassion / 2 + (close ? 35 : 0);
            int amount = o.Source.Kind == "robbery_taken" ? 12 :
                o.Source.Kind == "robbery_demanded" ? 8 : 3;
            if (judgment >= 0)
                o.Owner.Personality.Opinion(robber.PersonalityIdentity, robber.UnmodifiedName)
                    .AdjustSocial(trust: -amount, grievance: amount, fear: o.Owner == victim ? 10 : 0);
            else if (o.Owner != victim && o.Source.Kind == "robbery_resisted")
                o.Owner.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName)
                    .AdjustSocial(trust: -2);
            if (victim != null && o.Owner != victim && compassion > 0 &&
                (o.Source.Kind == "robbery_accepted" || o.Source.Kind == "robbery_fled" || o.Source.Kind == "robbery_resisted" ||
                 o.Source.Kind == "robbery_dropped"))
                o.Owner.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName)
                    .AdjustSocial(attachment: 2);
        }

        static void Hear(NpcReportContext c)
        {
            NpcFact f = c.Fact;
            if (f.Kind == "robbery_demanded" || f.Kind == "robbery_taken")
                NpcReputation.Reputation(c, false, false, true);
            if (c.Improvement <= 0 || !f.NamesOther || !f.NamesSubject) return;
            Guid robberId = f.Kind == "robbery_demanded" || f.Kind == "robbery_taken" ? f.SubjectId : f.OtherId;
            string robberName = f.Kind == "robbery_demanded" || f.Kind == "robbery_taken" ? f.ReportSubject : f.ReportOther;
            Guid victimId = robberId == f.SubjectId ? f.OtherId : f.SubjectId;
            string victimName = robberId == f.SubjectId ? f.ReportOther : f.ReportSubject;
            int law = PersonalitySystem.Bias(c.Listener, NpcPersonalityAxis.Law);
            int compassion = PersonalitySystem.Bias(c.Listener, NpcPersonalityAxis.Compassion);
            RelationshipRecord victim = c.Listener.Personality.Person(victimId);
            int judgment = law + compassion / 2 + (victim == null ? 0 : victim.Attachment / 2);
            if (robberId != c.Listener.PersonalityIdentity && judgment > 0 &&
                f.Kind != "robbery_demanded" && f.Kind != "robbery_taken")
                c.Listener.Personality.Opinion(robberId, robberName).AdjustSocial(
                    trust: -Math.Max(1, c.Improvement / 30), grievance: Math.Max(1, c.Improvement / 35));
            if (victimId != c.Listener.PersonalityIdentity && compassion > 0 &&
                (f.Kind == "robbery_accepted" || f.Kind == "robbery_fled" || f.Kind == "robbery_resisted" || f.Kind == "robbery_dropped"))
                c.Listener.Personality.Opinion(victimId, victimName).AdjustSocial(attachment: Math.Max(1, c.Improvement / 50));
        }
    }
}
