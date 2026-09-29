using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum NpcPerceptionKind { Person, Items, Surroundings }
    sealed class NpcPerceptionContext
    {
        public readonly RogueGame Game;
        public readonly Actor Owner, Person;
        public readonly Inventory Items;
        public readonly Location Place;
        public readonly int Turn, Risk;
        public NpcPerceptionContext(RogueGame game, Actor owner, Location place, Actor person = null, Inventory items = null, int risk = 0)
        { Game = game; Owner = owner; Place = place; Person = person; Items = items; Risk = risk; Turn = owner.Location.Map.LocalTime.TurnCounter; }
    }
    sealed class NpcReportContext
    {
        public readonly Actor Listener;
        public readonly NpcContentCatalog Catalog;
        public readonly NpcFact Fact;
        public readonly int Improvement;
        public NpcReportContext(Actor listener, NpcFact fact, int improvement, NpcContentCatalog catalog = null)
        { Listener = listener; Fact = fact; Improvement = improvement; Catalog = catalog ?? NpcContentCatalog.Default; }
        public void RememberPlace(string kind, int minimumConfidence = 40)
        {
            if (Fact.Confidence >= minimumConfidence)
                Listener.Personality.Knowledge.RememberPlace(new NpcKnownPlace(Fact.Place, kind, Fact.EventTurn, Fact.Units, Fact.Risk));
        }
    }
}
