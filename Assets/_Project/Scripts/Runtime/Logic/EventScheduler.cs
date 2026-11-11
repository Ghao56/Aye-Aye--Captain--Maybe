using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Bốc event theo trọng số, thuần C#: không UnityEngine.Random, không đụng resolver/evaluator.
    public static class EventScheduler
    {

        // Trả về event được chọn hoặc null nếu không còn event hợp lệ (caller phải xử lý rõ ràng).
        public static DayDefinition Pick(IList<DayDefinition> pool, ICollection<DayDefinition> used,
                                         GameState state, System.Random rng)
        {
            DayDefinition picked = PickFiltered(pool, used, state, rng);
            if (picked != null) return picked;

            // Pool cạn hoặc không event nào thỏa cờ → fallback: cho phép lặp event, vẫn tôn trọng điều kiện xuất hiện.
            return PickFiltered(pool, null, state, rng);
        }

        static DayDefinition PickFiltered(IList<DayDefinition> pool, ICollection<DayDefinition> used,
                                         GameState state, System.Random rng)
        {
            if (pool == null || rng == null) return null;

            int total = 0;
            DayDefinition[] candidates = new DayDefinition[pool.Count];
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                DayDefinition day = pool[i];
                if (day == null) continue;
                if (used != null && used.Contains(day)) continue;
                if (!DecisionResolver.IsConditionMet(day.spawnCondition, state)) continue;
                candidates[count++] = day;
                total += Math.Max(1, day.weight);
            }

            if (count == 0) return null;

            int roll = rng.Next(total);
            for (int i = 0; i < count; i++)
            {
                roll -= Math.Max(1, candidates[i].weight);
                if (roll < 0) return candidates[i];
            }
            return candidates[count - 1]; // phòng thủ: không bao giờ tới đây
        }
    }
}
