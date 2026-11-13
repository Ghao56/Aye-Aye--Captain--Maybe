using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    // FR-0602: điểm giao hàng — doanh thu theo hạn (IsLate) và tình trạng hàng.
    // Reputation đã bỏ (chỉnh 4) — chấm điểm chỉ còn doanh thu.
    public class ScoreCalculatorTests
    {
        private GameConfig _config;
        private OrderState _order;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _order = new OrderState
            {
                Id = "O-01",
                Revenue = 100,
                DeadlineAtSeconds = 400f,
                Cargo = CargoCondition.Intact,
            };
        }

        [Test]
        public void OnTime_Intact_FullRevenue()
        {
            int rev = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(100, rev);
        }

        [Test]
        public void OnTime_Scratched_FullRevenue()
        {
            _order.Cargo = CargoCondition.Scratched;
            int rev = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(100, rev);
        }

        [Test]
        public void OnTime_Ruined_ZeroRevenue()
        {
            _order.Cargo = CargoCondition.Ruined;
            int rev = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(0, rev);
        }

        [Test]
        public void Late_Intact_HalfRevenue()
        {
            _order.IsLate = true; // giao muộn: chạm hạn khi chưa giao — chốt một lần trong MarkLate
            int rev = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(50, rev);
        }

        [Test]
        public void Late_Ruined_ZeroRevenue()
        {
            _order.IsLate = true;
            _order.Cargo = CargoCondition.Ruined;
            int rev = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(0, rev);
        }
    }
}
