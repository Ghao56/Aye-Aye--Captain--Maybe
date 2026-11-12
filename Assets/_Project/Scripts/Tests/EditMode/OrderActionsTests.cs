using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // OrderActions: Accept/Decline đơn trong Inbox (FR-0302) + giới hạn đơn đang hoạt động (FR-0303).
    public class OrderActionsTests
    {
        static GameState State(int maxActive)
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            config.maxActiveOrders = maxActive;
            return GameStateBuilder.CreateRun(config, new System.Collections.Generic.List<ShipDef>(), 5);
        }

        static OrderState Order(string id)
        {
            return new OrderState { Id = id, Status = OrderStatus.InInbox };
        }

        [Test]
        public void Accept_MovesInboxOrderToAccepted()
        {
            GameState state = State(4);
            state.Orders.Add(Order("O-01"));

            Assert.IsNull(OrderActions.Accept(state, "O-01"));
            Assert.AreEqual(OrderStatus.Accepted, state.Orders[0].Status);
        }

        [Test]
        public void Accept_RejectsWhenActiveOrderLimitReached()
        {
            GameState state = State(1);
            state.Orders.Add(Order("O-01"));
            state.Orders.Add(Order("O-02"));
            Assert.IsNull(OrderActions.Accept(state, "O-01"));

            string reason = OrderActions.Accept(state, "O-02");
            Assert.IsNotNull(reason);
            Assert.AreEqual(OrderStatus.InInbox, state.Orders[1].Status);
        }

        [Test]
        public void Accept_RejectsOrderNotInInbox()
        {
            GameState state = State(4);
            state.Orders.Add(Order("O-01"));
            state.Orders.Add(Order("O-02"));
            OrderActions.Accept(state, "O-01");

            string reason = OrderActions.Accept(state, "O-01");
            Assert.IsNotNull(reason);
            Assert.AreEqual(OrderStatus.Accepted, state.Orders[0].Status);
        }

        [Test]
        public void Decline_MarksInboxOrderDeclined()
        {
            GameState state = State(4);
            state.Orders.Add(Order("O-01"));

            Assert.IsNull(OrderActions.Decline(state, "O-01"));
            Assert.AreEqual(OrderStatus.Declined, state.Orders[0].Status);
        }

        [Test]
        public void Decline_RejectsOrderNotInInbox()
        {
            GameState state = State(4);
            state.Orders.Add(Order("O-01"));
            OrderActions.Accept(state, "O-01");

            string reason = OrderActions.Decline(state, "O-01");
            Assert.IsNotNull(reason);
            Assert.AreEqual(OrderStatus.Accepted, state.Orders[0].Status);
        }
    }
}
