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
    /// "Celular" del jugador estilo telefono con apps: pantalla de inicio con iconos y apps
    /// de Estadisticas (balanzas como cuadrados que se llenan), Construir/Demoler y Oleada
    /// (empezar + control de velocidad). Panel izquierdo retractil. Todo se arma en runtime.
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        [SerializeField] private PlacementController placement;
        [SerializeField] private WaveManager waves;
        [SerializeField] private StallData[] palette;
        private BusinessMeters _meters;
        private GameEffects _effects;

        [SerializeField] private float width = 270f;
        [SerializeField] private float slideSpeed = 8f;

        private RectTransform _rt;
        private Text _tabLabel;
        private bool _shown = true;
        private Font _font;

        // pantallas
        private GameObject _home, _statsScreen, _buildScreen, _waveScreen;

        // stats
        private class Gauge { public RectTransform fill; public Text value; public Func<float> getter; public float maxH; }
        private readonly List<Gauge> _gauges = new List<Gauge>();
        private Text _econText;

        // build
        private readonly List<(Button btn, StallData data)> _buyButtons = new List<(Button, StallData)>();
        private Button _demolishBtn;

        // wave
        private Button _startBtn;
        private Text _startLabel;
        private Text _speedLabel;

        void Start()
        {
            if (placement == null) placement = FindFirstObjectByType<PlacementController>();
            if (waves == null) waves = FindFirstObjectByType<WaveManager>();
            _meters = FindFirstObjectByType<BusinessMeters>();
            _effects = FindFirstObjectByType<GameEffects>();
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

            BuildHome();
            BuildStats();
            BuildBuild();
            BuildWave();
            BuildTab();
        }

        // ---- pantallas ----

        GameObject MakeScreen(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 18, 14);
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
            _waveScreen.SetActive(s == _waveScreen);
        }

        void BuildHome()
        {
            _home = MakeScreen("Home");
            MakeLabel(_home.transform, "MI NEGOCIO", 22, new Color(0.9f, 0.95f, 1f), FontStyle.Bold, 34);
            MakeButton(_home.transform, "Estadisticas", new Color(0.25f, 0.45f, 0.8f), () => ShowScreen(_statsScreen), 64);
            MakeButton(_home.transform, "Construir / Demoler", new Color(0.2f, 0.6f, 0.55f), () => ShowScreen(_buildScreen), 64);
            MakeButton(_home.transform, "Oleada", new Color(0.6f, 0.45f, 0.2f), () => ShowScreen(_waveScreen), 64);
        }

        void BuildStats()
        {
            _statsScreen = MakeScreen("Stats");
            MakeButton(_statsScreen.transform, "< Inicio", new Color(0.3f, 0.32f, 0.38f), () => ShowScreen(_home), 34);
            MakeLabel(_statsScreen.transform, "Estadisticas", 20, Color.white, FontStyle.Bold, 26);

            // grilla 2x2 de cuadrados-balanza
            var grid = new GameObject("Gauges", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(_statsScreen.transform, false);
            grid.AddComponent<LayoutElement>().minHeight = 240;
            var g = grid.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(110, 112); g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;

            MakeGauge(grid.transform, "Hostilidad", new Color(0.90f, 0.30f, 0.25f), () => _meters != null ? _meters.hostility : 0);
            MakeGauge(grid.transform, "Reputacion", new Color(0.30f, 0.55f, 0.90f), () => _meters != null ? _meters.reputation : 0);
            MakeGauge(grid.transform, "Felicidad", new Color(0.90f, 0.80f, 0.20f), () => _meters != null ? _meters.happiness : 0);
            MakeGauge(grid.transform, "Profit", new Color(0.30f, 0.80f, 0.40f), () => _meters != null ? _meters.profit : 0);

            _econText = MakeLabel(_statsScreen.transform, "", 15, new Color(0.85f, 0.88f, 0.92f), FontStyle.Normal, 110);
            _econText.alignment = TextAnchor.UpperLeft;
        }

        void BuildBuild()
        {
            _buildScreen = MakeScreen("Build");
            MakeButton(_buildScreen.transform, "< Inicio", new Color(0.3f, 0.32f, 0.38f), () => ShowScreen(_home), 34);
            MakeLabel(_buildScreen.transform, "Construir", 20, Color.white, FontStyle.Bold, 26);
            MakeLabel(_buildScreen.transform, "(solo entre oleadas)", 13, new Color(0.6f, 0.65f, 0.72f), FontStyle.Italic, 18);

            if (palette != null)
                foreach (var stall in palette)
                {
                    if (stall == null) continue;
                    var s = stall;
                    var (btn, _) = MakeButton(_buildScreen.transform, $"{s.displayName} ({s.footprintWidth}x{s.footprintHeight})  ${s.cost}",
                        new Color(0.12f, 0.7f, 0.85f), () => placement.SelectStall(s), 42);
                    _buyButtons.Add((btn, s));
                }

            (_demolishBtn, _) = MakeButton(_buildScreen.transform, "Demoler", new Color(0.75f, 0.35f, 0.25f), () => placement.EnterDemolishMode(), 42);
        }

        void BuildWave()
        {
            _waveScreen = MakeScreen("Wave");
            MakeButton(_waveScreen.transform, "< Inicio", new Color(0.3f, 0.32f, 0.38f), () => ShowScreen(_home), 34);
            MakeLabel(_waveScreen.transform, "Oleada", 20, Color.white, FontStyle.Bold, 26);

            var (sb, sl) = MakeButton(_waveScreen.transform, "Empezar oleada", new Color(0.25f, 0.7f, 0.35f), () => waves.StartWave(), 48);
            _startBtn = sb; _startLabel = sl;

            MakeLabel(_waveScreen.transform, "Velocidad", 15, new Color(0.6f, 0.65f, 0.72f), FontStyle.Bold, 20);
            var row = new GameObject("SpeedRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_waveScreen.transform, false);
            row.AddComponent<LayoutElement>().minHeight = 40;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childForceExpandWidth = true; h.childControlHeight = true; h.childForceExpandHeight = true;
            foreach (var sp in new[] { 0.5f, 1f, 2f, 3f })
            {
                float speed = sp;
                MakeButton(row.transform, "x" + sp.ToString("0.##"), new Color(0.3f, 0.4f, 0.55f), () => Time.timeScale = speed, 40);
            }
            _speedLabel = MakeLabel(_waveScreen.transform, "", 14, new Color(0.8f, 0.85f, 0.9f), FontStyle.Normal, 20);
        }

        void BuildTab()
        {
            var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(26, 74); rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.99f);
            go.GetComponent<Button>().onClick.AddListener(() => _shown = !_shown);
            _tabLabel = MakeFilledText(go.transform, "<", 22, Color.white, TextAnchor.MiddleCenter);
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

            MakeFilledText(cell.transform, name, 13, Color.white, TextAnchor.UpperCenter);
            var val = MakeFilledText(cell.transform, "0", 20, Color.white, TextAnchor.LowerCenter);

            _gauges.Add(new Gauge { fill = frt, value = val, getter = getter, maxH = 104f });
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

            // stats
            foreach (var g in _gauges)
            {
                float v = Mathf.Clamp(g.getter(), 0f, 100f);
                g.fill.sizeDelta = new Vector2(-8, g.maxH * v / 100f);
                g.value.text = Mathf.RoundToInt(v).ToString();
            }
            if (_econText != null)
                _econText.text =
                    $"Plata: ${waves.Money}\n" +
                    $"Dia: {waves.Day}   Oleada: {waves.Wave}\n" +
                    $"Cuota: ${waves.DailyFee()}\n" +
                    $"Ventas: {waves.SalesWon}  Perdidas: {waves.SalesLost}\n" +
                    $"Escaparon: {waves.Escaped}  Clientes: {Client.Active.Count}" +
                    (_effects == null || _effects.ActiveDescriptions().Count == 0 ? ""
                        : "\nEfectos: " + string.Join(", ", _effects.ActiveDescriptions()));

            // build
            _demolishBtn.interactable = building;
            foreach (var (btn, data) in _buyButtons)
                btn.interactable = building && waves.CanAfford(data.cost);

            // wave
            _startBtn.interactable = building;
            _startLabel.text = building ? "Empezar oleada" : $"Oleada {waves.Wave} en curso";
            if (_speedLabel != null)
                _speedLabel.text = $"Actual: x{Time.timeScale:0.##}" + (building ? "" : "   (en curso)");
        }
    }
}
