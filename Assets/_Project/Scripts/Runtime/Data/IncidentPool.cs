using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "IncidentPool", menuName = "AyeAye/Incident Pool")]
    public class IncidentPool : ScriptableObject
    {
        [Tooltip("Sự cố cố định Ngày 1 (tutorial: I-01 Sương mù dày).")]
        public IncidentDef fixedFirstDay;

        [Tooltip("Sự cố cố định Ngày cuối (I-F Cuộc gọi Chủ tịch); chỉ sinh nếu còn tàu Moving có hàng.")]
        public IncidentDef fixedFinale;

        [Tooltip("Pool bốc theo trọng số + trigger (không lặp trong ván; cạn thì fallback cho phép lặp).")]
        public List<IncidentDef> incidents = new List<IncidentDef>();
    }
}
