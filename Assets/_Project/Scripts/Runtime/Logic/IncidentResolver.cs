using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{

    public class IncidentContext
    {

        public ShipState Ship;

        public OrderState CarriedOrder;

        public bool JustArrivedAtPort;

        public List<HazardType> EdgeHazards;
    }

    public class PendingIncident
    {

        public IncidentDef Incident;

        public IncidentContext Context;
    }

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

        public float ProgressDelta;
    }

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

        public static List<string> ValidateDef(IncidentDef def, IReadOnlyList<CrewDefinition> crews)
        {
            var errors = new List<string>();
            if (def.options == null || def.options.Count == 0)
            {
                errors.Add($"{def.incidentId}: no options at all");
                return errors;
            }
            var allEntries = new List<IncidentOutcomeEntry>();
            foreach (var opt in def.options)
            {
                if (opt.entries == null || opt.entries.Count == 0)
                {
                    errors.Add($"{def.incidentId}/{opt.label}: empty outcome table");
                    continue;
                }
                allEntries.AddRange(opt.entries);
            }
            foreach (DecisionOutcome outcome in System.Enum.GetValues(typeof(DecisionOutcome)))
            {
                if (!allEntries.Exists(e => e.outcome == outcome))
                    errors.Add($"{def.incidentId}: no outcome of type {outcome}");
            }
            foreach (var crew in crews)
            {
                if (crew == null || crew.id == CrewId.Any) continue;
                if (!allEntries.Exists(e => e.crew == crew.id || e.crew == CrewId.Any))
                    errors.Add($"{def.incidentId}: missing entry for {crew.displayName}");
            }
            return errors;
        }
    }
}

