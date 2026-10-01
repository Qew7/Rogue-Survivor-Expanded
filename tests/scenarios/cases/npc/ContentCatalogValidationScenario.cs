using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContentCatalogValidationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/content-catalog-validation", () => TownScenarioFactory.Arena(4692, "...", "...", "..."), world =>
        {
            Check.Throws<ArgumentException>(() => NpcCatalogBuilder.Compose(new PersonalityRegistry(), new NpcRestContent(), new NpcRestContent()), "duplicate modules fail during composition");
            var builder = new NpcCatalogBuilder(new PersonalityRegistry());
            builder.Event(new NpcEventDefinition("ready"));
            Check.Throws<ArgumentException>(() => builder.Event(new NpcEventDefinition("ready")), "duplicate event IDs are rejected");
            Check.Throws<ArgumentException>(() => builder.GoalSource(null), "null providers are rejected");
            builder.Fact("first"); builder.Fact("second");
            NpcContentCatalog catalog = builder.Build();
            Check.Equal(false, catalog.Facts.Mask("first").Equals(catalog.Facts.Mask("second")), "symbolic facts have distinct slots");
            Check.Equal(true, (catalog.Facts.Mask("first") & (ulong)NpcPlanFact.Food).Empty, "catalog facts never overwrite built-in facts");
            Check.Equal(true, (catalog.Facts.Mask("first") & NpcFactLayout.Locations).Empty, "catalog facts never overwrite location bindings");
            Check.Throws<InvalidOperationException>(() => builder.Fact("late"), "catalog cannot be changed after construction");
            Check.Throws<ArgumentException>(() => catalog.Facts.Mask("unknown"), "unknown facts fail explicitly");
            var reordered = new NpcCatalogBuilder(new PersonalityRegistry());
            reordered.Fact("second"); reordered.Fact("first"); reordered.Event(new NpcEventDefinition("ready"));
            NpcContentCatalog equivalent = reordered.Build();
            Check.Equal(catalog.Fingerprint, equivalent.Fingerprint, "registration order does not change persisted fingerprints");
            Check.Equal(catalog.Facts.Mask("first"), equivalent.Facts.Mask("first"), "fact IDs have stable slots across reconstruction");
            var revised = new NpcCatalogBuilder(new PersonalityRegistry());
            revised.Fact("first"); revised.Fact("second"); revised.Event(new NpcEventDefinition("ready")); revised.Revision("rules", 2);
            Check.Equal(false, catalog.Fingerprint == revised.Build().Fingerprint, "a parameter revision invalidates old plans");
            var capacity = new NpcCatalogBuilder(new PersonalityRegistry());
            for (int i = 0; i < 200; i++) capacity.Fact("extra." + i.ToString("D3"));
            Check.Equal(NpcPlanningState.Bit(255), capacity.Build().Facts.Mask("extra.199"), "last available named fact uses bit 255");
            var overflow = new NpcCatalogBuilder(new PersonalityRegistry());
            for (int i = 0; i < 201; i++) overflow.Fact("extra." + i);
            Check.Throws<ArgumentException>(() => overflow.Build(), "fact capacity overflow fails during composition");
            var bad = new NpcCatalogBuilder(new PersonalityRegistry());
            bad.Memory(new MemoryDefinition("lost", "Lost", 1, 1, new[] { new MemoryTrigger("missing", (a, e) => true) }, new MemoryOutcome(null, "missing-trait", null)));
            Check.Throws<ArgumentException>(() => bad.Build(), "unresolved memory references fail before simulation");
            var badReply = new NpcCatalogBuilder(new PersonalityRegistry());
            badReply.Event(new NpcEventDefinition("question") { PlayerReply =
                new NpcPlayerReply("supplies", "missing_yes", "missing_no", "Yes.", "No.") });
            Check.Throws<ArgumentException>(() => badReply.Build(), "request replies need registered outcomes");
            var incomplete = new NpcCatalogBuilder(new PersonalityRegistry());
            incomplete.Capability(new NpcIntentDefinition("unbound", "Unbound", 30));
            Check.Throws<ArgumentException>(() => incomplete.Build(), "selectable capabilities require a planner and result");
            Check.Throws<ArgumentOutOfRangeException>(() => NpcFactLayout.Question(NpcFactLayout.MaximumQuestions), "question binding overflow is explicit");
            Check.Throws<ArgumentOutOfRangeException>(() => NpcFactLayout.Location(NpcFactLayout.MaximumPlaces), "location binding overflow is explicit");
        });
    }
}
