using System;
using UnityEngine;

namespace AyeAye
{
    // Không phải ScriptableObject: entry nằm trong danh sách của DecisionOption.
    [Serializable]
    public class CrewOutcomeEntry
    {
        [Tooltip("Any = wildcard, chỉ dùng khi không có entry đúng người.")]
        public CrewId crew = CrewId.Any;

        [Tooltip("Điều kiện cờ; Always = luôn khớp.")]
        public FlagCondition condition = FlagCondition.Always;

        public DecisionOutcome outcome = DecisionOutcome.Normal;

        [TextArea] public string explanation;

        [TextArea] public string quote;

        [Tooltip("Cờ được đặt sau End Day; chỉ đặt, không tự xóa.")]
        public FlagToSet setFlag = FlagToSet.None;

        [Tooltip("0 = không đổi; giá trị âm = hạ mức Hàng (tối đa 1 mức mỗi lần).")]
        public int cargoDelta;
    }
}
