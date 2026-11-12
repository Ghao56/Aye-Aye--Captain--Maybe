using UnityEngine;

namespace AyeAye
{
    // Một đơn hàng (FR-0301). Các thẻ hàng có thể kết hợp (Dễ vỡ + Khẩn).
    [CreateAssetMenu(fileName = "Order_New", menuName = "AyeAye/Order Definition")]
    public class OrderDef : ScriptableObject
    {
        public string orderId;

        [Tooltip("Tên khách (hài).")]
        public string customerName;

        public OrderTag tags = OrderTag.None;

        public string fromPortId;
        public string toPortId;

        [Tooltip("Hạn giao (tuyệt đối theo ngày của ván, vd: 3 = trước hết Ngày 3).")]
        public int deadlineDay = 3;

        [Tooltip("Doanh thu nếu giao đúng hạn và hàng nguyên vẹn.")]
        public int revenue = 100;

        [Tooltip("Một dòng manh mối cho người chơi (FR-0307: không nêu thẳng outcome).")]
        public string clue;

        public int weight = 1;

        [Tooltip("Điều kiện xuất hiện trong pool (mức toàn ván).")]
        public FlagCondition spawnCondition = FlagCondition.Always;
    }
}
