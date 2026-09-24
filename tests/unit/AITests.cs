using System;
using System.Collections;
using System.Reflection;

static class AITests
{
    public static void Run()
    {
        Type baseAI = Check.Type("Gameplay.AI.BaseAI");
        object ai = Check.Empty("Gameplay.AI.CivilianAI");
        Type perceptType = Check.Type("Engine.AI.Percept");
        object location = Activator.CreateInstance(Check.Type("Data.Location"));
        object first = Activator.CreateInstance(perceptType, new object[] { new object(), 2, location });
        object second = Activator.CreateInstance(perceptType, new object[] { new object(), 9, location });
        object third = Activator.CreateInstance(perceptType, new object[] { new object(), 5, location });
        IList percepts = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(perceptType));
        percepts.Add(first); percepts.Add(second); percepts.Add(third);
        IList sorted = (IList)Check.CallOn(baseAI, ai, "SortByDate", null, percepts);
        Check.Same(second, sorted[0], "newest first");
        Check.Same(third, sorted[1], "middle second");
        Check.Same(first, sorted[2], "oldest last");
        Check.Same(first, percepts[0], "sort leaves input intact");
        Check.Equal(null, Check.CallOn(baseAI, ai, "SortByDate", null, null), "null perception list");

        Check.Equal(false, Check.CallOn(baseAI, ai, "IsItemWorthTellingAbout", new object[] { null }), "null item");
        Check.Equal(false, Check.CallOn(baseAI, ai, "IsItemWorthTellingAbout", Check.Empty("Engine.Items.ItemBarricadeMaterial")), "barricade material");
        object actor = Check.Empty("Data.Actor");
        Check.Type("Data.ActorController").GetField("m_Actor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ai, actor);
        Check.Equal(true, Check.CallOn(baseAI, ai, "IsItemWorthTellingAbout", Check.Empty("Engine.Items.ItemFood")), "unowned food");
    }
}
