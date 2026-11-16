using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "Ship_New", menuName = "AyeAye/Ship Definition")]

    public class ShipDef : ScriptableObject
    {

        public string shipId;

        public string displayName;

        public CrewId captain;
        [Tooltip("Display name of the captain (must match CrewId for the incident table lookup).")]

        public string captainName;

        public SpeedClass speedClass = SpeedClass.Normal;
        [Tooltip("Personality tag shown on the ship card (e.g. \"Fast · Loud · Shortcut lover\").")]

        public string traits;

        public string startPortId;

        public ShipCondition startCondition = ShipCondition.Good;
        [Tooltip("Captain's roaming profile (G5). Null = ship never roams.")]

        public RoamProfile roam;
    }
}

