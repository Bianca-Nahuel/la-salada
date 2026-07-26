using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salada.Game;
using Salada.Combat;
using Salada.Audio;

namespace Salada.UI
{
    /// <summary>
    /// Modal de evento: backdrop que bloquea + panel con el personaje que habla, titulo,
    /// descripcion y opciones. Al elegir una opcion muestra su dialogo final y "Continuar"
    /// (o la pantalla de Game Over si la opcion elegida termina la partida).
    /// </summary>
    public class EventPopup : MonoBehaviour
    {
        private GameObject _root;
        private Transform _content;
        private Font _font;

        private GameObject _portrait;
        private Image _portraitImage;

        private GameEvent _ev;
        private Action<int> _onOption;
        private Action _onClose;

        private WaveManager _waves;
        private BusinessMeters _meters;
        private GameOverController _gameOver;

        public void Init(WaveManager waves, BusinessMeters meters, GameOverController gameOver)
        {
            _waves = waves;
            _meters = meters;
            _gameOver = gameOver;
        }

        void Start()
        {
            _font = UIFont.Get();
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

            // Retrato del personaje: grande, a la derecha del panel, apenas superpuesto con el
            // borde (ignora la VerticalLayoutGroup para no ocupar lugar ni tapar el contenido).
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(panel.transform, false);
            portraitGo.AddComponent<LayoutElement>().ignoreLayout = true;
            var port = portraitGo.GetComponent<RectTransform>();
            port.anchorMin = new Vector2(1f, 0.5f); port.anchorMax = new Vector2(1f, 0.5f); port.pivot = new Vector2(0f, 0.5f);
            port.sizeDelta = new Vector2(400, 607);
            port.anchoredPosition = new Vector2(-20, -144);
            _portraitImage = portraitGo.GetComponent<Image>();
            _portraitImage.preserveAspect = true;
            _portrait = portraitGo;
            _portrait.SetActive(false);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        public void Show(GameEvent ev, Action<int> onOption, Action onClose)
        {
            _ev = ev; _onOption = onOption; _onClose = onClose;
            Sfx.Play(SfxId.NewWaveOrEvent);
            SetPortrait(_ev.speaker);
            BuildDescription();
            _root.SetActive(true);
        }

        void SetPortrait(EventCharacter ch)
        {
            var sprite = ch != null ? ch.sprite : null;
            _portrait.SetActive(sprite != null);
            _portraitImage.sprite = sprite;
        }

        void BuildDescription()
        {
            Clear();
            AddSpeaker(_ev.speaker);
            AddText(_ev.title, 24, new Color(1f, 0.85f, 0.5f), FontStyle.Bold, 34, TextAnchor.MiddleCenter);
            var desc = AddText(_ev.description, 17, new Color(0.88f, 0.9f, 0.94f), FontStyle.Normal, 60, TextAnchor.UpperLeft);
            desc.lineSpacing = 1.5f;
            if (_ev.options != null)
                for (int i = 0; i < _ev.options.Length; i++)
                {
                    int idx = i;
                    var o = _ev.options[i];
                    bool allowed = !WouldBreakLimits(o);
                    string label = o.label;

                    int totalMoney = TotalMoney(o);
                    if (o.money != 0 || o.moneyPerStall != 0) label += $"\n(${(totalMoney > 0 ? "+" : "")}{totalMoney})";
#if UNITY_EDITOR
                    label += $"\n<{Preview(o)}>";
#endif
                    AddButton(label, new Color(0.2f, 0.5f, 0.65f), () => ChooseOption(idx), allowed);
                }

            _portrait.transform.SetAsLastSibling(); // por encima del contenido en la zona de solape
        }

        int TotalMoney(EventOption o) =>
            o.money + (o.moneyPerStall != 0 && _waves != null ? o.moneyPerStall * _waves.PlayerStallCount() : 0);

        // Solo se griza si algo quedaria negativo. Superar el maximo no es problema:
        // el medidor simplemente clampea a 100 (BusinessMeters.Set), como siempre.
        bool WouldBreakLimits(EventOption o)
        {
            int totalMoney = TotalMoney(o);
            if (totalMoney != 0 && _waves != null && _waves.Money + totalMoney < 0) return true;
            if (_meters != null)
            {
                if (o.hostility != 0 && _meters.Get(MeterType.Hostility) + o.hostility < 0) return true;
                if (o.reputation != 0 && _meters.Get(MeterType.Reputation) + o.reputation < 0) return true;
                if (o.happiness != 0 && _meters.Get(MeterType.Happiness) + o.happiness < 0) return true;
                if (o.profit != 0 && _meters.Get(MeterType.Profit) + o.profit < 0) return true;
            }
            return false;
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
            var opt = _ev.options[i];
            var outcome = opt.outcome;
            AddText(string.IsNullOrEmpty(outcome) ? "..." : outcome, 18, new Color(0.9f, 0.92f, 0.96f), FontStyle.Italic, 60, TextAnchor.UpperLeft);

            if (opt.triggerGameOver || opt.triggerGameWin)
            {
                bool isWin = opt.triggerGameWin;
                AddButton("...", isWin ? new Color(0.2f, 0.5f, 0.25f) : new Color(0.5f, 0.2f, 0.2f), () =>
                {
                    _root.SetActive(false);
                    _gameOver?.Show(outcome, isWin);
                });
            }
            else
            {
                AddButton("Continuar", new Color(0.25f, 0.6f, 0.35f), () =>
                {
                    _root.SetActive(false);
                    _onClose?.Invoke();
                });
            }

            _portrait.transform.SetAsLastSibling();
        }

        // ---- helpers ----

        void Clear()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i).gameObject;
                if (c == _portrait) continue; // persistente: no se recrea en cada pantalla
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

        void AddButton(string label, Color color, UnityEngine.Events.UnityAction onClick, bool interactable = true)
        {
            var go = new GameObject("Opt", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = interactable ? color : Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f), 0.7f);
            go.AddComponent<LayoutElement>().minHeight = 50;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(Sfx.WithClick(onClick));
            btn.interactable = interactable;
            go.AddComponent<HoverSfx>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
            var t = textGo.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = interactable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f); t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter;
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
            if (o.moneyPerStall != 0) parts.Add($"${(o.moneyPerStall > 0 ? "+" : "")}{o.moneyPerStall} x puesto");
            if (o.salaryIncreasePercent != 0) parts.Add($"Sueldo x puesto {(o.salaryIncreasePercent > 0 ? "+" : "")}{o.salaryIncreasePercent:0.#%}");
            if (o.destroyBiggestStall) parts.Add("Destruye el puesto mas grande");
            if (o.triggerGameOver) parts.Add("GAME OVER");
            if (o.triggerGameWin) parts.Add("VICTORIA");
            string dur = o.specialPermanent ? "permanente" : (o.specialOneDayOnly ? "hoy" : $"x{o.specialWaves}");
            if (o.special != EffectType.None) parts.Add($"{GameEffects.Label(o.special, o.specialMagnitude)} ({dur})");
            if (o.special2 != EffectType.None) parts.Add($"{GameEffects.Label(o.special2, o.specialMagnitude2)} ({dur})");
            return parts.Count == 0 ? "sin efecto" : string.Join("   ", parts);
        }

        static string Signed(float v) => (v > 0 ? "+" : "") + v.ToString("0");
    }
}
