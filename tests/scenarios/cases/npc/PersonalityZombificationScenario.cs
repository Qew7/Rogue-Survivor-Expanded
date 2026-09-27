using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityZombificationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-zombification", () => TownScenarioFactory.Arena(4528,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            leader.Personality = new PersonalityState();
            leader.Personality.AddTrait(new TraitInstance("fearful"));
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            Actor hidden = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "hidden", false, false, 0);
            hidden.Personality = new PersonalityState();
            Actor unrelated = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "unrelated", false, false, 0);
            unrelated.Personality = new PersonalityState();
            world.Place(leader, 1, 1);
            world.Place(follower, 2, 1);
            world.Place(hidden, 5, 1);
            world.Place(unrelated, 0, 1);
            leader.AddFollower(follower);

            world.Game.KillActor(null, follower, "scenario", false);
            Check.Equal(null, follower.Leader, "death removes follower relationship");
            Check.Equal(true, leader.Personality.Events[0].RelatedToSubject,
                "death journal preserves the former relationship");
            for (int turn = 1; turn <= 40; turn++)
                leader.Personality.Remember(new ObservedEvent("raid", turn, null, null, false));
            Check.Equal(32, leader.Personality.Events.Count,
                "heavy events evict the earlier death observation");
            Check.Equal("raid", leader.Personality.Events[0].Kind,
                "death observation has left the bounded journal");
            Actor zombie = (Actor)Check.Call(world.Game, "Zombify",
                new[] { typeof(Actor), typeof(Actor), typeof(bool) }, null, follower, false);
            Check.Equal(true, zombie.Model.Abilities.IsUndead, "real zombification creates an undead actor");
            Check.Same(zombie, world.Map.GetActorAt(new Point(2, 1)),
                "zombie appears at the former follower's position");
            Check.Equal(2, leader.Personality.Memories.Count,
                "leader remembers both follower death and later zombification");
            Check.Equal("zombified_friend", leader.Personality.Memories[1].Id,
                "former relationship still triggers the zombification memory");
            Check.Equal(0, hidden.Personality.Memories.Count,
                "wall blocks unrelated observer from both memories");
            Check.Equal(0, unrelated.Personality.Memories.Count,
                "visible unrelated observer does not gain a companion memory");

            int due = 0;
            foreach (MemoryInstance memory in leader.Personality.Memories)
                due = System.Math.Max(due, memory.ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, leader.Personality.HasTrait("panic_attacks"),
                "fearful leader resolves the zombification into panic attacks");
        });
    }
}
