using UnityEngine;

namespace AyeAye
{
    // Mọi số cân bằng nằm đây (FR-0901, mục 2.5) — không hard-code số cân bằng trong logic/UI.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "AyeAye/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Ván chơi")]
        [Tooltip("Số ngày của một ván.")]
        public int gameLengthDays = 8;

        [Tooltip("Uy tín khi bắt đầu (0-100).")]
        public int startReputation = 50;

        [Tooltip("Số đơn đang hoạt động tối đa.")]
        public int maxActiveOrders = 4;

        [Tooltip("Số đơn mới mỗi ngày (bốc ngẫu nhiên trong khoảng; Ngày 1 luôn dùng đơn cố định).")]
        public int newOrdersPerDayMin = 2;
        public int newOrdersPerDayMax = 3;

        [Header("Sự cố")]
        [Range(0f, 1f), Tooltip("Xác suất mỗi tàu Moving gặp sự cố mỗi ngày.")]
        public float incidentChancePerMovingShip = 0.6f;

        [Tooltip("Tổng sự cố tối đa mỗi ngày (kể cả sự cố cố định).")]
        public int maxIncidentsPerDay = 2;

        [Header("Tốc độ (route-units/ngày)")]
        public float fastSpeed = 1.5f;
        public float normalSpeed = 1.0f;
        public float slowSpeed = 0.7f;

        [Header("Δ tiến độ theo Outcome (route-units, cộng thêm baseProgressDelta của phương án)")]
        public float smartProgressDelta = 0.5f;
        public float normalProgressDelta = 0f;
        public float slipUpProgressDelta = -0.7f;

        [Header("Chấm điểm (FR-0602)")]
        [Tooltip("Uy tín khi giao đúng hạn.")]
        public int onTimeReputation = 6;

        public int cargoIntactBonus = 2;
        public int cargoScratchedBonus = 0;
        public int cargoRuinedBonus = -8;

        [Tooltip("Uy tín khi giao trễ (không cộng thưởng theo mức hàng).")]
        public int lateReputationPenalty = -5;

        [Range(0f, 1f), Tooltip("Hệ số doanh thu khi giao trễ.")]
        public float lateRevenueFactor = 0.5f;

        [Tooltip("Uy tín bị trừ cho mỗi đơn đã nhận nhưng chưa giao khi hết ván.")]
        public int undeliveredReputationPenalty = 8;

        [Header("Ngưỡng kết thúc theo Uy tín (FR-0701)")]
        public int endingE1Threshold = 85;
        public int endingE2Threshold = 65;
        public int endingE3Threshold = 40;
        public int endingE4Threshold = 20;

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

        public float OutcomeDelta(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Smart: return smartProgressDelta;
                case DecisionOutcome.SlipUp: return slipUpProgressDelta;
                default: return normalProgressDelta;
            }
        }

        public int CargoBonus(CargoCondition cargo)
        {
            switch (cargo)
            {
                case CargoCondition.Intact: return cargoIntactBonus;
                case CargoCondition.Scratched: return cargoScratchedBonus;
                default: return cargoRuinedBonus;
            }
        }
    }
}
