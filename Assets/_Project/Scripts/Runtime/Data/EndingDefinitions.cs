using System;
using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "Endings", menuName = "AyeAye/Ending Definitions")]
    public class EndingDefinitions : ScriptableObject
    {
        [Serializable]
        public class EndingEntry
        {
            public EndingId id;

            public string title;

            [TextArea] public string body;
        }

        public List<EndingEntry> endings = new List<EndingEntry>();

        public EndingEntry Get(EndingId id)
        {
            return endings.Find(e => e.id == id);
        }
    }
}
