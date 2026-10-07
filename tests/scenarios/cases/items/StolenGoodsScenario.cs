using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
static class StolenGoodsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/stolen-goods", () => TownScenarioFactory.Arena(5941,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1, "lawful", "kind");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            thief.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            XpdBase claim = new XpdBase(owner, new[] { new Point(2, 1), new Point(3, 1), new Point(4, 1) });
            world.Map.AddXpdBase(claim);
            ItemFood clean = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            thief.Inventory.AddAll(clean);
            ItemFood stolen = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(stolen, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), stolen);
            Check.Equal(true, stolen.IsStolen, "taking without permission marks the actual item");
            Check.Equal(owner.PersonalityIdentity, stolen.StolenFromLeaderId, "item remembers its victim");
            Check.Equal(2, thief.Inventory.CountItems, "clean and stolen food do not merge");
            NpcFact theft = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(theft.StoryId, stolen.TheftStoryId, "provenance follows the theft story");
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 6, 1);
            int opinionBefore = owner.Personality.Person(thief.PersonalityIdentity).Feeling;
            world.Game.DoGiveItemTo(thief, owner, stolen);
            NpcFact passed = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "stolen_goods_passed");
            Check.Equal(true, passed != null, "victim recognizes goods received from the holder");
            Check.Equal(theft.StoryId, passed.StoryId, "transfer continues the same story");
            Check.Equal(true, owner.Personality.Person(thief.PersonalityIdentity).Feeling < opinionBefore,
                "victim judges the known holder according to loyalty and traits");
            Check.Equal(false, stranger.Personality.Knowledge.Facts.Exists(f => f.Kind == "stolen_goods_passed"),
                "a stranger does not learn ownership from the item alone");
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 0, 1, "lawful", "kind");
            Actor rebel = NpcIntentSupport.Actor(world, "rebel", 7, 1, "rebellious", "cruel");
            lawful.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            rebel.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, lawful, owner, passed),
                "lawful listener hears the provenance account");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, rebel, owner, passed),
                "rebellious listener hears the same account");
            Check.Equal(true, lawful.Personality.Person(thief.PersonalityIdentity).Feeling < 0 &&
                rebel.Personality.Person(thief.PersonalityIdentity).Feeling > 0,
                "traits give opposite judgments of the known holder");
            Actor member = NpcIntentSupport.Actor(world, "member", 5, 2);
            owner.AddFollower(member);
            Check.Equal(stolen.StolenFromGroupId, member.SocialGroup.Identity,
                "the marker identifies the permanent victim group");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, member, owner, passed),
                "a group member can hear of its own stolen goods later");
            Check.Equal(true, member.Personality.Person(thief.PersonalityIdentity).Feeling < 0,
                "membership alone makes the holder's conduct personal");
            ItemFood second = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(second, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), second);
            world.Game.DoDropItem(thief, second);
            world.Game.DoTakeItem(owner, thief.Location.Position, second);
            NpcFact found = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "stolen_goods_found");
            Check.Equal(true, found != null && found.StoryId == second.TheftStoryId,
                "picking abandoned goods reveals a linked discovery without naming a new thief");
            foreach (NpcFact part in owner.Personality.Knowledge.Facts)
                if (part.StoryId == theft.StoryId &&
                    (part.Kind == "base_theft" || part.Kind == "stolen_goods_passed" || part.Kind == "stolen_goods_found"))
                    NpcKnowledgeSystem.Hear(world.Game, stranger, owner, part);
            NpcFact heardFind = stranger.Personality.Knowledge.Facts.Find(f => f.EventId == found.EventId);
            Check.Equal(true, NpcConversation.Chapter(world.Game, stranger, null, heardFind).Count >= 3,
                "theft, handoff and discovery form one chapter for spoken rumors");
            bool aired = false;
            for (int slot = 1; slot <= 24; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, slot);
                if (program.Facts != null && Array.Exists(program.Facts, f => f.EventId == found.EventId))
                { aired = program.Text.Contains("found") && program.Text.Contains("stolen from"); break; }
            }
            Check.Equal(true, aired, "gang station can air the linked stolen-goods chapter");
            Actor player = NpcIntentSupport.Player(world, 7, 2);
            player.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName).Attachment = 30;
            player.Personality.AddTrait(new TraitInstance("timid"));
            int sanity = player.Sanity;
            Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                3, world.Map, player.Location.Position, null);
            Check.Equal(true, player.Sanity < sanity,
                "a timid friend of the victims is shaken by the stolen-goods broadcast");
            Actor trader = NpcIntentSupport.Actor(world, "trader", 5, 0);
            trader.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            ItemFood tradedLoot = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(tradedLoot, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), tradedLoot);
            ItemFood payment = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            trader.Inventory.AddAll(payment);
            Check.Call(world.Game, "SwapActorItems",
                new[] { typeof(Actor), typeof(Item), typeof(Actor), typeof(Item) },
                thief, tradedLoot, trader, payment);
            Check.Equal(true, trader.Inventory.Contains(tradedLoot) &&
                trader.Personality.Knowledge.Facts.Exists(f => f.Kind == "stolen_goods_passed" &&
                    f.StoryId == tradedLoot.TheftStoryId),
                "a completed trade retains provenance and creates a linked handoff rumor");
            Actor permitted = NpcIntentSupport.Actor(world, "permitted", 1, 1);
            permitted.Personality.Permit(new NpcSupplyPermission {
                Grantor = owner.PersonalityIdentity, Place = new Location(world.Map, new Point(2, 1)),
                Resource = "food", Units = 1, ExpiresTurn = 100, CauseId = 41 });
            ItemFood allowed = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(allowed, new Point(2, 1));
            world.Game.DoTakeItem(permitted, new Point(2, 1), allowed);
            Check.Equal(false, allowed.IsStolen, "taking fully permitted food leaves it unmarked");

            ItemFood corpseLoot = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            corpseLoot.IsUnique = true;
            world.Map.DropItemAt(corpseLoot, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), corpseLoot);
            Session.Get.GamePreset.Corpses = true;
            world.Game.KillActor(null, thief, "scenario", true);
            Check.Equal(true, world.Map.GetCorpsesAt(new Point(2, 1)) != null,
                "death leaves a corpse beside the dropped stolen goods");
            world.Game.DoTakeItem(owner, new Point(2, 1), corpseLoot);
            Check.Equal(2, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "collecting stolen goods at a corpse creates a second discovery");

            Guid ownerId = owner.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "stolen-goods-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor restoredOwner = NpcIntentSupport.Find(restored.Map, ownerId);
                Item kept = restoredOwner.Inventory.GetFirstByModel(world.Game.GameItems.CANNED_FOOD);
                Check.Equal(true, kept.IsStolen && kept.StolenFromLeaderId == ownerId,
                    "victim identity survives save and load with the item");
                Check.Equal(true, restoredOwner.Personality.Knowledge.Facts.Exists(f => f.Kind == "stolen_goods_passed" && f.ItemId == kept.StoryIdentity),
                    "linked transfer rumor survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
