using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AyeAye
{
    // Trang Briefing: tiêu đề + nội dung gõ chữ + nút CONTINUE (hoàn tất chữ trước, sang trang sau).
    public class BriefingScreenUI : MonoBehaviour
    {
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TerminalTextEffect typewriter;
        public Button continueButton;

        public event System.Action ContinueRequested;

        DayDefinition currentDay;
        bool awaitingSecondClick;

        void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            else
                Debug.LogError("[BriefingScreenUI] continueButton chưa được gán trên " + name);
        }

        public void SetTypewriterSpeed(float charsPerSecond)
        {
            if (typewriter != null) typewriter.SetSpeed(Mathf.RoundToInt(charsPerSecond));
        }

        public void Show(DayDefinition day)
        {
            currentDay = day;
            awaitingSecondClick = false;

            if (titleText != null) titleText.text = day != null ? day.title : "";
            if (bodyText != null && typewriter != null)
            {
                typewriter.Play(day != null ? day.briefingText : "");
            }
        }

        void OnContinueClicked()
        {
            if (typewriter != null && typewriter.IsTyping)
            {
                // Lần bấm đầu khi đang gõ: hiện đủ chữ; lần sau mới sang trang.
                typewriter.Complete();
                awaitingSecondClick = true;
                return;
            }

            if (currentDay == null && !awaitingSecondClick) return;
            awaitingSecondClick = false;
            if (ContinueRequested != null) ContinueRequested();
        }
    }
}
