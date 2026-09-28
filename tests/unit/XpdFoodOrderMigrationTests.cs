using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdFoodOrderMigrationTests
{
    public static void Run()
    {
        // A compact schema may omit an optional field; it keeps its default.
        using (MemoryStream stream = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(1); // root reference
            writer.Write(1); // object ID
            writer.Write(-1); // first type/schema ID
            writer.Write(typeof(CivilianAI).AssemblyQualifiedName);
            writer.Write(0); // schema has no fields on this test object
            writer.Write(0); // end of nodes
            stream.Position = 0;
            CivilianAI loaded = (CivilianAI)ObjectGraphStore.Read(stream);
            Check.Equal(false, typeof(OrderableAI).GetField("m_XpdFoodOnly",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loaded),
                "absent optional flag defaults to unrestricted supply orders");
        }
    }
}
