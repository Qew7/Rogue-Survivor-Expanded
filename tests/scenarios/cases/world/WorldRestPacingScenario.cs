using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class WorldRestPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/rest-paces-distant-districts", () => TownScenarioFactory.Arena(4991,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(2);
            city[0, 0] = world.Map.District;
            city[1, 1] = MakeDistrict(world, new Point(1, 1), 4992);
            Session.Get.World = city;
            Map distant = city[1, 1].EntryMap;
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 3, 1, "sociable");
            world.Map.RemoveActor(speaker);
            distant.PlaceActorAt(speaker, new Point(1, 1));
            PersonalitySystem.Report(world.Game, new SignificantEvent("building_explored", speaker, null,
                distant, speaker.Location.Position, 0, storyId: "paced-event"));
            NpcFact fact = speaker.Personality.Knowledge.Facts.Find(f => f.Kind == "building_explored");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 4, 1);
            world.Map.RemoveActor(listener);
            distant.PlaceActorAt(listener, new Point(2, 1));

            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 10;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, true);
            typeof(RogueGame).GetField("m_PlayerLongWaitEnd", fields).SetValue(world.Game, new WorldTime(15));
            Advance(world, player, flags, active);
            Check.Equal(11, Session.Get.WorldTime.TurnCounter, "player turn advances normally");
            Check.Equal(3, distant.LocalTime.TurnCounter, "first wait spreads older district turns");
            for (int turn = 12; turn <= 15; turn++) Advance(world, player, flags, active);
            Check.Equal(15, distant.LocalTime.TurnCounter, "all district turns finish at the natural wait end");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f =>
                f.EventId == fact.EventId && f.Source == NpcKnowledgeSource.Told),
                "rumor still reaches another NPC during paced catch-up");

            city[1, 0] = MakeDistrict(world, new Point(1, 0), 4993);
            typeof(RogueGame).GetField("m_IsPlayerLongWaitForcedStop", fields).SetValue(world.Game, true);
            Advance(world, player, flags, active);
            Check.Equal(16, city[1, 0].EntryMap.LocalTime.TurnCounter,
                "interrupted wait finishes all remaining district turns");

            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, false);
            typeof(RogueGame).GetField("m_IsPlayerLongWaitForcedStop", fields).SetValue(world.Game, false);
            city[0, 1] = MakeDistrict(world, new Point(0, 1), 4997);
            Advance(world, player, flags, active);
            Check.Equal(17, city[0, 1].EntryMap.LocalTime.TurnCounter,
                "a single wait still finishes its entire district backlog");
        });
    }

    static District MakeDistrict(ScenarioWorld world, Point position, int seed)
    {
        District district = new District(position, DistrictKind.RESIDENTIAL);
        Map map = new Map(seed, "distant", 5, 3);
        Map sewers = new Map(seed + 100, "sewers", 5, 3);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++)
        {
            map.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            sewers.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
        }
        district.EntryMap = map;
        district.SewersMap = sewers;
        return district;
    }

    static void Advance(ScenarioWorld world, Actor player, Type flags, object active)
    {
        player.ActionPoints = Rules.BASE_ACTION_COST;
        world.Game.DoWait(player);
        Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
    }
}
