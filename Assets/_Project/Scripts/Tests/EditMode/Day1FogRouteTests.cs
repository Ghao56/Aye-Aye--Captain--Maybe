using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Coverage theo yêu cầu boss (bản CHẠY LIÊN TỤC — xem ASSUMPTIONS.md G1):
    /// (1) I-01 bắt buộc xảy ra lần đầu một tàu vào cạnh Sương mù (cạnh đầu của tuyến O-02);
    /// (2) tuyến O-02 hai chặng (Gốc→Sương Mù→Cá Mập) đi qua node giữa rồi giao đúng hạn.
    /// </summary>
    public class Day1FogRouteTests
    {
        private TestWorld _world;
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            OrderScheduler.SpawnInitial(_world.OrderPool, _state); // 2 đơn tutorial lúc 0 giây
            // ValidateAssign chỉ nhận đơn đã Accept (FR-0302).
            foreach (var order in _state.Orders.Where(o => o.Status == OrderStatus.InInbox))
                order.Status = OrderStatus.Accepted;
            // Cô lập I-01: tắt sự cố ngẫu nhiên để không nhiễu.
            _world.Config.incidentChancePerEdge = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        private void AdvanceUntil(GameState state, SimWorld world, System.Func<bool> done, int maxTicks = 100000)
        {
            for (int i = 0; i < maxTicks && !done(); i++)
            {
                if (!state.CanAdvance)
                {
                    if (state.IsFinished) return;
                    // Sự cố chờ dừng đồng hồ → chọn phương án đầu (xác định) rồi chạy tiếp.
                    var pending = state.PendingIncidents[0];
                    var resolved = IncidentResolver.Resolve(pending.Incident, pending.Incident.options[0], state, pending.Context.Ship, _world.Crews);
                    IncidentResolver.Apply(resolved, state, pending.Context.Ship);
                    Simulation.ApplyProgressDelta(state, pending.Context.Ship, resolved.ProgressDelta);
                    state.PendingIncidents.RemoveAt(0);
                    continue;
                }
                Simulation.Advance(world, state, state.Config.simTickSeconds);
            }
        }

        [Test]
        public void I01_RoiVaoTauDauTienVaoCanhSuongMu()
        {
            var tiaChop = _state.Ships.First(s => s.Captain == CrewId.Bob);   // O-01: Gốc→Vàng (Thông thoáng)
            var ruaVang = _state.Ships.First(s => s.Captain == CrewId.Chen);  // O-02: Gốc→Cá Mập, cạnh đầu là Sương Mù
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, tiaChop.Id, "O-01", 0));
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, ruaVang.Id, "O-02", 0));
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, tiaChop.Id, "O-01", 0));
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ruaVang.Id, "O-02", 0));

            var world = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents, FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
            };
            AdvanceUntil(_state, world, () => _state.PendingIncidents.Count > 0);

            Assert.AreEqual(1, _state.PendingIncidents.Count, "đúng 1 sự cố cố định I-01");
            Assert.AreEqual("I-01", _state.PendingIncidents[0].Incident.incidentId);
            Assert.AreEqual(ruaVang.Id, _state.PendingIncidents[0].Context.Ship.Id,
                "I-01 rơi vào tàu đầu tiên vào cạnh Sương mù (cạnh 0 lúc xuất phát)");
        }

        [Test]
        public void TuyenO02_HaiChang_QuaNodeGiua_RoiGiaoDungHan()
        {
            var mira = _state.Ships.First(s => s.Captain == CrewId.Mira); // Normal 1.0 — đang ở Gốc
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, mira.Id, "O-02", 0));
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, mira.Id, "O-02", 0));

            Assert.AreEqual(ShipStatus.Moving, mira.Status);
            Assert.AreEqual(3, mira.Route.portPath.Count, "tuyến O-02 phải hai chặng (3 node): Gốc → Sương Mù → Cá Mập");
            Assert.IsTrue(mira.Route.hazards.Contains(HazardType.Fog));

            var world = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents, FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
            };

            // Chạy tới node giữa Sương Mù: ArrivedPortId = Suong, chưa giao.
            bool sawMidNode = false;
            AdvanceUntil(_state, world, () =>
            {
                if (mira.ArrivedPortId == TestWorld.Suong && mira.Status == ShipStatus.Moving) sawMidNode = true;
                return sawMidNode;
            });
            Assert.IsTrue(sawMidNode, "tàu phải đi qua node giữa Sương Mù");
            Assert.AreEqual(ShipStatus.Moving, mira.Status);

            // Chạy tiếp tới Cá Mập → giao ngay, đúng hạn (chưa từng trễ).
            AdvanceUntil(_state, world, () => mira.Status == ShipStatus.Idle);
            Assert.AreEqual(ShipStatus.Idle, mira.Status);
            var order = _state.Orders.First(o => o.Id == "O-02");
            Assert.AreEqual(OrderStatus.Delivered, order.Status);
            Assert.IsFalse(order.IsLate, "phải giao trước DeadlineAtSeconds");
            Assert.LessOrEqual(_state.ElapsedSeconds, order.DeadlineAtSeconds);
            Assert.AreEqual(1, _state.DeliveredCount);
            Assert.AreEqual(0, _state.LateCount);
        }
    }
}
