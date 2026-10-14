using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcImmediateEmotionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/immediate-emotion", () => TownScenarioFactory.Arena(8295,
            "...........", "...........", "..........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor kind = NpcIntentSupport.Actor(world, "kind leader", 1, 1, "kind");
            Actor ruthless = NpcIntentSupport.Actor(world, "ruthless leader", 7, 1, "cruel", "maniac");
            ruthless.Faction = world.Game.GameFactions.ThePsychopaths;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 4, 1);
            Actor kindFollower = NpcIntentSupport.Actor(world, "kind follower", 1, 2);
            Actor ruthlessFollower = NpcIntentSupport.Actor(world, "ruthless follower", 7, 2);
            kind.AddFollower(kindFollower); kindFollower.TrustInLeader = Rules.TRUST_MAX;
            ruthless.AddFollower(ruthlessFollower); ruthlessFollower.TrustInLeader = Rules.TRUST_MAX;

            int baseline = PersonalitySystem.Disposition(kind, NpcPersonalityAxis.Patience, world.Game.NpcContent);
            kind.FoodPoints = 0; kind.SleepPoints = 0; kind.Sanity = 0;
            Check.Equal(baseline - 32,
                PersonalitySystem.Disposition(kind, NpcPersonalityAxis.Patience, world.Game.NpcContent),
                "hunger, sleep and sanity alter patience without writing permanent experience");
            Check.Equal(0, kind.Personality.ExperienceValue(NpcPersonalityAxis.Patience),
                "temporary needs do not become saved personality experience");
            int scarce = PersonalitySystem.Disposition(kind, NpcPersonalityAxis.Scarcity, world.Game.NpcContent);
            NpcIntentSupport.Food(world, kind, 1);
            Check.Equal(scarce - 10,
                PersonalitySystem.Disposition(kind, NpcPersonalityAxis.Scarcity, world.Game.NpcContent),
                "food carried while hungry eases temporary resource anxiety");
            kind.FoodPoints = world.Game.Rules.ActorMaxFood(kind);
            kind.SleepPoints = world.Game.Rules.ActorMaxSleep(kind);
            kind.Sanity = world.Game.Rules.ActorMaxSanity(kind);
            Check.Equal(baseline,
                PersonalitySystem.Disposition(kind, NpcPersonalityAxis.Patience, world.Game.NpcContent),
                "recovering needs restores the same disposition");

            int before = kind.Sanity;
            PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", kind, null,
                world.Map, kind.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(before - 2, kind.Sanity, "direct experience has a small recoverable sanity effect");
            Check.Equal(0, kind.Personality.ExperienceValue(NpcPersonalityAxis.Scarcity),
                "direct experience waits for memory resolution before changing the lasting axis");
            before = kind.Sanity;
            PersonalitySystem.Report(world.Game, new SignificantEvent("kill_human", victim, kind,
                world.Map, victim.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(before - 3, kind.Sanity, "a kind killer reacts more strongly to killing a human");
            before = ruthless.Sanity;
            PersonalitySystem.Report(world.Game, new SignificantEvent("kill_human", victim, ruthless,
                world.Map, victim.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(before, ruthless.Sanity, "a ruthless killer barely reacts");

            before = kind.Sanity;
            world.Game.KillActor(null, kindFollower, "scenario", false);
            Check.Equal(before - 3 * WorldTime.TURNS_PER_HOUR, kind.Sanity,
                "an actual bonded follower death affects a kind leader");
            before = ruthless.Sanity;
            world.Game.KillActor(null, ruthlessFollower, "scenario", false);
            Check.Equal(before - WorldTime.TURNS_PER_HOUR / 4, ruthless.Sanity,
                "a ruthless leader loses little sanity when a bonded follower dies");

            kind.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName).Attachment = 30;
            ruthless.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName).Attachment = 30;
            NpcFact attack = new NpcFact { Kind = "attack", SubjectId = victim.PersonalityIdentity,
                SubjectName = victim.UnmodifiedName };
            before = kind.Sanity;
            Check.Call(world.Game, "ApplyRadioSanity", kind, attack);
            Check.Equal(before - 2, kind.Sanity, "radio news uses a kind listener's real disposition and trait");
            before = ruthless.Sanity;
            Check.Call(world.Game, "ApplyRadioSanity", ruthless, attack);
            Check.Equal(before, ruthless.Sanity, "radio news does not disturb a ruthless listener in the same way");
            NpcFact aid = new NpcFact { Kind = "shared_food", SubjectId = victim.PersonalityIdentity,
                SubjectName = victim.UnmodifiedName };
            before = kind.Sanity;
            Check.Call(world.Game, "ApplyRadioSanity", kind, aid);
            Check.Equal(before + 2, kind.Sanity, "real kind trait strengthens a positive radio reaction");
            before = ruthless.Sanity;
            Check.Call(world.Game, "ApplyRadioSanity", ruthless, aid);
            Check.Equal(before, ruthless.Sanity, "ruthless listener gets no automatic comfort from aid news");
        });
    }
}
