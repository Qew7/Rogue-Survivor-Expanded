using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class WorldParallelRestExactArrivalsScenario
{
    sealed class ExitAI : ActorController
    {
        readonly Point exit;
        public ExitAI(Point exitPoint) { exit = exitPoint; }
        public override ActorAction GetAction(RogueGame game)
        { return ControlledActor.Location.Map.GetExitAt(exit) != null ?
            (ActorAction)new ActionUseExit(ControlledActor, exit, game) :
            new ActionWait(ControlledActor, game); }
    }

    public static void Register()
    {
        ScenarioRunner.Add("world/parallel-rest-exact-arrivals", () => TownScenarioFactory.Arena(7851,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            preset.ParallelDistantSimulationDuringRest = true;
            Session.Get.GamePreset = preset;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(3);
            city[0, 0] = world.Map.District;
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            {
                if (x == 0 && y == 0) continue;
                var district = new District(new Point(x, y), DistrictKind.RESIDENTIAL);
                var map = new Map(7852 + x * 3 + y, "parallel district", 5, 3);
                var sewers = new Map(7952 + x * 3 + y, "sewers", 5, 3);
                for (int row = 0; row < 3; row++) for (int col = 0; col < 5; col++)
                { map.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
                    sewers.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT); }
                district.EntryMap = map; district.SewersMap = sewers;
                city[x, y] = district;
            }
            Session.Get.World = city;
            Map left = city[0, 2].EntryMap, right = city[2, 2].EntryMap;
            Map destination = city[1, 2].EntryMap;
            Point arrival = new Point(2, 1);
            left.SetExitAt(new Point(5, 1), new Exit(destination, arrival) { IsAnAIExit = true });
            right.SetExitAt(new Point(-1, 1), new Exit(destination, new Point(4, 1)) { IsAnAIExit = true });
            var waves = (System.Collections.Generic.List<System.Collections.Generic.List<int>>)
                typeof(RogueGame).GetMethod("RestSimulationWaves", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { new System.Collections.Generic.List<District> {
                        left.District, right.District }, Session.Get.NpcDirector, world.Map.District });
            Check.Equal(1, waves.Count, "sources with distinct arrival cells can share a wave");
            Actor leader = NpcIntentSupport.Actor(world, "parallel leader", 3, 1, "sociable");
            Actor follower = NpcIntentSupport.Actor(world, "parallel follower", 4, 1);
            Actor rival = NpcIntentSupport.Actor(world, "parallel rival", 1, 2);
            world.Map.RemoveActor(leader); left.PlaceActorAt(leader, new Point(4, 1));
            world.Map.RemoveActor(follower); left.PlaceActorAt(follower, new Point(3, 1));
            world.Map.RemoveActor(rival); right.PlaceActorAt(rival, new Point(0, 1));
            leader.AddFollower(follower);
            right.GetExitAt(new Point(-1, 1)).ToPosition = new Point(3, 1);
            var competingWaves = (System.Collections.Generic.List<System.Collections.Generic.List<int>>)
                typeof(RogueGame).GetMethod("RestSimulationWaves", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { new System.Collections.Generic.List<District> {
                        left.District, right.District }, Session.Get.NpcDirector, world.Map.District });
            Check.Equal(1, competingWaves.Count,
                "a follower must leave the neighboring entry free, so those sources share a wave");
            Check.Equal(true, world.Game.DoLeaveMap(leader, new Point(5, 1), false),
                "leader can use the district exit before the rival moves");
            Check.Same(destination, follower.Location.Map, "follower crosses before the rival");
            Check.Equal(false, follower.Location.Position == new Point(3, 1),
                "follower leaves the still-empty rival entry free");
            destination.RemoveActor(leader); destination.RemoveActor(follower);
            left.PlaceActorAt(leader, new Point(4, 1));
            left.PlaceActorAt(follower, new Point(3, 1));
            ActorController leaderController = leader.Controller;
            leader.Controller = new ExitAI(new Point(5, 1));
            follower.Controller = null;
            rival.Controller = new ExitAI(new Point(-1, 1));
            leader.ActionPoints = rival.ActionPoints = Rules.BASE_ACTION_COST;
            follower.ActionPoints = 0;
            PersonalitySystem.Report(world.Game, new SignificantEvent("building_explored", follower,
                null, left, leader.Location.Position, 0, storyId: "parallel-rumor"));
            NpcFact report = leader.Personality.Knowledge.Facts.Find(f => f.StoryId == "parallel-rumor");
            Check.Equal(true, report != null, "traveler starts with a reportable fact");
            var listener = NpcIntentSupport.Actor(world, "parallel listener", 0, 2);
            world.Map.RemoveActor(listener); destination.PlaceActorAt(listener, new Point(1, 1));
            listener.Controller = null; listener.ActionPoints = 0;
            typeof(RogueGame).GetField("ParallelRestWorkers",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.Game, 4);
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            player.ActionPoints = Rules.BASE_ACTION_COST; world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, city[0, 0], active);
            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => leader.Location.Map == destination && rival.Location.Map == destination,
                5000), "background district turns finish both arrivals");
            Check.Call(world.Game, "StopSimThread", false);
            Check.Same(destination, leader.Location.Map, "leader arrives from the first source");
            Check.Same(destination, follower.Location.Map, "follower crosses with the leader");
            Check.Same(destination, rival.Location.Map, "second source arrives at its distinct entry");
            Check.Equal(arrival, leader.Location.Position, "leader reaches the exact entry");
            Check.Equal(new Point(3, 1), rival.Location.Position, "neighboring entry stays clear for its traveler");
            Check.Equal(false, follower.Location.Position == rival.Location.Position,
                "follower never blocks another district's entry");
            Check.Equal(true, destination.HasActor(leader) && destination.HasActor(follower),
                "first arrival is in the destination map index");
            Check.Equal(true, destination.HasActor(rival), "second arrival is in the destination map index");
            Check.Equal(false, left.Actors.Contains(leader) || left.Actors.Contains(follower),
                "travelers are absent from their source map");
            Check.Equal(true, leader.Personality.Knowledge.Facts.Exists(f => f.EventId == report.EventId),
                "story knowledge survives travel");
            leader.Controller = leaderController;
            var tell = new ActionNpcTell(leader, world.Game, listener, report);
            Check.Equal(false, leader.IsSleeping, "traveler is awake to tell the rumor");
            Check.Equal(true, NpcConversation.EligibleFact(world.Game, leader, report,
                destination.LocalTime.TurnCounter), "traveler still has a tellable rumor");
            Check.Equal(true, ActionNpcTell.CanAddress(world.Game, leader, listener, report),
                "traveler can address the local listener");
            Check.Equal(true, tell.IsLegal(), "arrived NPC can address a local listener");
            tell.Perform();
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f =>
                f.EventId == report.EventId && f.Source == NpcKnowledgeSource.Told &&
                f.StoryId == report.StoryId), "arrived NPC relays the original story");
            rival.Controller = null;
            string path = Path.Combine(Path.GetTempPath(), "parallel-exact-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(true, loaded.World[1, 2].EntryMap.Actors.Any(a => a.Name == leader.Name),
                    "first arrival survives save/load");
                Check.Equal(true, loaded.World[1, 2].EntryMap.Actors.Any(a => a.Name == rival.Name),
                    "second arrival survives save/load");
                Check.Equal(true, loaded.World[1, 2].EntryMap.Actors.Any(a => a.Name == listener.Name &&
                    a.Personality.Knowledge.Facts.Exists(f => f.EventId == report.EventId)),
                    "cross-district rumor survives save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
