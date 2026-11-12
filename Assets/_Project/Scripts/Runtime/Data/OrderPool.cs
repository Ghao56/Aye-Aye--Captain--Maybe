using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    [CreateAssetMenu(fileName = "OrderPool", menuName = "AyeAye/Order Pool")]
    public class OrderPool : ScriptableObject
    {
        [Tooltip("Đơn cố định của Ngày 1 (tutorial, mục 5.4: O-01, O-02).")]
        public List<OrderDef> fixedFirstDay = new List<OrderDef>();

        [Tooltip("Pool bốc từ Ngày 2 (không lặp trong ván; hết pool thì fallback cho phép lặp).")]
        public List<OrderDef> pool = new List<OrderDef>();
    }
}
