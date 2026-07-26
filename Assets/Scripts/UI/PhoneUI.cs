using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Salada.Placement;
using Salada.Combat;
using Salada.Game;

namespace Salada.UI
{
    /// <summary>
    /// "Celular" del jugador con arte (PhoneSkin): marco iFhon + una sola pantalla (celu 1) con
    /// 3 botones de construir, demoler, 4 medidores (relleno segun valor) y abajo un boton
    /// cuadrado de oleada que cambia de sprite (empezar / velocidad x1-x2-x5 / avanzar dia).
    /// Se esconde a la izquierda dejando ver un pedacito; al clickearlo se despliega.
    /// [ExecuteAlways]: tambien se ve en modo edicion (para acomodar los iconos sin darle Play).
    /// </summary>
    [ExecuteAlways]
    public class PhoneUI : MonoBehaviour
    {
        [SerializeField] private PlacementController placement;
        [SerializeField] private WaveManager waves;
        [SerializeField] private StallData[] palette;
        [SerializeField] private PhoneSkin skin;
        private BusinessMeters _meters;
        private GameEffects _effects;

        [SerializeField] private float width = 300f;
        [SerializeField] private float slideSpeed = 8f;
        [SerializeField] private float peek = 46f; // cuanto se ve del celu cuando esta escondido
        const float FrameAspect = 2047f / 1069f; // alto/ancho del marco

        // pantalla dentro del marco (fracciones del marco)
        static readonly Vector2 ScreenMin = new Vector2(0.085f, 0.105f);
        static readonly Vector2 ScreenMax = new Vector2(0.915f, 0.845f);

        private RectTransform _rt;
        private bool _shown = true;
        private Font _font;

        private Text _moneyText;
        private Text _clockText;

        // boton de oleada
        private Image _waveImg;
        private int _speedIdx;
        private bool _prevBuilding = true;
        static readonly float[] Speeds = { 1f, 2f, 5f }; // sin x0.5

        // medidores
        private class Meter { public Image fill; public Func<float> getter; }
        private readonly List<Meter> _meterList = new List<Meter>();

        // construir
        private readonly List<(Image img, StallData data)> _buyButtons = new List<(Image, StallData)>();
        private Image _demolishImg;

        // tooltip (cartelito al pasar el mouse por un icono)
        private RectTransform _tooltipRT;
        private Text _tooltipText;

        // detecta el mouse encima de un icono y le pide al celu que muestre su cartelito
        private class Hoverable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public PhoneUI owner;
            public Func<string> text;
            public void OnPointerEnter(PointerEventData e)
            {
                if (owner != null && text != null) owner.ShowTooltip(text(), (RectTransform)transform);
            }
            public void OnPointerExit(PointerEventData e)
            {
                if (owner != null) owner.HideTooltip();
            }
        }

        void OnEnable()
        {
            // limpiar hijos viejos y reconstruir (asi los cambios de codigo se ven en edicion)
            _meterList.Clear(); _buyButtons.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var go = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            if (waves == null) waves = FindAnyObjectByType<WaveManager>();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _effects = FindAnyObjectByType<GameEffects>();
            _font = skin != null && skin.font != null ? skin.font : UIFont.Get();
            BuildPhone();
        }

        void BuildPhone()
        {
            _rt = GetComponent<RectTransform>();
            _rt.anchorMin = _rt.anchorMax = new Vector2(0, 0); // esquina inferior-izquierda
            _rt.pivot = new Vector2(0, 0);
            _rt.sizeDelta = new Vector2(width, width * FrameAspect);
            _rt.anchoredPosition = Vector2.zero;

            // marco: imagen del celu + boton que despliega/esconde al clickear el marco (sin flecha)
            var frameImg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            if (skin != null && skin.frame != null) { frameImg.sprite = skin.frame; frameImg.color = Color.white; }
            else frameImg.color = new Color(0.09f, 0.10f, 0.13f, 0.99f);
            frameImg.raycastTarget = true;
            var toggle = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            toggle.transition = Selectable.Transition.None;
            toggle.onClick.RemoveAllListeners();
            toggle.onClick.AddListener(() => _shown = !_shown);

            // pantalla (area util dentro del marco)
            var screen = new GameObject("Screen", typeof(RectTransform)).GetComponent<RectTransform>();
            screen.SetParent(transform, false);
            screen.anchorMin = ScreenMin; screen.anchorMax = ScreenMax;
            screen.offsetMin = screen.offsetMax = Vector2.zero;

            BuildHeader(screen);
            BuildGrid(screen);
            BuildWaveButton(screen);
            if (Application.isEditor) BuildDebugButton();
            BuildTooltip(); // ultimo: se dibuja encima de todo

            // en edicion: no guardar los objetos generados en la escena (solo preview)
            if (!Application.isPlaying) MarkPreview(transform);
        }

