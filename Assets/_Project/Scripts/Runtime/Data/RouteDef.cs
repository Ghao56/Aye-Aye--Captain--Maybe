using UnityEngine;

namespace AyeAye
{
    // Một cạnh của bản đồ (FR-0403): hai cảng kề nhau, độ dài route-units, hazard dọc đường.
    [CreateAssetMenu(fileName = "Route_New", menuName = "AyeAye/Route Definition")]
    public class RouteDef : ScriptableObject
    {
        public string fromPortId;

        public string toPortId;

        [Min(1), Tooltip("Độ dài cạnh theo route-units.")]
        public int length = 1;

        public HazardType hazard = HazardType.Clear;
    }
}
