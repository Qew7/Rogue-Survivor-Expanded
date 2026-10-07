using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine.Actions
{
    class ActionSwitchRadio : ActorAction
    {
        readonly RadioReceiver radio;
        public ActionSwitchRadio(Actor actor, RogueGame game, RadioReceiver target) : base(actor, game)
        { radio = target; }
        public override bool IsLegal()
        { return m_Game.Rules.IsSwitchableFor(m_Actor, radio, out m_FailReason); }
        public override void Perform() { m_Game.DoSwitchRadio(m_Actor, radio); }
    }
}
