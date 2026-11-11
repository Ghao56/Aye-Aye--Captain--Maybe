using System;
using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    [Serializable]
    public class DecisionOption
    {
        public string label;

        [TextArea] public string description;

        public List<CrewOutcomeEntry> entries = new List<CrewOutcomeEntry>();
    }
}
