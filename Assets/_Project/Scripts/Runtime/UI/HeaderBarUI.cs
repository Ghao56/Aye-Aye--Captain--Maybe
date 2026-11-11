using TMPro;
using UnityEngine;

namespace AyeAye
{
    // Header cố định: mã chuyến, tên tàu, ngày/deadline, trạng thái hệ thống, đèn báo.
    public class HeaderBarUI : MonoBehaviour
    {
        public TMP_Text tripText;
        public TMP_Text shipText;
        public TMP_Text dayText;
        public TMP_Text systemText;
        public TMP_Text lampText;

        [Header("Chuỗi kiểm tra glyph tiếng Việt (cần bật cờ debug)")]
        public bool showGlyphTest;
        public TMP_Text glyphTestText;

        static readonly Color GreenColor = new Color(0.608f, 0.910f, 0.643f);
        static readonly Color RedColor = new Color(1f, 0.353f, 0.302f);

        void Awake()
        {
            if (glyphTestText != null)
            {
                glyphTestText.text = "ăâêôơư đ àáảãạ ềếểễệ ồốổỗộ";
                glyphTestText.gameObject.SetActive(showGlyphTest);
            }
        }

        public void Refresh(GameState state, GameConfig config)
        {
            if (state == null || config == null) return;

            if (tripText != null) tripText.text = config.tripCode;
            if (shipText != null) shipText.text = config.shipName;
            if (dayText != null)
            {
                dayText.text = "NGÀY " + state.CurrentDay + "/" + config.deadlineDay;
                dayText.color = state.IsFinished ? RedColor : GreenColor;
            }
            if (systemText != null)
            {
                systemText.text = "HỆ THỐNG: ONLINE";
                systemText.color = GreenColor;
            }

            bool alarm = state.CargoShifted || state.TemporaryRepair;
            if (lampText != null)
            {
                // ASCII an toàn cho font tùy ý (thay cho ký hiệu hình học).
                lampText.text = alarm ? "ĐÈN: [!]" : "ĐÈN: [ok]";
                lampText.color = alarm ? RedColor : GreenColor;
            }
        }
    }
}
