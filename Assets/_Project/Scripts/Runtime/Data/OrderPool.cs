using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "OrderPool", menuName = "AyeAye/Order Pool")]

    public class OrderPool : ScriptableObject
    {
        [Tooltip("Fixed day-1 orders (tutorial, §5.4: O-01, O-02).")]

        public List<OrderDef> fixedFirstDay = new List<OrderDef>();
        [Tooltip("Pool drawn from day 2 (no repeats in a run; exhausted pools fall back to repeats).")]

        public List<OrderDef> pool = new List<OrderDef>();
    }
}

