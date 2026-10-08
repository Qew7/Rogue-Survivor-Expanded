using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class BaseRaidDiscoveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/base-raid-discovery", () => TownScenarioFactory.Arena(4923,
            "...#...", "...#...", "...#...", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_CORPSES_INFECTION);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor owner = NpcIntentSupport.Actor(world, "owner", 5, 1);
            Actor member = NpcIntentSupport.Actor(world, "member", 2, 1);
            Actor killer = NpcIntentSupport.Actor(world, "killer", 2, 2);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            owner.AddFollower(member);
            XpdBase claim = new XpdBase(owner, new[] { new Point(2, 1), new Point(2, 2),
                new Point(2, 3), new Point(3, 3), new Point(4, 3), new Point(5, 3),
                new Point(5, 2), new Point(5, 1) });
            world.Map.AddXpdBase(claim);
            world.Game.KillActor(killer, member, "scenario", true);
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_raid"),
                "owner behind a wall does not know about the fatal raid immediately");
            Check.Equal(1, claim.UnnoticedLosses.Count, "unwitnessed fatal raid waits for corpse discovery");
            Check.Equal(member, claim.UnnoticedLosses[0].Victim, "pending casualty retains the actual victim");
            Check.Equal(true, world.Map.GetCorpsesAt(new Point(2, 1)) != null,
                "a real corpse can provide evidence of the raid");
            world.Place(killer, 0, 0);

            Guid ownerId = owner.PersonalityIdentity;
            Guid memberId = member.PersonalityIdentity;
            Guid killerId = killer.PersonalityIdentity;
            Guid playerId = player.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "base-raid-" + Guid.NewGuid().ToString("N"));
            ScenarioWorld active;
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                active = NpcIntentSupport.Restore(world, BinarySaveStore.Load<Session>(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            Map map = active.Map;
            owner = NpcIntentSupport.Find(map, ownerId);
            player = NpcIntentSupport.Find(map, playerId);
            claim = map.XpdBaseAt(new Point(2, 1));
            Check.Equal(memberId, claim.UnnoticedLosses[0].Victim.PersonalityIdentity,
                "saved pending raid still identifies the dead group member");
            active.Game.DoMoveActor(owner, new Location(map, new Point(5, 2)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(5, 3)));
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_raid"),
                "wall still hides the body as the owner approaches");
            active.Game.DoMoveActor(owner, new Location(map, new Point(4, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(3, 3)));
            active.Game.DoMoveActor(owner, new Location(map, new Point(2, 3)));
            NpcFact discovered = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "base_raid");
            Check.Equal(true, discovered != null, "owner discovers the raid by seeing the member's corpse");
            Check.Equal(memberId, discovered.OtherId, "discovered account names the actual victim");
            Check.Equal(0, claim.UnnoticedLosses.Count, "corpse discovery is reported once");
            Check.Equal(true, NpcConversation.ReportSentence(active.Game, owner, discovered).Contains("base was raided"),
                "the survivor can describe the raid without identifying a killer");
            Actor listener = NpcIntentSupport.Actor(active, "listener", 2, 2);
            Check.Equal(true, active.Try(new ActionNpcTell(owner, active.Game, listener, discovered)),
                "discoverer shares the fatal raid through the existing rumor action");
            player.Personality.Knowledge.Facts.Clear();
            bool broadcast = false;
            for (int slot = 1; slot <= 12; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(active.Game, "GetRadioProgram", 3, slot);
                if (program.Facts != null && Array.Find(program.Facts, f => f.EventId == discovered.EventId) != null)
                { broadcast = program.Text.Contains("was raided") && program.Text.Contains("was killed") &&
                    !program.Text.Contains("base was raided"); break; }
            }
            Check.Equal(true, broadcast, "crime station airs the discovered fatal raid");
            Check.Equal(null, player.Personality.Person(killerId),
                "a corpse does not reveal the unseen killer's identity");
        });
    }
}
