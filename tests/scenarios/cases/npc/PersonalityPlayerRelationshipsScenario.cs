using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityPlayerRelationshipsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-player-relationships", () => TownScenarioFactory.Arena(4555,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            Actor attacker = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "attacker", true, false, 0);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "leader", true, false, 0);
            world.Place(player, 1, 1);
            world.Place(attacker, 2, 1);
            world.Place(leader, 3, 1);
            world.SetPlayer(player);
            leader.AddFollower(attacker);
            Check.Equal(null, player.Personality, "player does not receive random NPC traits");
            player.IsSleeping = true;
            PersonalitySystem.Report(world.Game, new SignificantEvent("murder", leader, attacker,
                world.Map, leader.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(null, player.Personality, "sleeping player learns no unwitnessed relationship");
            player.IsSleeping = false;
            SignificantEvent attack = new SignificantEvent("attack", player, attacker,
                world.Map, player.Location.Position, world.Map.LocalTime.TurnCounter);
            PersonalitySystem.Report(world.Game, attack);
            Check.Equal(true, player.Personality != null, "direct event starts the player's journal");
            Check.Equal(0, player.Personality.Traits.Count, "player gets no starting NPC traits");
            Check.Equal(-35, player.Personality.Person(attacker.PersonalityIdentity).Feeling,
                "player remembers attacker personally");
            Check.Equal(true, player.Personality.Group(leader.PersonalityIdentity) != null,
                "player remembers the attacker's leader group");
            Check.Equal(true, player.Personality.Faction(attacker.Faction.ID) != null,
                "player remembers the attacker's faction");
            IList<string> lines = (IList<string>)Check.Call(world.Game, "RelationshipLines",
                new[] { typeof(Actor) }, player);
            string visible = String.Join(" ", new List<string>(lines).ToArray());
            Check.Equal(true, visible.Contains("attacker: hostile"), "person appears in journal");
            Check.Equal(true, visible.Contains("leader's group: wary"), "group appears by leader name");
            Check.Equal(true, visible.Contains(attacker.Faction.Name + ": wary"),
                "faction appears in journal");
            Check.Equal(false, visible.Contains("Survived an attack"),
                "relationship screen does not expose memory content");
            PersonalitySystem.Report(world.Game, attack);
            Check.Equal(2, player.Personality.Person(attacker.PersonalityIdentity).Memories.Count,
                "duplicate event does not inflate player's relationship");
            foreach (MemoryInstance pending in player.Personality.Memories)
                world.Map.LocalTime.TurnCounter = Math.Max(world.Map.LocalTime.TurnCounter, pending.ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(0, player.Personality.Memories.Count, "player's pending memory resolves");
            Check.Equal(0, player.Personality.Traits.Count, "journal resolution grants no NPC trait");
            Check.Equal("none", player.Personality.Person(attacker.PersonalityIdentity).Memories[0].OutcomeId,
                "resolved event remains in personal history without NPC outcome");
            player.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, leader.Inventory.AddAll(food), "helper carries food");
            world.Game.DoGiveItemTo(leader, player, food);
            Check.Equal(30, player.Personality.Person(leader.PersonalityIdentity).Feeling,
                "needed gift creates a positive personal relationship for the player");
            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            int count = player.Personality.Memories.Count;
            PersonalitySystem.Report(world.Game, attack);
            Check.Equal(count, player.Personality.Memories.Count, "disabled preset records no new player memory");
        });
    }
}
