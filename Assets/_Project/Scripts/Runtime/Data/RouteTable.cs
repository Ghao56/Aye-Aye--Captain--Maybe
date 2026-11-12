using System;
using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // Một lộ trình thiết kế sẵn giữa hai cảng (FR-0404): chuỗi cảng đi qua + hazard từng cạnh.
    // Không pathfinding thời gian chạy — lộ trình là dữ liệu (mục 2.4).
    [Serializable]
    public class RouteOption
    {
        public string label;

        [Tooltip("Chuỗi portId từ cảng đi tới cảng đến (bao gồm cả hai đầu).")]
        public List<string> portPath = new List<string>();

        [Tooltip("Hazard từng cạnh, portPath.Count - 1 phần tử, theo thứ tự.")]
        public List<HazardType> hazards = new List<HazardType>();

        [Tooltip("Tổng độ dài (route-units) = tổng chiều dài các cạnh.")]
        public float length;

        public string FromPort { get { return portPath != null && portPath.Count > 0 ? portPath[0] : null; } }
        public string ToPort { get { return portPath != null && portPath.Count > 1 ? portPath[portPath.Count - 1] : null; } }
    }

    [Serializable]
    public class RoutePairEntry
    {
        public string fromPortId;
        public string toPortId;

        [Tooltip("Tối đa 2 lựa chọn mỗi cặp cảng (mục 2.4).")]
        public List<RouteOption> options = new List<RouteOption>();
    }

    [CreateAssetMenu(fileName = "RouteTable", menuName = "AyeAye/Route Table")]
    public class RouteTable : ScriptableObject
    {
        public List<RoutePairEntry> pairs = new List<RoutePairEntry>();

        public List<RouteOption> GetOptions(string fromPortId, string toPortId)
        {
            RoutePairEntry entry = pairs.Find(p => p != null && p.fromPortId == fromPortId && p.toPortId == toPortId);
            return entry != null ? entry.options : null;
        }
    }
}
