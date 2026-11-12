using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    /// Bối cảnh một tàu đang di chuyển — đầu vào cho trigger và cho sự cố.
    public class IncidentContext
    {
        public ShipState Ship;
        public OrderState CarriedOrder;
        public bool JustArrivedAtPort;
        public List<HazardType> EdgeHazards;
    }

    /// Một sự cố đã được chốt cho một tàu, chờ người chơi chọn lựa chọn.
    public class PendingIncident
    {
        public IncidentDef Incident;
        public IncidentContext Context;
    }

    /// Hệ quả đã tra xong của một (sự cố, lựa chọn, thuyền trưởng).
    public class ResolvedIncident
    {
        public IncidentDef Incident;
        public IncidentOption Option;
        public DecisionOutcome Outcome;
        public string Explanation;
        public string Quote;
        public string CrewName;
        public string OptionLabel;
        public FlagToSet FlagSet;
        public int CargoDelta;
        public bool ConditionDown;
        public int RevenueDelta;
        /// Outcome Δ = baseProgressDelta của lựa chọn + hệ số config theo Outcome.
        public float ProgressDelta;
    }

    /// FR-05xx — tra bảng (thuyền trưởng, lựa chọn) và áp dụng hệ quả lên state.
    public static class IncidentResolver
    {
        public static ResolvedIncident Resolve(IncidentDef def, IncidentOption option, GameState state, ShipState ship, IReadOnlyList<CrewDefinition> crews)
        {
            CrewDefinition crewDef = null;
            foreach (var c in crews)
                if (c != null && c.id == ship.Captain) { crewDef = c; break; }

            IncidentOutcomeEntry chosen = null;
            foreach (var e in option.entries)
                if (e.crew == ship.Captain && FlagEvaluator.IsMet(e.condition, state, ship)) { chosen = e; break; }
            if (chosen == null)
                foreach (var e in option.entries)
                    if (e.crew == CrewId.Any && e.condition == FlagCondition.Always) { chosen = e; break; }

            var resolved = new ResolvedIncident
            {
                Incident = def,
                Option = option,
                OptionLabel = option.label,
                CrewName = crewDef != null ? crewDef.displayName : ship.Captain.ToString(),
            };

            if (chosen != null)
            {
                resolved.Outcome = chosen.outcome;
                resolved.Explanation = chosen.explanation;
                resolved.Quote = chosen.quote;
                resolved.FlagSet = chosen.setFlag;
                resolved.CargoDelta = chosen.cargoDelta;
                resolved.ConditionDown = chosen.conditionDown;
                resolved.RevenueDelta = chosen.revenueDelta;
            }
            else
            {
                // Không entry nào khớp → mặc định Normal + câu mặc định của thuyền trưởng (FR-0504).
                resolved.Outcome = DecisionOutcome.Normal;
                resolved.Explanation = crewDef != null ? crewDef.defaultNormalLine : "";
                resolved.Quote = crewDef != null ? crewDef.defaultQuote : "";
            }
            resolved.ProgressDelta = option.baseProgressDelta + state.Config.OutcomeDelta(resolved.Outcome);
            return resolved;
        }

        public static void Apply(ResolvedIncident resolved, GameState state, ShipState ship)
        {
            if (resolved.FlagSet == FlagToSet.CargoShifted && !string.IsNullOrEmpty(ship.OrderId))
                state.CargoShiftedOrderIds.Add(ship.OrderId);
            else if (resolved.FlagSet == FlagToSet.TemporaryRepair)
                state.TemporaryRepairShipIds.Add(ship.Id);

            var order = state.Orders.Find(o => o.Id == ship.OrderId);
            if (order != null && resolved.CargoDelta < 0)
            {
                for (int i = 0; i < -resolved.CargoDelta && order.Cargo > CargoCondition.Ruined; i++)
                    order.Cargo = (CargoCondition)((int)order.Cargo - 1);
            }

            if (resolved.ConditionDown && ship.Condition > ShipCondition.Worn)
                ship.Condition = ShipCondition.Worn;

            state.Revenue = System.Math.Max(0, state.Revenue + resolved.RevenueDelta);
        }

        /// FR-0505 — bảng hệ quả phải đầy đủ ở mức SỰ CỐ: mỗi option có entry,
        /// mỗi outcome đạt được ở đâu đó, mỗi thuyền trưởng có ít nhất một entry chính xác.
        public static List<string> ValidateDef(IncidentDef def, IReadOnlyList<CrewDefinition> crews)
        {
            var errors = new List<string>();
            if (def.options == null || def.options.Count == 0)
            {
                errors.Add($"{def.incidentId}: không có lựa chọn nào");
                return errors;
            }
            var allEntries = new List<IncidentOutcomeEntry>();
            foreach (var opt in def.options)
            {
                if (opt.entries == null || opt.entries.Count == 0)
                {
                    errors.Add($"{def.incidentId}/{opt.label}: bảng hệ quả trống");
                    continue;
                }
                allEntries.AddRange(opt.entries);
            }
            foreach (DecisionOutcome outcome in System.Enum.GetValues(typeof(DecisionOutcome)))
            {
                if (!allEntries.Exists(e => e.outcome == outcome))
                    errors.Add($"{def.incidentId}: không có outcome nào là {outcome}");
            }
            foreach (var crew in crews)
            {
                if (crew == null || crew.id == CrewId.Any) continue;
                // §5.5: hàng "any" phủ mọi thuyền trưởng không liệt kê — entry Any cũng tính là phủ.
                if (!allEntries.Exists(e => e.crew == crew.id || e.crew == CrewId.Any))
                    errors.Add($"{def.incidentId}: thiếu entry chính xác cho {crew.displayName}");
            }
            return errors;
        }
    }
}
