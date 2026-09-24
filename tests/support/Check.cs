using System;
using System.Reflection;
using System.Runtime.Serialization;

static class Check
{
    static readonly Assembly Game = Assembly.LoadFrom("/src/WRogue/bin/Release/RogueSurvivor.exe");

    public static Type Type(string name) { return Game.GetType("djack.RogueSurvivor." + name, true); }
    public static object Empty(string name) { return FormatterServices.GetUninitializedObject(Type(name)); }

    public static object Call(object instance, string name, Type[] parameters, params object[] args)
    {
        Type type = instance as Type;
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                             (type == null ? BindingFlags.Instance : BindingFlags.Static);
        MethodInfo method = (type ?? instance.GetType()).GetMethod(name, flags, null, parameters, null);
        if (method == null) throw new Exception("Missing method: " + name);
        return method.Invoke(type == null ? instance : null, args);
    }

    public static object Call(object instance, string name, params object[] args)
    {
        return Call(instance, name, Array.ConvertAll(args, x => x == null ? typeof(object) : x.GetType()), args);
    }

    public static object CallOn(Type type, object instance, string name, params object[] args)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null) throw new Exception("Missing method: " + name);
        return method.Invoke(instance, args);
    }

    public static void Equal(object expected, object actual, string label)
    {
        if (!Object.Equals(expected, actual))
            throw new Exception(label + ": expected " + expected + ", got " + actual);
    }

    public static void Same(object expected, object actual, string label)
    {
        if (!Object.ReferenceEquals(expected, actual)) throw new Exception(label + ": reference changed");
    }

    public static void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (TargetInvocationException error)
        {
            if (error.InnerException is T) return;
            throw;
        }
        throw new Exception(label + ": expected " + typeof(T).Name);
    }
}
