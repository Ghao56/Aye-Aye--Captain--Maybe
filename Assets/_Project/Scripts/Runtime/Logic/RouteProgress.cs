namespace AyeAye
{
    // Hàm thuần dùng chung cho tiến độ trên RouteOption (FR-0501 + UI bản đồ G3).
    // shortcut: các cạnh của một RouteOption coi như đều nhau (length chia theo số cạnh) —
    // nếu sau này có độ dài từng cạnh riêng thì phải sửa SimulationStep.MoveAll cùng lúc.
    public static class RouteProgress
    {
        // Quãng đường tích lũy tới node thứ nodeIndex (node 0 = cảng đi).
        public static float CumulativeDistance(RouteOption route, int nodeIndex)
        {
            int edges = route.portPath.Count - 1;
            return edges <= 0 ? 0f : route.length * nodeIndex / edges;
        }

        // Đoạn đang đi theo quãng đường: trả về tỉ lệ 0..1 trong cạnh, kèm index cạnh (0..edges-1).
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
