namespace AyeAye
{

    public static class RouteProgress
    {

        public static float CumulativeDistance(RouteOption route, int nodeIndex)
        {
            int edges = route.portPath.Count - 1;
            return edges <= 0 ? 0f : route.length * nodeIndex / edges;
        }

        public static float SegmentAt(RouteOption route, float distance, out int segmentIndex)
        {
            int edges = route.portPath.Count - 1;
            float clamped = UnityEngine.Mathf.Clamp(distance, 0f, route.length);
            if (edges <= 0) { segmentIndex = 0; return 0f; }
            if (edges == 1)
            {
                segmentIndex = 0;
                return clamped / route.length;
            }
            float segLen = route.length / edges;
            segmentIndex = (int)(clamped / segLen);
            if (segmentIndex >= edges) segmentIndex = edges - 1;
            return UnityEngine.Mathf.Clamp((clamped - segmentIndex * segLen) / segLen, 0f, 1f);
        }
    }
}

