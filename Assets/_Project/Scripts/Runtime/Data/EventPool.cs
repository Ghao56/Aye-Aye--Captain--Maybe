using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // Pool event cho một ván: ngày 1 cố định, ngày 2..deadline bốc ngẫu nhiên,
    // ngày cuối có thể ghim fixedFinale (null = bốc như thường).
    [CreateAssetMenu(fileName = "EventPool", menuName = "AyeAye/Event Pool")]
    public class EventPool : ScriptableObject
    {
        public DayDefinition openingDay;

        [Tooltip("Ghim event cho ngày deadlineDay; để trống để bốc ngẫu nhiên.")]
        public DayDefinition fixedFinale;

        public List<DayDefinition> events = new List<DayDefinition>();
    }
}
