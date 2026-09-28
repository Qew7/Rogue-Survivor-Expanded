using System;
using System.Collections.Generic;
using System.Globalization;

namespace djack.RogueSurvivor.Engine
{
    sealed partial class RecordsQuery
    {
        public static readonly string[] FilterNames = { "Name contains", "Faction contains", "Group leader contains", "Life status",
            "Minimum items received", "Minimum memories", "Minimum days lived", "Maximum days lived", "Minimum events",
            "Minimum Unique participants in events", "Minimum human kills", "Minimum resolved memories", "Minimum gained traits", "Reset filters" };
        public string FilterValue(int index)
        {
            switch (index)
            {
                case 0: return Name; case 1: return Faction; case 2: return Group; case 3: return Life.ToString();
                case 4: return MinItems.ToString(); case 5: return MinMemories.ToString();
                case 6: return MinDays.ToString(CultureInfo.InvariantCulture);
                case 7: return MaxDays == Double.MaxValue ? "" : MaxDays.ToString(CultureInfo.InvariantCulture);
                case 8: return MinEvents.ToString(); case 9: return MinEncounters.ToString(); case 10: return MinKills.ToString();
                case 11: return MinResolved.ToString(); case 12: return MinTraitChanges.ToString(); default: return "";
            }
        }
        public bool SetFilter(int index, string value)
        {
            value = (value ?? "").Trim();
            if (index == 0) { Name = value; return true; }
            if (index == 1) { Faction = value; return true; }
            if (index == 2) { Group = value; return true; }
            if (index == 6 || index == 7)
            {
                double number = index == 7 ? Double.MaxValue : 0;
                if (value.Length > 0 && !Double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
                if (Double.IsNaN(number) || Double.IsInfinity(number) || number < 0 ||
                    (index == 6 ? number > MaxDays : number < MinDays)) return false;
                if (index == 6) MinDays = number; else MaxDays = number;
                return true;
            }
            long count = 0;
            if (value.Length > 0 && !Int64.TryParse(value, out count)) return false;
            if (count < 0 || (index != 4 && count > Int32.MaxValue)) return false;
            switch (index)
            {
                case 4: MinItems = count; break; case 5: MinMemories = (int)count; break;
                case 8: MinEvents = (int)count; break; case 9: MinEncounters = (int)count; break;
                case 10: MinKills = (int)count; break; case 11: MinResolved = (int)count; break;
                case 12: MinTraitChanges = (int)count; break; default: return false;
            }
            return true;
        }
        public void ClearFilters()
        {
            Name = Faction = Group = ""; Life = RecordsLife.Any; MinItems = 0;
            MinMemories = MinEvents = MinEncounters = MinKills = MinResolved = MinTraitChanges = 0;
            MinDays = 0; MaxDays = Double.MaxValue;
        }
        public string FilterSummary()
        {
            List<string> filters = new List<string>();
            for (int i = 0; i < 13; i++)
            {
                string value = FilterValue(i);
                if (String.IsNullOrEmpty(value) || value == "0" || (i == 3 && Life == RecordsLife.Any)) continue;
                filters.Add(FilterNames[i] + ": " + value);
            }
            return filters.Count == 0 ? "No filters" : String.Join("; ", filters.ToArray());
        }
    }
}
