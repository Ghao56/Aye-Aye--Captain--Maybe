using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Khung UI Toolkit + mở màn (tính năng được người dùng duyệt 11/11/2026, lệch có duyệt so với lệnh gốc):
    // chờ bấm vào máy tính → zoom vào đúng vùng kính (#screen) → hiệu ứng TV bật (vạch sáng giãn) → xong.
    // Co giãn #stage theo #root kiểu "contain" khi chưa mở màn; sau mở màn giữ kiểu "lấp đầy #screen".
    [RequireComponent(typeof(UIDocument))]
    public class TerminalView : MonoBehaviour
    {
        enum IntroState { WaitingClick, Zooming, PowerOn, Done }

        const float ZoomDurationMs = 600f;
        const float PowerOnDurationMs = 500f;

        UIDocument document;
        VisualElement root;
        VisualElement stage;
        VisualElement screen;
        VisualElement tvFx;
        VisualElement tvLine;
        bool bound;
        IntroState introState = IntroState.WaitingClick;
        double introStart;
        float translateX;
        float translateY;
        IVisualElementScheduledProperty introTick;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            TryBind();
        }

        void Update()
        {
            // rootVisualElement có thể chưa dựng cây lúc OnEnable (R-02): thử bind lại đến khi xong.
            if (!bound) TryBind();
        }

        void TryBind()
        {
            if (document == null || bound) return;
            root = document.rootVisualElement;
            if (root == null || root.childCount == 0) return;
            stage = root.Q("stage");
            screen = root.Q("screen");
            tvFx = root.Q("tv-fx");
            tvLine = root.Q("tv-line");
            if (stage == null || screen == null) return;

            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.RegisterCallback<PointerDownEvent>(OnIntroClick); // gỡ khi intro xong và ở OnDisable (R-03)
            bound = true;
            ApplyScale();
        }

        // ------------------------------------------------------------- mở màn

        void OnIntroClick(PointerDownEvent evt)
        {
            if (introState != IntroState.WaitingClick) return;
            if (!stage.worldBound.Contains((Vector2)evt.position)) return; // bấm ngoài máy tính không mở màn

            introState = IntroState.Zooming;
            introStart = Time.realtimeSinceStartupAsDouble * 1000.0;
            if (introTick != null) introTick.Pause();
            introTick = root.schedule.Execute(ZoomTick).Every(16);
        }

        void ZoomTick()
        {
            if (root == null || stage == null || screen == null) return;
            float k = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble * 1000.0 - introStart) / ZoomDurationMs));
            float s = Mathf.Lerp(FitScale(stage), FitScale(screen), Smooth(k));
            stage.style.scale = new Scale(new Vector3(s, s, 1f));
            if (k >= 1f)
            {
                introTick.Pause();
                introTick = null;
                AlignScreenToRoot();
                StartPowerOn();
            }
        }

        void StartPowerOn()
        {
            introState = IntroState.PowerOn;
            introStart = Time.realtimeSinceStartupAsDouble * 1000.0;
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
            float k = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble * 1000.0 - introStart) / PowerOnDurationMs));
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
            Debug.Log("[TerminalView] Mở màn xong (zoom + TV bật).");
        }

        // Đo và dịch #stage sao cho #screen căn đúng tâm khung (dùng worldBound, không phụ thuộc thứ tự transform).
        void AlignScreenToRoot()
        {
            root.schedule.Execute(AlignTick).ExecuteLater(0);
            root.schedule.Execute(AlignTick).ExecuteLater(48); // đo lại một lần để tự hiệu chỉnh phần dư
        }

        void AlignTick()
        {
            if (root == null || stage == null || screen == null) return;
            Vector2 delta = Center(root.worldBound) - Center(screen.worldBound);
            translateX += delta.x;
            translateY += delta.y;
            stage.style.translate = new Translate(translateX, translateY, 0f);
        }

        static Vector2 Center(Rect r) => new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        static float Smooth(float k) => k * k * (3f - 2f * k);

        // ------------------------------------------------------------- co giãn

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (!bound) return;
            if (introState == IntroState.WaitingClick)
                ApplyScale();          // contain khi chưa mở màn
            else if (introState == IntroState.Done)
                ApplyFillTransform();  // giữ kiểu lấp đầy khi cửa sổ đổi kích thước
            // Zooming/PowerOn: tick của intro tự quản, không đụng vào.
        }

        void ApplyScale()
        {
            if (root == null || stage == null) return;
            if (stage.layout.width <= 0f || stage.layout.height <= 0f) return;
            if (root.layout.width <= 0f || root.layout.height <= 0f) return;
            float s = FitScale(stage);
            stage.style.scale = new Scale(new Vector3(s, s, 1f));
        }

        // Sau mở màn: #screen lấp đầy khung (cửa sổ đổi kích thước cũng giữ).
        void ApplyFillTransform()
        {
            if (root == null || stage == null || screen == null) return;
            if (screen.layout.width <= 0f || screen.layout.height <= 0f) return;
            float s = FitScale(screen);
            stage.style.scale = new Scale(new Vector3(s, s, 1f));
            AlignScreenToRoot();
        }

        // Hệ số co: đưa phần tử `el` về vừa khít khung root (không méo).
        float FitScale(VisualElement el)
        {
            Rect rootRect = root.layout;
            Rect elRect = el.layout;
            if (rootRect.width <= 0f || rootRect.height <= 0f) return 1f;
            if (elRect.width <= 0f || elRect.height <= 0f) return 1f;
            return Mathf.Min(rootRect.width / elRect.width, rootRect.height / elRect.height);
        }

        // ------------------------------------------------------------- vòng đời

        void OnDisable()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (bound && root != null)
            {
                root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                root.UnregisterCallback<PointerDownEvent>(OnIntroClick);
            }
            if (stage != null) stage.style.translate = new Translate(0f, 0f, 0f);
            root = null;
            stage = null;
            screen = null;
            tvFx = null;
            tvLine = null;
            document = null;
            bound = false;
            introState = IntroState.WaitingClick; // Play lại từ đầu → chờ bấm lại
            translateX = 0f;
            translateY = 0f;
        }
    }
}
