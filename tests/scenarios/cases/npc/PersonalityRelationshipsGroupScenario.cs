using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRelationshipsGroupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-relationships-group", () => TownScenarioFactory.Arena(4552,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor victim = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "victim", false, false, 0);
            victim.Personality = new PersonalityState();
            Actor attacker = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "attacker", false, false, 0);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "Alex", true, false, 0);
            Actor mate = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "mate", false, false, 0);
            Actor otherLeader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "Alex", true, false, 0);
            Actor stranger = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "stranger", false, false, 0);
            world.Place(victim, 1, 1);
            world.Place(attacker, 2, 1);
            world.Place(leader, 2, 2);
            world.Place(mate, 3, 1);
            world.Place(otherLeader, 5, 2);
            world.Place(stranger, 5, 1);
            leader.AddFollower(attacker);
            leader.AddFollower(mate);
            otherLeader.AddFollower(stranger);

            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(2, victim.Personality.Memories.Count, "attack starts personal and faction memories");
            Check.Equal(-69, PersonalitySystem.Attitude(victim, attacker),
                "attacker combines personal, group and faction impressions");
            Check.Equal(-34, PersonalitySystem.Attitude(victim, mate),
                "same group shares only the group and faction impressions");
            Check.Equal(-23, PersonalitySystem.Attitude(victim, stranger),
                "other group shares only the faction impression");
            Check.Equal(null, victim.Personality.Group(otherLeader.PersonalityIdentity),
                "leaders with the same name are separate groups");
            Check.Equal(2, victim.Personality.Group(leader.PersonalityIdentity).Memories.Count,
                "group retains its attributed memory");
            Check.Equal(2, victim.Personality.Faction(attacker.Faction.ID).Memories.Count,
                "faction retains its weaker attributed memory");

            leader.RemoveFollower(attacker);
            Check.Equal(-58, PersonalitySystem.Attitude(victim, attacker),
                "former member keeps personal and faction impressions");
            string reason;
            Check.Equal(false, world.Game.Rules.CanActorTakeLead(attacker, victim, out reason),
                "victim refuses the attacker as leader");
            Check.Equal("does not trust this leader", reason,
                "recruitment refusal is caused by remembered treatment");
            otherLeader.AddFollower(attacker);
            Check.Equal(-58, PersonalitySystem.Attitude(victim, attacker),
                "joining a namesake's group does not inherit the old group's reputation");

            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            Check.Equal(0, PersonalitySystem.Attitude(victim, attacker),
                "disabled option suppresses relationship effects");
        });
    }
}
