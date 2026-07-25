using UnityEngine;
using UnityEngine.UI;
using Salada.Game;
using Salada.Combat;

namespace Salada.UI
{
    /// <summary>
    /// Modal que aparece al clickear una zona disputada: Atacar (inicia el minijuego, baja
    /// reputacion y sube hostilidad), Negociar (paga segun la paciencia y rellena la tregua) o
    /// Cancelar. No muestra el numero de paciencia, solo una frase cualitativa de tension.
    /// </summary>
    public class DisputePopup : MonoBehaviour
    {
        private GameObject _root;
        private Transform _content;
        private Font _font;

        void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _root.SetActive(false);
        }

        void Build()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch(_root.GetComponent<RectTransform>());

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(_root.transform, false);
            Stretch(bd.GetComponent<RectTransform>());
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(460, 120);
            panel.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 1f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 18, 18); vlg.spacing = 10;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = panel.transform;
        }

        public void Show(char zone, TerritoryManager territory, WaveManager waves)
        {
            if (_root == null) { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); Build(); }
            Clear();
            AddText($"Zona {zone} en disputa", 22, new Color(1f, 0.85f, 0.5f), FontStyle.Bold, 32);
            AddText($"Los rivales se ven {territory.TensionLine(zone)}.", 16, new Color(0.85f, 0.88f, 0.94f), FontStyle.Italic, 26);

            AddButton("Atacar  (−reputacion, +hostilidad)", new Color(0.7f, 0.3f, 0.25f), () =>
            {
                _root.SetActive(false);
                territory.PlayerAttack(zone);
            });

            int cost = territory.NegotiationCost(zone);
            bool canAfford = waves != null && waves.CanAfford(cost);
            var negBtn = AddButton($"Negociar (−${cost})", new Color(0.25f, 0.55f, 0.6f), () =>
            {
                _root.SetActive(false);
                territory.PlayerNegotiate(zone);
            });
            negBtn.interactable = canAfford;

            AddButton("Cancelar", new Color(0.3f, 0.32f, 0.38f), () => _root.SetActive(false));
            _root.SetActive(true);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void Clear()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        void AddText(string text, int size, Color color, FontStyle style, float minHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_content, false);
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        Button AddButton(string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = color;
            go.AddComponent<LayoutElement>().minHeight = 48;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
            var t = textGo.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = Color.white; t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return btn;
        }
    }
}
