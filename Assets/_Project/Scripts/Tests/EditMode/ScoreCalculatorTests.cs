using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    // FR-0602: điểm giao hàng theo đúng hạn/trễ, theo tình trạng hàng.
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
                DeadlineDay = 4,
                Cargo = CargoCondition.Intact,
            };
        }

        [Test]
        public void OnTime_Intact_GainsPlus8AndFullRevenue()
        {
            _order.DeliveredDay = 4;
            var (rep, rev) = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(8, rep);
            Assert.AreEqual(100, rev);
        }

        [Test]
        public void OnTime_Scratched_GainsPlus6()
        {
            _order.DeliveredDay = 3;
            _order.Cargo = CargoCondition.Scratched;
            var (rep, rev) = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(6, rep);
            Assert.AreEqual(100, rev);
        }

        [Test]
        public void OnTime_Ruined_GainsMinus2AndZeroRevenue()
        {
            _order.DeliveredDay = 2;
            _order.Cargo = CargoCondition.Ruined;
            var (rep, rev) = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(-2, rep);
            Assert.AreEqual(0, rev);
        }

        [Test]
        public void Late_Intact_FlatMinus5AndHalfRevenue()
        {
            _order.DeliveredDay = 5;
            var (rep, rev) = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(-5, rep);
            Assert.AreEqual(50, rev);
        }

        [Test]
        public void Late_Ruined_FlatMinus5AndZeroRevenue()
        {
            _order.DeliveredDay = 7;
            _order.Cargo = CargoCondition.Ruined;
            var (rep, rev) = ScoreCalculator.ScoreDelivery(_order, _config);
            Assert.AreEqual(-5, rep);
            Assert.AreEqual(0, rev);
        }

        [Test]
        public void UndeliveredCount_AcceptsAcceptedAndAssignedOnly()
        {
            var state = new GameState(_config);
            state.Orders.Add(new OrderState { Id = "A", Status = OrderStatus.Accepted });
            state.Orders.Add(new OrderState { Id = "B", Status = OrderStatus.Assigned });
            state.Orders.Add(new OrderState { Id = "C", Status = OrderStatus.Delivered });
            state.Orders.Add(new OrderState { Id = "D", Status = OrderStatus.Declined });
            state.Orders.Add(new OrderState { Id = "E", Status = OrderStatus.InInbox });
            Assert.AreEqual(2, ScoreCalculator.UndeliveredCount(state));
        }
    }
}
