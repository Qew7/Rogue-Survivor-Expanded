using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    // Check saved model IDs against the definitions loaded after mod fallback.
    static class ModSaveValidator
    {
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object left, object right)
            {
                return Object.ReferenceEquals(left, right);
            }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }

        public static void Validate(Session session)
        {
            Stack<object> pending = new Stack<object>();
            HashSet<object> seen = new HashSet<object>(new ReferenceComparer());
            pending.Push(session);
            while (pending.Count > 0)
            {
                object value = pending.Pop();
                if (value == null) continue;
                Type type = value.GetType();
                if (type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                    type == typeof(decimal) || type == typeof(DateTime) ||
                    type == typeof(TimeSpan) || type == typeof(Guid) ||
                    type == typeof(byte[])) continue;
                if (!type.IsValueType && !seen.Add(value)) continue;
                try
                {
                    Actor actor = value as Actor;
                    if (actor != null && (actor.Model == null || actor.Faction == null))
                        throw new InvalidDataException("Saved actor model is unavailable.");
                    Item item = value as Item;
                    if (item != null && item.Model == null)
                        throw new InvalidDataException("Saved item model is unavailable.");
                    Tile tile = value as Tile;
                    if (tile != null && tile.Model == null)
                        throw new InvalidDataException("Saved tile model is unavailable.");
                }
                catch (IndexOutOfRangeException error)
                {
                    throw new InvalidDataException("Saved model ID is unavailable.", error);
                }
                catch (KeyNotFoundException error)
                {
                    throw new InvalidDataException("Saved model ID is unavailable.", error);
                }
                IDictionary dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        pending.Push(entry.Key);
                        pending.Push(entry.Value);
                    }
                }
                else if (value is IEnumerable)
                {
                    foreach (object entry in (IEnumerable)value) pending.Push(entry);
                }
                else if (type.Assembly == typeof(Session).Assembly && type.IsSerializable)
                {
                    MemberInfo[] members = FormatterServices.GetSerializableMembers(type);
                    foreach (object field in FormatterServices.GetObjectData(value, members))
                        if (field != null) pending.Push(field);
                }
            }
        }
    }
}
