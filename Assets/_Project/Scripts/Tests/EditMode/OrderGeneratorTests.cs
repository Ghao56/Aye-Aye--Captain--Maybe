using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

    public class OrderGeneratorTests
    {
        TestWorld _world;
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
        static List<OrderState> GenerateMany(GameState state, RouteTable routes, int count)
        {
            var list = new List<OrderState>();
            for (int i = 0; i < count; i++)
            {
                var o = OrderGenerator.Generate(state, routes, 100f);
                if (o == null) continue;
                state.OrderSpawnCount++; 
                list.Add(o);
            }
            return list;
        }
        float ShortestEta(string from, string to)
        {
            var opts = _world.Routes.GetOptions(from, to);
            float min = float.MaxValue;
            foreach (var o in opts) if (o.length < min) min = o.length;
            return min / Mathf.Max(0.0001f, _world.Config.SpeedUnitsPerSecond(SpeedClass.Fast));
        }
        [Test]

        public void SameSeed_CungChuoiDon()
        {
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 42);
            var b = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 42);
            var oa = GenerateMany(a, _world.Routes, 50);
            var ob = GenerateMany(b, _world.Routes, 50);
            Assert.AreEqual(oa.Count, ob.Count);
            for (int i = 0; i < oa.Count; i++)
            {
                Assert.AreEqual(oa[i].Id, ob[i].Id, "đơn {0}", i);
                Assert.AreEqual(oa[i].FromPortId, ob[i].FromPortId);
                Assert.AreEqual(oa[i].ToPortId, ob[i].ToPortId);
                Assert.AreEqual(oa[i].Tags, ob[i].Tags);
                Assert.AreEqual(oa[i].DeadlineAtSeconds, ob[i].DeadlineAtSeconds, 0.0001f);
                Assert.AreEqual(oa[i].Revenue, ob[i].Revenue);
                Assert.AreEqual(oa[i].CustomerName, ob[i].CustomerName);
            }
        }
        [Test]

        public void MoiDon_CoTuyen_HanDuBien_PhanUng()
        {
            var config = _world.Config;
            var seen = new List<OrderState>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var state = GameStateBuilder.CreateRun(config, _world.ShipDefs, seed);
                seen.AddRange(GenerateMany(state, _world.Routes, 15));
            }
            Assert.Greater(seen.Count, 100, "phải sinh được đủ nhiều đơn để test có ý nghĩa");
            foreach (var o in seen)
            {
                Assert.IsNotNull(_world.Routes.GetOptions(o.FromPortId, o.ToPortId),
                    $"{o.Id}: phải có tuyến {o.FromPortId}→{o.ToPortId}");
                float deadline = o.DeadlineAtSeconds - 100f; 
                float eta = ShortestEta(o.FromPortId, o.ToPortId);
                Assert.GreaterOrEqual(deadline, eta + config.deadlineReactionMarginSeconds - 0.001f,
                    $"{o.Id}: hạn {deadline:0.#}s phải ≥ ETA {eta:0.#}s + biên phản ứng {config.deadlineReactionMarginSeconds}s");
            }
        }
        [Test]

        public void IdKhongTrung_Qua200LanSinh()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 9);
            var seen = GenerateMany(state, _world.Routes, 200);
            Assert.AreEqual(200, seen.Count);
            Assert.AreEqual(200, seen.Select(o => o.Id).Distinct().Count(), "Id phải duy nhất theo thứ tự sinh");
            Assert.IsTrue(seen.All(o => o.Id.StartsWith("O-G")));
        }
        [Test]

        public void DonDau_DungLuatDe()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            var o = OrderGenerator.Generate(state, _world.Routes, 0f);
            Assert.Greater(state.Ships.Count(s => s.Status == ShipStatus.Idle && s.PortId == o.FromPortId), 0,
                "cảng đi của đơn đầu phải có tàu Idle");
            var opts = _world.Routes.GetOptions(o.FromPortId, o.ToPortId);
            Assert.IsNotNull(opts);
            float minLen = opts.Min(x => x.length);
            Assert.LessOrEqual(minLen, 1f, "tuyến ngắn nhất của đơn đầu ≤ 1 route-unit");
            Assert.AreEqual(OrderTag.None, o.Tags, "đơn đầu không có tag");
            float deadline = o.DeadlineAtSeconds - 0f;
            float eta = ShortestEta(o.FromPortId, o.ToPortId);
            float expected = eta + _world.Config.deadlineReactionMarginSeconds
                + _world.Config.tutorialDeadlineBonusSeconds;
            Assert.AreEqual(expected, deadline, 0.001f, "hạn đơn đầu = ETA + biên phản ứng + biên hướng dẫn");
        }
        [Test]

        public void DonThuHai_TroDi_HoanToanNgauNhien()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            var orders = GenerateMany(state, _world.Routes, 30);
            Assert.IsTrue(orders.Skip(1).Any(o => o.Tags != OrderTag.None),
                "từ đơn thứ 2 tag được bật theo orderTagChance");
        }
        [Test]

        public void Tag_DoanhThu_Han_NamTrongKhoangConfig()
        {
            var config = _world.Config;
            var seen = new List<OrderState>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var state = GameStateBuilder.CreateRun(config, _world.ShipDefs, seed);
                seen.AddRange(GenerateMany(state, _world.Routes, 15));
            }
            foreach (var o in seen)
            {
                Assert.That((int)o.Tags, Is.InRange(0, 7), "tag chỉ là tổ hợp Fragile/Cold/Urgent");
                var opts = _world.Routes.GetOptions(o.FromPortId, o.ToPortId);
                float minLen = opts.Min(x => x.length);
                int tagCount = 0;
                foreach (var t in new[] { OrderTag.Fragile, OrderTag.Cold, OrderTag.Urgent })
                    if ((o.Tags & t) != 0) tagCount++;
                float baseRevenue = config.orderRevenueBase + config.orderRevenuePerRouteUnit * minLen
                    + config.orderRevenueTagBonus * tagCount;
                float low = baseRevenue * (1f - config.orderRevenueJitter) - 1f; 
                float high = baseRevenue * (1f + config.orderRevenueJitter) + 1f;
                Assert.That(o.Revenue, Is.InRange(low, high), $"{o.Id}: doanh thu phải trong ±jitter");
                float deadline = o.DeadlineAtSeconds - 100f;
                float eta = ShortestEta(o.FromPortId, o.ToPortId);
                float extra = deadline - eta - config.deadlineReactionMarginSeconds;
                Assert.LessOrEqual(extra, Mathf.Max(config.orderDeadlineExtraMaxSeconds,
                        config.orderDeadlineExtraMinSeconds) + 0.001f, $"{o.Id}: phần hạn cộng thêm không vượt max");
            }
        }
    }
}

