using System;
using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{
    [Serializable]

    public class RouteOption
    {

        public string label;
        [Tooltip("portId chain from departure port to destination port (both ends included).")]

        public List<string> portPath = new List<string>();
        [Tooltip("Hazard per edge, portPath.Count - 1 entries, in order.")]

        public List<HazardType> hazards = new List<HazardType>();
        [Tooltip("Total length (route-units) = sum of edge lengths.")]

        public float length;

        public string FromPort { get { return portPath != null && portPath.Count > 0 ? portPath[0] : null; } }

        public string ToPort { get { return portPath != null && portPath.Count > 1 ? portPath[portPath.Count - 1] : null; } }
    }
    [Serializable]

    public class RoutePairEntry
    {

        public string fromPortId;

        public string toPortId;
        [Tooltip("Max 2 options per port pair (§2.4).")]

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

