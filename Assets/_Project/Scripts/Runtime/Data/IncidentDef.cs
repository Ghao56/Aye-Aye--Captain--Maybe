using System;
using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // Điều kiện sinh của một sự cố (FR-0502): mỗi trigger là một nhóm điều kiện AND;
    // sự cố hợp lệ khi BẤT KỲ trigger nào khớp. Triggers rỗng = luôn đủ điều kiện (vd I-07).
    [Serializable]
    public class IncidentTrigger
    {
        [Tooltip("Hazard của cạnh tàu vừa đi qua / đang đi qua. Any = không lọc theo hazard.")]
        public HazardType requiredHazard = HazardType.Any;

        [Tooltip("Tàu phải đang chở hàng Dễ vỡ.")]
        public bool requireFragileCargo;

        [Tooltip("Tàu vừa cập một cảng trung gian trong bước mô phỏng này (I-04 Kẹt cảng).")]
        public bool requireJustArrivedAtPort;

        [Tooltip("Condition của tàu = Hao mòn (I-06).")]
        public bool requireWornCondition;

        [Tooltip("Tàu đang có cờ TemporaryRepair (I-06, nhánh trọng số cao).")]
        public bool requireTemporaryRepair;

        [Tooltip("Tàu phải đang chở hàng (I-F finale).")]
        public bool requireCarryingCargo;

        [Tooltip("Nhân trọng số khi trigger này khớp (I-06: TemporaryRepair ×3).")]
        public float weightMultiplier = 1f;
    }

    // Một phương án xử lý sự cố. baseProgressDelta cộng với Δ của Outcome (FR-0506).
    [Serializable]
    public class IncidentOption
    {
        public string label;

        [TextArea] public string description;

        [Tooltip("Δ tiến độ riêng của phương án (vd: liều/nhanh +0.3, chờ/chậm −0.3).")]
        public float baseProgressDelta;

        [Tooltip("Bảng (thuyền trưởng, điều kiện) → outcome. Không khớp bảng = Normal (FR-0505).")]
        public List<IncidentOutcomeEntry> entries = new List<IncidentOutcomeEntry>();
    }

    // Một dòng trong bảng outcome: thuyền trưởng × điều kiện cờ → hệ quả.
    [Serializable]
    public class IncidentOutcomeEntry
    {
        [Tooltip("Thuyền trưởng áp dụng; Any = mọi thuyền trưởng không có entry riêng.")]
        public CrewId crew = CrewId.Any;

        [Tooltip("Điều kiện cờ của tàu/đơn trong ngữ cảnh sự cố.")]
        public FlagCondition condition = FlagCondition.Always;

        public DecisionOutcome outcome = DecisionOutcome.Normal;

        [TextArea] public string explanation;

        [TextArea] public string quote;

        [Tooltip("Tối đa 1 cờ mỗi sự cố (FR-0506).")]
        public FlagToSet setFlag = FlagToSet.None;

        [Tooltip("Đổi mức hàng của đơn đang chở (âm = giảm mức; chỉ khi ghi rõ — FR-0506).")]
        public int cargoDelta;

        [Tooltip("Hạ Condition của tàu 1 mức (tối đa Hao mòn khi chưa có FR-0408).")]
        public bool conditionDown;

        [Tooltip("Đổi Doanh thu (vd phí qua đường −20). Doanh thu không âm.")]
        public int revenueDelta;
    }

    [CreateAssetMenu(fileName = "Incident_New", menuName = "AyeAye/Incident Definition")]
    public class IncidentDef : ScriptableObject
    {
        public string incidentId;

        public string title;

        [TextArea] public string situationText;

        [Tooltip("Manh mối hiển thị trên thẻ sự cố (log thuyền trưởng, thời tiết, tình trạng...).")]
        [TextArea] public string clueText;

        public int weight = 1;

        public List<IncidentTrigger> triggers = new List<IncidentTrigger>();

        [Tooltip("2–3 phương án; luôn có phương án \"mặc kệ/giữ nguyên\" khi hợp lý (FR-0504).")]
        public List<IncidentOption> options = new List<IncidentOption>();
    }
}
