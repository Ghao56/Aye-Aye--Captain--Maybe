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
            game.assignment.Confirmed += OnConfirmed;
            game.assignment.EndDayRequested += OnEndDay;
            game.resultOverlay.NextRequested += OnOverlayNext;
            game.endScreen.RestartRequested += OnRestart;
        }

        public void BeginDay()
        {
            GameState state = game.State;
            DayDefinition day = game.GetDay(state.CurrentDay);
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
            game.briefing.Show(day);
        }

        void ShowStatusScreen()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            GameState state = game.State;
            game.screenFlow.ShowStep(ScreenStep.Status);
            game.status.Show(state, game.GetDay(state.CurrentDay), game.crews);
            game.cakeStatus.Show(state.Cargo);

            isTransitioning = false;
        }

        void ShowAssignmentScreen()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            DayDefinition day = game.GetDay(game.State.CurrentDay);
            game.screenFlow.ShowStep(ScreenStep.Assignment);
            game.assignment.Show(day != null ? day.decision : null, game.crews);

            isTransitioning = false;
        }

        void OnConfirmed(int optionIndex, CrewId crew)
        {
            GameState state = game.State;
            if (isTransitioning || state.PendingResolved != null) return;
            isTransitioning = true;

            DayDefinition day = game.GetDay(state.CurrentDay);
            DecisionOption option = null;
            if (day != null && day.decision != null && day.decision.options != null
                && optionIndex >= 0 && optionIndex < day.decision.options.Count)
                option = day.decision.options[optionIndex];

            state.PendingResolved = DecisionResolver.Resolve(option, crew, state, game.GetCrew(crew));
            game.assignment.ShowConfirmed(state.PendingResolved.OptionLabel, state.PendingResolved.CrewName);

            isTransitioning = false;
        }

        void OnEndDay()
        {
            GameState state = game.State;
            if (isTransitioning || state.PendingResolved == null) return;
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

            game.screenFlow.ShowResultOverlay(false);
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
            game.State.Reset();

            isTransitioning = false;
            BeginDay();
        }
    }
}
