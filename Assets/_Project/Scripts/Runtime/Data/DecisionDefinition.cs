using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Day_New_Decision", menuName = "AyeAye/Decision Definition")]
    public class DecisionDefinition : ScriptableObject
    {
        [TextArea] public string prompt;

        public List<DecisionOption> options = new List<DecisionOption>();
    }
}
