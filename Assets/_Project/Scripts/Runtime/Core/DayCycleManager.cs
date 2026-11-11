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
            Debug.Log("[DBG] BeginDay: ngày " + state.CurrentDay + ", event=" + (day != null ? day.eventId : "NULL!") + ", decision=" + (day != null && day.decision != null ? "có" : "NULL!"));
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
            Debug.Log("[DBG] ShowAssignmentScreen: vào màn phân công");
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
            Debug.Log("[DBG] OnConfirmed: option=" + optionIndex + " crew=" + crew + " isTransitioning=" + isTransitioning + " PendingResolved=" + (state.PendingResolved != null ? "SET" : "null"));
            if (isTransitioning || state.PendingResolved != null) return;
            isTransitioning = true;

            DayDefinition day = game.GetCurrentEvent();
            DecisionOption option = null;
            if (day != null && day.decision != null && day.decision.options != null
                && optionIndex >= 0 && optionIndex < day.decision.options.Count)
                option = day.decision.options[optionIndex];

            state.PendingResolved = DecisionResolver.Resolve(option, crew, state, game.GetCrew(crew));
            Debug.Log("[DBG] OnConfirmed: resolve xong, outcome=" + state.PendingResolved.Outcome);
            game.assignment.ShowConfirmed(state.PendingResolved.OptionLabel, state.PendingResolved.CrewName);
            Debug.Log("[DBG] OnConfirmed: ShowConfirmed đã gọi");

            // Sau khi xác nhận, giữ trạng thái chuyển tiếp để user thấy kết quả phân công
            // trước khi chuyển sang bước END DAY, tránh chồng lấn UI (kết thúc ngày ↔ xác nhận quyết định).
            // Sử dụng Invoke để tránh chặn UI thread, cho phép UI cập nhật trước.
            Invoke(nameof(CompleteConfirmation), 0.3f);
        }

        void CompleteConfirmation()
        {
            isTransitioning = false;
        }

        void OnEndDay()
        {
            GameState state = game.State;
            // Không kiểm tra isTransitioning ở đây: END DAY chỉ bấm được sau CONFIRM (PendingResolved đã set),
            // còn cửa sổ 0.3s sau CONFIRM vẫn isTransitioning=true — nếu từ chối trong cửa sổ đó thì UI
            // đã khóa sẵn nút → nút chết vĩnh viễn, người chơi bị kẹt.
            if (state.PendingResolved == null) { Debug.Log("[DBG] OnEndDay: BỊ CHẶN — PendingResolved null!"); return; }
            isTransitioning = true;

            ResolvedDecision resolved = state.PendingResolved;
            state.PendingResolved = null; // xóa ngay sau khi dùng để ngày sau có thể CONFIRM lại.

            // Thứ tự bắt buộc: áp Hàng/cờ → cộng quãng đường → đánh giá giao hàng.
            CargoTracker.Apply(state, resolved);
            float distance = TravelProgress.GetDistance(resolved.Outcome, game.Config);
            state.Progress += distance;
            DeliveryEvaluator.Evaluate(state);
            Debug.Log("[DBG] OnEndDay: xong logic, overlay sắp hiện");

            game.screenFlow.ShowResultOverlay(true);
            GameObject op = game.screenFlow.resultOverlayPanel;
            Debug.Log("[DBG] overlay panel=" + (op != null ? op.name : "NULL!") + " activeSelf=" + (op != null ? op.activeSelf.ToString() : "n/a"));
            game.resultOverlay.Show(resolved, distance, state.IsFinished);
            game.screenFeedback.PlayFeedback(resolved.Outcome);
            StartCoroutine(AnimateProgressThenReady());
        }

        IEnumerator AnimateProgressThenReady()
        {
            Debug.Log("[DBG] Coroutine chạy: progress=" + game.State.Progress + "/" + game.State.TotalDistance);
            game.progressBar.AnimateTo(game.State.Progress, game.State.TotalDistance, progressTweenSeconds);
            yield return new WaitForSeconds(progressTweenSeconds + 0.05f);
            Debug.Log("[DBG] SetNextReady gọi");
            game.resultOverlay.SetNextReady();
            isTransitioning = false;
        }

        void OnOverlayNext()
        {
            Debug.Log("[DBG] OnOverlayNext: isTransitioning=" + isTransitioning + " IsFinished=" + game.State.IsFinished);
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
