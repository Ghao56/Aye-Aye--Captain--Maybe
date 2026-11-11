using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Day_New_Day", menuName = "AyeAye/Day Definition")]
    public class DayDefinition : ScriptableObject
    {
        public int dayNumber;

        public string title;

        [TextArea(5, 12)] public string briefingText;

        [TextArea(5, 12)]
        [Tooltip("Có thể chứa token {CARGO}, sẽ được thay bằng tên mức Hàng.")]
        public string statusText;

        [TextArea]
        [Tooltip("Hiển thị ở Status khi CargoShifted = true; có thể rỗng.")]
        public string cargoShiftedWarning;

        public DecisionDefinition decision;
    }
}
