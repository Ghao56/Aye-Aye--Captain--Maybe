using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // Thế giới mô phỏng: dữ liệu tĩnh của ván, dựng 1 lần (UI LoadWorld / test TestWorld).
    public sealed class SimWorld
    {
        public OrderPool Pool;
        public RouteTable Routes;
        public IReadOnlyList<IncidentDef> Incidents;
        public IncidentDef FixedFirstDay;
        public IncidentDef FixedFinale;

        public string HQPortId = ""; // G5: cảng trụ (isHeadquarters) — RoamPlanner dùng cho hubAffinity
    }

    // Vòng CHẠY LIÊN TỤC (bỏ ngày — xem ASSUMPTIONS.md G1): Advance(dt) thuần, xác định theo seed,
    // KHÔNG đọc thời gian hệ thống. UI/BalanceProbe gọi theo bước cố định config.simTickSeconds.
    // Đồng hồ dừng khi IsFinished hoặc có sự cố đang chờ (CanAdvance == false).
    public static class Simulation
    {
        // Một tick mô phỏng: bốc đơn → hết hạn hộp thư → chốt trễ → chạy tàu.
        public static void Advance(SimWorld world, GameState state, float dt)
        {
            if (world == null || state == null || dt <= 0f) return;
            if (!state.CanAdvance) return; // sự cố chờ / đã kết thúc → TẠM DỪNG

            state.ElapsedSeconds += dt;

            OrderScheduler.SpawnOrders(world.Pool, state);
            ExpireInbox(state);   // hộp thư không nhận quá hạn → xóa, KHÔNG tính trễ (FR-0304 bản liên tục)
            MarkLate(state);      // chốt trễ TRƯỚC khi tàu chạy: thua vì trễ ưu tiên hơn thắng cùng tick
            if (state.IsFinished) return; // đã chốt thua trong tick đó → tàu không chạy, không giao thêm

            foreach (var ship in state.Ships)
                AdvanceShip(world, state, ship, dt);
        }
        
        // Tính toán số tick cần thực hiện dựa trên simTickSeconds cố định
        public static int CalculateTicks(float dt, float simTickSeconds)
        {
            if (simTickSeconds <= 0f) return 1;
            int ticks = Mathf.Max(1, Mathf.FloorToInt(dt / simTickSeconds));
            return ticks;
        }
        
        // Chạy nhiều tick nhỏ (mỗi tick gọi Advance — không nhân bản logic; review boss 14/11).
        public static void AdvanceWithFixedTimestep(SimWorld world, GameState state, float dt)
        {
            if (world == null || state == null || dt <= 0f) return;
            int ticks = CalculateTicks(dt, state.Config.simTickSeconds);
            for (int i = 0; i < ticks && state.CanAdvance; i++)
                Advance(world, state, state.Config.simTickSeconds);
        }

        // FR-0506: Δ tiến độ từ sự cố; không âm; chạm đích thì giao luôn.
        public static void ApplyProgressDelta(GameState state, ShipState ship, float delta)
        {
            if (ship.Status != ShipStatus.Moving || ship.Route == null) return;
            ship.DistanceAlongRoute = Mathf.Max(0f, ship.DistanceAlongRoute + delta);
            if (ship.DistanceAlongRoute >= ship.Route.length - 0.0001f)
                FinalizeArrival(state, ship);
        }

        // FR-0501: tàu Moving tiến theo thời gian thật (SpeedUnitsPerSecond).
        // Vào cạnh mới (qua node giữa) → ArrivedPortId + roll sự cố theo hazard CẠNH HIỆN TẠI (G1.7).
        // Chạm đích → giao ngay (FR-0409): chấm điểm, DeliveredCount++, đủ winDeliveries → thắng.
        static void AdvanceShip(SimWorld world, GameState state, ShipState ship, float dt)
        {
            if (ship.Status == ShipStatus.Roaming)
            {
                AdvanceRoaming(state, ship, dt);
                return;
            }
            if (ship.Status != ShipStatus.Moving || ship.Route == null)
            {
                TryStartRoam(world, state, ship, dt);
                return;
            }
            var route = ship.Route;
            ship.ArrivedPortId = "";

            // Xuất phát (distance 0) = vừa vào cạnh 0 → roll sự cố cạnh 0 luôn (G1.7: I-01 cạnh Sương mù là cạnh đầu).
            bool justLaunched = ship.DistanceAlongRoute <= 0.0001f;

            int prevSeg;
            RouteProgress.SegmentAt(route, ship.DistanceAlongRoute, out prevSeg);

            ship.DistanceAlongRoute += state.Config.SpeedUnitsPerSecond(ship.Speed) * dt;

            int newSeg;
            RouteProgress.SegmentAt(route, Mathf.Min(ship.DistanceAlongRoute, route.length - 0.0001f), out newSeg);

            // Node giữa vừa đi qua (cuối cạnh i là node i; node cuối là đích → không set).
            for (int i = prevSeg + 1; i <= newSeg && i < route.portPath.Count - 1; i++)
            {
                float nodeDist = RouteProgress.CumulativeDistance(route, i);
                if (ship.DistanceAlongRoute >= nodeDist - 0.0001f && nodeDist < route.length - 0.0001f)
                    ship.ArrivedPortId = route.portPath[i];
            }

            if (ship.DistanceAlongRoute >= route.length - 0.0001f)
            {
                FinalizeArrival(state, ship);
                return;
            }

            // Vừa vào cạnh mới (cạnh 0 lúc xuất phát, hoặc qua node giữa) → roll sự cố cho CẠNH HIỆN TẠI.
            if (justLaunched || newSeg > prevSeg)
                RollIncident(world, state, ship, EdgeHazard(route, newSeg), justArrived: !justLaunched);
        }

        // G5 — roaming thật: tàu rảnh đi 1 cạnh trực tiếp tới cảng kề, tốc × roamSpeedFactor.
        // KHÔNG roll sự cố (incidentsWhileRoaming = false), KHÔNG chở hàng (OrderId luôn rỗng).
        static void AdvanceRoaming(GameState state, ShipState ship, float dt)
        {
            var route = ship.Route;
            if (route == null) { ship.Status = ShipStatus.Idle; return; }
            ship.DistanceAlongRoute += state.Config.SpeedUnitsPerSecond(ship.Speed)
                * Mathf.Max(0f, state.Config.roamSpeedFactor) * dt;
            if (ship.DistanceAlongRoute < route.length - 0.0001f) return;

            ship.PortId = route.ToPort;
            ship.Status = ShipStatus.Idle;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            ship.Route = null;
            if (ship.Roam != null) ship.RoamTimerSeconds = ship.Roam.dwellSeconds;
        }

        // Idle: giảm timer nghỉ; hết nghỉ + hết ân hạn + DƯ THỪA tại cảng → rời theo planner.
        static void TryStartRoam(SimWorld world, GameState state, ShipState ship, float dt)
        {
            if (ship.Roam == null || !state.Config.roamEnabled) return;
            ship.RoamTimerSeconds -= dt;
            var neighbors = RoamPlanner.Neighbors(world.Routes, ship.PortId);
            if (!RoamPlanner.ShouldLeave(state, ship, neighbors)) return;
            var next = RoamPlanner.ChooseNext(state, ship, neighbors, world.HQPortId);
            ship.LastPortId = ship.PortId;
            ship.Route = next.Route;
            ship.Status = ShipStatus.Roaming;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
        }

        static HazardType EdgeHazard(RouteOption route, int edgeIndex)
        {
            if (route.hazards == null || route.hazards.Count == 0) return HazardType.Any;
            return route.hazards[Mathf.Min(edgeIndex, route.hazards.Count - 1)];
        }

        // Roll sự cố khi tàu vào cạnh mới / node giữa (giới hạn maxPendingIncidents — G1.7).
        static void RollIncident(SimWorld world, GameState state, ShipState ship, HazardType edgeHazard, bool justArrived)
        {
            if (state.PendingIncidents.Count >= state.Config.maxPendingIncidents) return;
            OrderState carried = string.IsNullOrEmpty(ship.OrderId)
                ? null
                : state.Orders.Find(o => o.Id == ship.OrderId);
            var ctx = new IncidentContext
            {
                Ship = ship,
                CarriedOrder = carried,
                JustArrivedAtPort = justArrived,
                EdgeHazards = new List<HazardType> { edgeHazard },
            };
            var pick = IncidentScheduler.TryTrigger(world, state, ctx);
            if (pick != null) state.PendingIncidents.Add(pick);
        }

        // FR-0409: tới cảng đến của đơn → Delivered + chấm điểm NGAY; đủ winDeliveries → thắng (G1.5).
        static void FinalizeArrival(GameState state, ShipState ship)
        {
            ship.LastPortId = ship.Route.FromPort; // G5: để backtrack khi roaming kế tiếp
            ship.PortId = ship.Route.ToPort;
            ship.Status = ShipStatus.Idle;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            if (ship.Roam != null) ship.RoamTimerSeconds = ship.Roam.dwellSeconds;
            if (!string.IsNullOrEmpty(ship.OrderId))
            {
                var order = state.Orders.Find(o => o.Id == ship.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Delivered;
                    order.AssignedShipId = ship.Id;
                    state.Revenue += ScoreCalculator.ScoreDelivery(order, state.Config);
                    state.DeliveredCount++;
                    if (state.DeliveredCount >= state.Config.winDeliveries)
                        EndingEvaluator.FinishRun(state);
                }
                ship.OrderId = "";
            }
            ship.Route = null;
        }

        // Trễ (G1.4): đơn Accepted/Assigned quá hạn → IsLate MỘT LẦN, LateCount++ NGAY lúc quá hạn
        // (không đợi tới lúc giao). Đủ maxLateDeliveries → thua ngay.
        static void MarkLate(GameState state)
        {
            foreach (var o in state.Orders)
            {
                bool active = o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned;
                if (!active || o.IsLate) continue;
                if (state.ElapsedSeconds <= o.DeadlineAtSeconds) continue;
                o.IsLate = true;
                state.LateCount++;
                if (state.LateCount >= state.Config.maxLateDeliveries)
                {
                    EndingEvaluator.FinishRun(state);
                    return;
                }
            }
        }

        // FR-0304 (bản liên tục): đơn hộp thư quá hạn → xóa khỏi Orders, không tính trễ.
        static void ExpireInbox(GameState state)
        {
            state.Orders.RemoveAll(o =>
                o.Status == OrderStatus.InInbox && state.ElapsedSeconds > o.DeadlineAtSeconds);
        }
    }
}
