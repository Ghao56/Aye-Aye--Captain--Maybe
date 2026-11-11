using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "AyeAye/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Tooltip("Tổng quãng đường cần tích lũy để đến nơi.")]
        public float totalDistance = 3.2f;

        [Tooltip("Ngày cuối được phép giao hàng.")]
        public int deadlineDay = 3;

        [Tooltip("Quãng đường khi kết quả Normal.")]
        public float normalDistance = 1.0f;

        [Tooltip("Quãng đường khi kết quả Smart.")]
        public float smartDistance = 1.75f;

        [Tooltip("Quãng đường khi kết quả Slip-up.")]
        public float slipUpDistance = 0.4f;

        [Tooltip("Tốc độ gõ chữ của hiệu ứng terminal (ký tự/giây).")]
        public int typewriterCharsPerSecond = 90;

        [Tooltip("Mức Hàng khi bắt đầu chuyến.")]
        public CargoCondition startingCargo = CargoCondition.Intact;

        [Tooltip("Nếu bật: Restart chơi lại với đúng seed cũ (chuỗi event lặp lại). Mặc định tắt = mỗi lần restart một seed mới.")]
        public bool repeatSeedOnRestart = false;

        public string tripCode = "BDAY-0042";
        public string shipName = "S.S. CÓ LẼ";
        public string destinationName = "HÀNH TINH ZORP-9";
    }
}
