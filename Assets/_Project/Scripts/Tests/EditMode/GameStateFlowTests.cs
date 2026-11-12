using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Luồng 8 ngày theo §5 (mẫu đầy đủ): mỗi ngày Planning → MAKE IT SO → Simulation/Incidents → Summary.
    /// Chính sách test: đơn đầu tiềm năng được nhận khi còn slot, gán vào tàu gần đúng nhất, MAKE IT SO luôn bấm.
    /// Chạy nhiều seed: không treo, không ngoại lệ, kết thúc có ending (FR-0507, NFR-06 — mécanics-only;
    /// ngưỡng NFR-07 được kiểm tra bằng BalanceProbe ở Bước 6).
    /// </summary>
    public class GameStateFlowTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        /// <summary>Chính sách: nhận đơn đầu còn slot; gán mọi tàu đang đứng ở đúng port của đơn; MAKE IT SO luôn.</summary>
        private void PlayDayAuto(GameState state)
        {
            Assert.AreEqual(GamePhase.Planning, state.Phase);

            foreach (var order in state.Orders.Where(o => o.Status == OrderStatus.InInbox).ToList())
                if (CommandValidator.CanAccept(state, order.Id))
                    order.Status = OrderStatus.Accepted;

            foreach (var order in state.Orders.Where(o => o.Status == OrderStatus.Accepted && o.AssignedShipId == "").ToList())
            {
                var options = _world.Routes.GetOptions(order.FromPortId, order.ToPortId);
                if (options == null) continue;
                foreach (var option in options)
                {
                    var ship = state.Ships.FirstOrDefault(s => s.Id != null && s.Status == ShipStatus.Idle && s.PortId == order.FromPortId);
                    if (ship == null) break;
                    if (CommandValidator.ValidateAssign(state, _world.Routes, ship.Id, order.Id, 0) == null)
                    {
                        var staged = state.StagedCommands;
                        if (staged.All(c => c.ShipId != ship.Id))
                        {
                            staged.Add(new StagedCommand { Kind = CommandKind.Assign, ShipId = ship.Id, OrderId = order.Id, Route = option });
                        }
                    }
                }
            }

            SimulationStep.CommitStaged(state);
            state.Phase = GamePhase.Simulation;

            bool finale = state.CurrentDay >= state.Config.gameLengthDays;
            var incidents = IncidentScheduler.Schedule(_world.Incidents, _world.FixedFirstDay, _world.FixedFinale, state, SimulationStep.MovingContexts(state));
            foreach (var pending in incidents)
            {
                var option = pending.Incident.options[0];
                var resolved = IncidentResolver.Resolve(pending.Incident, option, state, pending.Context.Ship, _world.Crews);
                IncidentResolver.Apply(resolved, state, pending.Context.Ship);
                SimulationStep.ApplyProgressDelta(state, pending.Context.Ship, resolved.ProgressDelta);
            }

            SimulationStep.MoveAll(state);

            // Summary (FR-0601): điểm giao trong ngày
            foreach (var order in state.Orders.Where(o => o.Status == OrderStatus.Delivered && o.DeliveredDay == state.CurrentDay).ToList())
            {
                var (rep, rev) = ScoreCalculator.ScoreDelivery(order, state.Config);
                state.Reputation += rep;
                state.Revenue += rev;
            }
            state.Phase = GamePhase.Summary;

            if (state.Reputation <= 0) { EndingEvaluator.FinishRun(state); return; }

            if (state.CurrentDay >= state.Config.gameLengthDays)
            {
                EndingEvaluator.FinishRun(state);
            }
            else
            {
                OrderScheduler.ScheduleNewOrders(_world.OrderPool, state);
                state.CurrentDay++;
                state.Phase = GamePhase.Planning;
            }
        }

        [Test]
        public void FullRun_MultipleSeeds_FinishesWithEnding_NoSoftlock()
        {
            foreach (var seed in new[] { 1, 7, 42, 1234, 99_999 })
            {
                var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed);
                OrderScheduler.ScheduleNewOrders(_world.OrderPool, state);

                int guard = 0;
                while (!state.IsFinished && guard++ < 50)
                {
                    Assert.LessOrEqual(guard, 20, "chạy quá 20 ngày → treo (softlock)");
                    PlayDayAuto(state);
                }

                Assert.IsTrue(state.IsFinished, "seed " + seed + " phải kết thúc có ending");
                Assert.AreNotEqual(EndingId.None, state.FinalEnding, "seed " + seed + " phải có ending");
            }
        }

        [Test]
        public void FullRun_SameSeed_Deterministic()
        {
            var a = RunToEnd(4242);
            var b = RunToEnd(4242);
            Assert.AreEqual(a.reputation, b.reputation);
            Assert.AreEqual(a.revenue, b.revenue);
            Assert.AreEqual(a.delivered, b.delivered);
            Assert.AreEqual(a.ending, b.ending);
        }

        [Test]
        public void FullRun_Day1_TutorialOrdersFixed()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 5);
            OrderScheduler.ScheduleNewOrders(_world.OrderPool, state);
            Assert.That(state.Orders.Any(o => o.Id == "O-01"));
            Assert.That(state.Orders.Any(o => o.Id == "O-02"));
            Assert.AreEqual(2, state.Orders.Count);
        }

        [Test]
        public void Day1_FixedIncident_LandsOnFirstFogCrossingShip()
        {
            // FR-0502: ngày 1, I-01 rơi vào tàu ĐẦU TIÊN đi qua cạnh Sương mù (O-02 đi qua Sương Mù).
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 5);
            OrderScheduler.ScheduleNewOrders(_world.OrderPool, state);

            var o02 = state.Orders.First(o => o.Id == "O-02");
            o02.Status = OrderStatus.Accepted;
            var ship = state.Ships.First(s => s.PortId == "goc" && s.Status == ShipStatus.Idle);
            var options = _world.Routes.GetOptions(o02.FromPortId, o02.ToPortId);
            var fogRoute = options.First(o => o.hazards.Contains(HazardType.Fog));
            state.StagedCommands.Add(new StagedCommand { Kind = CommandKind.Assign, ShipId = ship.Id, OrderId = o02.Id, Route = fogRoute });

            SimulationStep.CommitStaged(state);
            var pendings = IncidentScheduler.Schedule(
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, state, SimulationStep.MovingContexts(state));

            Assert.AreEqual(1, pendings.Count);
            Assert.AreEqual("I-01", pendings[0].Incident.incidentId);
            Assert.AreEqual(ship.Id, pendings[0].Context.Ship.Id);
            Assert.That(pendings[0].Context.EdgeHazards.Contains(HazardType.Fog));
        }

        private (int reputation, int revenue, int delivered, EndingId ending) RunToEnd(int seed)
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed);
            OrderScheduler.ScheduleNewOrders(_world.OrderPool, state);
            while (!state.IsFinished) PlayDayAuto(state);
            return (state.Reputation, state.Revenue, state.Orders.Count(o => o.Status == OrderStatus.Delivered), state.FinalEnding);
        }
    }
}
