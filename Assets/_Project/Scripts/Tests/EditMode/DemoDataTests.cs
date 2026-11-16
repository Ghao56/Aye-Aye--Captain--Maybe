using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

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
            Assert.AreEqual(10, config.winDeliveries);
            Assert.AreEqual(5, ports.Count);
            Assert.AreEqual(5, ports.Select(p => p.portId).Distinct().Count());
            var hq = ports.First(p => p.isHeadquarters);
            Assert.AreEqual(DemoData.Goc, hq.portId);
            Assert.AreEqual(6, edges.Count);
            Assert.AreEqual(1, edges.Count(r =>
                (r.fromPortId == DemoData.Goc && r.toPortId == DemoData.Camap) ||
                (r.fromPortId == DemoData.Camap && r.toPortId == DemoData.Goc)));
            Assert.IsTrue(edges.Any(r => r.hazard == HazardType.Storm), "I-02 cần ít nhất một cạnh Storm");
            Assert.AreEqual(4, ships.Count);
            var tiaChop = ships.First(s => s.captain == CrewId.Bob);
            Assert.AreEqual(SpeedClass.Fast, tiaChop.speedClass);
            Assert.AreEqual(DemoData.Goc, tiaChop.startPortId);
            var dex = ships.First(s => s.captain == CrewId.Dex);
            Assert.AreEqual(DemoData.Vang, dex.startPortId);
            Assert.IsTrue(ships.All(s => !string.IsNullOrEmpty(s.displayName)));
            Assert.AreEqual(4, ships.Select(s => s.displayName).Distinct().Count());
            var bob = ships.First(s => s.captain == CrewId.Bob);
            Assert.AreEqual("CARGO-9", bob.displayName);
            Assert.AreEqual(2, orders.fixedFirstDay.Count);
            Assert.AreEqual("O-01", orders.fixedFirstDay[0].orderId);
            Assert.AreEqual("O-02", orders.fixedFirstDay[1].orderId);
            Assert.AreEqual(6, orders.pool.Count);
            Assert.IsTrue(orders.pool.All(o => o.orderId.StartsWith("O-")));
            Assert.AreEqual("I-01", incidents.fixedFirstDay.incidentId);
            Assert.AreEqual("I-F", incidents.fixedFinale.incidentId);
            Assert.AreEqual(6, incidents.incidents.Count);
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
            var direct = options.First(o => o.hazards.Contains(HazardType.Ice));
            Assert.AreEqual(2f, direct.length, 0.001f);
            var viaCamap = options.First(o => o.hazards.Contains(HazardType.Storm));
            Assert.AreEqual(3f, viaCamap.length, 0.001f);
            Assert.IsTrue(viaCamap.hazards.Contains(HazardType.Pirates));
        }
        [Test]

        public void RouteTable_AllPairsCovered_MaxTwoOptions()
        {
            var routes = Track(DemoData.BuildRouteTable());
            var ids = new[] { DemoData.Goc, DemoData.Vang, DemoData.Suong, DemoData.Camap, DemoData.Tram };
            foreach (var a in ids)
                foreach (var b in ids)
                {
                    if (a == b) continue;
                    var opts = routes.GetOptions(a, b);
                    Assert.IsNotNull(opts, $"thiếu tuyến {a}→{b}");
                    Assert.LessOrEqual(opts.Count, 2, $"tối đa 2 lựa chọn cho {a}→{b}");
                }
        }
        [Test]

        public void Incident_I05_MauKheChenRaNormal_NguoiKhacRaSlipUp()
        {
            var incidents = Track(DemoData.BuildIncidentPool());
            var i05 = incidents.incidents.First(d => d.incidentId == "I-05");
            var macKe = i05.options.First(o => o.label.Contains("Ignore"));
            Assert.IsNotNull(macKe.entries.Find(e => e.crew == CrewId.Chen && e.outcome == DecisionOutcome.Normal));
            Assert.IsNotNull(macKe.entries.Find(e => e.crew == CrewId.Any && e.outcome == DecisionOutcome.SlipUp));
        }
    }
}

