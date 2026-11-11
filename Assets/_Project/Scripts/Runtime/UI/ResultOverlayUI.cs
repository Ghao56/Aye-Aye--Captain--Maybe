using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AyeAye
{
    // Overlay kết quả End Day: nhãn kết quả, giải thích, thoại, quãng đường; không tiết lộ trước khi áp dụng.
    public class ResultOverlayUI : MonoBehaviour
    {
        public TMP_Text resultLabel;
        public TMP_Text explanationText;
        public TMP_Text quoteText;
        public TMP_Text distanceText;
        public Button nextButton;

        public event System.Action NextRequested;

        static readonly Color GreenColor = new Color(0.608f, 0.910f, 0.643f);
        static readonly Color AmberColor = new Color(1f, 0.722f, 0.302f);
        static readonly Color RedColor = new Color(1f, 0.353f, 0.302f);

        bool ready;

        void Awake()
        {
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);
            else
                Debug.LogError("[ResultOverlayUI] nextButton chưa được gán trên " + name);
        }

        public void Show(ResolvedDecision resolved, float distanceTravelled, bool isFinished)
        {
            ready = false;
            if (nextButton != null)
            {
                nextButton.interactable = false; // khóa tới khi thanh tiến trình chạy xong
                nextButton.GetComponentInChildren<TMP_Text>().text = isFinished ? "XEM KẾT QUẢ >" : "NGÀY TIẾP THEO >";
            }

            if (resolved == null) return;

            if (resultLabel != null)
            {
                resultLabel.text = OutcomeLabel(resolved.Outcome);
                resultLabel.color = OutcomeColor(resolved.Outcome);
            }
            if (explanationText != null) explanationText.text = resolved.Explanation ?? "";
            if (quoteText != null)
            {
                string quote = resolved.Quote ?? "";
                quoteText.text = resolved.CrewName + ": \"" + quote + "\"";
            }
            if (distanceText != null)
            {
                distanceText.text = "QUÃNG ĐƯỜNG VỪA ĐI: " + distanceTravelled.ToString("0.00") + " ly";
            }
        }

        // Gọi sau khi thanh tiến trình chạy xong: mở nút chuyển bước.
        public void SetNextReady()
        {
            ready = true;
            if (nextButton != null) nextButton.interactable = true;
        }

        void OnNextClicked()
        {
            if (!ready) return;
            if (NextRequested != null) NextRequested();
        }

        static string OutcomeLabel(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Smart: return "HỢP LÝ / SMART";
                case DecisionOutcome.SlipUp: return "LỎ TAY / SLIP-UP";
                default: return "BÌNH THƯỜNG / NORMAL";
            }
        }

        static Color OutcomeColor(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Smart: return GreenColor;
                case DecisionOutcome.SlipUp: return RedColor;
                default: return AmberColor;
            }
        }
    }
}
