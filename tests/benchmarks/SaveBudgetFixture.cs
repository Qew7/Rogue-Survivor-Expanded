using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

// Fixed workload near the profiled day-12 world; actual models and current save APIs.
static class SaveBudgetFixture
{
    public const int Seed = 4680, MapCount = 416, Width = 52, ActorCount = MapCount * 15, SocialCount = MapCount * 5;
    const int Turn = 12 * WorldTime.TURNS_PER_DAY;
    static readonly FieldInfo Identity = typeof(Actor).GetField("m_PersonalityIdentity", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Create()
    {
        Session session = Session.Get; session.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        session.RestoreResidentRecords(new ResidentRecords());
        session.World = new World(4); session.WorldTime.TurnCounter = Turn;
        Random random = new Random(Seed); Map previous = null; int index = 0;
        for (int dx = 0; dx < 4; dx++) for (int dy = 0; dy < 4; dy++)
        {
            District district = new District(new Point(dx, dy), DistrictKind.RESIDENTIAL); session.World[dx, dy] = district;
            for (int n = 0; n < 26; n++)
            {
                Map map = new Map(Seed + index, "Budget map " + index++, Width, Width); map.LocalTime.TurnCounter = Turn;
                for (int x = 0; x < Width; x++) for (int y = 0; y < Width; y++)
                {
                    map.SetTileModelAt(x, y, Models.Tiles[(int)(y > 2 && x > 0 && x % 9 == 0 && y % 7 == 0 ? GameTiles.IDs.WALL_BRICK : GameTiles.IDs.FLOOR_ASPHALT)]);
                    Tile tile = map.GetTileAt(x, y); tile.IsVisited = (x + y + n) % 3 == 0; tile.IsInside = n > 2;
                    if ((x * Width + y + n) % 29 == 0) tile.AddDecoration("budget decoration " + (x + y) % 5);
                }
                if (n == 0) district.EntryMap = map; else district.AddUniqueMap(map);
                if (previous != null) { previous.SetExitAt(Point.Empty, new Exit(map, Point.Empty)); map.SetExitAt(new Point(0, 1), new Exit(previous, Point.Empty)); }
                previous = map;
                var actors = new Actor[15];
                for (int a = 0; a < actors.Length; a++)
                {
                    Actor actor = actors[a] = new Actor(Models.Actors[(int)GameActors.IDs.MALE_CIVILIAN], Models.Factions[(int)GameFactions.IDs.TheCivilians], "Resident " + (index * 15 + a), true, false, 0);
                    byte[] bytes = new byte[16]; random.NextBytes(bytes); Identity.SetValue(actor, new Guid(bytes));
                    actor.Controller = new CivilianAI(); actor.HitPoints = 50; map.PlaceActorAt(actor, new Point(1 + a * 3, 1));
                    actor.Inventory.AddAll(new ItemFood(Models.Items[(int)GameItems.IDs.FOOD_CANNED_FOOD]) { Quantity = 3 });
                    actor.Inventory.AddAll(new ItemMedicine(Models.Items[(int)GameItems.IDs.MEDICINE_MEDIKIT]));
                }
                for (int a = 0; a < 5; a++) Social(session, actors[a], actors[a + 5], map, index * 1000 + a * 100);
            }
        }
        session.CurrentMap = session.World[0, 0].EntryMap;
        Console.WriteLine("SAVE BUDGET fixture maps={0}; tiles={1}; actors={2}; social actors={3}; historical events={4}; day=12", MapCount, MapCount * Width * Width, ActorCount, SocialCount, SocialCount * 32);
    }

    static void Social(Session session, Actor actor, Actor peer, Map map, int eventBase)
    {
        var state = actor.Personality = new PersonalityState();
        foreach (string id in new[] { "kind", "honest", "sociable" }) state.AddTrait(new TraitInstance(id));
        state.Knowledge.See(peer, Turn); state.Knowledge.RememberPlace(new NpcKnownPlace(peer.Location, "food", Turn, 3));
        foreach (string id in new[] { "promise_received", "boundary_defied" })
        {
            var memory = new MemoryInstance(id, Turn - 100, Turn + 200, peer.UnmodifiedName, subjectId: peer.PersonalityIdentity, relatedPersonId: peer.PersonalityIdentity);
            state.AddMemory(memory); state.RememberPerson(peer.PersonalityIdentity, peer.UnmodifiedName, memory, -4);
        }
        state.RememberCommitment(new NpcCommitment { Id = eventBase + 1, Promisor = actor.PersonalityIdentity, Beneficiary = peer.PersonalityIdentity,
            PromisorName = actor.UnmodifiedName, BeneficiaryName = peer.UnmodifiedName, Resource = "food", DueTurn = Turn + 180 });
        state.RememberDispute(new NpcResourceDispute { Place = peer.Location, Other = peer.PersonalityIdentity, Resource = "food", Turn = Turn, CauseId = eventBase + 2 });
        state.Attach(new NpcAttachment { Kind = "place", Place = actor.Location, Weight = 40, Name = map.Name });
        for (int e = 0; e < 32; e++)
        {
            var observed = new ObservedEvent(e % 2 == 0 ? "shared_food" : "boundary_defied", Turn - 320 + e * 10,
                peer.UnmodifiedName, actor.UnmodifiedName, true, subjectId: peer.PersonalityIdentity, otherId: actor.PersonalityIdentity, eventId: eventBase + e + 3);
            state.Remember(observed); session.ResidentRecords.Observe(actor, observed);
            state.Knowledge.Learn(new NpcFact { EventId = observed.EventId, Kind = observed.Kind, SubjectId = peer.PersonalityIdentity,
                SubjectName = peer.UnmodifiedName, Confidence = 90, EventTurn = observed.Turn, Place = peer.Location });
        }
        var generated = new NpcGeneratedGoal { Value = NpcGoalValue.Commitment, SubjectId = peer.PersonalityIdentity,
            Desired = 1, Deficit = 100, Importance = 80, Confidence = 100, Utility = 80, Resource = "food", ObligationId = eventBase + 1,
            Result = (ulong)NpcPlanFact.Delivered, Causes = new long[] { eventBase + 1 } };
        NpcIntent intent = state.StartGeneratedGoal(NpcIntentContent.Promise.Id, actor, state.Knowledge.Person(peer.PersonalityIdentity), generated,
            Turn, 180, 180, eventBase + 1, "budget:" + eventBase);
        intent.Plan = new NpcPlan { Desired = generated.Result };
        intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.GiveFood, Target = peer.PersonalityIdentity, Place = peer.Location });
        session.ResidentRecords.IntentChanged(actor, intent, "started", "performance fixture commitment");
    }

    public static void Validate(Session session)
    {
        int maps = 0, actors = 0, social = 0, events = 0;
        for (int x = 0; x < session.World.Size; x++) for (int y = 0; y < session.World.Size; y++)
            foreach (Map map in session.World[x, y].Maps)
            {
                maps++; Check.Equal(Width, map.Width, "map width survives"); Check.Equal(Width, map.Height, "map height survives");
                Check.Equal((maps - 1) % 26 > 2, map.GetTileAt(0, 0).IsInside, "tile flags survive");
                Check.Equal(false, Object.ReferenceEquals(map.GetTileAt(0, 0), map.GetTileAt(1, 0)), "tiles remain independent objects");
                foreach (Actor actor in map.Actors)
                {
                    actors++; Check.Same(actor, map.GetActorAt(actor.Location.Position), "actor position index reconstructed");
                    if (actor.Personality == null) continue; social++;
                    Check.Equal(3, actor.Personality.Traits.Count, "traits survive"); Check.Equal(2, actor.Personality.Memories.Count, "memories survive");
                    Check.Equal(1, actor.Personality.Commitments.Count, "obligation survives");
                    Check.Same(map, actor.Personality.Attachments[0].Place.Map, "attachment shares its map");
                    Check.Same(map, actor.Personality.Intents[0].Plan.Current.Place.Map, "plan shares its map");
                    Check.Equal(3, NpcIntentSupport.FoodUnits(actor), "inventory units survive");
                }
            }
        foreach (ResidentRecord resident in session.ResidentRecords.Residents)
            foreach (ResidentEntry entry in resident.Entries) if (entry.Kind == "shared_food" || entry.Kind == "boundary_defied") events++;
        Check.Equal(MapCount, maps, "all maps preserved"); Check.Equal(ActorCount, actors, "all actors preserved");
        Check.Equal(SocialCount, social, "all social states preserved"); Check.Equal(SocialCount * 32, events, "complete historical events preserved");
        Check.Equal(Turn, session.WorldTime.TurnCounter, "world clock preserved");
        Check.Same(session.World[0, 0].GetMap(1), session.World[0, 0].EntryMap.GetExitAt(Point.Empty).ToMap, "exit map alias preserved");
    }
}
