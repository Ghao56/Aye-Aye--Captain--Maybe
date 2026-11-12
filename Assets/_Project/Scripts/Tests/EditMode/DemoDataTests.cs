using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// DemoData là nguồn duy nhất của dữ liệu mẫu SRS §5: TestWorld (bộ nhớ) và DemoBuilder (assets)
    /// cùng gọi Populate. Test đây chốt bộ dữ liệu đầy đủ theo mục 5.
    /// </summary>
    public class DemoDataTests
    {
        private List<ScriptableObject> _sos;

        [SetUp]
        public void SetUp() { _sos = new List<ScriptableObject>(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _sos) Object.DestroyImmediate(so);
        }

        private T Track<T>(T so) where T : ScriptableObject
        {
            _sos.Add(so);
            return so;
        }

        [Test]
        public void FullSection5_DataSet()
        {
            var config = Track(ScriptableObject.CreateInstance<GameConfig>());
            var ports = DemoData.BuildPorts().Select(p => Track(p)).ToList();
            var edges = DemoData.BuildRouteDefs().Select(r => Track(r)).ToList();
            var routes = Track(DemoData.BuildRouteTable());
            var ships = DemoData.BuildShips().Select(s => Track(s)).ToList();
            var crews = DemoData.BuildCrews().Select(c => Track(c)).ToList();
            var orders = Track(DemoData.BuildOrderPool());
            var incidents = Track(DemoData.BuildIncidentPool());
            var endings = Track(DemoData.BuildEndings());

            // §2.5 / §5
            Assert.AreEqual(8, config.gameLengthDays);
            Assert.AreEqual(50, config.startReputation);

            // §5.1: 6 cảng, id duy nhất, HQ = goc
            Assert.AreEqual(6, ports.Count);
            Assert.AreEqual(6, ports.Select(p => p.portId).Distinct().Count());
            var hq = ports.First(p => p.isHeadquarters);
            Assert.AreEqual(DemoData.Goc, hq.portId);

            // §5.2: 7 cạnh
            Assert.AreEqual(7, edges.Count);

            // §5.3: 4 tàu, thuyền trưởng + speed đúng bảng
            Assert.AreEqual(4, ships.Count);
            var tiaChop = ships.First(s => s.captain == CrewId.Bob);
            Assert.AreEqual(SpeedClass.Fast, tiaChop.speedClass);
            Assert.AreEqual(DemoData.Goc, tiaChop.startPortId);
            var dex = ships.First(s => s.captain == CrewId.Dex);
            Assert.AreEqual(DemoData.Vang, dex.startPortId);

            // §5.4: ngày 1 cứng O-01/O-02, pool O-03..O-08
            Assert.AreEqual(2, orders.fixedFirstDay.Count);
            Assert.AreEqual("O-01", orders.fixedFirstDay[0].orderId);
            Assert.AreEqual("O-02", orders.fixedFirstDay[1].orderId);
            Assert.AreEqual(6, orders.pool.Count);
            Assert.IsTrue(orders.pool.All(o => o.orderId.StartsWith("O-")));

            // §5.5: I-01 ngày 1, I-F finale, pool I-02..I-07
            Assert.AreEqual("I-01", incidents.fixedFirstDay.incidentId);
            Assert.AreEqual("I-F", incidents.fixedFinale.incidentId);
            Assert.AreEqual(6, incidents.incidents.Count);

            // FR-0701: đủ 5 đoạn kết
            Assert.AreEqual(5, endings.endings.Count);
            for (int i = 1; i <= 5; i++)
                Assert.IsNotNull(endings.Get((EndingId)i), "thiếu ending " + i);
        }

        [Test]
        public void RouteTable_CoversGocToTram_WithTwoOptions()
        {
            var routes = Track(DemoData.BuildRouteTable());
            var options = routes.GetOptions(DemoData.Goc, DemoData.Tram);
            Assert.IsNotNull(options);
            Assert.AreEqual(2, options.Count);
            // §5.2: ven Sương Mù = 3 route-units (Sương mù + Băng); qua Mỏm Gió = 4 (Bão + Thông thoáng)
            var ven = options.First(o => o.hazards.Contains(HazardType.Fog));
            Assert.AreEqual(3f, ven.length, 0.001f);
            Assert.IsTrue(ven.hazards.Contains(HazardType.Ice));
        }

        [Test]
        public void Incident_I05_MauKheChenRaNormal_NguoiKhacRaSlipUp()
        {
            // Bảng 5.5 qua boss duyệt: Chen chọn "Mặc kệ" → Normal; thuyền trưởng khác → Slip-up
            var incidents = Track(DemoData.BuildIncidentPool());
            var i05 = incidents.incidents.First(d => d.incidentId == "I-05");
            var macKe = i05.options.First(o => o.label.Contains("c k"));
            Assert.IsNotNull(macKe.entries.Find(e => e.crew == CrewId.Chen && e.outcome == DecisionOutcome.Normal));
            Assert.IsNotNull(macKe.entries.Find(e => e.crew == CrewId.Any && e.outcome == DecisionOutcome.SlipUp));
        }
    }
}
