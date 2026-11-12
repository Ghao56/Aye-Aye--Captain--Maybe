using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Ship_New", menuName = "AyeAye/Ship Definition")]
    public class ShipDef : ScriptableObject
    {
        public string shipId;

        public string displayName;

        public CrewId captain;

        [Tooltip("Tên thuyền trưởng hiển thị (phải khớp CrewId để tra bảng sự cố).")]
        public string captainName;

        public SpeedClass speedClass = SpeedClass.Normal;

        [Tooltip("Tag tính cách hiển thị trên thẻ tàu (vd: \"Nhanh · Ẩu · Thích đường tắt\").")]
        public string traits;

        public string startPortId;

        public ShipCondition startCondition = ShipCondition.Good;
    }
}
