using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AyeAye
{
    // Trang Assignment: chọn 1 phương án + 1 thủy thủ → xem trước → CONFIRM (khóa) → END DAY.
    public class AssignmentScreenUI : MonoBehaviour
    {
        public TMP_Text promptText;
        public TMP_Text previewText;
        public Button[] optionButtons = new Button[0];
        public TMP_Text[] optionLabels = new TMP_Text[0];
        public Button[] crewButtons = new Button[0];
        public TMP_Text[] crewLabels = new TMP_Text[0];
        public Button confirmButton;
        public Button endDayButton;
        public Button backButton;
        public Button editButton;

        public event System.Action<int, CrewId> Confirmed;
        public event System.Action EndDayRequested;
        public event System.Action BackRequested;
        public event System.Action EditRequested;

        // Thứ tự cố định của thẻ thủy thủ, khớp mảng crews do builder gán.
        static readonly CrewId[] CrewOrder = { CrewId.Bob, CrewId.Mira, CrewId.Chen, CrewId.Dex };

        static readonly Color NormalBgColor = new Color(0.059f, 0.125f, 0.094f);
        static readonly Color SelectedBgColor = new Color(1f, 0.722f, 0.302f);
        static readonly Color LabelColor = new Color(0.608f, 0.910f, 0.643f);
        static readonly Color SelectedLabelColor = new Color(0.043f, 0.059f, 0.055f);

        DecisionDefinition currentDecision;
        int selectedOption = -1;
        CrewId selectedCrew = CrewId.Any;
        bool confirmed;
        bool locked;

        void Awake()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i; // đóng bắt giá trị cục bộ
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => OnOptionClicked(index));
            }
            for (int i = 0; i < crewButtons.Length; i++)
            {
                int index = i;
                if (crewButtons[i] != null)
                    crewButtons[i].onClick.AddListener(() => OnCrewClicked(index));
            }
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            else
                Debug.LogError("[AssignmentScreenUI] confirmButton chưa được gán trên " + name);
            if (endDayButton != null)
                endDayButton.onClick.AddListener(OnEndDayClicked);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (editButton != null)
                editButton.onClick.AddListener(OnEditClicked);
        }

        public void Show(DecisionDefinition decision, CrewDefinition[] crews)
        {
            currentDecision = decision;
            selectedOption = -1;
            selectedCrew = CrewId.Any;
            confirmed = false;
            locked = false;

            if (promptText != null) promptText.text = decision != null ? decision.prompt : "";

            for (int i = 0; i < optionButtons.Length; i++)
            {
                SetActiveButton(optionButtons[i], true);
                if (optionLabels != null && i < optionLabels.Length && optionLabels[i] != null)
                {
                    optionLabels[i].text = decision != null && decision.options != null && i < decision.options.Count
                        ? decision.options[i].label
                        : "";
                }
                SetSelected(optionButtons[i], i < optionLabels.Length ? optionLabels[i] : null, false);
            }

            for (int i = 0; i < crewButtons.Length; i++)
            {
                SetActiveButton(crewButtons[i], true);
                if (crewLabels != null && i < crewLabels.Length && crewLabels[i] != null)
                {
                    crewLabels[i].text = crews != null && i < crews.Length && crews[i] != null
                        ? crews[i].displayName
                        : CrewOrder[i].ToString();
                }
                SetSelected(crewButtons[i], i < crewLabels.Length ? crewLabels[i] : null, false);
            }

            if (previewText != null) previewText.text = "";
            if (confirmButton != null) confirmButton.gameObject.SetActive(true);
            if (confirmButton != null) confirmButton.interactable = false;
            if (endDayButton != null)
            {
                endDayButton.gameObject.SetActive(false);
                endDayButton.interactable = true; // ngày trước đã bấm END DAY → phải mở lại, nút chỉ bị ẩn chứ không chết.
            }
            if (editButton != null) editButton.gameObject.SetActive(false);
        }

        void OnOptionClicked(int index)
        {
            if (locked || confirmed || currentDecision == null) return;
            selectedOption = index;
            for (int i = 0; i < optionButtons.Length; i++)
                SetSelected(optionButtons[i], i < optionLabels.Length ? optionLabels[i] : null, i == index);
            RefreshPreview();
        }

        void OnCrewClicked(int index)
        {
            if (locked || confirmed) return;
            if (index < 0 || index >= CrewOrder.Length) return;
            selectedCrew = CrewOrder[index];
            for (int i = 0; i < crewButtons.Length; i++)
                SetSelected(crewButtons[i], i < crewLabels.Length ? crewLabels[i] : null, i == index);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            if (previewText == null) return;
            bool ready = selectedOption >= 0 && selectedCrew != CrewId.Any;
            previewText.text = ready
                ? "PHÂN CÔNG: " + selectedCrew.ToString().ToUpperInvariant() + " → " + OptionLabel(selectedOption)
                : "";
            if (confirmButton != null && !confirmed) confirmButton.interactable = ready;
        }

        string OptionLabel(int index)
        {
            if (currentDecision == null || currentDecision.options == null
                || index < 0 || index >= currentDecision.options.Count) return "";
            string label = currentDecision.options[index].label;
            return label ?? "";
        }

        void OnConfirmClicked()
        {
            if (locked || confirmed || selectedOption < 0 || selectedCrew == CrewId.Any) return;

            // Khóa ngay để chống bấm đúp; không hiển thị kết quả ở bước này.
            // Chỉ dùng cờ `confirmed` — nếu đặt cả `locked` thì OnEndDayClicked bị chặn vĩnh viễn.
            confirmed = true;
            if (confirmButton != null) confirmButton.interactable = false;
            for (int i = 0; i < optionButtons.Length; i++) SetActiveButton(optionButtons[i], false);
            for (int i = 0; i < crewButtons.Length; i++) SetActiveButton(crewButtons[i], false);

            if (Confirmed != null) Confirmed(selectedOption, selectedCrew);
        }

        // Gọi sau khi Confirm thành công: hiện dòng phân công + nút END DAY.
        public void ShowConfirmed(string optionLabel, string crewName)
        {
            if (previewText != null) previewText.text = "PHÂN CÔNG: " + crewName + " → " + optionLabel;
            // Hai nút chung một vị trí (builder gán cùng rect) → phải ẩn nút confirm khi hiện END DAY.
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            if (endDayButton != null) endDayButton.gameObject.SetActive(true);
            if (editButton != null) editButton.gameObject.SetActive(true);
            if (backButton != null) backButton.gameObject.SetActive(false);
        }

        void OnEndDayClicked()
        {
            if (locked) return; // đã END DAY rồi → không cộng quãng đường lần hai
            locked = true;
            if (endDayButton != null) endDayButton.interactable = false;
            if (EndDayRequested != null) EndDayRequested();
        }

        // QUAY LẠI (trước khi CONFIRM): về màn Status, giữ nguyên lựa chọn.
        void OnBackClicked()
        {
            if (confirmed || locked) return;
            if (BackRequested != null) BackRequested();
        }

        // CHỈNH LẠI (sau khi CONFIRM): mở khóa lựa chọn để chỉnh lại, END DAY bị hủy.
        void OnEditClicked()
        {
            if (!confirmed || locked) return;
            confirmed = false;
            if (endDayButton != null) endDayButton.gameObject.SetActive(false);
            if (confirmButton != null) confirmButton.gameObject.SetActive(true);
            if (editButton != null) editButton.gameObject.SetActive(false);
            if (backButton != null) backButton.gameObject.SetActive(true);
            for (int i = 0; i < optionButtons.Length; i++)
            {
                SetActiveButton(optionButtons[i], true);
                SetSelected(optionButtons[i], i < optionLabels.Length ? optionLabels[i] : null, i == selectedOption);
            }
            for (int i = 0; i < crewButtons.Length; i++)
            {
                SetActiveButton(crewButtons[i], true);
                SetSelected(crewButtons[i], i < crewLabels.Length ? crewLabels[i] : null, crewButtons[i] != null && CrewOrder[i] == selectedCrew);
            }
            RefreshPreview();
            if (EditRequested != null) EditRequested();
        }

        // Restart: dọn sạch lựa chọn và màu hiển thị.
        public void ClearSelection()
        {
            selectedOption = -1;
            selectedCrew = CrewId.Any;
            confirmed = false;
            locked = false;
            if (previewText != null) previewText.text = "";
            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = false;
            }
            if (endDayButton != null)
            {
                endDayButton.gameObject.SetActive(false);
                endDayButton.interactable = true;
            }
            if (editButton != null) editButton.gameObject.SetActive(false);
        }

        static void SetActiveButton(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        static void SetSelected(Button button, TMP_Text label, bool selected)
        {
            if (button != null)
            {
                Image image = button.image;
                if (image != null) image.color = selected ? SelectedBgColor : NormalBgColor;
            }
            if (label != null) label.color = selected ? SelectedLabelColor : LabelColor;
        }
    }
}
