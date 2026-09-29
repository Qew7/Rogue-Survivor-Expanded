namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Named aliases for existing callers; all definitions live in content modules.
    static class NpcIntentContent
    {
        public static NpcIntentDefinition Repay { get { return Find("repay_aid"); } }
        public static NpcIntentDefinition Obtain { get { return Find("obtain_food"); } }
        public static NpcIntentDefinition Recover { get { return Find("restore_health"); } }
        public static NpcIntentDefinition Request { get { return Find("request_food"); } }
        public static NpcIntentDefinition MedicalAid { get { return Find("medical_aid"); } }
        public static NpcIntentDefinition Promise { get { return Find("fulfil_promise"); } }
        public static NpcIntentDefinition Restitution { get { return Find("restore_property"); } }
        public static NpcIntentDefinition ValuedItem { get { return Find("recover_valued_item"); } }
        public static NpcIntentDefinition Help { get { return Find("answer_food_request"); } }
        public static NpcIntentDefinition Leave { get { return Find("leave_unsafe_group"); } }
        public static NpcIntentDefinition Seek { get { return Find("seek_companion"); } }
        public static NpcIntentDefinition Avoid { get { return Find("avoid_reported_threat"); } }
        public static NpcIntentDefinition Confront { get { return Find("confront_reported_aggressor"); } }
        public static NpcIntentDefinition Gather { get { return Find("gather_group_supplies"); } }
        public static NpcIntentDefinition Coordinate { get { return Find("coordinate_group_supplies"); } }
        public static NpcIntentDefinition Shelter { get { return Find("seek_group_shelter"); } }
        public static NpcIntentDefinition Find(string id) { return NpcContentCatalog.Default.Capability(id); }
    }
}
