using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// G5 — roaming có mục đích: Simulation điều tàu rảnh theo hồ sơ tính cách (RoamPlanner),
    /// xác định theo seed, RoamRng RIÊNG không đụng state.Rng (ASSUMPTIONS.md mục G5).
    /// Chỉ giao việc khi tàu đã cập cảng; không quay đầu giữa cạnh; không roll sự cố khi roaming.
    /// </summary>
    public class RoamTests
    {
        TestWorld _world;
        SimWorld _sim;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _world.Config.incidentChancePerEdge = 0f; // cô lập — sự cố có test riêng
            // Cô lập roaming: pool RỖNG → Simulation không bốc đơn tự động nào.
            // (Quy tắc dư thừa: tàu chỉ rời cảng khi nhu cầu tại cảng < số tàu Idle — đơn tutorial
            // sinh lúc 0s tại Goc sẽ giữ tàu ở lại mãi). Test cần nhu cầu tự tạo OrderDef riêng,
            // test Rng-isolation dựng pool thật riêng.
            _world.OrderPool.fixedFirstDay = new List<OrderDef>();
            _world.OrderPool.pool = new List<OrderDef>();
            _sim = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
                HQPortId = TestWorld.Goc,
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        void Advance(GameState state, int ticks, SimWorld sim = null)
        {
            var w = sim ?? _sim;
            for (int i = 0; i < ticks; i++) Simulation.Advance(w, state, 0.1f);
        }

        // Chạy tới khi tàu thoát Roaming (về Idle) — tránh chọn mốc tick khớp chính xác.
        static void AdvanceUntilIdle(SimWorld sim, GameState state, int maxTicks)
        {
            int guard = 0;
            while (state.Ships[0].Status == ShipStatus.Roaming && guard++ < maxTicks)
                Simulation.Advance(sim, state, 0.1f);
            Assert.That(guard, Is.LessThan(maxTicks), "tàu phải cập cảng trong giới hạn tick");
        }

        static RoamProfile Profile(float dwell = 1f, float demand = 0f, float far = 0f,
            float hub = 0f, float wander = 0f, float back = 5f) => new RoamProfile
        {
            dwellSeconds = dwell, demandWeight = demand, farBias = far,
            hubAffinity = hub, wanderWeight = wander, backtrackPenalty = back,
        };

        static ShipDef OneShip(string id, RoamProfile roam, string port)
        {
            var d = ScriptableObject.CreateInstance<ShipDef>();
            d.shipId = id; d.displayName = id; d.captain = CrewId.Bob;
            d.speedClass = SpeedClass.Normal; d.startPortId = port; d.roam = roam;
            return d;
        }

        GameState Run(ShipDef ship, float roamStart = 0f, bool spawnOrders = false)
        {
            _world.Config.roamStartSeconds = roamStart;
            var state = GameStateBuilder.CreateRun(_world.Config, new[] { ship }, seed: 7);
            if (spawnOrders) OrderScheduler.SpawnInitial(_world.OrderPool, state);
            return state;
        }

        // 1. Hết nghỉ + dư thừa + có cảng kề → chuyển Roaming trên MỘT cạnh trực tiếp.
        [Test]
        public void RoiCang_ChuyenRoaming_CanhTrucTiep()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); // 1.5s — qua dwell 1s, không ân hạn (roamStart = 0)
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "hết nghỉ + dư thừa phải rời cảng");
            Assert.AreEqual(2, s.Route.portPath.Count, "roaming chỉ đi 1 cạnh trực tiếp");
            Assert.AreEqual(TestWorld.Goc, s.LastPortId, "LastPortId = cảng vừa rời");
        }

        // 2. Roaming tới đích → Idle, PortId = đích, LastPortId = cảng cũ, timer = dwell.
        [Test]
        public void Roaming_ToDich_VeIdle_DungTrangThai()
        {
            _world.Config.roamSpeedFactor = 7f; // không phải số cân bằng — chỉ để test tới đích nhanh
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); // rời cảng ở ~1.1s
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");
            AdvanceUntilIdle(_sim, state, 200);
            s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status);
            Assert.AreEqual(TestWorld.Vang, s.PortId, "hòa điểm → lấy cảng kề đầu tiên (Vang) — xác định");
            Assert.AreEqual(TestWorld.Goc, s.LastPortId);
            Assert.AreEqual(1f, s.RoamTimerSeconds, 0.01f, "timer đặt lại = dwellSeconds");
        }

        // 3. Ân hạn roamStartSeconds: trước mốc không tàu nào rời cảng.
        [Test]
        public void TruocRoamStart_KhongTauNaoroiCang()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 0.5f), TestWorld.Goc), roamStart: 30f);
            Advance(state, 290); // 29s — vẫn trong ân hạn
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status);
            Assert.AreEqual(TestWorld.Goc, s.PortId);
            Advance(state, 30); // +3s → 32s: qua ân hạn + dwell → phải rời
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "qua ân hạn và dư thừa thì rời");
        }

        // 4. Tàu KHÔNG dư thừa (đơn đang chờ tại cảng ≥ số tàu Idle) thì ở lại.
        [Test]
        public void TauKhongDuThua_OLaiCang()
        {
            // Tự tạo 1 đơn tại Goc (pool SetUp đã rỗng): nhu cầu 1 ≥ 1 tàu Idle → không dư thừa.
            var od = ScriptableObject.CreateInstance<OrderDef>();
            _world.SOs.Add(od);
            od.orderId = "O-T4"; od.customerName = "T4"; od.fromPortId = TestWorld.Goc;
            od.toPortId = TestWorld.Vang; od.deadlineSeconds = 999f; od.revenue = 1; od.weight = 1;
            _world.OrderPool.fixedFirstDay.Add(od);

            var state = Run(OneShip("S-1", Profile(dwell: 0.5f), TestWorld.Goc), spawnOrders: true);
            Advance(state, 200); // 20s — lâu hơn dwell rất nhiều
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status, "đơn đang chờ ngay tại cảng → tàu ở lại");
            Assert.AreEqual(TestWorld.Goc, s.PortId);
        }

        // 5. Tàu Roaming bị từ chối Assign/Reposition, lý do nêu đích + số giây còn lại.
        [Test]
        public void Roaming_BiTuChoiLenh_CoDichVaThoiGian()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); // Roaming Goc→Vang
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");

            string errAssign = CommandValidator.ValidateAssign(state, _world.Routes, s.Id, "O-KHONG-TON-TAI", 0);
            Assert.IsNotNull(errAssign, "tàu Roaming không nhận lệnh giữa cạnh");
            StringAssert.Contains("vang", errAssign, "lý do phải nêu đích đến (id cảng đích)");
            StringAssert.Contains("cập cảng", errAssign);

            string errRepos = CommandValidator.ValidateReposition(state, _world.Routes, s.Id, TestWorld.Suong, 0);
            Assert.IsNotNull(errRepos);
            StringAssert.Contains("vang", errRepos);
        }

        // 6. Không quay lại ngay: cảng kế ≠ LastPortId (backtrackPenalty dương, wander 0).
        [Test]
        public void KhongQuayLaiNgay_CangKeKhacLastPort()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f, back: 5f, wander: 0f), TestWorld.Camap));
            state.Ships[0].LastPortId = TestWorld.Suong; // giả lập vừa đi Suong → Camap
            Advance(state, 15);
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status);
            Assert.AreEqual(TestWorld.Goc, s.Route.ToPort,
                "Suong bị trừ điểm backtrack → chọn Goc (hòa với Tram, lấy đầu theo thứ tự ổn định)");
            Assert.AreNotEqual(TestWorld.Suong, s.Route.ToPort);
        }

        // 7. Xác định: cùng seed → cùng chuỗi roaming.
        [Test]
        public void CungSeed_CungChuoiRoaming()
        {
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            var b = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            OrderScheduler.SpawnInitial(_world.OrderPool, a);
            OrderScheduler.SpawnInitial(_world.OrderPool, b);
            Advance(a, 600); // 60s — qua ân hạn + vài chuyến roaming
            Advance(b, 600);
            for (int i = 0; i < a.Ships.Count; i++)
            {
                Assert.AreEqual(a.Ships[i].Status, b.Ships[i].Status, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].PortId, b.Ships[i].PortId, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].LastPortId, b.Ships[i].LastPortId, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].DistanceAlongRoute, b.Ships[i].DistanceAlongRoute, 0.001f, "tàu {0}", i);
            }
        }

        // 8. Cô lập Rng: roaming bật/tắt → chuỗi đơn sinh ra GIỐNG HỆT (không đụng state.Rng).
        [Test]
        public void RoamKhongDungRngChung_ChoiDonGiongHet()
        {
            // Pool THẬT của DemoData: có sự kiện bốc đơn dùng state.Rng (PickWeighted) —
            // nếu roaming lạm dụng state.Rng thì chuỗi đơn hai phía sẽ lệch.
            var realPool = DemoData.BuildOrderPool();
            _world.SOs.Add(realPool);
            foreach (var o in realPool.fixedFirstDay) _world.SOs.Add(o);
            foreach (var o in realPool.pool) _world.SOs.Add(o);
            var sim = new SimWorld
            {
                Pool = realPool, Routes = _world.Routes, Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
                HQPortId = TestWorld.Goc,
            };

            _world.Config.roamStartSeconds = 0f;
            _world.Config.roamEnabled = true;
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            OrderScheduler.SpawnInitial(realPool, a);
            _world.Config.roamEnabled = false;
            var b = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            OrderScheduler.SpawnInitial(realPool, b);
            _world.Config.roamEnabled = true;

            Advance(a, 600, sim); // 60s: tutorial 0s + bốc thêm từ pool, tàu A roaming từ 0s
            Advance(b, 600, sim);

            var oa = a.Orders;
            var ob = b.Orders;
            Assert.AreEqual(oa.Count, ob.Count, "số đơn phải giống hệt");
            for (int i = 0; i < oa.Count; i++)
            {
                Assert.AreEqual(oa[i].Id, ob[i].Id, "đơn {0}", i);
                Assert.AreEqual(oa[i].Status, ob[i].Status, "đơn {0}", i);
                Assert.AreEqual(oa[i].FromPortId, ob[i].FromPortId, "đơn {0}", i);
                Assert.AreEqual(oa[i].ToPortId, ob[i].ToPortId, "đơn {0}", i);
                Assert.AreEqual(oa[i].DeadlineAtSeconds, ob[i].DeadlineAtSeconds, 0.0001f, "đơn {0}", i);
            }
            Assert.AreEqual(a.OrderSpawnCount, b.OrderSpawnCount);
        }

        // 9. Tàu không Roam (null) hoặc không có cảng kề trực tiếp → ở yên Idle.
        [Test]
        public void KhongRoam_HoacKhongCoCangKe_OLaiYen()
        {
            var shipA = OneShip("S-A", null, TestWorld.Goc);          // Roam == null
            var shipB = OneShip("S-B", Profile(dwell: 0.5f), "no-net"); // cảng không kề ai
            var defs = new List<ShipDef> { shipA, shipB };
            _world.SOs.Add(shipA);
            _world.SOs.Add(shipB);

            // RouteTable riêng chỉ có cặp Goc–Vang → "no-net" không kề cảng nào
            var rt = ScriptableObject.CreateInstance<RouteTable>();
            _world.SOs.Add(rt);
            rt.pairs = new List<RoutePairEntry>
            {
                new RoutePairEntry
                {
                    fromPortId = TestWorld.Goc, toPortId = TestWorld.Vang,
                    options = new List<RouteOption>
                    {
                        new RouteOption
                        {
                            label = "thẳng", length = 1f,
                            portPath = new List<string> { TestWorld.Goc, TestWorld.Vang },
                            hazards = new List<HazardType> { HazardType.Clear },
                        },
                    },
                },
            };
            var sim = new SimWorld
            {
                Pool = _world.OrderPool, Routes = rt, Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
                HQPortId = TestWorld.Goc,
            };

            _world.Config.roamStartSeconds = 0f;
            var state = GameStateBuilder.CreateRun(_world.Config, defs, seed: 7);
            for (int i = 0; i < 60; i++) Simulation.Advance(sim, state, 0.1f); // 6s

            var a = state.Ships.First(x => x.Id == "S-A");
            var b = state.Ships.First(x => x.Id == "S-B");
            Assert.AreEqual(ShipStatus.Idle, a.Status, "Roam == null → không roaming");
            Assert.AreEqual(ShipStatus.Idle, b.Status, "không có cảng kề → ở yên");
            Assert.AreEqual("no-net", b.PortId);
        }

        // 10. Roaming tạm dừng khi có sự cố chờ (CanAdvance == false).
        [Test]
        public void Roam_DungKhiSuCoCho()
        {
            _world.Config.roamSpeedFactor = 7f;
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15);
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");
            float d0 = s.DistanceAlongRoute;

            state.PendingIncidents.Add(new PendingIncident
            {
                Incident = _world.FixedFirstDay,
                Context = new IncidentContext { Ship = s },
            });
            Advance(state, 30);
            Assert.AreEqual(d0, s.DistanceAlongRoute, 0.0001f, "đồng hồ dừng → roaming dừng");
            Assert.AreEqual(ShipStatus.Roaming, s.Status);

            state.PendingIncidents.Clear();
            AdvanceUntilIdle(_sim, state, 200);
            s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status, "xử lý xong → roaming chạy tiếp tới đích");
            Assert.AreEqual(TestWorld.Vang, s.PortId);
        }

        // 11b. DemoData: mọi tàu mẫu phải có hồ sơ roam (khác null, dwell > 0) — bẫy lỗi quên gán s.roam.
        [Test]
        public void DemoDataShips_CoHoSoRoam()
        {
            foreach (var d in DemoData.BuildShips())
            {
                _world.SOs.Add(d);
                Assert.IsNotNull(d.roam, $"{d.shipId} phải có hồ sơ roam");
                Assert.Greater(d.roam.dwellSeconds, 0f, $"{d.shipId}.dwellSeconds");
            }
        }

        // 12. Hồ sơ khác nhau → thời điểm rời cảng khác nhau.
        [Test]
        public void DwellKhac_ThoiDiemRoiKhac()
        {
            var defs = new List<ShipDef>
            {
                OneShip("S-A", Profile(dwell: 2f), TestWorld.Goc),
                OneShip("S-B", Profile(dwell: 20f), TestWorld.Goc),
            };
            _world.SOs.Add(defs[0]);
            _world.SOs.Add(defs[1]);

            _world.Config.roamStartSeconds = 0f;
            var state = GameStateBuilder.CreateRun(_world.Config, defs, seed: 7);
            Advance(state, 40); // 4s: S-A (dwell 2) đã rời, S-B (dwell 20) chưa

            var a = state.Ships.First(x => x.Id == "S-A");
            var b = state.Ships.First(x => x.Id == "S-B");
            Assert.AreEqual(ShipStatus.Roaming, a.Status);
            Assert.AreEqual(ShipStatus.Idle, b.Status, "dwell 20s → chưa rời sau 4s");
        }
    }
}
