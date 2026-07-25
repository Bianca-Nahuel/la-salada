using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salada.Placement;
using Salada.Combat;
using Salada.Game;

namespace Salada.UI
{
    /// <summary>
    /// "Celular" del jugador. Tiene un encabezado fijo (plata, dia y reloj de la jornada) y un
    /// boton fijo abajo que combina empezar oleada / cambiar velocidad / avanzar de dia. En el
    /// medio, apps: Estadisticas (balanzas como cuadrados) y Construir/Demoler. Panel izquierdo
    /// retractil. Todo se arma en runtime.
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        [SerializeField] private PlacementController placement;
        [SerializeField] private WaveManager waves;
        [SerializeField] private StallData[] palette;
        private BusinessMeters _meters;
        private GameEffects _effects;

        [SerializeField] private float width = 320f;
        [SerializeField] private float slideSpeed = 8f;

        const float HeaderH = 82f;
        const float FooterH = 70f;
        static readonly float[] Speeds = { 1f, 2f, 3f, 0.5f };

        private RectTransform _rt;
        private Text _tabLabel;
        private bool _shown = true;
        private Font _font;

        // encabezado fijo
        private Text _moneyText;
        private Text _clockText;

        // boton de accion fijo (abajo)
        private Button _actionBtn;
        private Image _actionImg;
        private Text _actionLabel;
        private int _speedIdx;
        private bool _prevBuilding = true;

        // pantallas
        private GameObject _home, _statsScreen, _buildScreen;

        // stats
        private class Gauge { public RectTransform fill; public Text value; public Func<float> getter; public float maxH; }
        private readonly List<Gauge> _gauges = new List<Gauge>();
        private Text _econText;

        // build
        private readonly List<(Button btn, StallData data)> _buyButtons = new List<(Button, StallData)>();
        private Button _demolishBtn;

        void Start()
        {
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            if (waves == null) waves = FindAnyObjectByType<WaveManager>();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _effects = FindAnyObjectByType<GameEffects>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPhone();
            ShowScreen(_home);
        }

        void BuildPhone()
        {
            _rt = GetComponent<RectTransform>();
            _rt.anchorMin = new Vector2(0, 0);
            _rt.anchorMax = new Vector2(0, 1);
            _rt.pivot = new Vector2(0, 0.5f);
            _rt.sizeDelta = new Vector2(width, 0);
            _rt.anchoredPosition = Vector2.zero;
            gameObject.AddComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.99f);

