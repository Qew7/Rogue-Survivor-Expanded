using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalBeliefsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-beliefs", () => TownScenarioFactory.Arena(4660, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "lawful");
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 2, 1);
            Actor corroborator = NpcIntentSupport.Actor(world, "corroborator", 0, 1);
            Actor target = NpcIntentSupport.Actor(world, "target", 1, 0), victim = NpcIntentSupport.Actor(world, "victim", 2, 0);
            var report = new NpcFact { EventId = 600, Kind = "attack", SubjectId = victim.PersonalityIdentity, OtherId = target.PersonalityIdentity,
                SubjectName = victim.UnmodifiedName, OtherName = target.UnmodifiedName, Place = victim.Location, Confidence = 60 };
            speaker.Personality.Knowledge.Learn(report);
            Check.Equal(true, world.Try(new ActionNpcTell(speaker, world.Game, owner, report)), "real conversation delivers uncertain evidence");
            NpcKnownPerson known = owner.Personality.Knowledge.Person(target.PersonalityIdentity);
            Check.Equal(40, known.ViolationConfidence, "uncertainty belongs to the wrongdoing claim");
            Check.Equal(null, NpcIntentSupport.Intent(owner, "confront_reported_aggressor"), "weak confidence lowers expected benefit below admission");
            target.MarkAsAgressorOf(owner);
            owner.MarkAsSelfDefenceFrom(target);
            var sight = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, owner, sight.Sense(world.Game, owner));
            Check.Equal(100, known.ThreatConfidence, "current hostile presence establishes credible danger");
            Check.Equal(40, known.ViolationConfidence, "seeing the person does not confirm their reported crime");
            target.RemoveAggressorOf(owner);
            owner.RemoveSelfDefenceFrom(target);
            NpcKnowledgeSystem.Perceive(world.Game, owner, sight.Sense(world.Game, owner));
            NpcFact stronger = report.Retell(corroborator.PersonalityIdentity, 0, 90);
            corroborator.Personality.Knowledge.Learn(stronger);
            Check.Equal(true, world.Try(new ActionNpcTell(corroborator, world.Game, owner, stronger)), "another actual source supplies stronger evidence");
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "confronted"), "credible belief now motivates a real warning");
            Check.Equal(0, known.Violation, "the communicated boundary addresses the known incident");
            owner.Personality.Opinion(speaker.PersonalityIdentity, speaker.UnmodifiedName).AdjustSocial(trust: 100);
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, owner, speaker, stronger), "more trusted retelling updates retained evidence");
            Check.Equal(0, known.Violation, "retelling an addressed incident does not reopen the same obligation");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "attack"), "reports never fabricate personally witnessed violence");
        });
    }
}
