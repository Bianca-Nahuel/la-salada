using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Salada.UI
{
    /// <summary>
    /// Menu de opciones del juego (se abre desde el celular). Pausa (Time.timeScale=0) y ofrece
    /// Reanudar y Salir al menu principal. Como con timeScale=0 los botones del EventSystem no
    /// responden, los clicks se detectan con input crudo (Mouse) + hit-test de los rects.
    /// </summary>
    public class GameOptionsPopup : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "MainMenu";

        private GameObject _root;
        private Font _font;
        private float _prevTimeScale;
        private bool _showing;
        private RectTransform _resumeBtn, _menuBtn;

        public bool IsShowing => _showing;

        void Start()
        {
            _font = UIFont.Get();
            Build();
            _root.SetActive(false);
        }

        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch(_root.GetComponent<RectTransform>());

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(_root.transform, false);
            Stretch(bd.GetComponent<RectTransform>());
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(420, 260);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

            var title = MakeText(panel.transform, "Opciones", 24, new Color(1f, 0.85f, 0.5f), FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(380, 40));

            _resumeBtn = MakeButton(panel.transform, "Reanudar", new Color(0.22f, 0.55f, 0.35f), new Vector2(0, 30), new Vector2(320, 62), Hide);
            _menuBtn = MakeButton(panel.transform, "Salir al menu principal", new Color(0.5f, 0.28f, 0.28f), new Vector2(0, -48), new Vector2(320, 62), ToMenu);
        }

        public void Show()
        {
            if (_showing) return;
            if (_root == null) { _font = UIFont.Get(); Build(); }
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _showing = true;
            _root.SetActive(true);
        }

        public void Hide()
        {
            _showing = false;
            if (_root != null) _root.SetActive(false);
            Time.timeScale = _prevTimeScale;
        }

        void ToMenu()
        {
            _showing = false;
            Time.timeScale = 1f; // restaurar antes de cambiar de escena
            SceneManager.LoadScene(mainMenuScene);
        }

        void Update()
        {
            if (!_showing) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Hide(); return; }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var mp = Mouse.current.position.ReadValue();
                if (Hit(_menuBtn, mp)) { ToMenu(); return; }
                if (Hit(_resumeBtn, mp)) { Hide(); return; }
            }
        }

        static bool Hit(RectTransform rt, Vector2 screenPos)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        Text MakeText(Transform parent, string text, int size, Color color, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        RectTransform MakeButton(Transform parent, string label, Color color, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick); // fallback si el EventSystem responde
            var t = MakeText(go.transform, label, 18, Color.white, FontStyle.Bold);
            Stretch(t.rectTransform);
            return rt;
        }
    }
}
