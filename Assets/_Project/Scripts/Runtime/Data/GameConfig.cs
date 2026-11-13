using UnityEngine;

namespace AyeAye
{
    // Mọi số cân bằng nằm đây — bản CHẠY LIÊN TỤC (bỏ ngày, xem ASSUMPTIONS.md mục G1).
    [CreateAssetMenu(fileName = "GameConfig", menuName = "AyeAye/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Ván chơi")]
        [Tooltip("Số đơn đang hoạt động tối đa.")]
        public int maxActiveOrders = 4;

        [Header("Đồng hồ")]
        [Tooltip("Bước mô phỏng cố định (giây). UI cộng dồn dt thật rồi chạy Advance theo bước này — cùng seed cho cùng kết quả.")]
        public float simTickSeconds = 0.1f;

        [Tooltip("1 route-unit mất bao nhiêu giây ở tốc Thường.")]
        public float secondsPerRouteUnit = 20f;

        [Header("Đơn hàng")]
        [Tooltip("Khoảng giây giữa hai lần bốc đơn mới.")]
        public float orderIntervalSeconds = 40f;

        [Tooltip("Khoảng rút ngắn theo số mũ sau mỗi đơn đã bốc.")]
        public float orderIntervalDecayPerOrder = 0.92f;

        [Tooltip("Khoảng tối thiểu giữa hai lần bốc.")]
        public float orderIntervalMinSeconds = 15f;

        [Tooltip("Hạn đếm ngược thấp hơn số giây này thì tô đỏ cảnh báo (UI).")]
        public float deadlineWarnSeconds = 30f;

        [Tooltip("Biên phản ứng người chơi: hạn đơn ≥ ETA tàu nhanh nhất + biên này (DemoValidator kiểm).")]
        public float deadlineReactionMarginSeconds = 25f;

        [Tooltip("Biên thêm cho 2 đơn tutorial (O-01/O-02) — người chơi mới cần thời gian đọc hướng dẫn.")]
        public float tutorialDeadlineBonusSeconds = 30f;

        [Header("Thắng / Thua")]
        [Tooltip("Giao đủ số đơn này → thắng.")]
        public int winDeliveries = 10;

        [Tooltip("Số lần trễ tối đa — đủ số này là thua ngay.")]
        public int maxLateDeliveries = 3;

        [Tooltip("Thua quá sớm nếu lúc thua đã giao ít hơn số đơn này.")]
        public int earlyLoseDeliveries = 2;

        [Header("Sự cố")]
        [Range(0f, 1f), Tooltip("Xác suất mỗi lần một tàu vào cạnh mới.")]
        public float incidentChancePerEdge = 0.15f;

        [Tooltip("Số sự cố đang chờ tối đa cùng lúc (đủ cap → đồng hồ vẫn chạy nhưng không roll thêm).")]
        public int maxPendingIncidents = 2;

        [Tooltip("Cho phép roll sự cố khi tàu đi rảnh (roaming) — [S].")]
        public bool incidentsWhileRoaming = false;

        [Header("Tốc độ (hệ số nhân trên secondsPerRouteUnit)")]
        public float fastSpeed = 1.5f;
        public float normalSpeed = 1.0f;
        public float slowSpeed = 0.7f;

        [Header("Δ tiến độ theo Outcome (route-units, cộng thêm baseProgressDelta của phương án)")]
        public float smartProgressDelta = 0.5f;
        public float normalProgressDelta = 0f;
        public float slipUpProgressDelta = -0.7f;

        [Header("Chấm điểm (FR-0602)")]
        [Tooltip("Hệ số doanh thu khi giao trễ.")]
        public float lateRevenueFactor = 0.5f;

        [Header("Hiệu ứng")]
        [Tooltip("Tốc độ gõ chữ của hiệu ứng terminal (ký tự/giây).")]
        public int typewriterCharsPerSecond = 90;

        [Tooltip("Nếu bật: Restart chơi lại với đúng seed cũ (chuỗi sự cố/đơn lặp lại).")]
        public bool repeatSeedOnRestart = false;

        public float SpeedValue(SpeedClass speed)
        {
            switch (speed)
            {
                case SpeedClass.Fast: return fastSpeed;
                case SpeedClass.Slow: return slowSpeed;
                default: return normalSpeed;
            }
        }

        // Tốc chạy thật (route-units/giây) — dùng cho Advance và ETA trên UI.
        public float SpeedUnitsPerSecond(SpeedClass speed)
        {
            return SpeedValue(speed) / Mathf.Max(0.0001f, secondsPerRouteUnit);
        }

        public float OutcomeDelta(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Smart: return smartProgressDelta;
                case DecisionOutcome.SlipUp: return slipUpProgressDelta;
                default: return normalProgressDelta;
            }
        }
    }
}
