using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ProtectionMissingFactionScenario
{
    sealed class MissingFactionDB : FactionDB
    {
        readonly FactionDB original;
        readonly int missing;
        public MissingFactionDB(FactionDB original, int missing) { this.original = original; this.missing = missing; }
        public override Faction this[int id] { get { return id == missing ? null : original[id]; } }
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/protection-missing-faction", () => TownScenarioFactory.Arena(4828,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "officer", 1, 1, "protective", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 1);
            Actor aggressor = NpcIntentSupport.Actor(world, "aggressor", 2, 2);
            owner.Faction = world.Game.GameFactions.ThePolice;
            victim.Faction = world.Game.GameFactions.ThePolice;
            aggressor.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("hit"), 1000, 1, 0, 0);
            world.Game.DoMeleeAttack(aggressor, victim);
            Check.Equal(true, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "attack" &&
                f.SubjectId == victim.PersonalityIdentity && f.OtherId == aggressor.PersonalityIdentity),
                "owner witnessed a real attack on a same-faction resident");
            var module = new ConflictResolutionModule();
            var context = new NpcGoalContext(world.Game, owner, world.Game.NpcContent);
            var normal = new NpcGoalOffers(context);
            module.Evaluate(context, normal);
            Check.Equal(true, normal.Candidates.Exists(c => c.Capability.Id == "defend_person"),
                "security policy can propose protection for a fellow faction member");
            FactionDB original = Models.Factions;
            try
            {
                Models.Factions = new MissingFactionDB(original, owner.Faction.ID);
                var missing = new NpcGoalOffers(context);
                module.Evaluate(context, missing);
                Check.Equal(false, missing.Candidates.Exists(c => c.Capability.Id == "defend_person"),
                    "unavailable faction data cannot create a false policy-based protection goal");
            }
            finally { Models.Factions = original; }
        });
    }
}
