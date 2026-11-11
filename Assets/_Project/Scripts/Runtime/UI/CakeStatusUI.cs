using TMPro;
using UnityEngine;

namespace AyeAye
{
    // Biểu tượng bánh ASCII + tên mức Hàng.
    public class CakeStatusUI : MonoBehaviour
    {
        public TMP_Text cakeText;

        static readonly Color GreenColor = new Color(0.608f, 0.910f, 0.643f);
        static readonly Color AmberColor = new Color(1f, 0.722f, 0.302f);
        static readonly Color RedColor = new Color(1f, 0.353f, 0.302f);

        public void Show(CargoCondition cargo)
        {
            if (cakeText == null) return;

            cakeText.text = Icon(cargo) + "\nBÁNH: " + CargoName(cargo) + "\nKHÔNG ĐƯỢC LẮC · GIỮ THẲNG ĐỨNG";
            switch (cargo)
            {
                case CargoCondition.Scratched: cakeText.color = AmberColor; break;
                case CargoCondition.Ruined: cakeText.color = RedColor; break;
                default: cakeText.color = GreenColor; break;
            }
        }

        static string Icon(CargoCondition cargo)
        {
            switch (cargo)
            {
                case CargoCondition.Scratched:
                    return "    ii\n  /--|\\\n | oooo |\n  \\____/";
                case CargoCondition.Ruined:
                    return "    ..\n  .____.\n | x  x |\n  \\____/";
                default:
                    return "    ii\n  /----\\\n | oooo |\n  \\____/";
            }
        }

        // Tên mức Hàng dùng chung cho Status, Cake và End Screen.
        public static string CargoName(CargoCondition cargo)
        {
            switch (cargo)
            {
                case CargoCondition.Scratched: return "TRẦY";
                case CargoCondition.Ruined: return "HƯ";
                default: return "NGUYÊN VẸN";
            }
        }
    }
}
