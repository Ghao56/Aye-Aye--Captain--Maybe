using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace AyeAye
{
    [RequireComponent(typeof(UIDocument))]

    public class TerminalView : MonoBehaviour
    {
        enum IntroState { WaitingClick, Zooming, PowerOn, Done, TvOff, ZoomOut }
        const float ZoomSeconds = 0.6f;
        const float PowerOnSeconds = 0.5f;
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

        public bool IsMachineOn => introState == IntroState.Done;
        float introMs;
        Vector3 camStartPos;
        float camStartSize;
        Vector3 exitStartPos;          
        float exitStartSize;
        float exitMs;
        Vector2 screenCenterWorld;
        float targetOrthoSize;
        IVisualElementScheduledItem introTick;
        [Header("Machine adjustments in the Scene")]
        [Tooltip("Scale the whole machine (sprite + UI + click area) with one number. To move it: drag the GameObject in the Scene view.")]

        public float uiScale = 1f;
        Vector3 baseScale;
        float appliedScale = float.NaN;
        void OnEnable()
        {
            baseScale = transform.localScale; 
            document = GetComponent<UIDocument>();
            TryBind();
        }
        void Update()
        {
            if (!Mathf.Approximately(appliedScale, uiScale))
            {
                appliedScale = uiScale;
                transform.localScale = baseScale * uiScale;
            }
            if (!bound)
            {
                TryBind();
                return;
            }
            FitPanelToMachine(); 
            if (cam == null) return;
            if (introState == IntroState.Done && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
                ExitTerminal();
            if (introState == IntroState.WaitingClick)
                ComputeZoomTarget();           
            else if (introState == IntroState.Done)
                FitScreenToFrame();            
        }
        SpriteRenderer preview;
        Vector2 MachineWorldSize
        {
            get
            {
                if (preview != null && preview.sprite != null)
                {
                    Vector3 s = transform.lossyScale;
                    Vector3 b = preview.sprite.bounds.size;
                    return new Vector2(Mathf.Abs(b.x * s.x), Mathf.Abs(b.y * s.y));
                }
                return new Vector2(MachineWorldWidth, MachineWorldHeight);
            }
        }
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
                Debug.LogError("[TerminalView] Camera.main not found — the game camera must have the MainCamera tag.");
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
            root.RegisterCallback<PointerDownEvent>(OnIntroClick); 
            bound = true;
            var cols = GetComponentsInChildren<Collider>(false);
            var list = new System.Text.StringBuilder();
            foreach (var c in cols) list.Append(c.name).Append("(layer ").Append(LayerMask.LayerToName(c.gameObject.layer)).Append(") ");
            Debug.Log($"[TerminalView] Bind OK: panel={document.worldSpaceSize}, machine={MachineWorldSize}, camera={cam.name}, colliders={cols.Length}: {list}");
        }
        void OnIntroClick(PointerDownEvent evt)
        {
            if (introState != IntroState.WaitingClick) return;
            Debug.Log("[TerminalView] Click received — starting zoom.");
            introState = IntroState.Zooming;
            introMs = 0f;
            camStartPos = cam.transform.position;
            camStartSize = cam.orthographicSize;
            ComputeZoomTarget();
            if (introTick != null) introTick.Pause();
            introTick = root.schedule.Execute(ZoomTick).Every(16);
        }
        [SerializeField, Range(0.3f, 4f)] private float zoomOutFactor = 1f; 
        [SerializeField] private Vector2 zoomOffset = Vector2.zero;        
        const float ScreenLeftPct = 19.53125f, ScreenTopPct = 8.30078125f;
        const float ScreenWidthPct = 63.4765625f, ScreenHeightPct = 67.3828125f;
        static Rect ScreenRectPx => new Rect(
            ScreenLeftPct / 100f * 1920f, ScreenTopPct / 100f * 1280f,
            ScreenWidthPct / 100f * 1920f, ScreenHeightPct / 100f * 1280f);
        void ComputeZoomTarget()
        {
            float upp = MachineWorldSize.x / 1920f;
            Rect r = ScreenRectPx;
            Vector2 pxOffset = new Vector2(r.center.x - 960f, 640f - r.center.y); 
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
            tvLine.style.top = Length.Percent(50f); 
            introTick = root.schedule.Execute(PowerOnTick).Every(16);
        }
        void PowerOnTick()
        {
            if (tvFx == null || tvLine == null) { FinishIntro(); return; }
            introMs += 16f;
            float k = Mathf.Clamp01(introMs / (PowerOnSeconds * 1000f));
            if (k < 0.4f)
            {
                tvLine.style.width = Length.Percent(Smooth(k / 0.4f) * 100f);
            }
            else
            {
                float k2 = (k - 0.4f) / 0.6f;
                float h = Mathf.Lerp(2f, 100f, Smooth(k2));
                tvLine.style.width = Length.Percent(100f);
                tvLine.style.height = Length.Percent(h);
                tvLine.style.top = Length.Percent(50f - h * 0.5f); 
                tvFx.style.opacity = 1f - Smooth(k2);
            }
            if (k >= 1f) FinishIntro();
        }
        void FinishIntro()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
            if (tvFx != null) tvFx.style.display = DisplayStyle.None;
            if (cam != null && screen != null) FitScreenToFrame(); 
                introState = IntroState.Done;
            if (bound && root != null) root.UnregisterCallback<PointerDownEvent>(OnIntroClick);
            Debug.Log("[TerminalView] Intro done (camera zoom + TV power on).");
        }
        static float Smooth(float k) => k * k * (3f - 2f * k);

        public void ExitTerminal()
        {
            if (introTick != null) { introTick.Pause(); introTick = null; }
                if (cam == null || root == null || tvFx == null || tvLine == null) { FinishExit(); return; }
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
                float k1 = Smooth(k / 0.6f);
                float h = Mathf.Lerp(100f, 2f, k1);
                tvLine.style.height = Length.Percent(h);
                tvLine.style.top = Length.Percent(50f - h * 0.5f);
            }
            else
            {
                float k2 = Smooth((k - 0.6f) / 0.4f);
                tvLine.style.width = Length.Percent(Mathf.Lerp(100f, 0f, k2));
            }
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
            if (screen != null) RestoreScreen(); 
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
            if (tvFx != null) tvFx.style.display = DisplayStyle.None; 
            if (screen != null) RestoreScreen(); 
            introState = IntroState.WaitingClick;
            if (bound && root != null) root.RegisterCallback<PointerDownEvent>(OnIntroClick);
            Debug.Log("[TerminalView] Exited terminal (Esc) — camera back to the initial view.");
        }
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
            root.style.overflow = Overflow.Visible; 
        }
        void RestoreScreen()
        {
            screen.style.left = new Length(ScreenLeftPct, LengthUnit.Percent);
            screen.style.top = new Length(ScreenTopPct, LengthUnit.Percent);
            screen.style.width = new Length(ScreenWidthPct, LengthUnit.Percent);
            screen.style.height = new Length(ScreenHeightPct, LengthUnit.Percent);
            root.style.overflow = Overflow.Hidden;
        }
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
            introState = IntroState.WaitingClick; 
        }
    }
}

