using System.Collections.Generic;
using System.Linq;
using System.Text;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

    public class StuckClockTests
    {
        TestWorld _world;
        SimWorld _sim;
        [SetUp]

        public void SetUp()
        {
            _world = TestWorld.Create();
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
        static string Dump(GameState state, string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[StuckClock] {reason}");
            sb.AppendLine($"  t={state.ElapsedSeconds:0.0}s CanAdvance={state.CanAdvance} " +
                $"IsFinished={state.IsFinished} chờ={state.PendingIncidents.Count} " +
                $"giao={state.DeliveredCount} trễ={state.LateCount} doanhThu={state.Revenue}");
            foreach (var s in state.Ships)
                sb.AppendLine($"  tàu {s.Id} ({s.Captain}) status={s.Status} cảng={s.PortId} " +
                    $"route={(s.Route != null ? s.Route.FromPort + "→" + s.Route.ToPort : "null")} " +
                    $"điđược={s.DistanceAlongRoute:0.##} đơn={s.OrderId}");
            foreach (var o in state.Orders)
                sb.AppendLine($"  đơn {o.Id} [{o.Status}] {o.FromPortId}→{o.ToPortId} " +
                    $"hạn={o.DeadlineAtSeconds:0.#} trễ={o.IsLate} tàu={o.AssignedShipId}");
            foreach (var p in state.PendingIncidents)
                sb.AppendLine($"  sự cố chờ {p.Incident.incidentId} tàu={p.Context.Ship?.Id}");
            return sb.ToString();
        }
        bool RunOne(int seed, bool cluePolicy, float maxSeconds)
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed);
            OrderScheduler.SpawnInitial(_world.OrderPool, state);
            float lastElapsed = -1f;
            int frozenTicks = 0; 
            bool loggedDeliver3 = false;
            float lastActiveIdCheck = 0f;
            var activeSeen = new HashSet<string>();
            while (!state.IsFinished && state.ElapsedSeconds < maxSeconds)
            {
                bool hadPending = state.PendingIncidents.Count > 0;
                BalanceProbe.Step(_sim, state, _world.Crews, cluePolicy);
                if (!state.IsFinished && !hadPending && state.ElapsedSeconds <= lastElapsed)
                {
                    frozenTicks++;
                    if (frozenTicks > 600) 
                    {
                        Debug.Log(Dump(state, $"ĐỒNG HỒ ĐỨNG — seed {seed}, chính sách {(cluePolicy ? "manh mối" : "ngẫu nhiên")}"));
                        return false;
                    }
                }
                else frozenTicks = 0;
                lastElapsed = state.ElapsedSeconds;
                foreach (var o in state.Orders)
                    if (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned)
                        activeSeen.Add(o.Id);
                if (state.ElapsedSeconds - lastActiveIdCheck >= 1f)
                {
                    lastActiveIdCheck = state.ElapsedSeconds;
                    foreach (var id in activeSeen)
                    {
                        var o = state.Orders.Find(x => x.Id == id);
                        if (o == null || o.Status == OrderStatus.InInbox || o.Status == OrderStatus.Declined)
                        {
                            Debug.Log(Dump(state, $"ĐƠN {id} BỊ XOÁ/RESET khi đang Accepted/Assigned — seed {seed}"));
                            return false;
                        }
                    }
                }
                foreach (var s in state.Ships)
                {
                    if (string.IsNullOrEmpty(s.OrderId)) continue;
                    var o = state.Orders.Find(x => x.Id == s.OrderId);
                    if (o == null || o.Status != OrderStatus.Assigned)
                    {
                        Debug.Log(Dump(state, $"TÀU {s.Id} CHỞ ĐƠN {s.OrderId} KHÔNG TỒN TẠI/KHÔNG ASSIGNED — seed {seed}"));
                        return false;
                    }
                }
                if (!loggedDeliver3 && state.DeliveredCount >= 3)
                {
                    loggedDeliver3 = true;
                    Debug.Log(Dump(state, $"giao đơn thứ 3 — seed {seed}, chính sách {(cluePolicy ? "manh mối" : "ngẫu nhiên")}"));
                }
            }
            if (!state.IsFinished)
            {
                Debug.Log(Dump(state, $"ván CHƯA KẾT sau {maxSeconds:0}s — seed {seed}"));
                return false;
            }
            return true;
        }
        [Test]

        public void DongHoVanTien_Quet50Seed_HaiChinhSach()
        {
            var failed = new List<string>();
            foreach (bool cluePolicy in new[] { false, true })
                for (int seed = 1; seed <= 50; seed++)
                    if (!RunOne(seed, cluePolicy, 1800f))
                        failed.Add($"{(cluePolicy ? "manh-mối" : "ngẫu nhiên")}/seed-{seed}");
            Assert.IsEmpty(failed, "các ván bị 'đứng' hoặc dữ liệu hỏng: " + string.Join(", ", failed));
        }
        [Test]

        public void KhongDonNaoBiXoa_KhiDangDuocTauCho_1500s()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 77);
            OrderScheduler.SpawnInitial(_world.OrderPool, state);
            var activeSeen = new HashSet<string>();
            while (!state.IsFinished && state.ElapsedSeconds < 1500f)
            {
                BalanceProbe.Step(_sim, state, _world.Crews, cluePolicy: true);
                foreach (var o in state.Orders)
                    if (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned)
                        activeSeen.Add(o.Id);
                foreach (var id in activeSeen)
                {
                    var o = state.Orders.Find(x => x.Id == id);
                    Assert.IsNotNull(o, $"đơn {id} biến mất khỏi state.Orders lúc t={state.ElapsedSeconds:0.#}s");
                    Assert.That(o.Status, Is.Not.EqualTo(OrderStatus.InInbox).And.Not.EqualTo(OrderStatus.Declined),
                        $"đơn {id} bị reset trạng thái lúc t={state.ElapsedSeconds:0.#}s");
                }
            }
            Assert.Greater(activeSeen.Count, 0, "1500s phải có ít nhất một đơn đi qua trạng thái Accepted để test có ý nghĩa");
        }
    }
}

