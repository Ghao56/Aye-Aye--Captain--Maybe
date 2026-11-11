using UnityEngine;

namespace AyeAye
{
    // Một event trong ngày. dayNumber đã bỏ: số ngày lấy từ GameState.CurrentDay,
    // title chỉ chứa tên event, tiền tố "NGÀY n · " ghép lúc hiển thị.
    [CreateAssetMenu(fileName = "Event_New_Event", menuName = "AyeAye/Day Definition")]
    public class DayDefinition : ScriptableObject
    {
        [Tooltip("Định danh ổn định của event, dùng cho log/test.")] public string eventId;

        public string title;

        [TextArea(5, 12)] public string briefingText;

        [TextArea(5, 12)]
        [Tooltip("Có thể chứa token {CARGO}, sẽ được thay bằng tên mức Hàng.")]
        public string statusText;

        [TextArea]
        [Tooltip("Hiển thị ở Status khi CargoShifted = true; có thể rỗng.")]
        public string cargoShiftedWarning;

        public DecisionDefinition decision;

        [Tooltip("Event chỉ xuất hiện khi điều kiện cờ này thỏa.")] public FlagCondition spawnCondition = FlagCondition.Always;

        [Min(1)]
        [Tooltip("Trọng số khi bốc ngẫu nhiên; càng lớn càng hay gặp.")]
        public int weight = 1;
    }
}
