using System.Collections.Generic;

namespace AyeAye
{
    /// FR-04xx — rà soát tàu đang di chuyển, roll xác suất và chốt danh sách sự cố của ngày.
    public static class IncidentScheduler
    {
        public static List<PendingIncident> Schedule(IReadOnlyList<IncidentDef> pool, IncidentDef fixedFirstDay, IncidentDef fixedFinale, GameState state, IReadOnlyList<IncidentContext> contexts)
        {
            var picks = new List<PendingIncident>();
            if (contexts == null || contexts.Count == 0) return picks;

            // Ngày 1: chỉ sự cố tutorial cố định, cho thuyền đầu tiên khớp trigger, không dùng rng.
            if (state.CurrentDay <= 1)
            {
                if (fixedFirstDay != null)
                {
                    foreach (var ctx in contexts)
                    {
                        if (Matches(fixedFirstDay, ctx, state))
                        {
                            picks.Add(new PendingIncident { Incident = fixedFirstDay, Context = ctx });
                            break;
                        }
                    }
                }
                return picks;
            }

            // Ngày cuối: chỉ finale cố định nếu còn tàu đang chở hàng.
            if (state.CurrentDay >= state.Config.gameLengthDays)
            {
                if (fixedFinale != null)
                {
                    foreach (var ctx in contexts)
                    {
                        if (ctx.CarriedOrder != null)
                        {
                            picks.Add(new PendingIncident { Incident = fixedFinale, Context = ctx });
                            break;
                        }
                    }
                }
                return picks;
            }

            // Ngày thường: mỗi tàu (thứ tự cố định) roll một lần; ngân sách tổng theo config.
            int budget = state.Config.maxIncidentsPerDay;
            foreach (var ctx in contexts)
            {
                if (picks.Count >= budget) break;
                if (state.Rng.NextDouble() >= state.Config.incidentChancePerMovingShip) continue;
                if (pool == null || pool.Count == 0) continue;

                // Ưu tiên sự cố chưa dùng và khớp trigger; pool cạn mới cho phép lặp.
                var candidates = new List<IncidentDef>();
                foreach (var def in pool)
                    if (def != null && Matches(def, ctx, state) && !state.UsedIncidentIds.Contains(def.incidentId))
                        candidates.Add(def);
                if (candidates.Count == 0)
                    foreach (var def in pool)
                        if (def != null && Matches(def, ctx, state))
                            candidates.Add(def);
                if (candidates.Count == 0) continue;

                var pick = PickWeighted(candidates, ctx, state);
                state.UsedIncidentIds.Add(pick.incidentId);
                picks.Add(new PendingIncident { Incident = pick, Context = ctx });
            }
            return picks;
        }

        /// Sự cố đủ điều kiện khi BẤT KỲ trigger nào khớp.
        private static bool Matches(IncidentDef def, IncidentContext ctx, GameState state)
        {
            if (def.triggers == null || def.triggers.Count == 0) return true;
            foreach (var t in def.triggers)
                if (MatchTrigger(t, ctx, state)) return true;
            return false;
        }

        private static bool MatchTrigger(IncidentTrigger t, IncidentContext ctx, GameState state)
        {
            var ship = ctx.Ship;
            if (t.requiredHazard != HazardType.Any &&
                (ctx.EdgeHazards == null || !ctx.EdgeHazards.Contains(t.requiredHazard)))
                return false;
            if (t.requireFragileCargo &&
                (ctx.CarriedOrder == null || (ctx.CarriedOrder.Tags & OrderTag.Fragile) == 0))
                return false;
            if (t.requireJustArrivedAtPort && !ctx.JustArrivedAtPort)
                return false;
            if (t.requireWornCondition && ship.Condition != ShipCondition.Worn)
                return false;
            if (t.requireTemporaryRepair && !state.TemporaryRepairShipIds.Contains(ship.Id))
                return false;
            if (t.requireCarryingCargo && ctx.CarriedOrder == null)
                return false;
            return true;
        }

        /// Trọng số = weightMultiplier của trigger khớp (nhiều trigger khớp thì lấy lớn nhất).
        private static IncidentDef PickWeighted(List<IncidentDef> candidates, IncidentContext ctx, GameState state)
        {
            float total = 0f;
            var weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = 1f;
                if (candidates[i].triggers != null)
                    foreach (var t in candidates[i].triggers)
                        if (MatchTrigger(t, ctx, state) && t.weightMultiplier > w) w = t.weightMultiplier;
                weights[i] = candidates[i].weight * w;
                total += weights[i];
            }
            float roll = (float)state.Rng.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }
}
