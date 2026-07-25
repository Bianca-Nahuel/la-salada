using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salada.Game;

namespace Salada.UI
{
    /// <summary>
    /// Modal de evento: backdrop que bloquea + panel con el personaje que habla, titulo,
    /// descripcion y opciones. Al elegir una opcion muestra su dialogo final y "Continuar".
    /// </summary>
    public class EventPopup : MonoBehaviour
    {
        private GameObject _root;
        private Transform _content;
        private Font _font;

        private GameEvent _ev;
        private Action<int> _onOption;
        private Action _onClose;

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
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(500, 120);
            panel.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 1f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 18, 18); vlg.spacing = 10;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = panel.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        public void Show(GameEvent ev, Action<int> onOption, Action onClose)
        {
            _ev = ev; _onOption = onOption; _onClose = onClose;
            BuildDescription();
            _root.SetActive(true);
        }

        void BuildDescription()
        {
            Clear();
            AddSpeaker(_ev.speaker);
            AddText(_ev.title, 24, new Color(1f, 0.85f, 0.5f), FontStyle.Bold, 34, TextAnchor.MiddleCenter);
            AddText(_ev.description, 17, new Color(0.88f, 0.9f, 0.94f), FontStyle.Normal, 60, TextAnchor.UpperLeft);

            if (_ev.options != null)
                for (int i = 0; i < _ev.options.Length; i++)
                {
                    int idx = i;
                    var o = _ev.options[i];
                    AddButton($"{o.label}\n<{Preview(o)}>", new Color(0.2f, 0.5f, 0.65f), () => ChooseOption(idx));
                }
        }

        void ChooseOption(int i)
        {
            _onOption?.Invoke(i);   // aplica las consecuencias ahora
            BuildOutcome(i);
        }

        void BuildOutcome(int i)
        {
            Clear();
            AddSpeaker(_ev.speaker);
            var outcome = _ev.options[i].outcome;
            AddText(string.IsNullOrEmpty(outcome) ? "..." : outcome, 18, new Color(0.9f, 0.92f, 0.96f), FontStyle.Italic, 60, TextAnchor.UpperLeft);
            AddButton("Continuar", new Color(0.25f, 0.6f, 0.35f), () =>
            {
                _root.SetActive(false);
                _onClose?.Invoke();
            });
        }

        // ---- helpers ----

        void Clear()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        void AddSpeaker(EventCharacter ch)
        {
            if (ch == null) return;
            AddText(ch.characterName, 18, ch.color, FontStyle.Bold, 24, TextAnchor.MiddleLeft);
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
            var go = new GameObject("Opt", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = color;
            go.AddComponent<LayoutElement>().minHeight = 50;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
            var t = textGo.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = Color.white; t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static string Preview(EventOption o)
        {
            var parts = new List<string>();
            if (o.reputation != 0) parts.Add($"Rep {Signed(o.reputation)}");
            if (o.hostility != 0) parts.Add($"Host {Signed(o.hostility)}");
            if (o.happiness != 0) parts.Add($"Felic {Signed(o.happiness)}");
            if (o.profit != 0) parts.Add($"Profit {Signed(o.profit)}");
            if (o.money != 0) parts.Add($"${(o.money > 0 ? "+" : "")}{o.money}");
            if (o.special != EffectType.None && o.specialWaves > 0)
                parts.Add($"{GameEffects.Label(o.special, o.specialMagnitude)} x{o.specialWaves}");
            return parts.Count == 0 ? "sin efecto" : string.Join("   ", parts);
        }

        static string Signed(float v) => (v > 0 ? "+" : "") + v.ToString("0");
    }
}
