using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryDirectorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-director", () => TownScenarioFactory.Arena(4627, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor target = NpcIntentSupport.Player(world, 2, 1);
            Actor[] actors = { NpcIntentSupport.Actor(world, "one", 1, 1, "loyal"), NpcIntentSupport.Actor(world, "two", 3, 1, "loyal"),
                NpcIntentSupport.Actor(world, "three", 2, 0, "loyal"), NpcIntentSupport.Actor(world, "four", 2, 2, "loyal"),
                NpcIntentSupport.Actor(world, "five", 0, 1, "loyal") };
            var known = new NpcKnownPerson { Id = target.PersonalityIdentity, Name = target.UnmodifiedName, Place = target.Location };
            for (int i = 0; i < 4; i++) Check.Equal(true, NpcStorySystem.StartKnown(actors[i], known, NpcIntentContent.Seek) != null, "director admits bounded local stories");
            Check.Equal(null, NpcStorySystem.StartKnown(actors[4], known, NpcIntentContent.Seek), "fifth concurrent local story is rejected");
            NpcIntent first = actors[0].Personality.Intents[0];
            Check.Equal(true, world.Try(new ActionNpcIntent(actors[0], world.Game, first, target)), "real reunion completes a slot");
            Check.Equal("completed", Session.Get.NpcDirector.Find(first.StoryId).Stage, "completion releases the active slot");
            Check.Equal(true, NpcStorySystem.StartKnown(actors[4], known, NpcIntentContent.Seek) != null, "another story can use the released slot");
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            NpcStoryDirector director = Session.Get.NpcDirector;
            Location cache = new Location(world.Map, new Point(5, 1));
            NpcStory reserved = director.Open("reserved", "group_supplies", actors[0], 0, 900, "reservation-one", cache, actors[0].PersonalityIdentity);
            Check.Equal(true, reserved != null, "known resource and acting role can be reserved");
            Check.Equal(null, director.Open("conflict", "group_supplies", actors[1], 0, 900, "reservation-two", cache, actors[1].PersonalityIdentity), "another story cannot reserve the same resource");
            Check.Equal(null, director.Open("role-conflict", "group_supplies", actors[1], 0, 900, "reservation-three", default(Location), actors[0].PersonalityIdentity), "one reserved collector cannot accept conflicting roles");
            world.Map.LocalTime.TurnCounter = 900; NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(true, director.CanOpen(world.Map, 900, "reservation-two", cache, actors[1].PersonalityIdentity), "deadline releases both reservations");
            for (int i = 0; i < 80; i++)
            { NpcStory story = director.Open("history" + i, "test", actors[0], 0, 2000, "history" + i); director.End(story, "completed", 900); }
            Check.Equal(64, director.Stories.Count, "retained director history is bounded");
            var participants = new System.Collections.Generic.List<Actor>(actors);
            for (int i = 0; i < 4; i++) participants.Add(NpcIntentSupport.Actor(world, "role" + i, i + (i > 1 ? 1 : 0), 0, "loyal"));
            for (int i = 0; i < 8; i++)
                Check.Equal(true, NpcStorySystem.StartKnown(participants[i], known, NpcIntentContent.Seek, storyId: "shared") != null, "eight independent goals bind to one episode");
            Check.Equal(null, NpcStorySystem.StartKnown(participants[8], known, NpcIntentContent.Seek, storyId: "shared"), "ninth role is rejected before creating a goal");
            Check.Equal(8, director.Find("shared").Roles.Count, "role history and admitted intentions agree");
            director = new NpcStoryDirector();
            Location original = actors[0].Location;
            for (int m = 0; m < 4; m++)
            {
                Map map = m == 0 ? world.Map : new Map(4700 + m, "budget" + m, 2, 2);
                actors[0].Location = new Location(map, new Point(0, 0));
                for (int i = 0; i < 4; i++) Check.Equal(true, director.Open("global" + m + ":" + i, "test", actors[0], 0, 2000, "global" + m + ":" + i) != null, "four maps admit sixteen episodes");
            }
            actors[0].Location = new Location(new Map(4704, "overflow", 2, 2), new Point(0, 0));
            Check.Equal(null, director.Open("overflow", "test", actors[0], 0, 2000, "overflow"), "global cap applies even to an otherwise empty map");
            actors[0].Location = original;
            Session.Get.Reset(); Check.Equal(0, Session.Get.NpcDirector.Stories.Count, "a new game does not retain maps or stories from the old world");
        });
    }
}
