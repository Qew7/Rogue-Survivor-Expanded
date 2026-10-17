using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class BaseRobberyDiscoveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/base-robbery-discovery", () => TownScenarioFactory.Arena(4922,
            "...#...", "...#...", "...#...", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 5, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            XpdBase claim = new XpdBase(owner, new[] { new Point(2, 1), new Point(2, 2),
                new Point(2, 3), new Point(3, 3), new Point(4, 3), new Point(5, 3),
                new Point(5, 2), new Point(5, 1) });
            claim.SetFoodRoom(new Rectangle(2, 1, 1, 1));
            world.Map.AddXpdBase(claim);
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(food, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), food);
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_robbed"),
                "absent owner is not told about a theft at once");
            Check.Equal(1, claim.UnnoticedLosses.Count, "unwitnessed theft leaves an undiscovered loss at the base");
            Check.Equal(null, player.Personality.Person(thief.PersonalityIdentity),
                "the hidden thief has no reputation effect before discovery");
            world.Game.DoMoveActor(thief, new Location(world.Map, new Point(2, 2)));
            Check.Equal(1, claim.UnnoticedLosses.Count, "the intruder cannot discover the owner's missing supplies");
            world.Game.DoMoveActor(thief, new Location(world.Map, new Point(2, 1)));

            Guid ownerId = owner.PersonalityIdentity;
            Guid thiefId = thief.PersonalityIdentity;
            Guid playerId = player.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "base-robbery-" + Guid.NewGuid().ToString("N"));
            ScenarioWorld active;
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                active = NpcIntentSupport.Restore(world, loaded);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            Map map = active.Map;
            owner = NpcIntentSupport.Find(map, ownerId);
            thief = NpcIntentSupport.Find(map, thiefId);
            player = NpcIntentSupport.Find(map, playerId);
            claim = map.XpdBaseAt(new Point(2, 1));
            Check.Equal(1, claim.UnnoticedLosses.Count, "undiscovered loss survives saving and loading");
            Check.Equal(3, claim.UnnoticedLosses[0].Units, "actual missing quantity survives loading");
            active.Game.DoMoveActor(owner, new Location(map, new Point(5, 2)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(5, 3)));
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_robbed"),
                "wall still hides the empty storage while the owner approaches");
            active.Game.DoMoveActor(owner, new Location(map, new Point(4, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(3, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(2, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(2, 2)));
            NpcFact discovered = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "base_robbed");
            Check.Equal(true, discovered != null, "owner discovers the loss on seeing the storage");
            Check.Equal(3, discovered.Units, "discovery reports the real number of missing units");
            Check.Equal(Guid.Empty, discovered.OtherId, "discovery does not invent an unseen thief");
            Check.Equal(0, claim.UnnoticedLosses.Count, "the discovered loss is consumed once");
            Check.Equal(false, map.HasUnnoticedBaseLosses, "map's movement guard clears after discovery");
            active.Game.DoMoveActor(owner, new Location(map, new Point(2, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(2, 2)));
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "base_robbed").Count,
                "returning to the same storage cannot announce the robbery twice");
            Check.Equal(true, NpcConversation.ReportSentence(active.Game, owner, discovered).Contains("base robbed of 3 units of food"),
                "owner can report the anonymous robbery and supply damage");
            Actor listener = NpcIntentSupport.Actor(active, "listener", 2, 3);
            Check.Equal(true, active.Try(new ActionNpcTell(owner, active.Game, listener, discovered)),
                "owner passes discovered damage through the existing rumor action");
            Check.Equal(NpcKnowledgeSource.Told,
                listener.Personality.Knowledge.Facts.Find(f => f.EventId == discovered.EventId).Source,
                "listener keeps the account as hearsay");
            player.Personality.Knowledge.Facts.Clear();
            bool broadcast = false;
            for (int slot = 25; slot <= 36; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(active.Game, "GetRadioProgram", 3, slot);
                if (program.Facts == null || Array.Find(program.Facts, f => f.EventId == discovered.EventId) == null) continue;
                Check.Equal(true, program.Text.Contains("robbed of 3 units of food") &&
                    !program.Text.Contains("base robbed"),
                    "crime station airs the discovered loss without the thief's name");
                broadcast = true;
                break;
            }
            Check.Equal(true, broadcast, "crime station eventually airs the discovered robbery");
            Check.Equal(null, player.Personality.Person(thiefId), "anonymous robbery cannot harm a personal reputation");
        });
    }
}
