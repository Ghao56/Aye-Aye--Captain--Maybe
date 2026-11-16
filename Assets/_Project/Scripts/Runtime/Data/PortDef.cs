using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "Port_New", menuName = "AyeAye/Port Definition")]

    public class PortDef : ScriptableObject
    {

        public string portId;

        public string displayName;
        [Tooltip("Home port (HQ) — start port of the ships and where most orders originate.")]

        public bool isHeadquarters;
        [Tooltip("Position on the star map, normalized 0..1 (G3).")]

        public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
    }
}

