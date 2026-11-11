using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Kiến trúc B (người dùng duyệt 11/11/2026): máy tính là VẬT THỂ world-space.
    // UIDocument (renderMode WorldSpace) + BoxCollider trên cùng GameObject — kéo đi đâu ảnh + UI đi theo.
    // Bấm vào máy → camera ortho dời + thu vào đúng vùng kính (#screen) → hiệu ứng TV bật (như kiến trúc A).
    [RequireComponent(typeof(UIDocument))]
    public class TerminalView : MonoBehaviour
    {
        enum IntroState { WaitingClick, Zooming, PowerOn, Done }

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
        float introMs;
        Vector3 camStartPos;
        float camStartSize;
        Vector2 screenCenterWorld;
        float targetOrthoSize;
        IVisualElementScheduledItem introTick;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            TryBind();
        }

        void Update()
        {
            // rootVisualElement có thể chưa dựng cây lúc OnEnable (R-02): thử bind lại đến khi xong.
            if (!bound)
            {
                TryBind();
                return;
            }

            if (cam == null) return;
            if (introState == IntroState.WaitingClick)
                FitCameraToMachine();          // chưa zoom: khung hình thấy trọn chiếc máy, bám theo vị trí object
            else if (introState == IntroState.Done)
                KeepScreenFitted();            // sau mở màn: kính lấp đầy khung, giữ khi đổi tỉ lệ cửa sổ
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
                    return new Vector2(b.x * s.x, b.y * s.y);
                }
                return new Vector2(MachineWorldWidth, MachineWorldHeight);
            }
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
        }

        // ------------------------------------------------------------- mở màn

        void OnIntroClick(PointerDownEvent evt)
        {
            if (introState != IntroState.WaitingClick) return;
            // PointerDown tới được root của panel này = click đã trúng vật thể (raycast world-space lo việc lọc).

            introState = IntroState.Zooming;
            introMs = 0f;
            camStartPos = cam.transform.position;
            camStartSize = cam.orthographicSize;
            ComputeZoomTarget();
            if (introTick != null) introTick.Pause();
            introTick = root.schedule.Execute(ZoomTick).Every(16);
        }

        // panel px → world: container world = worldSpaceSize (Fixed), panel px = 1920x1280.
        float UnitsPerPixel
        {
            get
            {
                float px = root != null && root.layout.width > 0f ? root.layout.width : 1920f;
                return MachineWorldSize.x / px;
            }
        }

        void ComputeZoomTarget()
        {
            float upp = UnitsPerPixel;
            // Vùng kính trong toạ độ px của container (#stage nằm ở góc container).
            Rect r = new Rect(stage.layout.position + screen.layout.position, screen.layout.size);
            Vector2 containerCenterPx = new Vector2(root.layout.width, root.layout.height) * 0.5f;
            Vector2 pxOffset = new Vector2(r.center.x - containerCenterPx.x, containerCenterPx.y - r.center.y); // world y hướng lên
            screenCenterWorld = (Vector2)transform.position + pxOffset * upp;

            float halfW = r.width * upp * 0.5f;
            float halfH = r.height * upp * 0.5f;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            targetOrthoSize = Mathf.Max(halfH, halfW / aspect); // contain: kính nằm gọn trong khung, không méo
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
                // Vạch giãn dọc lấp màn + lớp đen mờ dần đi.
                float k2 = (k - 0.4f) / 0.6f;
                tvLine.style.width = Length.Percent(100f);
                tvLine.style.height = Length.Percent(Mathf.Lerp(2f, 100f, Smooth(k2)));
                tvFx.style.opacity = 1f - Smooth(k2);
            }
            if (k >= 1f) FinishIntro();
        }

        void FinishIntro()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (tvFx != null) tvFx.style.display = DisplayStyle.None;
            introState = IntroState.Done;
            // Gỡ handler click: từ đây nút bấm của game nhận click bình thường (Phase 4).
            if (bound && root != null) root.UnregisterCallback<PointerDownEvent>(OnIntroClick);
            Debug.Log("[TerminalView] Mở màn xong (zoom camera + TV bật).");
        }

        static float Smooth(float k) => k * k * (3f - 2f * k);

        // ------------------------------------------------------------- căn camera

        // Chưa mở màn: thấy trọn chiếc máy (contain theo tỉ lệ cửa sổ), camera bám tâm object.
        void FitCameraToMachine()
        {
            Vector2 size = MachineWorldSize;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            cam.orthographicSize = Mathf.Max(size.y * 0.5f, size.x * 0.5f / aspect);
            Vector3 p = cam.transform.position;
            cam.transform.position = new Vector3(transform.position.x, transform.position.y, p.z);
        }

        // Sau mở màn: kính lấp đầy khung (đổi kích thước cửa sổ cũng giữ).
        void KeepScreenFitted()
        {
            ComputeZoomTarget();
            cam.orthographicSize = targetOrthoSize;
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
