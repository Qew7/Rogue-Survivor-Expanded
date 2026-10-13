namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcFactionPolicy
    {
        public readonly int Supply, Shelter, Care, Security, Courage;
        public readonly int HumanLife, CareCircle, MessageTrust, Scarcity;
        public NpcFactionPolicy(int supply, int shelter, int care = 0, int security = 0, int courage = 0,
            int humanLife = 0, int careCircle = 0, int messageTrust = 0, int scarcity = 0)
        { Supply = supply; Shelter = shelter; Care = care; Security = security; Courage = courage;
            HumanLife = humanLife; CareCircle = careCircle; MessageTrust = messageTrust; Scarcity = scarcity; }
    }
}
