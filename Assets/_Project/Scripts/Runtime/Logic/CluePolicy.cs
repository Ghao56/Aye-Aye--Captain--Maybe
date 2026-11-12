using System.Collections.Generic;

namespace AyeAye
{
    /// Chính sách "đọc manh mối" cho BalanceProbe (NFR-07): chọn tuỳ chọn sự cố như người chơi tinh mắt —
    /// né tuỳ chọn Slip-up với thuyền trưởng hiện tại (và cả điều kiện cờ), ưu tiên Smart + tiến độ cao.
    public static class CluePolicy
    {
        public static IncidentOption PickByClue(IncidentDef def, GameState state, ShipState ship, IReadOnlyList<CrewDefinition> crews)
        {
            if (def == null || def.options == null || def.options.Count == 0)
                return null;
            IncidentOption best = null;
            float bestScore = float.MinValue;
            foreach (var opt in def.options)
            {
                var resolved = IncidentResolver.Resolve(def, opt, state, ship, crews);
                if (resolved.Outcome == DecisionOutcome.SlipUp) continue; // biết trước là hỏng → né
                float score = (resolved.Outcome == DecisionOutcome.Smart ? 1f : 0f) + opt.baseProgressDelta;
                if (score > bestScore) { bestScore = score; best = opt; }
            }
            if (best != null) return best;
            return def.options[0]; // mọi đường đều Slip-up → chịu tuỳ chọn đầu.
        }
    }
}
