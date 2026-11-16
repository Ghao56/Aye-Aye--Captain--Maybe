using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "IncidentPool", menuName = "AyeAye/Incident Pool")]

    public class IncidentPool : ScriptableObject
    {
        [Tooltip("Fixed day-1 incident (tutorial: I-01 Thick Fog).")]

        public IncidentDef fixedFirstDay;
        [Tooltip("Fixed finale incident (I-F The Chairman Calls); spawns only while a Moving ship carries cargo.")]

        public IncidentDef fixedFinale;
        [Tooltip("Pool drawn by weight + trigger (no repeats in a run; exhausted pools fall back to repeats).")]

        public List<IncidentDef> incidents = new List<IncidentDef>();
    }
}

