using System.Collections.Generic;

namespace AyeAye
{
    /// Sự cố CHẠY LIÊN TỤC (G1.7): roll khi một tàu vào cạnh mới / node giữa (do Simulation gọi),
    /// tối đa maxPendingIncidents cùng lúc (Simulation giới hạn trước khi gọi).
    public static class IncidentScheduler
    {
        /// Trả về PendingIncident khi xảy ra (đã đánh dấu UsedIncidentIds); null khi không có.
        public static PendingIncident TryTrigger(SimWorld world, GameState state, IncidentContext ctx)
        {
            if (world == null || state == null || ctx == null) return null;

            // I-01 tutorial: bắt buộc xảy ra LẦN ĐẦU TIÊN một tàu vào cạnh khớp trigger (Sương mù).
            if (world.FixedFirstDay != null && !state.UsedIncidentIds.Contains(world.FixedFirstDay.incidentId)
                && Matches(world.FixedFirstDay, ctx, state))
            {
                state.UsedIncidentIds.Add(world.FixedFirstDay.incidentId);
                return new PendingIncident { Incident = world.FixedFirstDay, Context = ctx };
            }

            // I-F finale: khi DeliveredCount == winDeliveries - 1 và còn tàu đang chở hàng → xảy ra MỘT LẦN.
            if (world.FixedFinale != null && !state.UsedIncidentIds.Contains(world.FixedFinale.incidentId)
                && ctx.CarriedOrder != null
                && state.DeliveredCount == state.Config.winDeliveries - 1)
            {
                state.UsedIncidentIds.Add(world.FixedFinale.incidentId);
                return new PendingIncident { Incident = world.FixedFinale, Context = ctx };
            }

            // Thường: roll theo incidentChancePerEdge qua Rng (mỗi lần vào cạnh mới roll đúng một lần).
            if (world.Incidents == null || world.Incidents.Count == 0) return null;
            if (state.Rng.NextDouble() >= state.Config.incidentChancePerEdge) return null;

            // Ưu tiên sự cố chưa dùng và khớp trigger; pool cạn mới cho phép lặp.
            var candidates = new List<IncidentDef>();
            foreach (var def in world.Incidents)
                if (def != null && Matches(def, ctx, state) && !state.UsedIncidentIds.Contains(def.incidentId))
                    candidates.Add(def);
            if (candidates.Count == 0)
                foreach (var def in world.Incidents)
                    if (def != null && Matches(def, ctx, state))
                        candidates.Add(def);
            if (candidates.Count == 0) return null;

            var pick = PickWeighted(candidates, ctx, state);
            state.UsedIncidentIds.Add(pick.incidentId);
            return new PendingIncident { Incident = pick, Context = ctx };
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
