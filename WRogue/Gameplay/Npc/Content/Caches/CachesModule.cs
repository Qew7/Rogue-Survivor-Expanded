using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class CachesModule : INpcContentModule, INpcGoalSource
    {
        const string SearchFact = "cache.searched";
        const string SearchOperator = "cache.search";
        const string StashFact = "cache.stashed";
        const string StashOperator = "cache.stash";
        const string WriteFact = "cache.note_written";
        const string WriteOperator = "cache.write";
        const string ReportFact = "cache.report_written";
        const string ReportOperator = "cache.report";
        const string ShareFact = "cache.note_shared";
        const string ShareOperator = "cache.share";
        const string TakeFact = "cache.food_taken";
        const string TakeMedicineFact = "cache.medicine_taken";
        const string TakeValuedFact = "cache.valued_item_taken";
        const string TakeOperator = "cache.take";
        const string TakeMedicineOperator = "cache.take_medicine";
        const string TakeValuedOperator = "cache.take_valued";
        public string Id { get { return "caches"; } }

        public void Register(NpcCatalogBuilder catalog)
        {
            CacheEncounters.Register(catalog);
            catalog.Event(new NpcEventDefinition("cache_theft", NpcRecordCategory.None, true,
                e => (e.Subject ?? "Someone") + " took claimed supplies from a cache.",
                f => (f.ReportSubject ?? "Someone") + " took " + f.Units + " " +
                    (f.Resource ?? "item") + " from " + (f.ReportOther ?? "someone") + "'s cache")
            { SelfReportTone = NpcSelfReportTone.Harmful,
              ImmediateSanity = NpcImmediateEmotion.Scarcity });
            catalog.Event(new NpcEventDefinition("cache_loss_discovered", NpcRecordCategory.World,
                true, e => (e.Subject ?? "Someone") + " found supplies missing from a cache.",
                f => (f.ReportSubject ?? "Someone") + " found " + f.Units + " " +
                    (f.Resource ?? "supplies") + " missing from a cache")
            { ReportActorRole = NpcReportActorRole.Subject });
            catalog.Event(new NpcEventDefinition("cache_note_shared", NpcRecordCategory.Encounters,
                true, e => (e.Subject ?? "Someone") + " handed " + (e.Other ?? "someone") + " a note.",
                f => (f.ReportSubject ?? "Someone") + " handed " +
                    (f.ReportOther ?? "someone") + " a note")
            { ReportActorRole = NpcReportActorRole.Subject });
            catalog.Fact(SearchFact);
            catalog.Fact(StashFact);
            catalog.Fact(WriteFact);
            catalog.Fact(ReportFact);
            catalog.Fact(ShareFact);
            catalog.Fact(TakeFact);
            catalog.Fact(TakeMedicineFact);
            catalog.Fact(TakeValuedFact);
            catalog.Value(new NpcValueDefinition("CacheSearch", "Check a reported supply cache", null,
                m => 32 + PersonalitySystem.Bias(m.Owner, NpcPersonalityAxis.Explore) / 2 +
                    m.Supplies / 2 + m.Scarcity / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var search = new NpcIntentDefinition("search_reported_cache", "Check a reported cache", 120, 180);
            search.ResultFacts = (c, g) => c.Facts.Mask(SearchFact);
            search.Assess = (g, a, i) =>
            {
                NpcKnownCache known = FindCache(a.Personality.Knowledge, i.Generated.ItemId);
                if (known == null) return new NpcIntentOutcome(NpcIntentStatus.Abandoned, "lost the report");
                return known.Source != NpcKnowledgeSource.Told && known.SeenTurn >= i.StartedTurn || known.Units == 0 ?
                    new NpcIntentOutcome(NpcIntentStatus.Completed, "checked the reported cache") : null;
            };
            catalog.Capability(search);
            catalog.Value(new NpcValueDefinition("CacheStash", "Protect spare supplies in a container", null,
                m => 18 + m.Supplies / 2 + m.Scarcity / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var stash = new NpcIntentDefinition("stash_food", "Store spare supplies", 120, 180);
            stash.ResultFacts = (c, g) => c.Facts.Mask(StashFact);
            stash.Assess = (g, a, i) => FindStashItem(a, CountFoodUnits(a)) == null ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "spare supplies changed") : null;
            catalog.Capability(stash);
            catalog.Value(new NpcValueDefinition("CacheWrite", "Record a known personal cache", null,
                m => 26 + m.Group / 2 + m.MessageTrust / 4 + m.Supplies / 4, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var write = new NpcIntentDefinition("write_cache_note", "Write a note about a cache", 120, 180);
            write.ResultFacts = (c, g) => c.Facts.Mask(WriteFact);
            write.Assess = (g, a, i) =>
            {
                NpcKnownCache known = FindCache(a.Personality.Knowledge, i.Generated.ItemId);
                return known == null || known.HasWrittenNote && known.WrittenUnits == known.OwnUnits &&
                    known.WrittenResource == known.OwnResource && known.WrittenPlace == known.Place ?
                    new NpcIntentOutcome(NpcIntentStatus.Completed, "cache note recorded") : null;
            };
            catalog.Capability(write);
            catalog.Value(new NpcValueDefinition("CacheReport", "Write down an experienced event", null,
                m => 40 + m.MessageTrust / 3 + m.CareCircle / 4 + m.Group / 4, true)
                { IdentitySuffix = g => ":" + g.ObligationId });
            var report = new NpcIntentDefinition("write_cache_report", "Write a note about an experienced event", 120, 180);
            report.ResultFacts = (c, g) => c.Facts.Mask(ReportFact);
            report.Assess = (g, a, i) => a.Personality.Knowledge.HasWrittenNote(
                i.Generated.Resource + ":" + i.Generated.ObligationId) ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "event note written") : null;
            catalog.Capability(report);
            catalog.Value(new NpcValueDefinition("CacheShare", "Share a cache note with a trusted person", null,
                m => 20 + m.Group / 2 + m.Compassion / 2 + m.Attachment / 3 +
                    m.Feeling / 4 - m.Scarcity / 2, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var share = new NpcIntentDefinition("share_cache_note", "Give a cache note", 120, 180);
            share.ResultFacts = (c, g) => c.Facts.Mask(ShareFact);
            share.Assess = (g, a, i) => FindAuthoredNote(a, i.Generated.ItemId) == null ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "cache note left the inventory") : null;
            catalog.Capability(share);
            catalog.Value(new NpcValueDefinition("CacheFood", "Get food from a known container", null,
                m => 36 + m.Scarcity / 2 + m.Supplies / 4 + m.Courage / 4 - m.Law / 4, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var take = new NpcIntentDefinition("take_cache_food", "Take food from a cache", 120, 120);
            take.ResultFacts = (c, g) => c.Facts.Mask(TakeFact);
            take.Assess = (g, a, i) => CountFoodUnits(a) >= 2 ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "food reserve acquired") : null;
            catalog.Capability(take);
            catalog.Value(new NpcValueDefinition("CacheMedicine", "Get medicine from a known container", null,
                m => 48 + m.Supplies / 3 + m.Scarcity / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var takeMedicine = new NpcIntentDefinition("take_cache_medicine", "Take medicine from a cache", 120, 120);
            takeMedicine.ResultFacts = (c, g) => c.Facts.Mask(TakeMedicineFact);
            takeMedicine.Assess = (g, a, i) => CountMedicineUnits(a) > 0 || a.HitPoints >= g.Rules.ActorMaxHPs(a) ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "medicine reserve acquired") : null;
            catalog.Capability(takeMedicine);
            catalog.Value(new NpcValueDefinition("CacheValued", "Recover a personally stored item", null,
                m => 34 + m.Scarcity / 2 + PersonalitySystem.Bias(m.Owner, NpcPersonalityAxis.Item) / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var takeValued = new NpcIntentDefinition("take_cache_valued_item", "Retrieve a stored valued item", 120, 120);
            takeValued.ResultFacts = (c, g) => c.Facts.Mask(TakeValuedFact);
            takeValued.Assess = (g, a, i) => !g.Rules.IsActorHungry(a) ||
                FindCache(a.Personality.Knowledge, i.Generated.ItemId) == null ||
                FindCache(a.Personality.Knowledge, i.Generated.ItemId).OwnItemId == Guid.Empty ||
                HasItem(a, FindCache(a.Personality.Knowledge, i.Generated.ItemId).OwnItemId) ?
                new NpcIntentOutcome(NpcIntentStatus.Completed, "stored item is no longer needed") : null;
            catalog.Capability(takeValued);
            catalog.OperatorSource(new NpcOperatorSource("cache.seek", c => c.Facts.Mask(SearchFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(SearchFact);
                d.Add(SearchOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 1);
            }));
            catalog.Operator(new NpcOperatorDefinition(SearchOperator, null, Search,
                archiveText: step => "check a reported cache"));
            catalog.OperatorSource(new NpcOperatorSource("cache.store", c => c.Facts.Mask(StashFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(StashFact);
                d.Add(StashOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(StashOperator, null, Stash,
                archiveText: step => "store spare supplies in a container"));
            catalog.OperatorSource(new NpcOperatorSource("cache.paper", c => c.Facts.Mask(WriteFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(WriteFact);
                d.Add(WriteOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(WriteOperator, null, Write,
                archiveText: step => "write a cache note on paper"));
            catalog.OperatorSource(new NpcOperatorSource("cache.report.paper", c => c.Facts.Mask(ReportFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObligationId <= 0) return;
                Location here = d.Owner.Location;
                ulong at = d.At(here);
                if (at == 0) return;
                var fact = d.Catalog.Facts.Mask(ReportFact);
                d.Add(ReportOperator, here, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 1);
            }));
            catalog.Operator(new NpcOperatorDefinition(ReportOperator, null, WriteReport,
                archiveText: step => "write a witnessed event on paper"));
            catalog.OperatorSource(new NpcOperatorSource("cache.handoff", c => c.Facts.Mask(ShareFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.LastKnown.Map == null) return;
                Location place = d.Goal.LastKnown;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Goal.TargetId, at);
                var fact = d.Catalog.Facts.Mask(ShareFact);
                d.Add(ShareOperator, place, d.Goal.TargetId,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(ShareOperator, null, Share,
                archiveText: step => "hand a trusted person a cache note"));
            catalog.OperatorSource(new NpcOperatorSource("cache.food", c => c.Facts.Mask(TakeFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(TakeFact);
                d.Add(TakeOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(TakeOperator, null, TakeFood,
                archiveText: step => "take visible food from a cache"));
            catalog.OperatorSource(new NpcOperatorSource("cache.medicine", c => c.Facts.Mask(TakeMedicineFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(TakeMedicineFact);
                d.Add(TakeMedicineOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(TakeMedicineOperator, null, TakeMedicine,
                archiveText: step => "take visible medicine from a cache"));
            catalog.OperatorSource(new NpcOperatorSource("cache.valued", c => c.Facts.Mask(TakeValuedFact), d =>
            {
                if (d.Goal.Generated == null || d.Goal.Generated.ObjectPlace.Map == null) return;
                Location place = d.Goal.Generated.ObjectPlace;
                ulong at = d.At(place);
                if (at == 0) return;
                d.Travel(place, d.Owner.PersonalityIdentity, at);
                var fact = d.Catalog.Facts.Mask(TakeValuedFact);
                d.Add(TakeValuedOperator, place, d.Owner.PersonalityIdentity,
                    (NpcPlanningState)at, fact, fact, default(NpcPlanningState), 2);
            }));
            catalog.Operator(new NpcOperatorDefinition(TakeValuedOperator, null, TakeValued,
                archiveText: step => "retrieve a personally stored item"));
            catalog.Perception(NpcPerceptionKind.Storage, PerceiveStorage);
            catalog.Perception(NpcPerceptionKind.Items, PerceiveGroundNotes);
            catalog.Perception(NpcPerceptionKind.Surroundings, ForgetMissingGroundNote);
            catalog.GoalSource(this);
        }

        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            CacheEncounters.Evaluate(context, offers);
            if (Session.Get.GamePreset.NotesEnabled && FindBlankNote(context.Owner) != null)
                foreach (NpcFact fact in context.Owner.Personality.Knowledge.Facts)
                {
                    NoteTopic topic = NpcNoteTemplates.TopicFor(context.Owner, fact);
                    if (topic == 0 || fact.EventId <= 0 ||
                        context.Owner.Personality.Knowledge.HasWrittenNote(fact.Kind + ":" + fact.EventId)) continue;
                    offers.Add(context.Self, "CacheReport", "write_cache_report", 0, 1,
                        70, fact.Confidence, cause: fact.EventId, story: fact.StoryId,
                        resource: fact.Kind, obligation: fact.EventId, objectPlace: fact.Place, self: true);
                    break;
                }
            if (Session.Get.GamePreset.NotesEnabled && FindBlankNote(context.Owner) != null)
                foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
                    if (known.OwnUnits > 0 && known.Place.Map == context.Owner.Location.Map &&
                        (!known.HasWrittenNote || known.WrittenUnits != known.OwnUnits ||
                         known.WrittenResource != known.OwnResource || known.WrittenPlace != known.Place))
                    {
                        offers.Add(context.Self, "CacheWrite", "write_cache_note", 0, 1,
                            100, known.Confidence, objectPlace: known.Place, itemId: known.Id, self: true);
                        break;
                    }
            if (Session.Get.GamePreset.NotesEnabled)
                foreach (Item item in context.Owner.Inventory.Items)
                {
                    ItemNote note = item as ItemNote;
                    if (note == null || note.IsBlank ||
                        (note.Topic != NoteTopic.CacheSupplies && note.Claim == null) ||
                        note.AuthorId != context.Owner.PersonalityIdentity) continue;
                    foreach (NpcKnownPerson person in context.People)
                    {
                        if (person.Id == context.Self.Id || person.Dead ||
                            person.Place.Map != context.Owner.Location.Map) continue;
                        bool group = context.Owner.SocialGroup != null &&
                            person.GroupId == context.Owner.SocialGroup.Identity;
                        RelationshipRecord opinion = context.Owner.Personality.Person(person.Id);
                        if (!group && (opinion == null || opinion.Trust < 20 || opinion.Feeling < 0)) continue;
                        offers.Add(person, "CacheShare", "share_cache_note", 0, 1, 100,
                            person.Confidence, objectPlace: note.Place, itemId: note.Id);
                        break;
                    }
                    break;
                }
            if (!Session.Get.GamePreset.CachesEnabled) return;
            foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
            {
                if (known.Source != NpcKnowledgeSource.Told || known.Units <= 0 ||
                    known.Place.Map == null || known.Place.Map != context.Owner.Location.Map) continue;
                offers.Add(context.Self, "CacheSearch", "search_reported_cache", 0, 1,
                    100, known.Confidence, resource: known.Resource,
                    objectPlace: known.Place, itemId: known.Id, self: true);
                break;
            }
            if (context.Hungry || context.FoodUnits < 2)
                foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
                    if (known.Units > 0 && (known.Resource == "food" || known.Resource == "mixed") &&
                        known.Place.Map == context.Owner.Location.Map &&
                        (known.OwnUnits == 0 || context.Hungry))
                    {
                        offers.Add(context.Self, "CacheFood", "take_cache_food", context.FoodUnits, 2,
                            context.Hungry ? 100 : 75, known.Confidence,
                            resource: "food", objectPlace: known.Place, itemId: known.Id, self: true);
                        break;
                    }
            if (context.Owner.HitPoints < context.MaxHP && CountMedicineUnits(context.Owner) == 0)
                foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
                    if (known.Units > 0 && (known.Resource == "medicine" || known.Resource == "mixed") &&
                        known.Place.Map == context.Owner.Location.Map)
                    {
                        offers.Add(context.Self, "CacheMedicine", "take_cache_medicine", 0, 1,
                            100, known.Confidence, resource: "medicine",
                            objectPlace: known.Place, itemId: known.Id, self: true);
                        break;
                    }
            if (context.Hungry && context.FoodUnits == 0)
                foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
                    if (known.OwnItemId != Guid.Empty && known.OwnUnits > 0 && known.Units != 0 &&
                        known.Place.Map == context.Owner.Location.Map &&
                        !HasItem(context.Owner, known.OwnItemId))
                    {
                        offers.Add(context.Self, "CacheValued", "take_cache_valued_item", 0, 1,
                            100, known.Confidence, resource: "item",
                            objectPlace: known.Place, itemId: known.Id, self: true);
                        break;
                    }
            if (context.Hungry || FindStashItem(context.Owner, context.FoodUnits) == null) return;
            foreach (NpcKnownCache known in context.Owner.Personality.Knowledge.Caches)
            {
                if (known.Units != 0 || known.Place.Map != context.Owner.Location.Map) continue;
                XpdBase territory = Math.Abs(known.Place.Position.X - context.Owner.Location.Position.X) <= 1 &&
                    Math.Abs(known.Place.Position.Y - context.Owner.Location.Position.Y) <= 1 ?
                    known.Place.Map.XpdBaseAt(known.Place.Position) : null;
                bool foreign = territory != null && !territory.Owns(context.Owner);
                int desire = foreign ? Math.Max(10, Math.Min(80,
                    35 + context.CourageBias - context.LawBias)) : 100;
                offers.Add(context.Self, "CacheStash", "stash_food", 0, 1, desire,
                    known.Confidence, objectPlace: known.Place, itemId: known.Id, self: true);
                break;
            }
        }

        static NpcKnownCache FindCache(NpcKnowledge knowledge, Guid id)
        {
            foreach (NpcKnownCache cache in knowledge.Caches)
                if (cache.Id == id) return cache;
            return null;
        }

        static ItemNote FindNote(Actor actor, Guid id)
        {
            foreach (Item item in actor.Inventory.Items)
            {
                ItemNote note = item as ItemNote;
                if (note != null && !note.IsBlank && note.Id == id) return note;
            }
            return null;
        }

        static ItemNote FindVisibleNote(Actor actor, Inventory items)
        {
            if (items == null) return null;
            foreach (Item item in items.Items)
            {
                ItemNote note = item as ItemNote;
                if (note != null && !note.IsBlank && !actor.Personality.Knowledge.HasReadNote(note.Id))
                    return note;
            }
            return null;
        }

        internal static ActorAction NearbyAction(RogueGame game, Actor actor)
        {
            if (Session.Get.GamePreset.NotesEnabled)
            {
                foreach (Item item in actor.Inventory.Items)
                {
                    ItemNote note = item as ItemNote;
                    if (note == null || note.IsBlank || actor.Personality.Knowledge.HasReadNote(note.Id)) continue;
                    var read = new ActionUseItem(actor, game, note);
                    if (read.IsLegal()) return read;
                }
                foreach (NpcKnownPlace place in actor.Personality.Knowledge.Places)
                {
                    if (place.Units <= 0 || place.Place.Map != actor.Location.Map ||
                        !NpcKnowledgeSystem.Visible(game, actor, place.Place) ||
                        game.Rules.GridDistance(actor.Location.Position, place.Place.Position) > 1) continue;
                    if (place.Risk > 0 && PersonalitySystem.Bias(actor, NpcPersonalityAxis.Law) >
                        PersonalitySystem.Bias(actor, NpcPersonalityAxis.Courage)) continue;
                    StorageContainer storage = place.Kind == "note:storage" ?
                        actor.Location.Map.GetMapObjectAt(place.Place.Position) as StorageContainer : null;
                    Inventory items = place.Kind == "note:storage" ?
                        storage != null && storage.IsOpen ? storage.Contents : null :
                        place.Kind == "note:ground" ? actor.Location.Map.GetItemsAt(place.Place.Position) : null;
                    ItemNote note = FindVisibleNote(actor, items);
                    if (note == null) continue;
                    ActorAction action = storage != null ?
                        (ActorAction)new ActionGetFromContainer(actor, game, place.Place.Position, note) :
                        actor.Location.Position == place.Place.Position ?
                        (ActorAction)new ActionTakeItem(actor, game, place.Place.Position, note) :
                        new ActionMoveStep(actor, game, place.Place.Position);
                    if (action.IsLegal()) return action;
                }
            }
            if (Session.Get.GamePreset.CachesEnabled)
                foreach (NpcKnownCache known in actor.Personality.Knowledge.Caches)
                {
                    if (known.Units >= 0 || known.Place.Map != actor.Location.Map ||
                        !NpcKnowledgeSystem.Visible(game, actor, known.Place) ||
                        game.Rules.GridDistance(actor.Location.Position, known.Place.Position) > 1) continue;
                    StorageContainer storage = actor.Location.Map.GetMapObjectAt(known.Place.Position) as StorageContainer;
                    if (storage == null || storage.Id != known.Id || storage.IsOpen) continue;
                    var open = new ActionUseStorageContainer(actor, game, storage, true);
                    if (open.IsLegal()) return open;
                }
            return null;
        }

        static bool HasItem(Actor actor, Guid id)
        {
            foreach (Item item in actor.Inventory.Items)
                if (item.StoryIdentity == id) return true;
            return false;
        }

        static void RememberVisibleNotes(NpcPerceptionContext context, Inventory items, string kind, int risk)
        {
            if (!Session.Get.GamePreset.NotesEnabled) return;
            int count = 0;
            if (items != null) foreach (Item item in items.Items)
            {
                ItemNote note = item as ItemNote;
                if (note != null && !note.IsBlank &&
                    !context.Owner.Personality.Knowledge.HasReadNote(note.Id)) count++;
            }
            NpcKnowledge knowledge = context.Owner.Personality.Knowledge;
            if (count > 0 || knowledge.Places.Exists(p => p.Kind == kind && p.Place == context.Place))
                knowledge.RememberPlace(new NpcKnownPlace(context.Place, kind, context.Turn, count, risk));
        }

        static void PerceiveGroundNotes(NpcPerceptionContext context)
        {
            RememberVisibleNotes(context, context.Items, "note:ground", context.Risk);
        }

        static void ForgetMissingGroundNote(NpcPerceptionContext context)
        {
            if (!Session.Get.GamePreset.NotesEnabled) return;
            NpcKnowledge knowledge = context.Owner.Personality.Knowledge;
            foreach (NpcKnownPlace place in knowledge.Places)
                if (place.Kind == "note:ground" && place.Units > 0 &&
                    place.Place.Map == context.Owner.Location.Map &&
                    NpcKnowledgeSystem.Visible(context.Game, context.Owner, place.Place) &&
                    FindVisibleNote(context.Owner, place.Place.Map.GetItemsAt(place.Place.Position)) == null)
                {
                    knowledge.RememberPlace(new NpcKnownPlace(place.Place, place.Kind, context.Turn));
                    break;
                }
        }

        static Item FindStashItem(Actor actor, int foodUnits)
        {
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemFood && !item.IsEquipped && !item.IsStolen &&
                    (!(item as ItemFood).IsPerishable || actor.Location.Map.LocalTime.TurnCounter <
                        2 * (item as ItemFood).BestBefore.TurnCounter) &&
                    foodUnits - item.Quantity >= 1) return item;
            if (actor.HitPoints < actor.Sheet.BaseHitPoints / 2) return null;
            int medicineUnits = CountMedicineUnits(actor);
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemMedicine && !item.IsEquipped && !item.IsStolen &&
                    medicineUnits - item.Quantity >= 1) return item;
            if (actor.Personality.HasAttachments)
                foreach (NpcAttachment attachment in actor.Personality.Attachments)
                    if (attachment.Kind == "item" && attachment.ItemId != Guid.Empty &&
                        attachment.Weight >= 30)
                        foreach (Item item in actor.Inventory.Items)
                            if (item.StoryIdentity == attachment.ItemId && !item.Model.IsStackable &&
                                !item.IsEquipped && !item.IsStolen && !(item is ItemNote)) return item;
            return null;
        }

        static int CountMedicineUnits(Actor actor)
        {
            int units = 0;
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemMedicine) units += item.Quantity;
            return units;
        }

        static ItemNote FindBlankNote(Actor actor)
        {
            foreach (Item item in actor.Inventory.Items)
            {
                ItemNote note = item as ItemNote;
                if (note != null && note.IsBlank) return note;
            }
            return null;
        }

        static ItemNote FindAuthoredNote(Actor actor, Guid id)
        {
            ItemNote note = FindNote(actor, id);
            return note != null && note.AuthorId == actor.PersonalityIdentity &&
                (note.Topic == NoteTopic.CacheSupplies || note.Claim != null) ? note : null;
        }

        static int CountFoodUnits(Actor actor)
        {
            int units = 0;
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemFood && (!(item as ItemFood).IsPerishable ||
                    actor.Location.Map.LocalTime.TurnCounter <
                        2 * (item as ItemFood).BestBefore.TurnCounter)) units += item.Quantity;
            return units;
        }

        static void PerceiveStorage(NpcPerceptionContext context)
        {
            StorageContainer storage = context.Storage;
            if (storage == null) return;
            NpcKnowledge knowledge = context.Owner.Personality.Knowledge;
            if (!storage.IsOpen)
            {
                NpcKnownCache known = FindCache(knowledge, storage.Id);
                if (known == null || known.Place != context.Place)
                    knowledge.RememberCache(new NpcKnownCache { Id = storage.Id,
                        Place = context.Place, Units = -1, SeenTurn = context.Turn,
                        Confidence = 100, Source = NpcKnowledgeSource.Witness });
                return;
            }
            string resource;
            int units;
            storage.CountSupplies(out resource, out units);
            knowledge.RememberCache(new NpcKnownCache { Id = storage.Id,
                Place = context.Place, Resource = resource, Units = units,
                SeenTurn = context.Turn, Confidence = 100, Source = NpcKnowledgeSource.Witness });
            XpdBase territory = context.Place.Map.XpdBaseAt(context.Place.Position);
            RememberVisibleNotes(context, storage.Contents, "note:storage",
                territory != null && !territory.Owns(context.Owner) ? 1 : 0);
        }

        static ActorAction Search(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            Location place = context.Goal.Generated.ObjectPlace;
            Guid id = context.Goal.Generated.ItemId;
            if (!NpcPlanExecution.Near(context.Game, context.Owner, place)) return null;
            StorageContainer storage = place.Map.GetMapObjectAt(place.Position) as StorageContainer;
            if (storage != null && storage.Id == id && !storage.IsOpen)
            {
                var open = new ActionUseStorageContainer(context.Owner, context.Game, storage, true);
                return context.Action(() => open.IsLegal(), () => open.Perform());
            }
            if (storage != null && storage.Id == id)
            {
                var inspect = new ActionInspectStorage(context.Owner, context.Game, storage);
                return context.Action(() => inspect.IsLegal(), () =>
                {
                    inspect.Perform();
                    context.Done(context.Game.NpcContent.Facts.Mask(SearchFact));
                });
            }
            return context.Action(() => NpcPlanExecution.Near(context.Game, context.Owner, place), () =>
            {
                context.Game.DoWait(context.Owner);
                NpcKnownCache old = FindCache(context.Owner.Personality.Knowledge, id);
                if (old != null && old.Place == place)
                    context.Owner.Personality.Knowledge.RememberCache(new NpcKnownCache {
                        Id = id, Place = place, Units = 0,
                        SeenTurn = context.Owner.Location.Map.LocalTime.TurnCounter,
                        Confidence = 100, Source = NpcKnowledgeSource.Inferred });
                context.Done(context.Game.NpcContent.Facts.Mask(SearchFact));
            });
        }

        static ActorAction Stash(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            Location place = context.Goal.Generated.ObjectPlace;
            Guid id = context.Goal.Generated.ItemId;
            if (!NpcPlanExecution.Near(context.Game, context.Owner, place)) return null;
            StorageContainer storage = place.Map.GetMapObjectAt(place.Position) as StorageContainer;
            if (storage == null || storage.Id != id)
                return context.Action(() => true, () => {
                    context.Game.DoWait(context.Owner);
                    NpcIntentSystem.Finish(context.Game.NpcContent, context.Owner, context.Goal,
                        NpcIntentStatus.Abandoned, "container moved");
                });
            if (!storage.IsOpen)
            {
                var open = new ActionUseStorageContainer(context.Owner, context.Game, storage, true);
                return context.Action(() => open.IsLegal(), () => open.Perform());
            }
            if (context.Owner.Location.Position != place.Position)
            {
                var move = new ActionMoveStep(context.Owner, context.Game, place.Position);
                if (!move.IsLegal()) return CannotStore(context, "container is occupied");
                return context.Action(() => move.IsLegal(), () => move.Perform());
            }
            int foodUnits = 0;
            foreach (Item item in context.Owner.Inventory.Items)
                if (item is ItemFood && !context.Game.Rules.IsFoodSpoiled((ItemFood)item,
                    context.Owner.Location.Map.LocalTime.TurnCounter)) foodUnits += item.Quantity;
            Item supply = FindStashItem(context.Owner, foodUnits);
            if (supply == null) return null;
            var drop = new ActionDropItem(context.Owner, context.Game, supply);
            if (!drop.IsLegal()) return CannotStore(context, "container cannot accept supplies");
            return context.Action(() => drop.IsLegal(), () => {
                drop.Perform();
                context.Done(context.Game.NpcContent.Facts.Mask(StashFact));
            });
        }

        static ActorAction CannotStore(NpcActionContext context, string reason)
        {
            return context.Action(() => true, () => {
                context.Game.DoWait(context.Owner);
                NpcIntentSystem.Finish(context.Game.NpcContent, context.Owner, context.Goal,
                    NpcIntentStatus.Abandoned, reason);
            });
        }

        static ActorAction Write(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            Location place = context.Goal.Generated.ObjectPlace;
            Guid id = context.Goal.Generated.ItemId;
            if (!NpcPlanExecution.Near(context.Game, context.Owner, place)) return null;
            StorageContainer storage = place.Map.GetMapObjectAt(place.Position) as StorageContainer;
            if (storage == null || storage.Id != id)
                return CannotStore(context, "cache moved before writing");
            if (!storage.IsOpen)
            {
                var open = new ActionUseStorageContainer(context.Owner, context.Game, storage, true);
                return context.Action(() => open.IsLegal(), () => open.Perform());
            }
            ItemNote note = FindBlankNote(context.Owner);
            if (note == null) return CannotStore(context, "no blank paper remains");
            var write = new ActionWriteNote(context.Owner, context.Game, note,
                NoteTopic.CacheSupplies, storage, true);
            if (!write.IsLegal()) return CannotStore(context, "cache contents changed");
            return context.Action(() => write.IsLegal(), () => {
                write.Perform();
                NpcKnownCache known = FindCache(context.Owner.Personality.Knowledge, id);
                if (!note.IsBlank && known != null)
                {
                    known.HasWrittenNote = true;
                    known.WrittenUnits = known.OwnUnits;
                    known.WrittenResource = known.OwnResource;
                    known.WrittenPlace = known.Place;
                    context.Done(context.Game.NpcContent.Facts.Mask(WriteFact));
                }
            });
        }

        static ActorAction WriteReport(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            NpcFact evidence = context.Owner.Personality.Knowledge.Facts.Find(f =>
                f.EventId == context.Goal.Generated.ObligationId &&
                f.Kind == context.Goal.Generated.Resource);
            ItemNote paper = FindBlankNote(context.Owner);
            if (evidence == null || paper == null) return CannotStore(context, "report or paper is unavailable");
            NoteTopic topic = NpcNoteTemplates.TopicFor(context.Owner, evidence);
            var write = new ActionWriteNote(context.Owner, context.Game, paper, topic,
                null, true, evidence);
            if (!write.IsLegal()) return CannotStore(context, "report is no longer valid");
            return context.Action(() => write.IsLegal(), () => {
                write.Perform();
                if (paper.IsBlank) return;
                context.Owner.Personality.Knowledge.MarkNoteWritten(evidence.Kind + ":" + evidence.EventId);
                context.Done(context.Game.NpcContent.Facts.Mask(ReportFact));
            });
        }

        static ActorAction Share(NpcExecutionContext execution)
        {
            NpcActionContext context = new NpcActionContext(execution);
            ItemNote note = FindAuthoredNote(context.Owner, context.Goal.Generated.ItemId);
            if (note == null || !context.NearPerson(context.Goal.TargetId)) return null;
            StorageContainer underfoot = context.Owner.Location.Map.GetMapObjectAt(
                context.Owner.Location.Position) as StorageContainer;
            if (underfoot != null && underfoot.IsOpen)
            {
                Point from = context.Owner.Location.Position;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Point to = new Point(from.X + dx, from.Y + dy);
                        if (context.Game.Rules.GridDistance(to, context.Target.Location.Position) > 1) continue;
                        var move = new ActionMoveStep(context.Owner, context.Game, to);
                        if (move.IsLegal()) return context.Action(() => move.IsLegal(), () => move.Perform());
                    }
                return CannotStore(context, "no place to hand over the note");
            }
            string reason;
            if (!context.Owner.Inventory.Contains(note) ||
                !context.Game.Rules.CanActorGiveItemTo(context.Owner, context.Target, note, out reason))
                return CannotStore(context, "recipient cannot take the note");
            return context.Action(() => context.NearPerson(context.Goal.TargetId) &&
                context.Owner.Inventory.Contains(note) &&
                context.Game.Rules.CanActorGiveItemTo(context.Owner, context.Target, note, out reason), () =>
            {
                context.Game.DoGiveItemTo(context.Owner, context.Target, note);
                if (!context.Target.Inventory.Contains(note)) return;
                context.Game.DoSay(context.Owner, context.Target,
                    note.Topic == NoteTopic.CacheSupplies ?
                        "I wrote down where I saw supplies." : "I wrote down what I know.",
                    RogueGame.Sayflags.IS_FREE_ACTION);
                PersonalitySystem.Report(context.Game,
                    new SignificantEvent("cache_note_shared", context.Owner, context.Target,
                        context.Owner.Location.Map, context.Owner.Location.Position,
                        context.Owner.Location.Map.LocalTime.TurnCounter) { ItemId = note.Id });
                context.Done(context.Game.NpcContent.Facts.Mask(ShareFact));
            });
        }

        static ActorAction TakeFood(NpcExecutionContext execution)
        {
            return TakeSupply(execution, false, false);
        }

        static ActorAction TakeMedicine(NpcExecutionContext execution)
        {
            return TakeSupply(execution, true, false);
        }

        static ActorAction TakeValued(NpcExecutionContext execution)
        {
            return TakeSupply(execution, false, true);
        }

        static ActorAction TakeSupply(NpcExecutionContext execution, bool medicine, bool valued)
        {
            NpcActionContext context = new NpcActionContext(execution);
            var takenFact = context.Game.NpcContent.Facts.Mask(valued ? TakeValuedFact :
                medicine ? TakeMedicineFact : TakeFact);
            Location place = context.Goal.Generated.ObjectPlace;
            Guid id = context.Goal.Generated.ItemId;
            if (!NpcPlanExecution.Near(context.Game, context.Owner, place)) return null;
            StorageContainer storage = place.Map.GetMapObjectAt(place.Position) as StorageContainer;
            if (storage == null || storage.Id != id)
                return context.Action(() => true, () => {
                    context.Game.DoWait(context.Owner);
                    NpcKnownCache old = FindCache(context.Owner.Personality.Knowledge, id);
                    if (old != null && old.Place == place)
                        context.Owner.Personality.Knowledge.RememberCache(new NpcKnownCache {
                            Id = id, Place = place, Units = 0,
                            SeenTurn = context.Owner.Location.Map.LocalTime.TurnCounter,
                            Confidence = 100, Source = NpcKnowledgeSource.Inferred });
                    NpcIntentSystem.Finish(context.Game.NpcContent, context.Owner, context.Goal,
                        NpcIntentStatus.Abandoned, "reported cache is gone");
                });
            if (!storage.IsOpen)
            {
                var open = new ActionUseStorageContainer(context.Owner, context.Game, storage, true);
                if (!open.IsLegal()) return CannotStore(context, "cache cannot be opened");
                return context.Action(() => open.IsLegal(), () => open.Perform());
            }
            NpcKnownCache known = FindCache(context.Owner.Personality.Knowledge, id);
            if (known != null && known.Source == NpcKnowledgeSource.Told)
            {
                var inspect = new ActionInspectStorage(context.Owner, context.Game, storage);
                return context.Action(() => inspect.IsLegal(), () => inspect.Perform());
            }
            Item supply = null;
            foreach (Item item in storage.Contents.Items)
                if (valued ? known != null && item.StoryIdentity == known.OwnItemId :
                    medicine ? item is ItemMedicine : item is ItemFood &&
                    !context.Game.Rules.IsFoodSpoiled((ItemFood)item,
                        context.Owner.Location.Map.LocalTime.TurnCounter))
                { supply = item; break; }
            if (supply == null)
            {
                var inspect = new ActionInspectStorage(context.Owner, context.Game, storage);
                return context.Action(() => inspect.IsLegal(), () => {
                    inspect.Perform();
                    context.Done(takenFact);
                });
            }
            var take = new ActionGetFromContainer(context.Owner, context.Game, place.Position, supply);
            if (!take.IsLegal()) return CannotStore(context, "cannot carry cache supplies");
            return context.Action(() => take.IsLegal(), () => {
                long before = context.Owner.Inventory.TotalReceived;
                take.Perform();
                if (context.Owner.Inventory.TotalReceived > before)
                    context.Done(takenFact);
            });
        }
    }
}
