using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PrivateContentEventScenario
{
    sealed class PrivateContent : INpcContentModule
    {
        public string Id { get { return "scenario.private"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("private_worry", NpcRecordCategory.Life, describe: e => e.Other + " privately worried about " + e.Subject,
                required: NpcEventFields.Subject | NpcEventFields.Other, isPrivate: true) { PrivateAudience = e => e.Other });
            catalog.Memory(new MemoryDefinition("private_worry", "Private worry", 1, 1,
                new[] { new MemoryTrigger("private_worry", (a, e) => a == e.Other) }, new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE)));
        }
    }
    public static void Register()
    {
        ScenarioRunner.Add("npc/private-content-event", () => TownScenarioFactory.Arena(4693, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new PrivateContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "worried", 1, 1);
            Actor subject = NpcIntentSupport.Actor(world, "remembered", 2, 1);
            Actor witness = NpcIntentSupport.Actor(world, "nearby", 3, 1);
            var source = new SignificantEvent("private_worry", subject, owner, world.Map, owner.Location.Position, 0);
            PersonalitySystem.Report(world.Game, source);
            PersonalitySystem.Report(world.Game, source);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "private_worry"), "registered audience observes its private event");
            Check.Equal(false, NpcIntentSupport.HasEvent(subject, "private_worry"), "thinking about someone does not notify them");
            Check.Equal(false, NpcIntentSupport.HasEvent(witness, "private_worry"), "nearby actors cannot witness another mind");
            Check.Equal(1, owner.Personality.Memories.Count, "private observation is idempotent");
            foreach (ResidentRecord resident in Session.Get.ResidentRecords.Residents)
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Kind == "private_worry") Check.Equal(owner.PersonalityIdentity, resident.Identity, "private evidence appears in its owner's chronicle only");
        });
    }
}
