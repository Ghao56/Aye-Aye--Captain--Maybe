using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// DemoValidator (FR-0905): tham chiếu đủ, bảng outcome đủ (FR-0505), route nối được,
    /// không có đơn không thể giao. Menu Tools/AyeAye/Validate chỉ gọi lớp thuần này.
    /// </summary>
    public class DemoValidatorTests
    {
        private TestWorld _world;
        private List<ScriptableObject> _sos;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _sos = new List<ScriptableObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
            foreach (var so in _sos) Object.DestroyImmediate(so);
        }

        [Test]
        public void TestWorldData_NoErrors()
        {
            var errors = DemoValidator.Validate(_world.Config, _world.Routes,
                _world.OrderPool, _world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _world.ShipDefs, _world.Crews);
            Assert.AreEqual(0, errors.Count, "dữ liệu mẫu phải sạch: " + string.Join("; ", errors));
        }

        [Test]
        public void IncidentMissingOptionEntries_Reported()
        {
            var bad = ScriptableObject.CreateInstance<IncidentDef>();
            _sos.Add(bad);
            bad.incidentId = "I-BAD";
            bad.title = "bad";
            bad.options = new List<IncidentOption>
            {
                new IncidentOption { label = "trống", entries = new List<IncidentOutcomeEntry>() },
            };
            var errors = DemoValidator.Validate(_world.Config, _world.Routes,
                _world.OrderPool, _world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _world.ShipDefs, _world.Crews, extraIncidents: new[] { bad });
            Assert.IsTrue(errors.Any(e => e.Contains("I-BAD")), "phải báo incident thiếu bảng");
        }

        [Test]
        public void OrderWithoutRouteOption_Reported()
        {
            var bad = ScriptableObject.CreateInstance<OrderDef>();
            _sos.Add(bad);
            bad.orderId = "O-BAD";
            bad.fromPortId = TestWorld.Goc;
            bad.toPortId = "no-port";
            bad.deadlineSeconds = 60f;
            var errors = DemoValidator.Validate(_world.Config, _world.Routes,
                _world.OrderPool, _world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _world.ShipDefs, _world.Crews, extraOrders: new[] { bad });
            Assert.IsTrue(errors.Any(e => e.Contains("O-BAD")), "phải báo đơn không thể giao");
        }

        [Test]
        public void DeadlineBelowReactionMargin_Reported()
        {
            var bad = ScriptableObject.CreateInstance<OrderDef>();
            _sos.Add(bad);
            bad.orderId = "O-MARGIN";
            bad.fromPortId = TestWorld.Goc;
            bad.toPortId = TestWorld.Vang; // cạnh 1 đơn vị → ETA nhanh nhất ≈ 13.3s
            bad.deadlineSeconds = 30f;     // < 13.3 + 25 (biên phản ứng)
            var errors = DemoValidator.Validate(_world.Config, _world.Routes,
                _world.OrderPool, _world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _world.ShipDefs, _world.Crews, extraOrders: new[] { bad });
            Assert.IsTrue(errors.Any(e => e.Contains("O-MARGIN") && e.Contains("biên")),
                "phải báo hạn dưới ETA + biên phản ứng: " + string.Join("; ", errors));
        }

        [Test]
        public void TutorialOrder_UsesDeadlineBonus()
        {
            var bad = ScriptableObject.CreateInstance<OrderDef>();
            _sos.Add(bad);
            bad.orderId = "O-TUT";
            bad.fromPortId = TestWorld.Goc;
            bad.toPortId = TestWorld.Vang; // 13.3 + 25 = 38.3 ≤ 60 → chỉ vi phạm khi cộng bonus hướng dẫn (+30)
            bad.deadlineSeconds = 60f;
            _world.OrderPool.fixedFirstDay.Add(bad);
            var errors = DemoValidator.Validate(_world.Config, _world.Routes,
                _world.OrderPool, _world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _world.ShipDefs, _world.Crews);
            Assert.IsTrue(errors.Any(e => e.Contains("O-TUT") && e.Contains("biên")),
                "đơn ngày 1 phải áp biên phản ứng + bonus hướng dẫn: " + string.Join("; ", errors));
        }
    }
}
