using System;
using System.Collections;
using UnityEngine;

namespace AyeAye
{
    // Điều phối vòng đời một ngày: Briefing → Status → Assignment → Confirm → End Day → Kết quả.
    // Không tự tính quãng đường (TravelProgress) và không tự quyết thắng/thua (DeliveryEvaluator).
    public class DayCycleManager : MonoBehaviour
    {
        [Tooltip("Thời gian nội suy thanh tiến trình sau End Day (giây).")]
        [SerializeField] float progressTweenSeconds = 1.0f;

        GameManager game;
        bool isTransitioning;

        public void Init(GameManager owner)
        {
            game = owner;

            game.briefing.ContinueRequested += ShowStatusScreen;
            game.status.ContinueRequested += ShowAssignmentScreen;
            game.status.BackRequested += ShowBriefingScreen;
            game.assignment.Confirmed += OnConfirmed;
            game.assignment.EndDayRequested += OnEndDay;
            game.assignment.BackRequested += ShowStatusScreen;
            game.assignment.EditRequested += OnEditRequested;
            game.resultOverlay.NextRequested += OnOverlayNext;
            game.endScreen.RestartRequested += OnRestart;
        }

        public void BeginDay()
        {
            GameState state = game.State;
            DayDefinition day = game.GetCurrentEvent();
            if (day == null)
            {
                Debug.LogError("[DayCycleManager] Không tìm thấy DayDefinition cho ngày " + state.CurrentDay);
                return;
            }

            isTransitioning = false;
            float cps = game.Config.typewriterCharsPerSecond;
            game.briefing.SetTypewriterSpeed(cps);
            game.status.SetTypewriterSpeed(cps);

            game.screenFlow.ShowStep(ScreenStep.Briefing);
            game.header.Refresh(state, game.Config);
            game.briefing.Show(day, state.CurrentDay);
        }

        void ShowStatusScreen()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            GameState state = game.State;
            game.screenFlow.ShowStep(ScreenStep.Status);
            game.status.Show(state, game.GetCurrentEvent(), game.crews);
            game.cakeStatus.Show(state.Cargo);

            isTransitioning = false;
        }

        void ShowBriefingScreen()
        {
            if (isTransitioning) return;

            GameState state = game.State;
            game.screenFlow.ShowStep(ScreenStep.Briefing);
            game.briefing.Show(game.GetCurrentEvent(), state.CurrentDay); // Show() chạy lại typewriter.
        }

        // CHỈNH LẠI: hủy phân công đã xác nhận; UI tự khôi phục các nút lựa chọn.
        void OnEditRequested()
        {
            game.State.PendingResolved = null;
        }

        void ShowAssignmentScreen()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            DayDefinition day = game.GetCurrentEvent();
            game.screenFlow.ShowStep(ScreenStep.Assignment);
            game.assignment.Show(day != null ? day.decision : null, game.crews);

            isTransitioning = false;
        }

        void OnConfirmed(int optionIndex, CrewId crew)
        {
            GameState state = game.State;
            if (isTransitioning || state.PendingResolved != null) return;
            isTransitioning = true;

            DayDefinition day = game.GetCurrentEvent();
            DecisionOption option = null;
            if (day != null && day.decision != null && day.decision.options != null
                && optionIndex >= 0 && optionIndex < day.decision.options.Count)
                option = day.decision.options[optionIndex];

            state.PendingResolved = DecisionResolver.Resolve(option, crew, state, game.GetCrew(crew));
            game.assignment.ShowConfirmed(state.PendingResolved.OptionLabel, state.PendingResolved.CrewName);

            // Hết ngay cửa sổ khóa: giữ Invoke 0.3s tạo khe CHỈNH LẠI giữa chừng mà PendingResolved
            // đã bị hủy → CONFIRM bị chặn → END DAY chết nút, người chơi kẹt.
            CompleteConfirmation();
        }

        void CompleteConfirmation()
        {
            isTransitioning = false;
        }

        void OnEndDay()
        {
            GameState state = game.State;
            if (state.PendingResolved == null) return;
            isTransitioning = true;

            ResolvedDecision resolved = state.PendingResolved;
            state.PendingResolved = null; // xóa ngay sau khi dùng để ngày sau có thể CONFIRM lại.

            // Thứ tự bắt buộc: áp Hàng/cờ → cộng quãng đường → đánh giá giao hàng.
            CargoTracker.Apply(state, resolved);
            float distance = TravelProgress.GetDistance(resolved.Outcome, game.Config);
            state.Progress += distance;
            DeliveryEvaluator.Evaluate(state);

            game.screenFlow.ShowResultOverlay(true);
            game.resultOverlay.Show(resolved, distance, state.IsFinished);
            game.screenFeedback.PlayFeedback(resolved.Outcome);
            StartCoroutine(AnimateProgressThenReady());
        }

        IEnumerator AnimateProgressThenReady()
        {
            game.progressBar.AnimateTo(game.State.Progress, game.State.TotalDistance, progressTweenSeconds);
            yield return new WaitForSeconds(progressTweenSeconds + 0.05f);
            game.resultOverlay.SetNextReady();
            isTransitioning = false;
        }

        void OnOverlayNext()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            if (game.State.IsFinished)
            {
                ShowEndScreen();
                return;
            }

            // Ngày mới = event mới: xóa cache để GetCurrentEvent bốc tiếp từ pool.
            game.State.CurrentEvent = null;
            game.screenFlow.ShowResultOverlay(false);
            // Chờ màn hình kết quả biến mất trước khi bắt đầu ngày mới
            // tránh chồng lấn UI giữa kết quả ngày cũ và giao diện ngày mới.
            Invoke(nameof(CompleteTransitionAndBeginDay), 0.2f);
        }

        void CompleteTransitionAndBeginDay()
        {
            isTransitioning = false;
            BeginDay();
        }

        void ShowEndScreen()
        {
            game.screenFlow.ShowEndScreen();
            game.endScreen.Show(game.State, game.Config, game.endings);
            isTransitioning = false;
        }

        void OnRestart()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            // Dứt điểm mọi hiệu ứng đang chạy trước khi dựng lại trạng thái.
            StopAllCoroutines();
            game.screenFlow.ResetAll();
            game.screenFeedback.ResetVisuals();
            game.assignment.ClearSelection();
            game.progressBar.ResetBar();
            // Restart = ván mới: NewRun dọn cả CurrentEvent; seed lặp lại hay không theo config.
            game.State.NewRun(game.Config.repeatSeedOnRestart ? game.State.Seed : Environment.TickCount);

            isTransitioning = false;
            BeginDay();
        }
    }
}
