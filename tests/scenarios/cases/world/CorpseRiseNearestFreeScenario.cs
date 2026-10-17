using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class CorpseRiseNearestFreeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/corpse-rise-nearest-free", () => TownScenarioFactory.Arena(7997,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_CORPSES_INFECTION);
            preset.Infection = false;
            preset.CorpseBaseRiseChance = 100;
            preset.CorpseRiseChance = 500;
            Session.Get.GamePreset = preset;
            Map map = world.Map;
            map.LocalTime.TurnCounter = 600; // Night, beyond the six-hour rise delay.

            Actor occupied = NewActor(world, "occupied");
            map.PlaceActorAt(occupied, new Point(4, 4));
            Actor dead = NewActor(world, "dead");
            dead.IsDead = true;
            Corpse corpse = new Corpse(dead, 30, 30, 420, 0, 1);
            map.AddCorpseAt(corpse, new Point(0, 4));
            dead.Location = occupied.Location; // Reproduce the reported stale actor coordinates.
            Check.Equal(100, world.Game.Rules.CorpseZombifyChance(corpse, map.LocalTime),
                "corpse is guaranteed to rise in this turn");
            Check.Call(world.Game, "AdvanceCorpsesAndInfection", map);
            Check.Equal(false, map.HasCorpse(corpse), "raised corpse is removed");
            Check.Equal(true, map.GetActorAt(0, 4).Model.Abilities.IsUndead,
                "zombie rises at the corpse, not at stale actor coordinates");
            Check.Same(occupied, map.GetActorAt(4, 4), "occupant is preserved");

            Actor blocker = NewActor(world, "blocker");
            map.PlaceActorAt(blocker, new Point(2, 2));
            Actor secondDead = NewActor(world, "second dead");
            secondDead.IsDead = true;
            Corpse blockedCorpse = new Corpse(secondDead, 30, 30, 420, 0, 1);
            map.AddCorpseAt(blockedCorpse, new Point(2, 2));
            Check.Call(world.Game, "AdvanceCorpsesAndInfection", map);
            Check.Equal(false, map.HasCorpse(blockedCorpse), "occupied corpse can rise nearby");
            Check.Same(blocker, map.GetActorAt(2, 2), "blocked tile retains its occupant");
            Actor nearbyZombie = null;
            foreach (Actor actor in map.Actors)
                if (actor.Model.Abilities.IsUndead && actor.Location.Position != new Point(0, 4))
                    nearbyZombie = actor;
            Check.Equal(true, nearbyZombie != null, "a second zombie was created");
            Point risenAt = nearbyZombie.Location.Position;
            Check.Equal(true, Math.Max(Math.Abs(risenAt.X - 2), Math.Abs(risenAt.Y - 2)) <= 1,
                "nearest free tile is one step from corpse");
            Check.Equal(true, map.GetActorAt(risenAt).Model.Abilities.IsUndead,
                "nearby rise creates an undead actor");

            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                if (map.GetActorAt(x, y) == null)
                    map.PlaceActorAt(NewActor(world, "filler"), new Point(x, y));
            Actor thirdDead = NewActor(world, "third dead");
            thirdDead.IsDead = true;
            Corpse fullCorpse = new Corpse(thirdDead, 30, 30, 420, 0, 1);
            map.AddCorpseAt(fullCorpse, new Point(2, 2));
            int actors = map.CountActors;
            Check.Call(world.Game, "AdvanceCorpsesAndInfection", map);
            Check.Equal(true, map.HasCorpse(fullCorpse), "corpse waits when no tile is free");
            Check.Equal(actors, map.CountActors, "full map creates no overlapping zombie");
        });
    }

    static Actor NewActor(ScenarioWorld world, string name)
    {
        return new Actor(world.Game.GameActors.MaleCivilian,
            world.Game.GameFactions.TheCivilians, name, false, false, 0);
    }
}
