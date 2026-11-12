using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Kiến trúc B (người dùng duyệt 11/11/2026): máy tính là VẬT THỂ world-space.
    // UIDocument (renderMode WorldSpace) + BoxCollider trên cùng GameObject — kéo đi đâu ảnh + UI đi theo.
    // Bấm vào máy → camera ortho dời + thu vào đúng vùng kính (#screen) → hiệu ứng TV bật (như kiến trúc A).
    [RequireComponent(typeof(UIDocument))]
    public class TerminalView : MonoBehaviour
    {
        enum IntroState { WaitingClick, Zooming, PowerOn, Done, TvOff, ZoomOut }

        const float ZoomSeconds = 0.6f;
        const float PowerOnSeconds = 0.5f;
        // Phải khớp worldSpaceSize đặt trong SetupTerminalUI: 19.2x12.8 unit (PPU 100 → panel 1920x1280 px).
        const float MachineWorldWidth = 19.2f;
        const float MachineWorldHeight = 12.8f;

        UIDocument document;
        VisualElement root;
        VisualElement stage;
        VisualElement screen;
        VisualElement tvFx;
        VisualElement tvLine;
        Camera cam;
            bool bound;
        IntroState introState = IntroState.WaitingClick;

        /// Máy đã bật xong (kính CRT mở màn) — TerminalHud chỉ hiện game UI khi true.
        public bool IsMachineOn => introState == IntroState.Done;
        float introMs;
        Vector3 camStartPos;
        float camStartSize;
        Vector3 exitStartPos;          // vị trí/zoom camera lúc bắt đầu thoát (Esc)
        float exitStartSize;
        float exitMs;
        Vector2 screenCenterWorld;
        float targetOrthoSize;
        IVisualElementScheduledItem introTick;

        [Header("Điều chỉnh máy trong Scene")]
        [Tooltip("Phóng to/thu nhỏ toàn máy (ảnh + UI + vùng bấm) bằng một số. Đổi vị trí: kéo GameObject trong Scene view.")]
        public float uiScale = 1f;
        Vector3 baseScale;
        float appliedScale = float.NaN;

        void OnEnable()
        {
            baseScale = transform.localScale; // giữ nguyên scale dựng sẵn trên object, uiScale chỉ nhân thêm
            document = GetComponent<UIDocument>();
            TryBind();
        }

        void Update()
        {
            // Áp uiScale mới (kể cả khi chưa bind): giữ scale dựng sẵn trên object, uiScale chỉ nhân thêm.
            if (!Mathf.Approximately(appliedScale, uiScale))
            {
                appliedScale = uiScale;
                transform.localScale = baseScale * uiScale;
            }

            // rootVisualElement có thể chưa dựng cây lúc OnEnable (R-02): thử bind lại đến khi xong.
            if (!bound)
            {
                TryBind();
                return;
            }
            FitPanelToMachine(); // UI luôn phủ đúng ảnh máy, kể cả khi đổi scale giữa lúc chơi

            if (cam == null) return;
            // Phím Esc khi đang ở trong máy: thoát về khung nhìn ban đầu, chờ bấm máy lần nữa.
            if (introState == IntroState.Done && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
                ExitTerminal();
            // Camera đứng yên lúc chờ bấm — máy là 1 vật thể đặt đâu ở đấy, không bám theo.
            if (introState == IntroState.WaitingClick)
                ComputeZoomTarget();           // tính sớm để gizmo Scene view hiện vùng zoom trước khi bấm
            else if (introState == IntroState.Done)
                FitScreenToFrame();            // UI trong kính phóng đúng bằng khung vàng, giữ khi đổi tỉ lệ cửa sổ
        }

        // Ảnh máy giờ do SpriteRenderer "ImagePreview" vẽ → mọi kích thước camera/zoom lấy theo sprite thật,
        // người dùng scale object bao nhiêu cũng khớp.
        SpriteRenderer preview;
        Vector2 MachineWorldSize
        {
            get
            {
                if (preview != null && preview.sprite != null)
                {
                    Vector3 s = transform.lossyScale;
                    Vector3 b = preview.sprite.bounds.size;
                    // Abs: scale âm (lật/scene lưu hỏng) không được biến panel thành chiều rộng âm — mesh biến dạng, mất click.
                    return new Vector2(Mathf.Abs(b.x * s.x), Mathf.Abs(b.y * s.y));
                }
                return new Vector2(MachineWorldWidth, MachineWorldHeight);
            }
        }

        // Panel world-space để Dynamic: container tự bám nội dung thiết kế (stage 1920x1280 px @ PPU 100),
        // scale của GO thì scale cả panel → luôn khớp ảnh máy ở mọi scale.
        // shortcut: KHÔNG ép Fixed + worldSpaceSize theo sprite×scale — làm panel px lệch thiết kế,
        // raycast EventSystem rơi ngoài vùng pick → mất click (lỗi 12/11/2026).
        void FitPanelToMachine()
        {
            if (document == null) return;
            if (document.worldSpaceSizeMode != UIDocument.WorldSpaceSizeMode.Dynamic)
                document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
        }

        void TryBind()
        {
            if (document == null || bound) return;
            if (preview == null) preview = GetComponentInChildren<SpriteRenderer>();
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[TerminalView] Không tìm thấy Camera.main — camera game phải có tag MainCamera.");
                return;
            }
            FitPanelToMachine();
            root = document.rootVisualElement;
            if (root == null || root.childCount == 0) return;
            stage = root.Q("stage");
            screen = root.Q("screen");
            tvFx = root.Q("tv-fx");
            tvLine = root.Q("tv-line");
            if (stage == null || screen == null) return;


            // Sự kiện pointer world-space tới panel nhờ PanelInputConfiguration + BoxCollider (Setup tạo).
            root.RegisterCallback<PointerDownEvent>(OnIntroClick); // gỡ khi intro xong và ở OnDisable
            bound = true;
            var cols = GetComponentsInChildren<Collider>(false);
            var list = new System.Text.StringBuilder();
            foreach (var c in cols) list.Append(c.name).Append("(layer ").Append(LayerMask.LayerToName(c.gameObject.layer)).Append(") ");
            Debug.Log($"[TerminalView] Bind OK: panel={document.worldSpaceSize}, máy={MachineWorldSize}, camera={cam.name}, collider={cols.Length}: {list}");
        }

        // ------------------------------------------------------------- mở màn

        void OnIntroClick(PointerDownEvent evt)
        {
            if (introState != IntroState.WaitingClick) return;
            // PointerDown tới được root của panel này = click đã trúng vật thể (raycast world-space lo việc lọc).
            Debug.Log("[TerminalView] Nhận click — bắt đầu zoom.");

            introState = IntroState.Zooming;
            introMs = 0f;
            camStartPos = cam.transform.position;
            camStartSize = cam.orthographicSize;
            ComputeZoomTarget();
            if (introTick != null) introTick.Pause();
            introTick = root.schedule.Execute(ZoomTick).Every(16);
        }

        [SerializeField, Range(0.3f, 4f)] private float zoomOutFactor = 1f; // 1 = khung vàng đúng bằng vùng kính; 0.8 = kính chiếm 80% khung (luôn nằm trọn, không cắt)
        [SerializeField] private Vector2 zoomOffset = Vector2.zero;        // lệch tâm zoom (world unit): x phải, y lên

        // Vùng kính gốc theo USS (% trên sân khấu 1920x1280 px). Đọc hằng chứ KHÔNG đọc layout live —
        // vì .screen bị override kích thước khi đang bật máy (UI phóng đúng bằng khung vàng).
        const float ScreenLeftPct = 19.53125f, ScreenTopPct = 8.30078125f;
        const float ScreenWidthPct = 63.4765625f, ScreenHeightPct = 67.3828125f;

        static Rect ScreenRectPx => new Rect(
            ScreenLeftPct / 100f * 1920f, ScreenTopPct / 100f * 1280f,
            ScreenWidthPct / 100f * 1920f, ScreenHeightPct / 100f * 1280f);

        void ComputeZoomTarget()
        {
            // Khung vàng = vùng kính (giữ config của boss). upp gồm cả scale GO (panel px thiết kế = 1920).
            float upp = MachineWorldSize.x / 1920f;
            Rect r = ScreenRectPx;
            Vector2 pxOffset = new Vector2(r.center.x - 960f, 640f - r.center.y); // world y hướng lên
            screenCenterWorld = (Vector2)transform.position + pxOffset * upp + zoomOffset;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            targetOrthoSize = Mathf.Max(r.height * upp * 0.5f, r.width * upp * 0.5f / aspect) / zoomOutFactor;
        }

        void ZoomTick()
        {
            if (cam == null) { FinishIntro(); return; }
            introMs += 16f;
            float k = Mathf.Clamp01(introMs / (ZoomSeconds * 1000f));
            float s = Smooth(k);
            cam.orthographicSize = Mathf.Lerp(camStartSize, targetOrthoSize, s);
            Vector3 target = new Vector3(screenCenterWorld.x, screenCenterWorld.y, cam.transform.position.z);
            cam.transform.position = Vector3.Lerp(camStartPos, target, s);
            if (k >= 1f)
            {
                introTick.Pause();
                introTick = null;
                StartPowerOn();
            }
        }

        void StartPowerOn()
        {
            introState = IntroState.PowerOn;
            introMs = 0f;
            if (tvFx == null || tvLine == null)
            {
                FinishIntro();
                return;
            }
            tvFx.style.display = DisplayStyle.Flex;
            tvFx.style.opacity = 1f;
            tvLine.style.width = Length.Percent(0f);
            tvLine.style.height = Length.Percent(2f);
            tvLine.style.top = Length.Percent(50f); // giữa kính — nở đối xứng lên cả xuống (CRT bật)
            introTick = root.schedule.Execute(PowerOnTick).Every(16);
        }

        void PowerOnTick()
        {
            if (tvFx == null || tvLine == null) { FinishIntro(); return; }
            introMs += 16f;
            float k = Mathf.Clamp01(introMs / (PowerOnSeconds * 1000f));
            if (k < 0.4f)
            {
                // Vạch sáng giãn ngang trước.
                tvLine.style.width = Length.Percent(Smooth(k / 0.4f) * 100f);
            }
            else
            {
                // Vạch nở đối xứng từ giữa lấp trọn kính + lớp đen mờ dần đi.
                float k2 = (k - 0.4f) / 0.6f;
                float h = Mathf.Lerp(2f, 100f, Smooth(k2));
                tvLine.style.width = Length.Percent(100f);
                tvLine.style.height = Length.Percent(h);
                tvLine.style.top = Length.Percent(50f - h * 0.5f); // bám tâm: nở lên trên lẫn xuống dưới
                tvFx.style.opacity = 1f - Smooth(k2);
            }
            if (k >= 1f) FinishIntro();
        }

        void FinishIntro()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (tvFx != null) tvFx.style.display = DisplayStyle.None;
            if (cam != null && screen != null) FitScreenToFrame(); // UI vào đúng khung vàng ngay khi mở màn xong
                introState = IntroState.Done;
            // Gỡ handler click: từ đây nút bấm của game nhận click bình thường (Phase 4).
            if (bound && root != null) root.UnregisterCallback<PointerDownEvent>(OnIntroClick);
            Debug.Log("[TerminalView] Mở màn xong (zoom camera + TV bật).");
        }

        static float Smooth(float k) => k * k * (3f - 2f * k);

        // ------------------------------------------------------------- thoát máy

        // Thoát máy: TV tắt (màn thu về vạch giữa → chấm → tối) rồi camera lerp về chỗ cũ.
        public void ExitTerminal()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
                if (cam == null || root == null || tvFx == null || tvLine == null) { FinishExit(); return; }

            // Lớp đen phủ trọn kính ngay, vạch sáng trải tròn màn hình rồi thu dần.
            tvFx.style.display = DisplayStyle.Flex;
            tvFx.style.opacity = 1f;
            tvLine.style.width = Length.Percent(100f);
            tvLine.style.height = Length.Percent(100f);
            tvLine.style.top = Length.Percent(0f);
            exitMs = 0f;
            introState = IntroState.TvOff;
            introTick = root.schedule.Execute(TvOffTick).Every(16);
        }

        void TvOffTick()
        {
            if (introTick == null) return;
            if (tvFx == null || tvLine == null) { StartZoomOut(); return; }
            exitMs += 16f;
            float k = Mathf.Clamp01(exitMs / (PowerOnSeconds * 1000f));
            if (k < 0.6f)
            {
                // Màn thu dọc về vạch sáng ở giữa (đảo ngược lúc mở).
                float k1 = Smooth(k / 0.6f);
                float h = Mathf.Lerp(100f, 2f, k1);
                tvLine.style.height = Length.Percent(h);
                tvLine.style.top = Length.Percent(50f - h * 0.5f);
            }
            else
            {
                // Vạch thu ngang về một chấm giữa — chạy đến hết timeline, không chặn khoảng chết.
                float k2 = Smooth((k - 0.6f) / 0.4f);
                tvLine.style.width = Length.Percent(Mathf.Lerp(100f, 0f, k2));
            }
            // Tắt hiệu ứng NGAY khi chấm biến mất — trước khi camera lùi.
            if (k >= 1f)
            {
                tvFx.style.display = DisplayStyle.None;
                StartZoomOut();
            }
        }

        void StartZoomOut()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (cam == null || root == null) { FinishExit(); return; }
            if (screen != null) RestoreScreen(); // kính về % gốc — tvFx đen đang che nên không thấy
            exitMs = 0f;
            exitStartPos = cam.transform.position;
            exitStartSize = cam.orthographicSize;
            introState = IntroState.ZoomOut;
            introTick = root.schedule.Execute(ZoomOutTick).Every(16);
        }

        void ZoomOutTick()
        {
            if (cam == null || introTick == null) { FinishExit(); return; }
            exitMs += 16f;
            float k = Mathf.Clamp01(exitMs / (ZoomSeconds * 1000f));
            float s = Smooth(k);
            cam.orthographicSize = Mathf.Lerp(exitStartSize, camStartSize, s);
            cam.transform.position = Vector3.Lerp(exitStartPos, camStartPos, s);
            if (k >= 1f) FinishExit();
        }

        void FinishExit()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (tvFx != null) tvFx.style.display = DisplayStyle.None; // gỡ lớp hiệu ứng đọng — kính về trạng thái thường
            if (screen != null) RestoreScreen(); // đề phòng thoát sớm qua FinishExit trực tiếp
            introState = IntroState.WaitingClick;
            if (bound && root != null) root.RegisterCallback<PointerDownEvent>(OnIntroClick);
            Debug.Log("[TerminalView] Đã thoát máy (Esc) — camera về khung nhìn ban đầu.");
        }

        // ------------------------------------------------------------- gizmo vùng zoom

        // Chọn object Terminal trong Scene view: khung vàng = vùng camera sẽ thu vào sau khi bấm
        // (= đúng hình UIDocument). Edit mode vẽ theo hằng thiết kế; Play mode vẽ theo tính toán thật.
        void OnDrawGizmosSelected()
        {
            bool playing = Application.isPlaying;
            if (playing && targetOrthoSize <= 0f) return;

            float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            Vector3 scale = transform.lossyScale;
            Vector3 center;
            float ortho;
            if (playing)
            {
                center = new Vector3(screenCenterWorld.x, screenCenterWorld.y, transform.position.z);
                ortho = targetOrthoSize;
            }
            else
            {
                // Edit mode: dùng vùng kính gốc theo USS (px sân khấu 1920x1280, 1px = 0.01 unit @ scale 1).
                Rect r = ScreenRectPx;
                center = transform.position + Vector3.Scale(
                    new Vector3(-9.6f + r.x * 0.01f + r.width * 0.005f, 6.4f - r.y * 0.01f - r.height * 0.005f, 0f), scale)
                    + new Vector3(zoomOffset.x, zoomOffset.y, 0f);
                float halfW = r.width * 0.005f * scale.x;
                float halfH = r.height * 0.005f * scale.y;
                ortho = Mathf.Max(halfH, halfW / aspect) / zoomOutFactor;
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(ortho * 2f * aspect, ortho * 2f, 0f));
        }

        // ------------------------------------------------------------- căn camera

        // UI trong kính có kích thước = khung camera CHÍNH XÁC cả 2 chiều (ngang nới theo tỉ lệ cửa sổ).
        void FitScreenToFrame()
        {
            ComputeZoomTarget();
            cam.orthographicSize = targetOrthoSize;
            float upp = MachineWorldSize.x / 1920f;
            float frameH = targetOrthoSize * 2f;
            float frameW = frameH * cam.aspect;
            Vector2 centerPx = ScreenRectPx.center + new Vector2(zoomOffset.x, -zoomOffset.y) / upp;
            float w = frameW / upp, h = frameH / upp;
            screen.style.left = centerPx.x - w * 0.5f;
            screen.style.top = centerPx.y - h * 0.5f;
            screen.style.width = w;
            screen.style.height = h;
            root.style.overflow = Overflow.Visible; // UI có thể rộng hơn sân khấu ở cửa sổ rộng — tắt clip
        }

        // Trả .screen về % gốc trong USS (chạy khi tắt máy — lớp tvFx đen che sẵn nên không thấy nhảy).
        void RestoreScreen()
        {
            screen.style.left = new Length(ScreenLeftPct, LengthUnit.Percent);
            screen.style.top = new Length(ScreenTopPct, LengthUnit.Percent);
            screen.style.width = new Length(ScreenWidthPct, LengthUnit.Percent);
            screen.style.height = new Length(ScreenHeightPct, LengthUnit.Percent);
            root.style.overflow = Overflow.Hidden;
        }

        // ------------------------------------------------------------- vòng đời

        void OnDisable()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (bound && root != null)
                root.UnregisterCallback<PointerDownEvent>(OnIntroClick);
            root = null;
            stage = null;
            screen = null;
            tvFx = null;
            tvLine = null;
            document = null;
            cam = null;
                bound = false;
            introState = IntroState.WaitingClick; // Play lại từ đầu → chờ bấm lại
        }
    }
}