        static void MarkPreview(Transform root)
        {
            foreach (Transform c in root)
            {
                c.gameObject.hideFlags = HideFlags.DontSaveInEditor;
                MarkPreview(c);
            }
        }

        // ---- header: plata + dia/reloj/gastos ----

        void BuildHeader(RectTransform screen)
        {
            _moneyText = MakeText(screen, "$0", 26, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(_moneyText.rectTransform, 0.02f, 0.885f, 0.98f, 1.0f);
            _clockText = MakeText(screen, "", 12, new Color(0.85f, 0.9f, 0.96f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(_clockText.rectTransform, 0.02f, 0.82f, 0.98f, 0.885f);
            _clockText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---- grilla celu 1: 3 columnas x 3 filas ----
        // fila0: construir 1/2/3 ; fila1: demoler / (logo) / profit ; fila2: hostil / opiniones / clima

        void BuildGrid(RectTransform screen)
        {
            // posiciones (fracciones dentro de la pantalla). Ajustadas a mano por el usuario.
            AddBuild(screen, 0, skin != null ? skin.build1 : null, 0.020f, 0.570f, 0.310f, 0.720f);
            AddBuild(screen, 1, skin != null ? skin.build2 : null, 0.355f, 0.570f, 0.645f, 0.720f);
            AddBuild(screen, 2, skin != null ? skin.build3 : null, 0.710f, 0.570f, 1.000f, 0.720f);

            _demolishImg = SpriteButton(CellAt(screen, 0.020f, 0.360f, 0.310f, 0.510f), skin != null ? skin.demolish : null, () => placement.EnterDemolishMode());
            AddTooltip(_demolishImg.gameObject, () => "Demoler puesto");

            AddMeter(CellAt(screen, 0.690f, 0.360f, 0.980f, 0.510f), skin?.profitVacio, skin?.profitColor, "Ganancias", () => _meters != null ? _meters.profit : 0f);
            AddMeter(CellAt(screen, 0.023f, 0.194f, 0.313f, 0.344f), skin?.hostilVacio, skin?.hostilColor, "Hostilidad", () => _meters != null ? _meters.hostility : 0f);
            AddMeter(CellAt(screen, 0.358f, 0.194f, 0.648f, 0.344f), skin?.reputacionVacio, skin?.reputacionColor, "Reputacion", () => _meters != null ? _meters.reputation : 0f);
            AddMeter(CellAt(screen, 0.693f, 0.194f, 0.983f, 0.344f), skin?.felicidadVacio, skin?.felicidadColor, "Clima laboral", () => _meters != null ? _meters.happiness : 0f);
        }

        // capturamos 'data' en una variable local (no la del for) para que cada boton apunte a su puesto
        void AddBuild(RectTransform screen, int idx, Sprite sprite, float x0, float y0, float x1, float y1)
        {
            StallData data = (palette != null && idx < palette.Length) ? palette[idx] : null;
            var img = SpriteButton(CellAt(screen, x0, y0, x1, y1), sprite, () => { if (data != null) placement.SelectStall(data); });
            if (data != null) _buyButtons.Add((img, data));
            AddTooltip(img.gameObject, () => data != null ? $"{data.displayName}  -  ${data.cost}" : "Puesto");
        }

        RectTransform CellAt(RectTransform screen, float x0, float y0, float x1, float y1)
        {
            var rt = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(screen, false);
            Place(rt, x0, y0, x1, y1);
            return rt;
        }

        Image SpriteButton(RectTransform cell, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(cell, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = Color.white; img.preserveAspect = true;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            return img;
        }

        // medidor: fondo tenue (delimita el cuadrado) + relleno (color) que sube segun el valor + icono (vacio) por encima
        void AddMeter(RectTransform cell, Sprite vacio, Sprite color, string label, Func<float> getter)
        {
            // fondo: el mismo color pero transparente, para ver el cuadrado completo (lo que falta llenar)
            var bgGo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(cell, false);
            Stretch(bgGo.GetComponent<RectTransform>());
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = color; bg.color = new Color(1f, 1f, 1f, 0.28f); bg.preserveAspect = true; bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(cell, false);
            Stretch(fillGo.GetComponent<RectTransform>());
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = color; fill.color = Color.white; fill.preserveAspect = true;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom; fill.fillAmount = 0f;

            var iconGo = new GameObject("Icono", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cell, false);
            Stretch(iconGo.GetComponent<RectTransform>());
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = vacio; icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false;

            AddTooltip(fillGo, () => $"{label}: {Mathf.RoundToInt(getter())}");
            _meterList.Add(new Meter { fill = fill, getter = getter });
        }

        // ---- boton de oleada (cuadrado, cambia de sprite) ----

        void BuildWaveButton(RectTransform screen)
        {
            var go = new GameObject("Wave", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(screen, false);
            Place(go.GetComponent<RectTransform>(), 0.370f, 0.008f, 0.630f, 0.173f);
            _waveImg = go.GetComponent<Image>();
            _waveImg.color = Color.white; _waveImg.preserveAspect = true;
            if (skin != null) _waveImg.sprite = skin.wavePlay;
            go.GetComponent<Button>().onClick.AddListener(OnAction);
            AddTooltip(go, WaveTooltip);
        }

        string WaveTooltip()
        {
            if (waves == null) return "Oleada";
            if (waves.IsBuilding)
                return waves.DayComplete ? "Avanzar al dia siguiente" : "Empezar oleada";
            return $"Velocidad x{Speeds[_speedIdx]:0} (click para cambiar)";
        }

        void OnAction()
        {
            if (waves == null) return;
            if (waves.IsBuilding)
            {
                if (waves.DayComplete) waves.AdvanceDay();
                else { _speedIdx = 0; Time.timeScale = 1f; waves.StartWave(); }
            }
            else
            {
                _speedIdx = (_speedIdx + 1) % Speeds.Length; // x1 -> x2 -> x5 -> x1...
                Time.timeScale = Speeds[_speedIdx];
            }
        }

        void BuildDebugButton()
        {
            var go = new GameObject("DebugSkipDay", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(150, 28); rt.anchoredPosition = new Vector2(-6, -34);
            go.GetComponent<Image>().color = new Color(0.55f, 0.25f, 0.6f);
            go.GetComponent<Button>().onClick.AddListener(() => waves?.DebugSkipToDayComplete());
            var t = MakeText(rt, "DEBUG: saltar dia", 12, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        // ---- tooltip ----

        void BuildTooltip()
        {
            var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(transform, false); // hijo del celu, se limpia/reconstruye con el resto
            _tooltipRT = go.GetComponent<RectTransform>();
            _tooltipRT.pivot = new Vector2(0.5f, 0f); // crece hacia arriba desde el icono

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);
            bg.raycastTarget = false;

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 6, 6);
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltipText = MakeText(go.transform, "", 13, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Overflow;

            go.SetActive(false);
        }

        void AddTooltip(GameObject target, Func<string> text)
        {
            var h = target.AddComponent<Hoverable>();
            h.owner = this; h.text = text;
        }

        public void ShowTooltip(string text, RectTransform target)
        {
            if (_tooltipRT == null || _tooltipText == null || target == null) return;
            _tooltipText.text = text;
            _tooltipRT.gameObject.SetActive(true);
            _tooltipRT.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRT);
            // posicionar justo arriba del icono (coords de mundo = pantalla en Canvas Overlay)
            var c = new Vector3[4];
            target.GetWorldCorners(c);
            _tooltipRT.position = (c[1] + c[2]) * 0.5f + new Vector3(0f, 6f, 0f);
        }

        public void HideTooltip()
        {
            if (_tooltipRT != null) _tooltipRT.gameObject.SetActive(false);
        }

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        Text MakeText(Transform parent, string text, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---- loop ----

        void Update()
        {
            if (!Application.isPlaying) return; // en edicion se ve estatico (para acomodar iconos)

            // escondido: baja dejando ver un pedacito (peek) arriba; desplegado: pegado abajo
            float targetY = _shown ? 0f : -(width * FrameAspect - peek);
            var p = _rt.anchoredPosition;
            p.y = Mathf.Lerp(p.y, targetY, Time.unscaledDeltaTime * slideSpeed);
            _rt.anchoredPosition = p;

            if (waves == null) return;
            bool building = waves.IsBuilding;

            if (building && !_prevBuilding) { _speedIdx = 0; Time.timeScale = 1f; _shown = true; }
            _prevBuilding = building;

            _moneyText.text = "$" + waves.Money;
            _clockText.text = $"Dia {waves.Day} · {waves.ClockText} · Gastos ${waves.DailyFee()}";

            // sprite del boton de oleada segun estado
            if (_waveImg != null && skin != null)
            {
                if (building) _waveImg.sprite = waves.DayComplete ? skin.waveSkipDay : skin.wavePlay;
                else _waveImg.sprite = _speedIdx == 0 ? skin.waveV1 : _speedIdx == 1 ? skin.waveV2 : skin.waveV3;
            }

            // medidores (relleno de abajo hacia arriba)
            foreach (var m in _meterList)
                if (m.fill != null) m.fill.fillAmount = Mathf.Clamp01(m.getter() / 100f);

            // construir: habilitado solo en Building y si alcanza la plata (si no, gris)
            if (_demolishImg != null) Dim(_demolishImg, building);
            foreach (var (img, data) in _buyButtons)
                Dim(img, building && waves.CanAfford(data.cost));
        }

        static void Dim(Image img, bool enabled)
        {
            var b = img.GetComponent<Button>(); if (b != null) b.interactable = enabled;
            img.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.7f);
        }
    }
}
