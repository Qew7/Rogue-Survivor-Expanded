using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class CacheEncounters
    {
        const string DoneFact = "cache.encounter_done";
        const string Operator = "cache.encounter";

        static string Key(string role, long eventId, Guid cacheId, Guid target)
        { return role + ":" + eventId + ":" + cacheId.ToString("N") + ":" + target.ToString("N"); }

        public static void Register(NpcCatalogBuilder catalog)
        {
            Event(catalog, "cache_search_announced", " announced a search for a cache.",
                f => (f.ReportSubject ?? "Someone") + " said they were looking for a cache", NpcRecordCategory.Intentions);
            Event(catalog, "cache_return_requested", " asked for stolen supplies back.",
                f => (f.ReportSubject ?? "Someone") + " asked for cache supplies to be returned", NpcRecordCategory.Encounters);
            Event(catalog, "cache_returned", " returned stolen cache supplies.",
                f => (f.ReportSubject ?? "Someone") + " returned supplies taken from a cache", NpcRecordCategory.Help);
            Event(catalog, "cache_apologized", " apologized after taking from a cache.",
                f => (f.ReportSubject ?? "Someone") + " apologized for taking cache supplies", NpcRecordCategory.Encounters);
            Event(catalog, "cache_justified", " explained taking cache supplies.",
                f => (f.ReportSubject ?? "Someone") + " offered an explanation for taking cache supplies", NpcRecordCategory.Encounters);
            Event(catalog, "cache_threatened", " threatened someone at a cache.",
                f => (f.ReportSubject ?? "Someone") + " threatened another person at a cache", NpcRecordCategory.Combat);
            Event(catalog, "cache_withdrew", " left a confrontation at a cache.",
                f => (f.ReportSubject ?? "Someone") + " withdrew from a cache confrontation", NpcRecordCategory.Encounters);
            Event(catalog, "cache_loss_asked", " asked about missing cache supplies.",
                f => (f.ReportSubject ?? "Someone") + " asked about missing cache supplies", NpcRecordCategory.Encounters);
            Event(catalog, "cache_owner_warned", " warned an owner about a stated cache search.",
                f => (f.ReportSubject ?? "Someone") + " warned a cache owner of an announced search", NpcRecordCategory.Encounters);
            Event(catalog, "cache_account_asked", " asked someone about a reported cache theft.",
                f => (f.ReportSubject ?? "Someone") + " asked about a reported cache theft", NpcRecordCategory.Encounters);
            catalog.Fact(DoneFact);
            catalog.Value(new NpcValueDefinition("CacheEncounter", "Respond to a cache encounter", null,
                m => 42 + Math.Abs(m.Law) / 3 + Math.Abs(m.Courage) / 4 +
                    m.CareCircle / 4 + m.Scarcity / 3 + m.Feeling / 5, true)
                { IdentitySuffix = g => ":" + g.Resource + ":" + g.ObligationId + ":" + g.ItemId.ToString("N") });
            catalog.Value(new NpcValueDefinition("CacheAnnouncement", "Tell someone about a planned cache search", null,
                m => 35 + m.Group / 2 + m.MessageTrust / 4 - m.Scarcity / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            catalog.Value(new NpcValueDefinition("CacheWarning", "Warn a cache owner about a stated search", null,
                m => 42 + m.Law / 2 + m.CareCircle / 3, true)
                { IdentitySuffix = g => ":" + g.ObligationId });
            var intent = new NpcIntentDefinition("respond_at_cache", "Respond to a cache encounter", 25, 120);
            intent.ResultFacts = (c, g) => c.Facts.Mask(DoneFact);
            intent.Assess = (g, a, i) => a.Personality.Knowledge.HasHandledCacheEncounter(
                Key(i.Generated.Resource, i.Generated.ObligationId, i.Generated.ItemId, i.TargetId)) ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "responded at the cache") : null;
            catalog.Capability(intent);
            catalog.OperatorSource(new NpcOperatorSource("cache.encounter.contact", c => c.Facts.Mask(DoneFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.LastKnown.Map == null) return;
                Location place = d.Goal.LastKnown;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Goal.TargetId, at);
                var fact = d.Catalog.Facts.Mask(DoneFact);
                d.Add(Operator, place, d.Goal.TargetId, (NpcPlanningState)at,
                    fact, fact, default(NpcPlanningState), 1);
            }));
            catalog.Operator(new NpcOperatorDefinition(Operator, null, Act,
                archiveText: step => "respond to someone at a cache"));
            catalog.OnReport("cache_search_announced", HearSearch);
            catalog.On("cache_threatened", NpcObservationPhase.Knowledge, o => {
                if (o.Owner != o.Source.Other || !o.SeesSubject) return;
                NpcKnownPerson person = o.Owner.Personality.Knowledge.Person(o.Source.Subject.PersonalityIdentity);
                if (person == null) return;
                person.Danger = 100; person.ThreatTurn = o.Source.Turn;
                person.ThreatConfidence = 100; person.ThreatCause = o.Source.Id;
                o.Owner.Personality.Opinion(person.Id, person.Name).AdjustSocial(fear: 15);
            });
            catalog.On("cache_returned", NpcObservationPhase.Relationships, o => {
                if (o.Owner == o.Source.Other && o.Source.Subject != null)
                    o.Owner.Personality.Opinion(o.Source.Subject.PersonalityIdentity,
                        o.Source.Subject.UnmodifiedName).AdjustSocial(trust: 10, grievance: -15);
            });
            catalog.On("cache_apologized", NpcObservationPhase.Relationships, o => {
                if (o.Owner == o.Source.Other && o.Source.Subject != null)
                    o.Owner.Personality.Opinion(o.Source.Subject.PersonalityIdentity,
                        o.Source.Subject.UnmodifiedName).AdjustSocial(grievance: -5);
            });
        }

        static void Event(NpcCatalogBuilder catalog, string kind, string action,
            Func<NpcFact, string> report, NpcRecordCategory category)
        {
            catalog.Event(new NpcEventDefinition(kind, category, true,
                e => (e.Subject ?? "Someone") + action, report)
            { ReportActorRole = NpcReportActorRole.Subject });
        }

        static void HearSearch(NpcReportContext report)
        {
            // A declaration is evidence of speech, never evidence of a theft.
            if (report.Improvement <= 0 || report.Fact.Kind != "cache_search_announced") return;
            report.Listener.Personality.Knowledge.Revision++;
        }

        static void Offer(NpcGoalContext context, NpcGoalOffers offers, NpcKnownPerson target,
            string role, NpcFact evidence, Guid cacheId, int desire)
        {
            if (target == null || target.Dead || target.Place.Map != context.Owner.Location.Map ||
                NpcGoalContext.Distance(context.Owner.Location, target.Place) > 2 ||
                context.Owner.Personality.Knowledge.HasHandledCacheEncounter(
                    Key(role, evidence == null ? 0 : evidence.EventId, cacheId, target.Id))) return;
            offers.Add(target, role == "announce" ? "CacheAnnouncement" :
                role == "warn" ? "CacheWarning" : "CacheEncounter",
                "respond_at_cache", 0, 1, desire,
                evidence == null ? 70 : role == "ask" ? 100 : evidence.Confidence,
                cause: evidence == null ? 0 : evidence.EventId,
                story: evidence == null ? null : evidence.StoryId,
                resource: role, obligation: evidence == null ? 0 : evidence.EventId,
                itemId: cacheId);
        }

        public static void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Guid self = context.Self.Id;
            foreach (NpcFact fact in context.Owner.Personality.Knowledge.Facts)
            {
                if (fact.EventId <= 0 || fact.Place.Map != context.Owner.Location.Map ||
                    context.Turn - fact.EventTurn > 12) continue;
                // Base theft already offers restitution and boundary goals through
                // Justice and ConflictResolution; a second encounter goal would
                // preempt those choices for the same physical act.
                if (fact.Kind == "cache_theft")
                {
                    if (NpcGoalContext.Distance(context.Owner.Location, fact.Place) > 2) continue;
                    if (fact.OtherId == self && fact.Source != NpcKnowledgeSource.Told)
                        Offer(context, offers, context.FindPerson(p => p.Id == fact.SubjectId),
                            "victim", fact, fact.ItemId, 100);
                    if (fact.SubjectId == self && fact.Source == NpcKnowledgeSource.Participant)
                        Offer(context, offers, context.FindPerson(p => p.Id == fact.OtherId),
                            "thief", fact, fact.ItemId, 80);
                    if (fact.NamesSubject && fact.SubjectId != self && fact.OtherId != self &&
                        context.LawBias > 5)
                        Offer(context, offers, context.FindPerson(p => p.FactionId ==
                            (int)GameFactions.IDs.ThePolice), "police", fact, fact.ItemId,
                            Math.Min(100, 60 + context.LawBias / 2));
                    if (fact.Source == NpcKnowledgeSource.Told && fact.NamesSubject &&
                        fact.SubjectId != self && fact.Confidence >= 40 && fact.Confidence < 80)
                        Offer(context, offers, context.FindPerson(p => p.Id == fact.SubjectId),
                            "ask", fact, fact.ItemId, 80);
                }
                else if (fact.Kind == "cache_return_requested" && fact.OtherId == self)
                    Offer(context, offers, context.FindPerson(p => p.Id == fact.SubjectId),
                        "answer", fact, fact.ItemId, 95);
                else if (fact.Kind == "cache_loss_discovered" && fact.SubjectId == self)
                    Offer(context, offers, context.FindPerson(p => p.Id != self && !p.Dead &&
                        p.Place.Map == context.Owner.Location.Map &&
                        context.Owner.Personality.Person(p.Id) != null &&
                        context.Owner.Personality.Person(p.Id).Trust >= 0),
                        "loss", fact, fact.ItemId, 70);
                else if (fact.Kind == "cache_search_announced" && fact.ClaimantId != Guid.Empty &&
                    fact.ClaimantId != self && context.LawBias + context.CareCircle > 10)
                    Offer(context, offers, context.FindPerson(p => p.Id == fact.ClaimantId),
                        "warn", fact, fact.ItemId, 90);
            }
            if (!Session.Get.GamePreset.CachesEnabled) return;
            foreach (NpcKnownCache cache in context.Owner.Personality.Knowledge.Caches)
            {
                if (cache.Source != NpcKnowledgeSource.Told || cache.Units <= 0 ||
                    cache.Place.Map != context.Owner.Location.Map || cache.ClaimantId == self) continue;
                NpcKnownPerson listener = context.FindPerson(p => p.Id != self && !p.Dead &&
                    p.Place.Map == context.Owner.Location.Map &&
                    (p.GroupId == context.Self.GroupId && p.GroupId != Guid.Empty ||
                     context.Owner.Personality.Person(p.Id) != null &&
                     context.Owner.Personality.Person(p.Id).Trust >= 20));
                Offer(context, offers, listener, "announce", null, cache.Id,
                    Math.Max(10, 80 + context.GroupBias / 2 - context.Scarcity / 3));
                break;
            }
        }

        static Item StolenItem(Actor thief, long theftId)
        {
            foreach (Item item in thief.Inventory.Items)
                if (item.IsStolen && item.TheftEventId == theftId && !item.IsEquipped)
                    return item;
            return null;
        }

        static ActorAction Act(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            string role = context.Goal.Generated.Resource;
            Actor owner = context.Owner, target = context.Target;
            if (!context.NearPerson(context.Goal.TargetId)) return null;
            long eventId = context.Goal.Generated.ObligationId;
            NpcFact fact = eventId == 0 ? null : owner.Personality.Knowledge.Facts.Find(f => f.EventId == eventId);
            if (eventId > 0 && fact == null) return null;
            Guid cacheId = context.Goal.Generated.ItemId;
            string key = Key(role, eventId, cacheId, target.PersonalityIdentity);
            if (owner.Personality.Knowledge.HasHandledCacheEncounter(key)) return null;
            if (role == "police")
                return context.Action(() => context.NearPerson(context.Goal.TargetId) && fact != null &&
                    NpcConversation.EligibleFact(context.Game, owner, fact,
                        owner.Location.Map.LocalTime.TurnCounter) &&
                    NpcConversation.EligibleListener(context.Game, owner, target, fact),
                    () => {
                        NpcConversation.ShareRumor(context.Game, owner, target, fact, false);
                        owner.Personality.Knowledge.HandleCacheEncounter(key);
                        context.Done(context.Game.NpcContent.Facts.Mask(DoneFact));
                    });
            Item stolen = StolenItem(owner, role == "answer" && fact != null ? fact.CauseId : eventId);
            int courage = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Courage);
            int law = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law);
            int care = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion);
            int patience = PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience);
            int life = PersonalitySystem.Disposition(owner, NpcPersonalityAxis.HumanLife);
            RelationshipRecord opinion = owner.Personality.Person(target.PersonalityIdentity);
            int fear = opinion == null ? 0 : opinion.Fear;
            if ((role == "thief" || role == "answer") && stolen != null &&
                law + care + fear - courage >= 5)
            {
                string reason;
                if (context.Game.Rules.CanActorGiveItemTo(owner, target, stolen, out reason))
                    return context.Action(() => context.NearPerson(context.Goal.TargetId) &&
                        context.Game.Rules.CanActorGiveItemTo(owner, target, stolen, out reason), () => {
                        long before = target.Inventory.TotalReceived;
                        context.Game.DoGiveItemTo(owner, target, stolen);
                        if (target.Inventory.TotalReceived <= before) return;
                        Report(context, "cache_returned", fact, cacheId);
                        owner.Personality.Knowledge.HandleCacheEncounter(key);
                        context.Done(context.Game.NpcContent.Facts.Mask(DoneFact));
                    });
            }
            if ((role == "victim" && fear - courage > 25 ||
                 (role == "thief" || role == "answer") && fear - courage > 35))
            {
                Point from = owner.Location.Position;
                Point to = new Point(from.X + Math.Sign(from.X - target.Location.Position.X),
                    from.Y + Math.Sign(from.Y - target.Location.Position.Y));
                var move = new ActionMoveStep(owner, context.Game, to);
                if (move.IsLegal()) return context.Action(() => move.IsLegal(), () => {
                    move.Perform();
                    Report(context, "cache_withdrew", fact, cacheId);
                    owner.Personality.Knowledge.HandleCacheEncounter(key);
                    context.Done(context.Game.NpcContent.Facts.Mask(DoneFact));
                });
            }
            string kind, speech;
            if (role == "victim")
            {
                bool calm = patience + life + law - fear >= 0;
                kind = calm ? "cache_return_requested" : "cache_threatened";
                speech = calm ? "Those were my supplies. Please return them." :
                    "Keep away from my cache, or I will stop you.";
            }
            else if (role == "thief" || role == "answer")
            {
                kind = law + care >= 10 ? "cache_apologized" :
                    courage - life > 35 ? "cache_threatened" : "cache_justified";
                speech = kind == "cache_apologized" ? "I took the supplies. I am sorry." :
                    kind == "cache_threatened" ? "Try to stop me and you will regret it." :
                    "I needed those supplies. That does not make them mine.";
            }
            else if (role == "loss")
            { kind = "cache_loss_asked"; speech = "My cache is missing supplies. Have you seen what happened?"; }
            else if (role == "warn")
            { kind = "cache_owner_warned"; speech = "Someone said they are looking for your cache."; }
            else if (role == "ask")
            { kind = "cache_account_asked"; speech = "I heard you took supplies from a cache. What happened?"; }
            else
            {
                NpcKnownCache cache = null;
                foreach (NpcKnownCache known in owner.Personality.Knowledge.Caches)
                    if (known.Id == cacheId) { cache = known; break; }
                if (cache == null || cache.Units <= 0) return null;
                kind = "cache_search_announced";
                speech = "I am looking for a reported cache at " + cache.Place.Map.Name +
                    " (" + cache.Place.Position.X + "," + cache.Place.Position.Y + ").";
            }
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            bool request = role == "victim" && kind == "cache_return_requested" ||
                role == "loss" || role == "ask";
            if (request && !owner.Personality.Knowledge.CanRequest(context.Goal.Sequence,
                target.PersonalityIdentity, speech, turn)) return null;
            return context.Action(() => context.NearPerson(context.Goal.TargetId) &&
                (!request || owner.Personality.Knowledge.CanRequest(context.Goal.Sequence,
                    target.PersonalityIdentity, speech, owner.Location.Map.LocalTime.TurnCounter)), () => {
                context.Game.DoSay(owner, target, speech, RogueGame.Sayflags.IS_STORY |
                    (request ? RogueGame.Sayflags.IS_REQUEST : RogueGame.Sayflags.IS_RUMOR),
                    context.Goal.CauseId, context.Goal.StoryId);
                if (request) owner.Personality.Knowledge.Requested(context.Goal.Sequence,
                    target.PersonalityIdentity, speech, owner.Location.Map.LocalTime.TurnCounter);
                Report(context, kind, fact, cacheId);
                owner.Personality.Knowledge.HandleCacheEncounter(key);
                context.Done(context.Game.NpcContent.Facts.Mask(DoneFact));
            });
        }

        static void Report(NpcActionContext context, string kind, NpcFact fact, Guid cacheId)
        {
            Actor owner = context.Owner;
            NpcKnownCache cache = null;
            foreach (NpcKnownCache known in owner.Personality.Knowledge.Caches)
                if (known.Id == cacheId) { cache = known; break; }
            var outcome = new SignificantEvent(kind, owner, context.Target, owner.Location.Map,
                owner.Location.Position, owner.Location.Map.LocalTime.TurnCounter,
                causeId: fact == null ? 0 : fact.EventId, storyId: context.Goal.StoryId)
            { ItemId = cacheId, ClaimantId = cache != null ? cache.ClaimantId :
                fact == null ? Guid.Empty : fact.ClaimantId,
              ResourcePlace = cache != null ? cache.Place : fact == null ? default(Location) : fact.Place };
            PersonalitySystem.Report(context.Game, outcome);
        }
    }
}
