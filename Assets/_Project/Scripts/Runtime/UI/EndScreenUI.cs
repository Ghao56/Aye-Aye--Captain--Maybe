using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AyeAye
{
    // Màn hình kết thúc: tiêu đề + nội dung đoạn kết, thống kê, nút Restart.
    public class EndScreenUI : MonoBehaviour
    {
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text statsText;
        public Button restartButton;

        public event System.Action RestartRequested;

        static readonly Color GreenColor = new Color(0.608f, 0.910f, 0.643f);
        static readonly Color AmberColor = new Color(1f, 0.722f, 0.302f);
        static readonly Color RedColor = new Color(1f, 0.353f, 0.302f);

        void Awake()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
            else
                Debug.LogError("[EndScreenUI] restartButton chưa được gán trên " + name);
        }

        public void Show(GameState state, GameConfig config, EndingDefinitions endings)
        {
            if (state == null || config == null || endings == null || state.FinalEnding == null)
            {
                Debug.LogError("[EndScreenUI] Thiếu dữ liệu để hiển thị màn hình kết thúc.");
                return;
            }

            EndingDefinitions.EndingEntry entry = endings.Get(state.FinalEnding.Value);
            if (entry == null)
            {
                Debug.LogError("[EndScreenUI] Không tìm thấy nội dung đoạn kết: " + state.FinalEnding.Value);
                return;
            }

            if (titleText != null)
            {
                titleText.text = entry.title ?? "";
                titleText.color = EndingColor(state.FinalEnding.Value);
            }
            if (bodyText != null)
            {
                bodyText.text = entry.body ?? "";
            }

            if (statsText != null)
            {
                statsText.text = "THỐNG KÊ\n"
                    + "Số ngày dùng: " + state.CurrentDay + "/" + config.deadlineDay + "\n"
                    + "Tiến độ: " + Mathf.RoundToInt(state.ProgressPercent * 100f) + "%\n"
                    + "Tình trạng bánh: " + CakeStatusUI.CargoName(state.Cargo)
                    + (state.ReachedEarly ? "\nĐẾN SỚM ĐỂ CHUẨN BỊ TIỆC." : "");
            }
        }

        void OnRestartClicked()
        {
            if (RestartRequested != null) RestartRequested();
        }

        static Color EndingColor(EndingId id)
        {
            switch (id)
            {
                case EndingId.PerfectDelivery: return GreenColor;
                case EndingId.MinorDamage: return AmberColor;
                case EndingId.DisasterDelivery: return RedColor;
                case EndingId.LateButIntact: return AmberColor;
                default: return RedColor;
            }
        }
    }
}
