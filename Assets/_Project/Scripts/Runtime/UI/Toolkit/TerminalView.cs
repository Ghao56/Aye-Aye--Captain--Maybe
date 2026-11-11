using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Khung UI Toolkit: co giãn #stage (1920x1280) theo #root theo kiểu "contain"
    // để toàn bộ UI + ảnh máy tính luôn trọn vẹn, có letterbox đen, không méo.
    // Kích thước stage ĐỌC từ layout (không hard-code, khớp R6 với USS).
    [RequireComponent(typeof(UIDocument))]
    public class TerminalView : MonoBehaviour
    {
        UIDocument document;
        VisualElement root;
        VisualElement stage;
        bool bound;

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
            if (stage == null) return;

            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            bound = true;
            ApplyScale();
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (bound) ApplyScale();
        }

        void ApplyScale()
        {
            if (root == null || stage == null) return;
            Rect rootRect = root.layout;
            Rect stageRect = stage.layout;
            if (rootRect.width <= 0f || rootRect.height <= 0f) return;
            if (stageRect.width <= 0f || stageRect.height <= 0f) return;

            float s = Mathf.Min(rootRect.width / stageRect.width, rootRect.height / stageRect.height);
            stage.style.scale = new Scale(new Vector3(s, s, 1f));
        }

        void OnDisable()
        {
            // Gỡ callback để UIDocument enable lại sẽ bind lại từ đầu (R-03).
            if (bound && root != null)
                root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root = null;
            stage = null;
            document = null;
            bound = false;
        }
    }
}
