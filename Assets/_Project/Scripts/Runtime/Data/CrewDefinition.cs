using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Crew_New", menuName = "AyeAye/Crew Definition")]
    public class CrewDefinition : ScriptableObject
    {
        public CrewId id;

        public string displayName;

        [Tooltip("Nhãn tính cách ngắn.")]
        public string personalityLabel;

        [TextArea] public string strengthText;

        [TextArea] public string weaknessText;

        [Tooltip("Khung chân dung ASCII 1-3 dòng, thay cho ảnh chân dung.")]
        [TextArea(1, 4)] public string portraitGlyph;

        [Tooltip("Dòng giải thích mặc định khi tổ hợp không có entry riêng.")]
        [TextArea] public string defaultNormalLine;

        [Tooltip("Lời thoại mặc định khi tổ hợp không có entry riêng.")]
        [TextArea] public string defaultQuote;
    }
}
