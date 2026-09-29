namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcContentDefaults
    {
        public static NpcContentCatalog Create(PersonalityRegistry registry, params INpcContentModule[] additional)
        {
            var modules = new System.Collections.Generic.List<INpcContentModule> {
                new CoreEventsModule(), new KnowledgeModule(), new NutritionModule(), new FoodAidModule(), new SafetyModule(), new JusticeModule(),
                new CompanionsModule(), new MedicalCareModule(), new PromisesModule(), new PossessionsModule(), new GroupsModule(),
                new ResourceCompetitionModule(), new MovementModule() };
            modules.AddRange(additional); return NpcCatalogBuilder.Compose(registry, modules.ToArray());
        }
    }
}
