using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AyeAye
{
    // Trang Status: tình trạng tàu/bánh, cảnh báo cờ, đèn cờ, biểu tượng bánh, thẻ thủy thủ.
    public class StatusScreenUI : MonoBehaviour
    {
        public TMP_Text statusText;
        public TMP_Text warningText;
        public TMP_Text flagsText;
        public TMP_Text[] crewCards = new TMP_Text[0];
        public TerminalTextEffect typewriter;
        public Button continueButton;
        public Button backButton;

        public event System.Action ContinueRequested;
        public event System.Action BackRequested;

        static readonly string CargoToken = "{CARGO}";

        void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            else
                Debug.LogError("[StatusScreenUI] continueButton chưa được gán trên " + name);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        public void SetTypewriterSpeed(float charsPerSecond)
        {
            if (typewriter != null) typewriter.SetSpeed(Mathf.RoundToInt(charsPerSecond));
        }

        public void Show(GameState state, DayDefinition day, CrewDefinition[] crews)
        {
            if (state == null || day == null) return;

            string text = day.statusText;
            if (!string.IsNullOrEmpty(text)) text = text.Replace(CargoToken, CakeStatusUI.CargoName(state.Cargo));

            string warning = (state.CargoShifted && !string.IsNullOrEmpty(day.cargoShiftedWarning))
                ? day.cargoShiftedWarning
                : "";

            if (warningText != null) warningText.text = warning;
            if (flagsText != null)
            {
                flagsText.text = "CỜ CHUYỂN LỆCH: " + (state.CargoShifted ? "[*]" : "[ ]")
                                 + "   SỬA TẠM: " + (state.TemporaryRepair ? "[*]" : "[ ]");
            }

            if (typewriter != null) typewriter.Play(text);

            if (crewCards != null && crews != null)
            {
                for (int i = 0; i < crewCards.Length; i++)
                {
                    if (crewCards[i] == null) continue;
                    if (i < crews.Length && crews[i] != null)
                    {
                        crewCards[i].text = crews[i].displayName + " · " + crews[i].personalityLabel
                                            + "\nMẠNH: " + crews[i].strengthText
                                            + "\nYẾU: " + crews[i].weaknessText;
                    }
                    else
                    {
                        crewCards[i].text = "";
                    }
                }
            }
        }

        void OnContinueClicked()
        {
            if (typewriter != null && typewriter.IsTyping)
            {
                typewriter.Complete();
                return;
            }

            if (ContinueRequested != null) ContinueRequested();
        }

        void OnBackClicked()
        {
            if (BackRequested != null) BackRequested();
        }
    }
}
