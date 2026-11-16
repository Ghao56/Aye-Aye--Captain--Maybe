using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

    public class OrderSchedulerTests
    {
        TestWorld _world;
        [SetUp]

        public void SetUp()
        {
            _world = TestWorld.Create();
            _world.Config.incidentChancePerEdge = 0f; 
        }
        [TearDown]

        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }
        SimWorld Sim() => new SimWorld
        {
            Pool = _world.OrderPool, Routes = _world.Routes,
            Incidents = _world.Incidents,
            FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
            HQPortId = TestWorld.Goc,
        };
        GameState State(int seed)
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed);
            OrderScheduler.SpawnInitial(_world.OrderPool, state);
            return state;
        }
        [Test]

        public void VanMoi_OrderRong_Tai0Giay()
        {
            var state = State(7);
            Assert.AreEqual(0, state.Orders.Count, "ván mới phải KHÔNG có đơn (bỏ SpawnInitial O-01/O-02)");
            Assert.AreEqual(_world.Config.orderFirstDelaySeconds, state.NextOrderSpawnAtSeconds,
                "mốc bốc đơn đầu = orderFirstDelaySeconds");
            Assert.AreEqual(0, state.OrderSpawnCount);
        }
        [Test]

        public void TruocMocLamQuen_AdvanceKhongSinhDon()
        {
            var state = State(7);
            var sim = Sim();
            for (int i = 0; i < 249; i++) Simulation.Advance(sim, state, 0.1f); 
            Assert.AreEqual(0, state.Orders.Count, "trước orderFirstDelaySeconds không được sinh đơn");
            Assert.AreEqual(0, state.OrderSpawnCount);
        }
        [Test]

        public void DonDauXuatHienTaiMocLamQuen()
        {
            var state = State(7);
            var sim = Sim();
            for (int i = 0; i < 251; i++) Simulation.Advance(sim, state, 0.1f); 
            Assert.AreEqual(1, state.Orders.Count, "đúng 1 đơn đầu tiên sau thời gian làm quen");
            Assert.AreEqual("O-G1", state.Orders[0].Id);
        }
        [Test]

        public void SpawnOrders_DonDauTaiMoc_IdDuyNhatO_G1()
        {
            var state = State(7);
            state.ElapsedSeconds = _world.Config.orderFirstDelaySeconds + 0.1f;
            OrderScheduler.SpawnOrders(Sim(), state);
            Assert.AreEqual(1, state.Orders.Count);
            Assert.AreEqual("O-G1", state.Orders[0].Id);
            Assert.AreEqual(OrderStatus.InInbox, state.Orders[0].Status);
            Assert.That(state.UsedOrderIds, Does.Contain("O-G1"));
        }
        [Test]

        public void SameSeed_CungChuoiDon()
        {
            var a = State(42);
            var b = State(42);
            var sim = Sim();
            for (int i = 0; i < 6; i++)
            {
                float t = _world.Config.orderFirstDelaySeconds + i * _world.Config.orderIntervalSeconds + 0.1f;
                a.ElapsedSeconds = t; OrderScheduler.SpawnOrders(sim, a);
                b.ElapsedSeconds = t; OrderScheduler.SpawnOrders(sim, b);
            }
            Assert.AreEqual(a.Orders.Count, b.Orders.Count);
            for (int i = 0; i < a.Orders.Count; i++)
            {
                Assert.AreEqual(a.Orders[i].Id, b.Orders[i].Id, "đơn {0}", i);
                Assert.AreEqual(a.Orders[i].FromPortId, b.Orders[i].FromPortId);
                Assert.AreEqual(a.Orders[i].ToPortId, b.Orders[i].ToPortId);
                Assert.AreEqual(a.Orders[i].DeadlineAtSeconds, b.Orders[i].DeadlineAtSeconds, 0.0001f);
                Assert.AreEqual(a.Orders[i].Revenue, b.Orders[i].Revenue);
            }
        }
        [Test]

        public void Interval_DecaysPerSpawn_FlooredAtMin()
        {
            _world.Config.orderIntervalDecayPerOrder = 0.5f;
            var state = State(9); 
            var sim = Sim();
            state.ElapsedSeconds = 25f;
            OrderScheduler.SpawnOrders(sim, state);
            Assert.AreEqual(65f, state.NextOrderSpawnAtSeconds, "interval 40 * decay^0 = 40 → Next=65");
            state.ElapsedSeconds = 65f;
            OrderScheduler.SpawnOrders(sim, state);
            Assert.AreEqual(85f, state.NextOrderSpawnAtSeconds, "interval 40 * 0.5 = 20 → Next=85");
            state.ElapsedSeconds = 85f;
            OrderScheduler.SpawnOrders(sim, state);
            Assert.AreEqual(100f, state.NextOrderSpawnAtSeconds, "interval 10 < min 15 → Next=100");
        }
        [Test]

        public void InboxDuNguoc_BoLuotSinh_NhungVanDiMoc()
        {
            _world.Config.maxInboxOrders = 2;
            _world.Config.orderIntervalDecayPerOrder = 0.5f; 
            var state = State(3);
            var sim = Sim();
            state.ElapsedSeconds = 85f;
            OrderScheduler.SpawnOrders(sim, state);
            Assert.AreEqual(2, state.Orders.Count, "hộp thư đạt maxInboxOrders → không sinh thêm");
            Assert.AreEqual(2, state.OrderSpawnCount, "lượt bị bỏ không tính là đơn đã sinh");
            Assert.AreEqual(100f, state.NextOrderSpawnAtSeconds, "mốc kế tiếp vẫn dời (interval min 15)");
        }
    }
}

