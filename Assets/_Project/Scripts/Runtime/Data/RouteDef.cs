using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "Route_New", menuName = "AyeAye/Route Definition")]

    public class RouteDef : ScriptableObject
    {

        public string fromPortId;

        public string toPortId;
        [Min(1), Tooltip("Edge length in route-units.")]

        public int length = 1;

        public HazardType hazard = HazardType.Clear;
    }
}

