using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Port_New", menuName = "AyeAye/Port Definition")]
    public class PortDef : ScriptableObject
    {
        public string portId;

        public string displayName;

        [Tooltip("Cảng gốc (HQ) — điểm bắt đầu của ba tàu và nơi các đơn thường xuất phát.")]
        public bool isHeadquarters;
    }
}
