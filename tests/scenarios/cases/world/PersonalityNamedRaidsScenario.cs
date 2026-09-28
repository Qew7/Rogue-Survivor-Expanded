using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityNamedRaidsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/personality-named-raids", () => TownScenarioFactory.Arena(4575,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            GameGangs.IDs[] gangs = { GameGangs.IDs.BIKER_HELLS_SOULS, GameGangs.IDs.BIKER_FREE_ANGELS,
                GameGangs.IDs.GANGSTA_CRAPS, GameGangs.IDs.GANGSTA_FLOODS };
            string[] memories = { "hells_souls_raid", "free_angels_raid", "craps_raid", "floods_raid" };
            for (int i = 0; i < gangs.Length; i++)
            {
                bool biker = i < 2;
                Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
                witness.Personality = new PersonalityState();
                Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                    biker ? world.Game.GameFactions.TheBikers : world.Game.GameFactions.TheGangstas,
                    GameGangs.NAMES[(int)gangs[i]] + " leader", true, false, 0);
                leader.GangID = (int)gangs[i];
                leader.Personality = new PersonalityState();
                Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                    leader.Faction, "follower", true, false, 0);
                follower.Personality = new PersonalityState();
                leader.AddFollower(follower);
                world.Place(witness, 1, 1);
                world.Place(leader, 2, 1);
                world.Place(follower, 3, 1);
                Check.Call(world.Game, "NotifyOrderablesAI",
                    new[] { typeof(Map), typeof(RaidType), typeof(Point), typeof(Actor) },
                    world.Map, biker ? RaidType.BIKERS : RaidType.GANGSTA, leader.Location.Position, leader);
                Check.Equal(memories[i], witness.Personality.Memories[0].Id,
                    "real raid notification identifies the named gang");
                Check.Equal(-20, witness.Personality.Person(leader.PersonalityIdentity).Feeling,
                    "raid is attributed to its leader");
                Check.Equal(1, witness.Personality.Group(leader.PersonalityIdentity).Memories.Count,
                    "raid is retained in the leader's group history");
                Check.Equal(0, leader.Personality.Memories.Count, "leader does not fear their own raid");
                Check.Equal(0, follower.Personality.Memories.Count, "fellow gang member does not fear their own raid");
                world.Map.LocalTime.TurnCounter = witness.Personality.Memories[0].ResolveTurn;
                PersonalitySystem.ResolveDue(world.Game, world.Map);
                Check.Equal(1, witness.Personality.Group(leader.PersonalityIdentity).Memories.Count,
                    "group remembers the raid after its memory resolves");
                world.Map.RemoveActor(witness);
                world.Map.RemoveActor(leader);
                world.Map.RemoveActor(follower);
            }
        });
    }
}
