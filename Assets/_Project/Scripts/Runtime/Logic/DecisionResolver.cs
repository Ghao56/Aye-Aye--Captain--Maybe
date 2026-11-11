using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Thuần logic: chọn entry khớp ĐẦU TIÊN (đúng người trước, wildcard sau), không sửa state.
    public static class DecisionResolver
    {
        public static ResolvedDecision Resolve(DecisionOption option, CrewId crew, GameState state, CrewDefinition crewDef)
        {
            if (option != null && option.entries != null)
            {
                // 1) Entry đúng người, điều kiện cờ thỏa, lấy đầu tiên theo thứ tự danh sách.
                CrewOutcomeEntry exact = option.entries.Find(
                    e => e != null && e.crew == crew && IsConditionMet(e.condition, state));
                if (exact != null) return ToResolved(exact, crew, option.label);

                // 2) Wildcard Any chỉ dùng khi không có entry đúng người.
                CrewOutcomeEntry wildcard = option.entries.Find(
                    e => e != null && e.crew == CrewId.Any && IsConditionMet(e.condition, state));
                if (wildcard != null) return ToResolved(wildcard, crew, option.label);
            }

            // 3) Không có entry khớp → mặc định Normal của nhân vật.
            return DefaultResolved(crewDef, crew, option != null ? option.label : "");
        }

        public static bool IsConditionMet(FlagCondition condition, GameState state)
        {
            switch (condition)
            {
                case FlagCondition.CargoShiftedRequired: return state.CargoShifted;
                case FlagCondition.CargoShiftedForbidden: return !state.CargoShifted;
                case FlagCondition.TemporaryRepairRequired: return state.TemporaryRepair;
                case FlagCondition.TemporaryRepairForbidden: return !state.TemporaryRepair;
                default: return true; // Always
            }
        }

        static ResolvedDecision ToResolved(CrewOutcomeEntry entry, CrewId crew, string optionLabel)
        {
            return new ResolvedDecision
            {
                Outcome = entry.outcome,
                Explanation = entry.explanation,
                Quote = entry.quote,
                FlagToSet = entry.setFlag,
                CargoDelta = entry.cargoDelta,
                CrewName = CrewName(crew),
                OptionLabel = optionLabel ?? "",
            };
        }

        static ResolvedDecision DefaultResolved(CrewDefinition crewDef, CrewId crew, string optionLabel)
        {
            return new ResolvedDecision
            {
                Outcome = DecisionOutcome.Normal,
                Explanation = crewDef != null ? crewDef.defaultNormalLine : "",
                Quote = crewDef != null ? crewDef.defaultQuote : "",
                FlagToSet = FlagToSet.None,
                CargoDelta = 0,
                CrewName = CrewName(crew),
                OptionLabel = optionLabel,
            };
        }

        static string CrewName(CrewId crew)
        {
            return crew.ToString().ToUpperInvariant();
        }
    }
}
