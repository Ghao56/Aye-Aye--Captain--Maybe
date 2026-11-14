using UnityEngine;

namespace AyeAye
{
    // G5 — hồ sơ roaming của một thuyền trưởng (tính cách tàu rảnh). Số nằm ở data (ShipDef),
    // logic chỉ đọc — không hard-code trong RoamPlanner/Simulation.
    [System.Serializable]
    public class RoamProfile
    {
        [Tooltip("Thời gian nghỉ tối thiểu ở cảng trước khi cân nhắc rời đi (giây).")]
        public float dwellSeconds = 15f;

        [Tooltip("Trọng số hướng nhu cầu: cảng có đơn hộp thư/đã nhận chưa gán xuất phát từ đó.")]
        public float demandWeight = 2f;

        [Tooltip("Thích cảng xa (+) hay gần (−) — nhân độ dài cạnh.")]
        public float farBias = 0f;

        [Tooltip("Điểm cộng khi cảng đích là HQ (Gốc).")]
        public float hubAffinity = 0f;

        [Tooltip("Độ nhiễu ngẫu nhiên — đi qua RoamRng riêng, KHÔNG đụng state.Rng (chuỗi đơn/sự cố không đổi).")]
        public float wanderWeight = 0f;

        [Tooltip("Trừ điểm cảng vừa đi qua — tàu không quay lại ngay.")]
        public float backtrackPenalty = 2f;
    }
}
