using System;
using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{
    [Serializable]

    public class IncidentTrigger
    {
        [Tooltip("Hazard of the edge the ship just entered / is entering. Any = no hazard filter.")]

        public HazardType requiredHazard = HazardType.Any;
        [Tooltip("The ship must be carrying Fragile cargo.")]

        public bool requireFragileCargo;
        [Tooltip("The ship just arrived at an intermediate port this sim step (I-04 Port Jam).")]

        public bool requireJustArrivedAtPort;
        [Tooltip("Ship Condition = Worn (I-06).")]

        public bool requireWornCondition;
        [Tooltip("The ship has the TemporaryRepair flag (I-06, high-weight branch).")]

        public bool requireTemporaryRepair;
        [Tooltip("The ship must be carrying cargo (I-F finale).")]

        public bool requireCarryingCargo;
        [Tooltip("Weight multiplier when this trigger matches (I-06: TemporaryRepair ×3).")]

        public float weightMultiplier = 1f;
    }
    [Serializable]

    public class IncidentOption
    {

        public string label;
        [TextArea] public string description;
        [Tooltip("The option's own progress Δ (e.g. risky/fast +0.3, wait/slow −0.3).")]

        public float baseProgressDelta;
        [Tooltip("Table (captain, condition) → outcome. No match = Normal (FR-0505).")]

        public List<IncidentOutcomeEntry> entries = new List<IncidentOutcomeEntry>();
    }
    [Serializable]

    public class IncidentOutcomeEntry
    {
        [Tooltip("Captain it applies to; Any = every captain without its own entry.")]

        public CrewId crew = CrewId.Any;
        [Tooltip("Flag condition of the ship/order in the incident context.")]

        public FlagCondition condition = FlagCondition.Always;

        public DecisionOutcome outcome = DecisionOutcome.Normal;
        [TextArea] public string explanation;
        [TextArea] public string quote;
        [Tooltip("Max 1 flag per incident (FR-0506).")]

        public FlagToSet setFlag = FlagToSet.None;
        [Tooltip("Change the cargo level of the carried order (negative = degrade; only when explicit — FR-0506).")]

        public int cargoDelta;
        [Tooltip("Drop the ship's Condition by 1 level (max Worn until FR-0408).")]

        public bool conditionDown;
        [Tooltip("Change Revenue (e.g. toll −20). Revenue never goes negative.")]

        public int revenueDelta;
    }
    [CreateAssetMenu(fileName = "Incident_New", menuName = "AyeAye/Incident Definition")]

    public class IncidentDef : ScriptableObject
    {

        public string incidentId;

        public string title;
        [TextArea] public string situationText;
        [Tooltip("Clue shown on the incident card (captain's log, weather, condition...).")]
        [TextArea] public string clueText;

        public int weight = 1;

        public List<IncidentTrigger> triggers = new List<IncidentTrigger>();
        [Tooltip("2–3 options; always include a \"do nothing/hold\" option when sensible (FR-0504).")]

        public List<IncidentOption> options = new List<IncidentOption>();
    }
}
