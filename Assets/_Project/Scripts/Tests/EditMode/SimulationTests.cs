using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Hành vi Simulation.Advance (bản chạy liên tục — xem ASSUMPTIONS.md G1).
    /// Bọc lại các test hành vi của 3 file bị xóa khi dời ngày (DayRunnerTests, GameStateFlowTests, SimulationStepTests):
    /// trễ chốt 1 lần, hộp thư hết hạn không tính trễ, 3 trễ thua, đủ N thắng, thua ưu tiên hơn thắng cùng tick,
    /// ApplyProgressDelta không âm / vượt đích giao, tick nhỏ vs gộp tick cho cùng kết quả.
    /// </summary>
    public class SimulationTests
    {
        TestWorld _world;
        SimWorld _sim;
        GameState _state;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _world.Config.incidentChancePerEdge = 0f; // cô lập hành vi đang test — sự cố có test riêng
            _sim = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
            };
            _state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            OrderScheduler.SpawnInitial(_world.OrderPool, _state);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        void AcceptAll()
        {
            foreach (var o in _state.Orders.Where(o => o.Status == OrderStatus.InInbox))
                if (CommandValidator.CanAccept(_state, o.Id))
                    OrderActions.Accept(_state, o.Id);
        }

        // Advance theo tick; sự cố chờ (vd I-F ép khi DeliveredCount == win-1) → chọn phương án đầu rồi chạy tiếp.
        void AdvanceAndResolve(int maxTicks)
        {
            for (int i = 0; i < maxTicks && !_state.IsFinished; i++)
            {
                if (!_state.CanAdvance)
                {
                    var pending = _state.PendingIncidents[0];
                    var resolved = IncidentResolver.Resolve(pending.Incident, pending.Incident.options[0], _state, pending.Context.Ship, _world.Crews);
                    IncidentResolver.Apply(resolved, _state, pending.Context.Ship);
                    Simulation.ApplyProgressDelta(_state, pending.Context.Ship, resolved.ProgressDelta);
                    _state.PendingIncidents.RemoveAt(0);
                    continue;
                }
                Simulation.Advance(_sim, _state, 0.1f);
            }
        }

        // ---- trễ: chốt MỘT LẦN, LateCount++ ngay lúc quá hạn ----------------------------------

        [Test]
        public void QuaHan_LateCountTangDungMotLan()
        {
            AcceptAll();
            var o = _state.Orders.First(x => x.Id == "O-01");
            // chạy tới quá hạn O-01 (hạn 70s) — không giao (không điều tàu)
            for (int i = 0; i < 702; i++) Simulation.Advance(_sim, _state, 0.1f);
            Assert.IsTrue(o.IsLate, "quá hạn phải chốt IsLate");
            Assert.AreEqual(1, _state.LateCount);
            Assert.IsFalse(_state.IsFinished, "1 trễ chưa đủ thua (maxLate 3)");

            // chạy tiếp lâu hơn nữa — không được tăng thêm
            for (int i = 0; i < 100; i++) Simulation.Advance(_sim, _state, 0.1f);
            Assert.AreEqual(1, _state.LateCount, "một đơn chỉ tính trễ đúng MỘT lần");
            Assert.AreEqual(1, _state.Orders.Count(x => x.IsLate));
        }

        // ---- hộp thư hết hạn → xóa, KHÔNG tính trễ (FR-0304 bản liên tục) ----------------------

        [Test]
        public void InboxQuaHan_BiXoa_KhongTinhTre()
        {
            // không nhận đơn nào — cả 2 đơn tutorial nằm hộp thư tới khi hết hạn (70s/85s)
            for (int i = 0; i < 860; i++) Simulation.Advance(_sim, _state, 0.1f);
            Assert.IsFalse(_state.Orders.Any(o => o.Id == "O-01"), "đơn hộp thư quá hạn phải bị xóa");
            Assert.AreEqual(0, _state.LateCount, "hộp thư hết hạn KHÔNG tính trễ");
            Assert.AreEqual(0, _state.Orders.Count(o => o.IsLate));
        }

        // ---- đủ maxLateDeliveries trễ → thua ngay ---------------------------------------------

        [Test]
        public void BaTre_ThuaNgay()
        {
            for (int tick = 0; tick < 3000 && !_state.IsFinished; tick++)
            {
                AcceptAll(); // nhận mọi đơn xuất hiện — cứ để hết hạn
                Simulation.Advance(_sim, _state, 0.1f);
            }
            Assert.IsTrue(_state.IsFinished, "3 lần trễ phải thua ngay");
            Assert.GreaterOrEqual(_state.LateCount, _world.Config.maxLateDeliveries);
            Assert.AreEqual(EndingId.E5_Bankrupt, _state.FinalEnding, "không giao đơn nào → thua quá sớm E5");
        }

        // ---- đủ winDeliveries → thắng ngay (E1 khi 0 trễ) --------------------------------------

        [Test]
        public void DuN_ThangNgay()
        {
            _world.Config.winDeliveries = 1;
            AcceptAll(); // điều tàu cần đơn ở trạng thái ĐÃ NHẬN
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            AdvanceAndResolve(300);
            var o = _state.Orders.First(x => x.Id == "O-01");
            Assert.AreEqual(OrderStatus.Delivered, o.Status);
            Assert.AreEqual(1, _state.DeliveredCount);
            Assert.IsTrue(_state.IsFinished, "đủ winDeliveries phải thắng ngay");
            Assert.AreEqual(EndingId.E1_Empire, _state.FinalEnding, "thắng với 0 trễ → E1");
            Assert.Greater(_state.Revenue, 0);
        }

        // ---- thua-trễ ưu tiên hơn thắng cùng tick ----------------------------------------------

        [Test]
        public void ThuaTre_UuTienHonThang_CungTick()
        {
            // Đặt tàu cách đích nửa tick và đơn sắp hết hạn ngay tick kế:
            // trong MỘT tick Advance cả hai cùng xảy ra — kết cục phải là THUA, không phải thắng.
            _world.Config.winDeliveries = 1;
            _world.Config.maxLateDeliveries = 1;
            AcceptAll(); // điều tàu cần đơn ở trạng thái ĐÃ NHẬN
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            var o = _state.Orders.First(x => x.Id == "O-01");
            float speedPerTick = _world.Config.SpeedUnitsPerSecond(ship.Speed) * 0.1f;
            ship.DistanceAlongRoute = ship.Route.length - speedPerTick * 0.5f;
            o.DeadlineAtSeconds = _state.ElapsedSeconds + 0.05f;

            Simulation.Advance(_sim, _state, 0.1f);

            Assert.IsTrue(_state.IsFinished, "trễ đủ maxLate phải kết thúc ngay trong tick đó");
            Assert.AreEqual(1, _state.LateCount, "trễ được chốt trước khi tàu chạy trong cùng tick");
            Assert.AreEqual(EndingId.E5_Bankrupt, _state.FinalEnding, "thua-trễ ưu tiên hơn thắng cùng tick");
            Assert.AreEqual(0, _state.DeliveredCount, "ván đã thua tick đó → tàu không còn giao");
        }

        // ---- FR-0506: Δ tiến độ không âm; vượt đích thì giao luôn -------------------------------

        [Test]
        public void ApplyProgressDelta_KhongAm_VaVuotDich_Giao()
        {
            _world.Config.winDeliveries = 1;
            AcceptAll(); // điều tàu cần đơn ở trạng thái ĐÃ NHẬN
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            ship.DistanceAlongRoute = 0.5f;

            Simulation.ApplyProgressDelta(_state, ship, -2f); // Δ âm lớn hơn vị trí → kẹp về 0
            Assert.AreEqual(0f, ship.DistanceAlongRoute, 0.0001f, "tiến độ không được âm");
            Assert.AreEqual(ShipStatus.Moving, ship.Status);

            Simulation.ApplyProgressDelta(_state, ship, 2f); // vượt đích → giao luôn
            var o = _state.Orders.First(x => x.Id == "O-01");
            Assert.AreEqual(OrderStatus.Delivered, o.Status, "vượt đích thì giao luôn");
            Assert.AreEqual(ShipStatus.Idle, ship.Status);
            Assert.AreEqual(TestWorld.Vang, ship.PortId);
            Assert.Greater(_state.Revenue, 0);
            Assert.AreEqual(1, _state.DeliveredCount);
        }

        // ---- cùng ván: 1 tick lớn (gộp) vs nhiều tick nhỏ cho CÙNG kết quả cuối -----------------

        [Test]
        public void GopTick_Vs_TickNho_CungKetQuaCuoi()
        {
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 11);
            var b = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 11);
            OrderScheduler.SpawnInitial(_world.OrderPool, a);
            OrderScheduler.SpawnInitial(_world.OrderPool, b);

            // cùng hành động người chơi lúc 0 tick: nhận + điều tàu
            foreach (var s in new[] { a, b })
            {
                foreach (var o in s.Orders.Where(o => o.Status == OrderStatus.InInbox))
                    OrderActions.Accept(s, o.Id);
                var ship = s.Ships.First(x => x.PortId == TestWorld.Goc);
                FleetActions.Assign(s, _world.Routes, ship.Id, "O-01", 0);
            }

            for (int i = 0; i < 300; i++) Simulation.Advance(_sim, a, 0.1f); // 300 tick nhỏ (30s)
            Simulation.AdvanceWithFixedTimestep(_sim, b, 30f);              // 1 lần gộp 300 tick

            Assert.AreEqual(a.ElapsedSeconds, b.ElapsedSeconds, 0.0001f);
            Assert.AreEqual(a.DeliveredCount, b.DeliveredCount, "gộp tick phải cho cùng số đơn giao");
            Assert.AreEqual(a.LateCount, b.LateCount);
            Assert.AreEqual(a.Revenue, b.Revenue);
            Assert.AreEqual(a.Orders.Count, b.Orders.Count);
            for (int i = 0; i < a.Ships.Count; i++)
            {
                Assert.AreEqual(a.Ships[i].Status, b.Ships[i].Status, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].DistanceAlongRoute, b.Ships[i].DistanceAlongRoute, 0.0001f, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].PortId, b.Ships[i].PortId, "tàu {0}", i);
            }
        }
    }
}
