using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    // DayRunner: vòng ngày điều khiển bởi UI (FR-0102/0103/0304/0602/0105).
    // Khác BalanceProbe: ngày kế bốc đơn SAU khi ++CurrentDay — đơn Ngày 1 không bị nạp lại đè đơn đã nhận.
    public class DayRunnerTests
    {
        TestWorld world;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            world = TestWorld.Create();
            state = GameStateBuilder.CreateRun(world.Config, world.ShipDefs, 42);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in world.SOs) Object.DestroyImmediate(so);
        }

        void StageAssign(string shipId, string orderId)
        {
            var ship = state.Ships.Find(s => s.Id == shipId);
            var order = state.Orders.Find(o => o.Id == orderId);
            order.Status = OrderStatus.Accepted;
            var option = world.Routes.GetOptions(order.FromPortId, order.ToPortId)[0];
            state.StagedCommands.Add(new StagedCommand
            {
                Kind = CommandKind.Assign,
                ShipId = ship.Id,
                OrderId = order.Id,
                RouteOptionIndex = 0,
                Route = option,
            });
        }

        [Test]
        public void CommitPlanning_ExpiresUnhandledInbox()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state); // Ngày 1: O-01, O-02
            StageAssign("tau-tia-chop", "O-01");

            DayRunner.CommitPlanning(state);

            Assert.AreEqual(GamePhase.Simulation, state.Phase);
            Assert.AreEqual(OrderStatus.Assigned, state.Orders[0].Status); // staged đã chốt
            Assert.AreEqual(OrderStatus.Declined, state.Orders[1].Status); // FR-0304: inbox hết hạn
            Assert.AreEqual(0, state.StagedCommands.Count);
        }

        [Test]
        public void Simulate_ShipRunsAssignedRoute_Delivers_ReachesSummary()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state);
            StageAssign("tau-tia-chop", "O-01");

            DayRunner.CommitPlanning(state);
            DayRunner.Simulate(state, world.Incidents, world.FixedFirstDay, world.FixedFinale, world.Crews);

            Assert.AreEqual(GamePhase.Summary, state.Phase);
            var ship = state.Ships.Find(s => s.Id == "tau-tia-chop");
            Assert.AreEqual(ShipStatus.Idle, ship.Status);
            Assert.AreEqual(TestWorld.Vang, ship.PortId);
            Assert.AreEqual(OrderStatus.Delivered, state.Orders[0].Status);
        }

        [Test]
        public void Simulate_Day1FixedIncident_ResolvedAndApplied()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state);
            // O-02 đi tuyến dài 2 route-units (chưa tới đích) → còn Moving khi xét sự cố.
            StageAssign("tau-tia-chop", "O-02");

            // Sự cố cố định Ngày 1 tự dựng: 1 phương án, Δ tiến độ +0.3 cho mọi thuyền trưởng → xác định.
            IncidentDef incident = ScriptableObject.CreateInstance<IncidentDef>();
            incident.incidentId = "I-T1";
            incident.options.Add(new IncidentOption
            {
                label = "A",
                baseProgressDelta = 0.3f,
                entries = { new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal } },
            });
            world.SOs.Add(incident);

            DayRunner.CommitPlanning(state);
            DayRunner.Simulate(state, new List<IncidentDef>(), incident, null, world.Crews);

            // Tàu Fast 1.5 + Δ 0.3 → 1.8 trên tuyến dài 2: vẫn Moving (FR-0506: Δ tiến độ áp vào tàu).
            var ship = state.Ships.Find(s => s.Id == "tau-tia-chop");
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
            Assert.That(ship.DistanceAlongRoute, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.AreEqual(GamePhase.Summary, state.Phase);
        }

        [Test]
        public void CompleteSummary_ScoresDelivery_AdvancesToNextDay()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state);
            StageAssign("tau-tia-chop", "O-01");
            DayRunner.CommitPlanning(state);
            DayRunner.Simulate(state, world.Incidents, world.FixedFirstDay, world.FixedFinale, world.Crews);

            DayRunner.CompleteSummary(state, world.OrderPool);

            // Intact đúng hạn: Uy tín +8 (6+2), Doanh thu +120 (FR-0602).
            Assert.AreEqual(58, state.Reputation);
            Assert.AreEqual(120, state.Revenue);
            Assert.AreEqual(2, state.CurrentDay);
            Assert.AreEqual(GamePhase.Planning, state.Phase);
            // Đơn đã nhận của Ngày 1 không bị nạp lại (OrderState giữ nguyên).
            Assert.IsTrue(state.Orders.Any(o => o.Id == "O-01" && o.Status == OrderStatus.Delivered));
            // Ngày 2 bốc đơn mới từ pool (FR-0306).
            Assert.IsTrue(state.Orders.Any(o => o.Status == OrderStatus.InInbox));
        }

        [Test]
        public void CompleteSummary_BankruptImmediately()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state);
            state.Reputation = 0;

            DayRunner.CompleteSummary(state, world.OrderPool);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.E5_Bankrupt, state.FinalEnding);
        }

        [Test]
        public void CompleteSummary_LastDayFinishesRun()
        {
            OrderScheduler.ScheduleNewOrders(world.OrderPool, state);
            state.CurrentDay = world.Config.gameLengthDays;

            DayRunner.CompleteSummary(state, world.OrderPool);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.E3_Survived, state.FinalEnding); // Uy tín 50
        }
    }
}
