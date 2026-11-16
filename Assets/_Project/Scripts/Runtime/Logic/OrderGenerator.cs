using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{

    public static class OrderGenerator
    {

        public static OrderState Generate(GameState state, RouteTable routes, float now)
        {
            var config = state.Config;
            bool first = state.OrderSpawnCount == 0;
            var ports = DistinctPorts(routes);
            if (ports.Count < 2) return null;
            float rollFrom = (float)state.Rng.NextDouble();
            var idlePorts = ports.FindAll(p => IdleShipsAt(state, p) > 0);
            string from;
            if (first && idlePorts.Count > 0)
                from = idlePorts[(int)(rollFrom * idlePorts.Count) % idlePorts.Count];
            else
            {
                var weights = new List<int>(ports.Count);
                foreach (var p in ports) weights.Add(IdleShipsAt(state, p) > 0 ? 2 : 1);
                from = ports[PickWeightedIndex(weights, rollFrom)];
            }
            float rollTo = (float)state.Rng.NextDouble();
            var tos = new List<string>();
            var toLens = new List<float>();
            foreach (var p in ports)
            {
                if (p == from) continue;
                var opts = routes.GetOptions(from, p);
                if (opts == null || opts.Count == 0) continue;
                tos.Add(p);
                toLens.Add(ShortestLength(opts));
            }
            if (tos.Count == 0) return null;
            string to;
            if (first)
            {
                int best = 0; 
                for (int i = 1; i < toLens.Count; i++) if (toLens[i] < toLens[best]) best = i;
                to = tos[best];
            }
            else to = tos[(int)(rollTo * tos.Count) % tos.Count];
            var options = routes.GetOptions(from, to);
            float minLen = ShortestLength(options);
            OrderTag tags = OrderTag.None;
            foreach (var t in new[] { OrderTag.Fragile, OrderTag.Cold, OrderTag.Urgent })
                if (state.Rng.NextDouble() < config.orderTagChance) tags |= t;
            if (first) tags = OrderTag.None;
            float eta = ShortestEta(options, config);
            float extraMin = Mathf.Max(0f, config.orderDeadlineExtraMinSeconds);
            float extraMax = Mathf.Max(extraMin, config.orderDeadlineExtraMaxSeconds);
            float extra;
            if (first) extra = Mathf.Max(0f, config.tutorialDeadlineBonusSeconds);
            else if ((tags & OrderTag.Urgent) != 0) extra = extraMin;
            else extra = extraMin + (float)state.Rng.NextDouble() * (extraMax - extraMin);
            float deadlineSeconds = eta + Mathf.Max(0f, config.deadlineReactionMarginSeconds) + extra;
            int tagCount = 0;
            foreach (var t in new[] { OrderTag.Fragile, OrderTag.Cold, OrderTag.Urgent })
                if ((tags & t) != 0) tagCount++;
            float jitter = 1f + ((float)state.Rng.NextDouble() * 2f - 1f) * Mathf.Clamp01(config.orderRevenueJitter);
            int revenue = Mathf.Max(1, Mathf.RoundToInt(
                (config.orderRevenueBase + config.orderRevenuePerRouteUnit * minLen
                    + config.orderRevenueTagBonus * tagCount) * jitter));
            string id = "O-G" + (state.OrderSpawnCount + 1);
            string customer = DemoData.OrderCustomers[state.Rng.Next(DemoData.OrderCustomers.Count)];
            string clue = DemoData.OrderClue(tags, WorstHazard(options), state.Rng);
            return new OrderState
            {
                Id = id,
                CustomerName = customer,
                Tags = tags,
                FromPortId = from,
                ToPortId = to,
                DeadlineAtSeconds = now + deadlineSeconds,
                Revenue = revenue,
                Clue = clue,
            };
        }
        static int IdleShipsAt(GameState state, string portId)
        {
            int count = 0;
            foreach (var s in state.Ships)
                if (s.Status == ShipStatus.Idle && s.PortId == portId) count++;
            return count;
        }
        static List<string> DistinctPorts(RouteTable routes)
        {
            var ports = new List<string>();
            if (routes == null || routes.pairs == null) return ports;
            foreach (var pair in routes.pairs)
            {
                if (pair == null) continue;
                if (!string.IsNullOrEmpty(pair.fromPortId) && !ports.Contains(pair.fromPortId)) ports.Add(pair.fromPortId);
                if (!string.IsNullOrEmpty(pair.toPortId) && !ports.Contains(pair.toPortId)) ports.Add(pair.toPortId);
            }
            return ports;
        }
        static float ShortestLength(List<RouteOption> options)
        {
            float min = float.MaxValue;
            foreach (var o in options)
                if (o != null && o.length < min) min = o.length;
            return min;
        }
        static float ShortestEta(List<RouteOption> options, GameConfig config)
        {
            return ShortestLength(options) / Mathf.Max(0.0001f, config.SpeedUnitsPerSecond(SpeedClass.Fast));
        }
        static HazardType WorstHazard(List<RouteOption> options)
        {
            RouteOption shortest = null;
            foreach (var o in options)
                if (shortest == null || o.length < shortest.length) shortest = o;
            if (shortest == null || shortest.hazards == null || shortest.hazards.Count == 0) return HazardType.Clear;
            HazardType worst = HazardType.Clear;
            foreach (var h in shortest.hazards)
                if (h != HazardType.Clear && h != HazardType.Any) worst = h;
            return worst;
        }
        static int PickWeightedIndex(List<int> weights, float roll)
        {
            int total = 0;
            foreach (int w in weights) total += Mathf.Max(1, w);
            int cursor = (int)(roll * total) % Mathf.Max(1, total);
            for (int i = 0; i < weights.Count; i++)
            {
                cursor -= Mathf.Max(1, weights[i]);
                if (cursor < 0) return i;
            }
            return weights.Count - 1;
        }
    }
}

