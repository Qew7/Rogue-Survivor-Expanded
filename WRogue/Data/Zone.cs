using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace djack.RogueSurvivor.Data
{
    enum BuildingKind
    {
        None, ConstructionStore, GeneralStore, Grocery, Gunshop, Pharmacy,
        SportswearStore, HuntingStore, House, Apartments, Park, Hospital,
        PoliceStation, Office, CharAgency
    }

    [Serializable]
    class Zone
    {
        static int s_BoundsVersion;
        internal static int BoundsVersion { get { return System.Threading.Volatile.Read(ref s_BoundsVersion); } }
        #region Fields
        string m_Name = "unnamed zone";
        Rectangle m_Bounds;
        Dictionary<string, object> m_Attributes = null;
        [System.Runtime.Serialization.OptionalField] BuildingKind m_BuildingKind;
        #endregion

        #region Properties
        public string Name
        {
            get { return m_Name; }
            set { m_Name = value; }
        }

        public Rectangle Bounds
        {
            get { return m_Bounds; }
            set { m_Bounds = value; System.Threading.Interlocked.Increment(ref s_BoundsVersion); }
        }
        public BuildingKind BuildingKind
        {
            get { return m_BuildingKind; }
            set { m_BuildingKind = value; }
        }

        public static readonly Color StoreColor = Color.Gold;
        public static readonly Color FoodStoreColor = Color.LightGreen;
        public static readonly Color GunshopColor = Color.OrangeRed;
        public static readonly Color PharmacyColor = Color.LightPink;
        public static readonly Color HomeColor = Color.LightSkyBlue;
        public static readonly Color ParkColor = Color.GreenYellow;
        public static readonly Color CivicColor = Color.CornflowerBlue;
        public static readonly Color OfficeColor = Color.Plum;

        public static string BuildingLabel(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.ConstructionStore: return "construction store";
                case BuildingKind.GeneralStore: return "general store";
                case BuildingKind.Grocery: return "grocery store";
                case BuildingKind.Gunshop: return "gun shop";
                case BuildingKind.Pharmacy: return "pharmacy";
                case BuildingKind.SportswearStore: return "sportswear store";
                case BuildingKind.HuntingStore: return "hunting store";
                case BuildingKind.House: return "house";
                case BuildingKind.Apartments: return "apartment building";
                case BuildingKind.Park: return "park";
                case BuildingKind.Hospital: return "hospital";
                case BuildingKind.PoliceStation: return "police station";
                case BuildingKind.Office: return "office building";
                case BuildingKind.CharAgency: return "CHAR agency";
                default: return null;
            }
        }

        public static Color BuildingColor(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Grocery: return FoodStoreColor;
                case BuildingKind.Gunshop: return GunshopColor;
                case BuildingKind.Pharmacy: return PharmacyColor;
                case BuildingKind.House:
                case BuildingKind.Apartments: return HomeColor;
                case BuildingKind.Park: return ParkColor;
                case BuildingKind.Hospital:
                case BuildingKind.PoliceStation: return CivicColor;
                case BuildingKind.Office:
                case BuildingKind.CharAgency: return OfficeColor;
                default: return StoreColor;
            }
        }

        public static Zone BuildingAt(Location place)
        {
            if (place.Map == null) return null;
            Zone building = null;
            List<Zone> zones = place.Map.GetZonesAt(place.Position.X, place.Position.Y);
            if (zones == null) return null;
            foreach (Zone zone in zones)
                if (zone.BuildingKind != BuildingKind.None &&
                    (building == null || zone.Bounds.Width * zone.Bounds.Height < building.Bounds.Width * building.Bounds.Height))
                    building = zone;
            return building;
        }
        #endregion

        #region Init
        public Zone(string name, Rectangle bounds)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            m_Name = name;
            m_Bounds = bounds;
        }
        #endregion

        #region Game attributes
        public bool HasGameAttribute(string key)
        {
            if (m_Attributes == null)
                return false;
            return m_Attributes.Keys.Contains(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">must be serializable</param>
        public void SetGameAttribute<_T_>(string key, _T_ value)
        {
            if (m_Attributes == null)
                m_Attributes = new Dictionary<string, object>(1);

            if (m_Attributes.Keys.Contains(key))
                m_Attributes[key] = value;
            else
                m_Attributes.Add(key, value);
        }

        public _T_ GetGameAttribute<_T_>(string key)
        {
            if (m_Attributes == null)
                return default(_T_);

            object value;
            if (m_Attributes.TryGetValue(key, out value))
            {
                if (!(value is _T_))
                    throw new InvalidOperationException("game attribute is not of requested type");
                return (_T_) value;                
            }
            else
                return default(_T_);
        }
        #endregion
    }
}
