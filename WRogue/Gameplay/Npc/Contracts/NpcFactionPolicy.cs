namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcFactionPolicy
    {
        public readonly int Supply, Shelter;
        public NpcFactionPolicy(int supply, int shelter) { Supply = supply; Shelter = shelter; }
    }
}
