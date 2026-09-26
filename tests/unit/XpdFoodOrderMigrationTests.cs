using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdFoodOrderMigrationTests
{
    public static void Run()
    {
        // Earlier object graphs have no food-only flag on the NPC controller.
        using (MemoryStream stream = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(1); // root reference
            writer.Write(1); // object ID
            writer.Write(typeof(CivilianAI).AssemblyQualifiedName);
            writer.Write((byte)0); // ordinary object
            writer.Write(0); // old save has no fields on this test object
            writer.Write(0); // end of nodes
            stream.Position = 0;
            CivilianAI loaded = (CivilianAI)ObjectGraphStore.Read(stream);
            Check.Equal(false, typeof(OrderableAI).GetField("m_XpdFoodOnly",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loaded),
                "old NPC saves default to unrestricted supply orders");
        }
    }
}
