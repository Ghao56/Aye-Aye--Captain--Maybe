using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "Order_New", menuName = "AyeAye/Order Definition")]

    public class OrderDef : ScriptableObject
    {

        public string orderId;
        [Tooltip("Customer name (comedic).")]

        public string customerName;

        public OrderTag tags = OrderTag.None;

        public string fromPortId;

        public string toPortId;
        [Tooltip("Deadline counted from WHEN THE ORDER APPEARS (seconds on the run clock).")]

        public float deadlineSeconds = 60f;
        [Tooltip("Revenue if delivered on time with cargo intact.")]

        public int revenue = 100;
        [Tooltip("One clue line for the player (FR-0307: never states the outcome directly).")]

        public string clue;

        public int weight = 1;
        [Tooltip("Condition to appear in the pool (run-level).")]

        public FlagCondition spawnCondition = FlagCondition.Always;
    }
}

