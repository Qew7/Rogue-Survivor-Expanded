namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcFactionPolicy
    {
        public readonly int Supply, Shelter, Care, Security, Courage;
        public NpcFactionPolicy(int supply, int shelter, int care = 0, int security = 0, int courage = 0)
        { Supply = supply; Shelter = shelter; Care = care; Security = security; Courage = courage; }
    }
}
