using System;
using UnityEngine;
using UnityEngine.UI;
using Salada.Game;

namespace Salada.UI
{
    /// <summary>
    /// Modal del tutorial (mismo estilo que los eventos): retrato del que habla + texto + un boton.
    /// El backdrop puede ser mas transparente para que se vea el celular detras cuando el tutorial
    /// resalta algo (una stat, un boton). Lo maneja el TutorialManager.
    /// </summary>
    public class TutorialDialog : MonoBehaviour
    {
        private GameObject _root;
        private Image _backdrop;
        private Transform _content;
        private Image _portraitImage;
        private GameObject _portrait;
        private Font _font;
        private Action _onNext;
        private bool _built;

        public bool IsShowing => _root != null && _root.activeSelf;

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _font = UIFont.Get();

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch(_root.GetComponent<RectTransform>());

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(_root.transform, false);
            Stretch(bd.GetComponent<RectTransform>());
            _backdrop = bd.GetComponent<Image>();
            _backdrop.color = new Color(0f, 0f, 0f, 0.65f);

            // retrato del encargado: esquina INFERIOR DERECHA de la pantalla (como en los eventos)
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(_root.transform, false);
            var port = portraitGo.GetComponent<RectTransform>();
            port.anchorMin = new Vector2(1f, 0f); port.anchorMax = new Vector2(1f, 0f); port.pivot = new Vector2(1f, 0f);
            port.sizeDelta = new Vector2(420, 638);
            port.anchoredPosition = new Vector2(-10, -30);
            _portraitImage = portraitGo.GetComponent<Image>();
            _portraitImage.preserveAspect = true; _portraitImage.raycastTarget = false;
            _portrait = portraitGo;

            // panel de texto: centrado, como en los eventos
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, 0f);
            prt.sizeDelta = new Vector2(520, 130);
            panel.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 0.98f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 18, 18); vlg.spacing = 10;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = panel.transform;

            _root.SetActive(false);
        }

        /// <summary>Muestra un mensaje del tutorial. backdropAlpha bajo = se ve el celu detras.</summary>
        public void Show(EventCharacter speaker, string text, string buttonLabel, float backdropAlpha, Action onNext)
        {
            EnsureBuilt();
            _onNext = onNext;
            _backdrop.color = new Color(0f, 0f, 0f, Mathf.Clamp01(backdropAlpha));

            var sprite = speaker != null ? speaker.sprite : null;
            _portrait.SetActive(sprite != null);
            _portraitImage.sprite = sprite;

            Clear();
            if (speaker != null) AddText(speaker.characterName, 18, speaker.color, FontStyle.Bold, 24, TextAnchor.MiddleLeft);
            AddText(text, 19, new Color(0.92f, 0.94f, 0.98f), FontStyle.Normal, 54, TextAnchor.UpperLeft);
            AddButton(string.IsNullOrEmpty(buttonLabel) ? "Dale" : buttonLabel, new Color(0.25f, 0.6f, 0.35f), () =>
            {
                _root.SetActive(false);
                _onNext?.Invoke();
            });

            _portrait.transform.SetAsLastSibling();
            _root.transform.SetAsLastSibling(); // por encima del celu/mapa
            _root.SetActive(true);
        }

        public void Hide() { if (_root != null) _root.SetActive(false); }

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void Clear()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i).gameObject;
                if (c == _portrait) continue;
                c.SetActive(false); Destroy(c);
            }
        }

        Text AddText(string text, int size, Color color, FontStyle style, float minHeight, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_content, false);
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void AddButton(string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = color;
            go.AddComponent<LayoutElement>().minHeight = 48;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
            var t = textGo.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = Color.white; t.fontSize = 17; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