            BuildHeader();
            BuildHome();
            BuildStats();
            BuildBuild();
            BuildFooter();
            BuildTab();
            if (Application.isEditor) BuildDebugButton();
        }

        // ---- encabezado fijo (plata / dia / reloj) ----

        void BuildHeader()
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, HeaderH); rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 1f);
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 10, 8);
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            _moneyText = MakeLabel(go.transform, "$0", 30, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, 38);
            _moneyText.alignment = TextAnchor.MiddleLeft;
            _clockText = MakeLabel(go.transform, "", 20, new Color(0.82f, 0.88f, 0.96f), FontStyle.Normal, 26);
            _clockText.alignment = TextAnchor.MiddleLeft;
        }

        // ---- pantallas ----

        GameObject MakeScreen(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            // insertada entre el encabezado (arriba) y el boton de accion (abajo)
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0, FooterH); rt.offsetMax = new Vector2(0, -HeaderH);
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            go.SetActive(false);
            return go;
        }

        void ShowScreen(GameObject s)
        {
            _home.SetActive(s == _home);
            _statsScreen.SetActive(s == _statsScreen);
            _buildScreen.SetActive(s == _buildScreen);
        }

        void BuildHome()
        {
            _home = MakeScreen("Home");
            MakeLabel(_home.transform, "MI NEGOCIO", 26, new Color(0.9f, 0.95f, 1f), FontStyle.Bold, 40);
            MakeButton(_home.transform, "Estadisticas", new Color(0.25f, 0.45f, 0.8f), () => ShowScreen(_statsScreen), 76);
            MakeButton(_home.transform, "Construir / Demoler", new Color(0.2f, 0.6f, 0.55f), () => ShowScreen(_buildScreen), 76);
        }

        void BuildStats()
        {
            _statsScreen = MakeScreen("Stats");
            MakeButton(_statsScreen.transform, "< Inicio", new Color(0.3f, 0.32f, 0.38f), () => ShowScreen(_home), 40);
            MakeLabel(_statsScreen.transform, "Estadisticas", 24, Color.white, FontStyle.Bold, 30);

            // grilla 2x2 de cuadrados-balanza
            var grid = new GameObject("Gauges", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(_statsScreen.transform, false);
            grid.AddComponent<LayoutElement>().minHeight = 300;
            var g = grid.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(138, 140); g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;

            MakeGauge(grid.transform, "Hostilidad", new Color(0.90f, 0.30f, 0.25f), () => _meters != null ? _meters.hostility : 0);
            MakeGauge(grid.transform, "Reputacion", new Color(0.30f, 0.55f, 0.90f), () => _meters != null ? _meters.reputation : 0);
            MakeGauge(grid.transform, "Felicidad", new Color(0.90f, 0.80f, 0.20f), () => _meters != null ? _meters.happiness : 0);
            MakeGauge(grid.transform, "Profit", new Color(0.30f, 0.80f, 0.40f), () => _meters != null ? _meters.profit : 0);

            _econText = MakeLabel(_statsScreen.transform, "", 17, new Color(0.85f, 0.88f, 0.92f), FontStyle.Normal, 140);
            _econText.alignment = TextAnchor.UpperLeft;
        }

        void BuildBuild()
        {
            _buildScreen = MakeScreen("Build");
            MakeButton(_buildScreen.transform, "< Inicio", new Color(0.3f, 0.32f, 0.38f), () => ShowScreen(_home), 40);
            MakeLabel(_buildScreen.transform, "Construir", 24, Color.white, FontStyle.Bold, 30);
            MakeLabel(_buildScreen.transform, "(solo entre oleadas, mirando a un camino)", 14, new Color(0.6f, 0.65f, 0.72f), FontStyle.Italic, 22);

            if (palette != null)
                foreach (var stall in palette)
                {
                    if (stall == null) continue;
                    var s = stall;
                    var (btn, _) = MakeButton(_buildScreen.transform, $"{s.displayName} ({s.footprintWidth}x{s.footprintHeight})  ${s.cost}",
                        new Color(0.12f, 0.7f, 0.85f), () => placement.SelectStall(s), 56);
                    _buyButtons.Add((btn, s));
                }

            (_demolishBtn, _) = MakeButton(_buildScreen.transform, "Demoler", new Color(0.75f, 0.35f, 0.25f), () => placement.EnterDemolishMode(), 56);
        }

        // ---- boton de accion fijo (empezar / velocidad / avanzar de dia) ----

        void BuildFooter()
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-16, FooterH - 12); rt.anchoredPosition = new Vector2(0, 8);
            _actionImg = go.GetComponent<Image>();
            _actionImg.color = new Color(0.25f, 0.7f, 0.35f);
            _actionBtn = go.GetComponent<Button>();
            _actionBtn.onClick.AddListener(OnAction);
            _actionLabel = MakeFilledText(go.transform, "Comenzar oleada", 20, Color.white, TextAnchor.MiddleCenter);
            _actionLabel.fontStyle = FontStyle.Bold;
        }

        void OnAction()
        {
            if (waves == null) return;
            if (waves.IsBuilding)
            {
                if (waves.DayComplete) waves.AdvanceDay();     // dia jugado -> avanzar (manual)
                else { _speedIdx = 0; Time.timeScale = 1f; waves.StartWave(); } // arranca a x1
            }
            else
            {
                _speedIdx = (_speedIdx + 1) % Speeds.Length;    // x1 -> x2 -> x3 -> x0.5 -> x1...
                Time.timeScale = Speeds[_speedIdx];
            }
        }

        // ---- boton de debug (solo en el editor): saltar al final del dia sin jugar oleadas ----

        void BuildDebugButton()
        {
            var go = new GameObject("DebugSkipDay", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(160, 32);
            rt.anchoredPosition = new Vector2(-10, FooterH + 6);
            go.GetComponent<Image>().color = new Color(0.55f, 0.25f, 0.6f);
            go.GetComponent<Button>().onClick.AddListener(() => waves?.DebugSkipToDayComplete());
            MakeFilledText(go.transform, "DEBUG: saltar dia", 13, Color.white, TextAnchor.MiddleCenter);
        }

        void BuildTab()
        {
            var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(32, 92); rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.99f);
            go.GetComponent<Button>().onClick.AddListener(() => _shown = !_shown);
            _tabLabel = MakeFilledText(go.transform, "<", 26, Color.white, TextAnchor.MiddleCenter);
        }

        // ---- helpers de UI ----

        Text MakeLabel(Transform parent, string text, int size, Color color, FontStyle style, float minHeight)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        (Button, Text) MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick, float minHeight)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var text = MakeFilledText(go.transform, label, 16, Color.white, TextAnchor.MiddleCenter);
            return (go.GetComponent<Button>(), text);
        }

        Text MakeFilledText(Transform parent, string label, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(4, 2); rt.offsetMax = new Vector2(-4, -2);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = color; t.fontSize = size; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void MakeGauge(Transform gridParent, string name, Color color, Func<float> getter)
        {
            var cell = new GameObject("Gauge_" + name, typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(gridParent, false);
            cell.GetComponent<Image>().color = new Color(0.17f, 0.18f, 0.21f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(cell.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.anchoredPosition = new Vector2(0, 4);
            frt.sizeDelta = new Vector2(-8, 0);
            fillGo.GetComponent<Image>().color = color;

            MakeFilledText(cell.transform, name, 15, Color.white, TextAnchor.UpperCenter);
            var val = MakeFilledText(cell.transform, "0", 24, Color.white, TextAnchor.LowerCenter);

            _gauges.Add(new Gauge { fill = frt, value = val, getter = getter, maxH = 132f });
        }

        // ---- loop ----

        void Update()
        {
            float targetX = _shown ? 0f : -width;
            var p = _rt.anchoredPosition;
            p.x = Mathf.Lerp(p.x, targetX, Time.unscaledDeltaTime * slideSpeed);
            _rt.anchoredPosition = p;
            if (_tabLabel != null) _tabLabel.text = _shown ? "<" : ">";

            if (waves == null) return;
            bool building = waves.IsBuilding;

            // al terminar una oleada, volver a velocidad normal
            if (building && !_prevBuilding) { _speedIdx = 0; Time.timeScale = 1f; }
            _prevBuilding = building;

            // encabezado fijo
            _moneyText.text = "$" + waves.Money;
            _clockText.text = $"Dia {waves.Day}   ·   {waves.ClockText}   ·   Oleada {Mathf.Min(waves.WavesToday + 1, waves.wavesPerDay)}/{waves.wavesPerDay}";

            // boton de accion
            if (building)
            {
                if (waves.DayComplete)
                {
                    _actionLabel.text = "Avanzar de dia  >";
                    _actionImg.color = new Color(0.55f, 0.45f, 0.85f);
                }
                else
                {
                    _actionLabel.text = "Comenzar oleada";
                    _actionImg.color = new Color(0.25f, 0.7f, 0.35f);
                }
            }
            else
            {
                _actionLabel.text = $"Velocidad  x{Speeds[_speedIdx]:0.##}   (tocar para cambiar)";
                _actionImg.color = new Color(0.24f, 0.42f, 0.68f);
            }

            // stats
            foreach (var g in _gauges)
            {
                float v = Mathf.Clamp(g.getter(), 0f, 100f);
                g.fill.sizeDelta = new Vector2(-8, g.maxH * v / 100f);
                g.value.text = Mathf.RoundToInt(v).ToString();
            }
            if (_econText != null)
                _econText.text =
                    $"Cuota proxima: ${waves.DailyFee()}\n" +
                    $"Ventas: {waves.SalesWon}  Perdidas: {waves.SalesLost}\n" +
                    $"Escaparon: {waves.Escaped}  Clientes: {Client.Active.Count}" +
                    (_effects == null || _effects.ActiveDescriptions().Count == 0 ? ""
                        : "\nEfectos: " + string.Join(", ", _effects.ActiveDescriptions()));

            // build
            _demolishBtn.interactable = building;
            foreach (var (btn, data) in _buyButtons)
                btn.interactable = building && waves.CanAfford(data.cost);
        }
    }
}
