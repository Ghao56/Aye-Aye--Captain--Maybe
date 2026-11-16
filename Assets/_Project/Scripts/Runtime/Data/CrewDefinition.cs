using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "Crew_New", menuName = "AyeAye/Crew Definition")]

    public class CrewDefinition : ScriptableObject
    {

        public CrewId id;

        public string displayName;
        [Tooltip("Short personality label.")]

        public string personalityLabel;
        [TextArea] public string strengthText;
        [TextArea] public string weaknessText;
        [Tooltip("1–3 line ASCII portrait, standing in for a face image.")]
        [TextArea(1, 4)] public string portraitGlyph;
        [Tooltip("Default explanation line when no specific entry matches.")]
        [TextArea] public string defaultNormalLine;
        [Tooltip("Default quote when no specific entry matches.")]
        [TextArea] public string defaultQuote;
    }
}

