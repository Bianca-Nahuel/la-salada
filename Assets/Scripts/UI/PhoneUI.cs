using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        // disposicion guardada de los botones (persiste). Vacia = usa las posiciones del codigo.
        // El celu se re-arma por codigo cada vez (para que los clicks funcionen tras Play), pero
        // aplica estas posiciones si existen. Se llena con "Guardar disposicion actual".
        [System.Serializable]
        private struct NamedRect { public string name; public Vector2 min; public Vector2 max; }
        [SerializeField, HideInInspector] private List<NamedRect> savedLayout = new List<NamedRect>();
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
        private Text _dayTimeText;  // dia + hora (derecha del header, misma fila que la plata)
        private Text _clockText;    // gastos + % del mapa que dominas (linea de abajo, mas chica)
        private TerritoryManager _territory;   // para el % de dominancia
        private TerritoryController _zoneView; // modo "ver zonas"
        private GameOptionsPopup _options;     // menu de opciones (salir/reanudar)
        private Canvas _canvas;                // para saber la zona del celu en pantalla (auto-retraer)
        private bool _prevInterfere;           // estaba en modo construir/demoler/zona el frame anterior
        private bool _leftPhoneSinceMode;      // el mouse ya salio del celu desde que entraste al modo
        private RectTransform _moneyPill;      // plata siempre visible (cuando el celu esta escondido)
        private Text _moneyPillText;

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

        // feedback visual de un boton: normal apagadito, hover ilumina, click se hunde/oscurece
        private class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
            IPointerDownHandler, IPointerUpHandler
        {
            public Image img;
            private Color _base = Color.white; // color base (blanco = activo, gris = deshabilitado)
            private bool _hover, _press;

            public void SetBase(Color c) { _base = c; Apply(); }
            void Start() { Apply(); }

            void Apply()
            {
                if (img == null) return;
                float mul = _press ? 0.72f : _hover ? 1f : 0.9f; // hover a full = "se ilumina" respecto al 0.9 normal
                img.color = new Color(_base.r * mul, _base.g * mul, _base.b * mul, _base.a);
                float s = _press ? 0.94f : _hover ? 1.06f : 1f;
                img.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            public void OnPointerEnter(PointerEventData e) { _hover = true; Apply(); }
            public void OnPointerExit(PointerEventData e) { _hover = false; _press = false; Apply(); }
            public void OnPointerDown(PointerEventData e) { _press = true; Apply(); }
            public void OnPointerUp(PointerEventData e) { _press = false; Apply(); }
        }

        void OnEnable() => Rebuild();

        // re-arma todo el celu por codigo (los clicks/tooltips se recablean; aplica savedLayout si hay)
        void Rebuild()
        {
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
            _territory = FindAnyObjectByType<TerritoryManager>();
            _zoneView = FindAnyObjectByType<TerritoryController>();
            _options = FindAnyObjectByType<GameOptionsPopup>(FindObjectsInactive.Include);
            _canvas = GetComponentInParent<Canvas>();
            _font = skin != null && skin.font != null ? skin.font : UIFont.Get();
            BuildPhone();
        }

        // nombres de los elementos cuya posicion se puede guardar/mover
        static readonly string[] LayoutNames =
            { "Build_0", "Build_1", "Build_2", "Demoler", "Meter_Ganancias", "Meter_Hostilidad",
              "Meter_Reputacion", "Meter_ClimaLaboral", "Wave", "Zonas", "Opciones" };

        bool TryGetSaved(string name, out Vector2 min, out Vector2 max)
        {
            if (savedLayout != null)
                foreach (var e in savedLayout)
                    if (e.name == name) { min = e.min; max = e.max; return true; }
            min = max = Vector2.zero; return false;
        }

        [ContextMenu("Guardar disposicion actual")]
        void CaptureLayout()
        {
            Canvas.ForceUpdateCanvases();
            savedLayout.Clear();
            foreach (var name in LayoutNames)
            {
                var rt = FindDeep(transform, name);
                if (rt == null) continue;
                EffectiveAnchors(rt, out var min, out var max);
                savedLayout.Add(new NamedRect { name = name, min = min, max = max });
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            Rebuild(); // aplicar ya
        }

        [ContextMenu("Reconstruir (borrar disposicion guardada)")]
        void ClearLayout()
        {
            savedLayout.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            Rebuild();
        }

        static RectTransform FindDeep(Transform root, string name)
        {
            foreach (Transform c in root)
            {
                if (c.name == name) return c as RectTransform;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // fraccion efectiva del rect (contempla que se haya movido por offsets al arrastrar)
        static void EffectiveAnchors(RectTransform rt, out Vector2 min, out Vector2 max)
        {
            var parent = rt.parent as RectTransform;
            Vector2 ps = parent != null ? parent.rect.size : Vector2.one;
            float ix = ps.x != 0f ? 1f / ps.x : 0f, iy = ps.y != 0f ? 1f / ps.y : 0f;
            min = rt.anchorMin + new Vector2(rt.offsetMin.x * ix, rt.offsetMin.y * iy);
            max = rt.anchorMax + new Vector2(rt.offsetMax.x * ix, rt.offsetMax.y * iy);
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
            BuildZoneButton(screen);
            BuildOptionsButton(screen);
            BuildMoneyPill(); // plata siempre visible aunque se oculte el celu
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

        // ---- header: (fila 1) plata a la izquierda + dia/hora a la derecha ; (fila 2) gastos + % tuyo ----

        void BuildHeader(RectTransform screen)
        {
            _moneyText = MakeText(screen, "$0", 26, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(_moneyText.rectTransform, 0.02f, 0.885f, 0.52f, 1.0f);
            _moneyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            _dayTimeText = MakeText(screen, "", 15, new Color(0.88f, 0.92f, 0.98f), FontStyle.Bold, TextAnchor.MiddleRight);
            Place(_dayTimeText.rectTransform, 0.50f, 0.885f, 0.98f, 1.0f);
            _dayTimeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            _clockText = MakeText(screen, "", 12, new Color(0.82f, 0.86f, 0.93f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(_clockText.rectTransform, 0.02f, 0.815f, 0.98f, 0.882f);
            _clockText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---- grilla celu 1: 3 columnas x 3 filas ----
        // fila0: construir 1/2/3 ; fila1: demoler / (logo) / profit ; fila2: hostil / opiniones / clima

        // grilla uniforme: 3 columnas (mismo ancho) x 4 filas (misma altura y mismo gap).
        // build2 y oleada quedan en la columna del centro (X = 0.50); demoler en la fila del logo.
        // columnas
        const float ColL0 = 0.02f, ColL1 = 0.30f;   // izquierda
        const float ColC0 = 0.36f, ColC1 = 0.64f;   // centro (centro exacto = 0.50)
        const float ColR0 = 0.70f, ColR1 = 0.98f;   // derecha
        // filas (alto 0.15, gap 0.06)
        const float Row1_0 = 0.65f, Row1_1 = 0.80f; // construir 1/2/3
        const float Row2_0 = 0.44f, Row2_1 = 0.59f; // demoler / (logo) / profit
        const float Row3_0 = 0.23f, Row3_1 = 0.38f; // medidores hostil / reput / clima
        const float Row4_0 = 0.02f, Row4_1 = 0.17f; // zonas / oleada

        void BuildGrid(RectTransform screen)
        {
            AddBuild(screen, 0, skin != null ? skin.build1 : null, ColL0, Row1_0, ColL1, Row1_1);
            AddBuild(screen, 1, skin != null ? skin.build2 : null, ColC0, Row1_0, ColC1, Row1_1);
            AddBuild(screen, 2, skin != null ? skin.build3 : null, ColR0, Row1_0, ColR1, Row1_1);

            _demolishImg = SpriteButton(screen, "Demoler", skin != null ? skin.demolish : null, ColL0, Row2_0, ColL1, Row2_1, () => placement.EnterDemolishMode());
            AddTooltip(_demolishImg.gameObject, () => "Demoler puesto");
            // (columna del centro de la fila 2 = el logo del celu, va en el arte del marco)
            AddMeter(screen, "Meter_Ganancias", ColR0, Row2_0, ColR1, Row2_1, skin?.profitVacio, skin?.profitColor, "Ganancias", () => _meters != null ? _meters.profit : 0f);

            AddMeter(screen, "Meter_Hostilidad", ColL0, Row3_0, ColL1, Row3_1, skin?.hostilVacio, skin?.hostilColor, "Hostilidad", () => _meters != null ? _meters.hostility : 0f);
            AddMeter(screen, "Meter_Reputacion", ColC0, Row3_0, ColC1, Row3_1, skin?.reputacionVacio, skin?.reputacionColor, "Reputacion", () => _meters != null ? _meters.reputation : 0f);
            AddMeter(screen, "Meter_ClimaLaboral", ColR0, Row3_0, ColR1, Row3_1, skin?.felicidadVacio, skin?.felicidadColor, "Clima laboral", () => _meters != null ? _meters.happiness : 0f);
        }

        // capturamos 'data' en una variable local (no la del for) para que cada boton apunte a su puesto
        void AddBuild(RectTransform screen, int idx, Sprite sprite, float x0, float y0, float x1, float y1)
        {
            StallData data = (palette != null && idx < palette.Length) ? palette[idx] : null;
            var img = SpriteButton(screen, "Build_" + idx, sprite, x0, y0, x1, y1, () => { if (data != null) placement.SelectStall(data); });
            if (data != null) _buyButtons.Add((img, data));
            AddTooltip(img.gameObject, () => data != null ? $"{data.displayName}  -  ${data.cost}" : "Puesto");
        }

        // como Place, pero si hay una disposicion guardada para 'name' usa esa
        void PlaceSaved(RectTransform rt, string name, float x0, float y0, float x1, float y1)
        {
            if (TryGetSaved(name, out var min, out var max)) { rt.anchorMin = min; rt.anchorMax = max; }
            else { rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); }
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // el boton ES el objeto nombrado y posicionado (para que al arrastrarlo se guarde su posicion)
        Image SpriteButton(RectTransform parent, string name, Sprite sprite, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            PlaceSaved(go.GetComponent<RectTransform>(), name, x0, y0, x1, y1);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = Color.white; img.preserveAspect = true;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            go.AddComponent<ButtonFeedback>().img = img; // hover ilumina + click se hunde
            return img;
        }

        // medidor: contenedor nombrado/posicionado (es lo que se arrastra, con raycast propio invisible)
        // + fondo tenue + relleno (color) que sube segun el valor + icono (vacio) por encima
        void AddMeter(RectTransform parent, string name, float x0, float y0, float x1, float y1, Sprite vacio, Sprite color, string label, Func<float> getter)
        {
            var cont = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            cont.SetParent(parent, false);
            PlaceSaved(cont, name, x0, y0, x1, y1);
            var contImg = cont.GetComponent<Image>();
            contImg.color = new Color(0f, 0f, 0f, 0f); contImg.raycastTarget = true; // invisible pero agarra el mouse (hover/arrastre)

            var bgGo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(cont, false);
            Stretch(bgGo.GetComponent<RectTransform>());
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = color; bg.color = new Color(1f, 1f, 1f, 0.28f); bg.preserveAspect = true; bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(cont, false);
            Stretch(fillGo.GetComponent<RectTransform>());
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = color; fill.color = Color.white; fill.preserveAspect = true; fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom; fill.fillAmount = 0f;

            var iconGo = new GameObject("Icono", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cont, false);
            Stretch(iconGo.GetComponent<RectTransform>());
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = vacio; icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false;

            AddTooltip(cont.gameObject, () => $"{label}: {Mathf.RoundToInt(getter())}");
            _meterList.Add(new Meter { fill = fill, getter = getter });
        }

        // ---- boton de oleada (cuadrado, cambia de sprite) ----

        void BuildWaveButton(RectTransform screen)
        {
            var go = new GameObject("Wave", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(screen, false);
            PlaceSaved(go.GetComponent<RectTransform>(), "Wave", ColC0, Row4_0, ColC1, Row4_1); // centro, fila de abajo
            _waveImg = go.GetComponent<Image>();
            _waveImg.color = Color.white; _waveImg.preserveAspect = true;
            if (skin != null) _waveImg.sprite = skin.wavePlay;
            go.GetComponent<Button>().onClick.AddListener(OnAction);
            go.AddComponent<ButtonFeedback>().img = _waveImg; // hover ilumina + click se hunde
            AddTooltip(go, WaveTooltip);
        }

        string WaveTooltip()
        {
            if (waves == null) return "Oleada";
            if (waves.IsBuilding)
                return waves.DayComplete ? "Avanzar al dia siguiente" : "Empezar oleada";
            return $"Velocidad x{Speeds[_speedIdx]:0} (click para cambiar)";
        }

        // pill de plata: hijo del marco pero se mantiene fijo en la esquina de pantalla (contrarresta
        // el deslizamiento) y solo se muestra cuando el celu esta escondido, asi la plata nunca se va.
        void BuildMoneyPill()
        {
            var go = new GameObject("MoneyPill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _moneyPill = go.GetComponent<RectTransform>();
            _moneyPill.anchorMin = _moneyPill.anchorMax = new Vector2(0f, 0f);
            _moneyPill.pivot = new Vector2(0f, 0f);
            _moneyPill.sizeDelta = new Vector2(150f, 46f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.09f, 0.9f);
            bg.raycastTarget = false;
            _moneyPillText = MakeText(go.transform, "$0", 24, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(_moneyPillText.rectTransform);
            go.SetActive(false);
        }

        // boton de ver-zonas (izquierda, fila de abajo) con su sprite
        void BuildZoneButton(RectTransform screen)
        {
            var img = SpriteButton(screen, "Zonas", skin != null ? skin.zones : null, ColL0, Row4_0, ColL1, Row4_1,
                () => { if (_zoneView != null) _zoneView.ToggleZoneView(); });
            AddTooltip(img.gameObject, () => _zoneView != null && _zoneView.Active ? "Cerrar mapa de zonas" : "Ver zonas (facciones)");
        }

        // boton de opciones del juego (derecha, fila de abajo) - sin sprite propio por ahora
        void BuildOptionsButton(RectTransform screen)
        {
            var img = SpriteButton(screen, "Opciones", null, ColR0, Row4_0, ColR1, Row4_1,
                () => { if (_options != null) _options.Show(); });
            var fb = img.GetComponent<ButtonFeedback>();
            if (fb != null) fb.SetBase(new Color(0.80f, 0.82f, 0.88f)); // caja neutra (todavia sin icono)
            AddTooltip(img.gameObject, () => "Opciones");
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
            rt.sizeDelta = new Vector2(150, 28); rt.anchoredPosition = new Vector2(-6, 34);
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

            // en modo ver-zonas / construir / demoler, si el mouse se acerca al celu se retrae
            // solo para no estorbar (sin cambiar el estado manual _shown); al salir, vuelve.
            bool interfereMode = (_zoneView != null && _zoneView.Active)
                || (placement != null && placement.CurrentMode != PlacementController.Mode.Idle);
            // al ENTRAR al modo no se retrae hasta que el mouse salga del celu por primera vez
            // (asi no se esconde de una al tocar un boton, que deja el mouse encima del celu).
            if (interfereMode && !_prevInterfere) _leftPhoneSinceMode = false;
            if (!interfereMode) _leftPhoneSinceMode = false;
            if (!MouseOverPhoneArea()) _leftPhoneSinceMode = true;
            _prevInterfere = interfereMode;
            bool autoHide = interfereMode && _leftPhoneSinceMode && MouseOverPhoneArea();
            bool shown = autoHide ? false : _shown;

            // escondido: baja dejando ver un pedacito (peek) arriba; desplegado: pegado abajo
            float targetY = shown ? 0f : -(width * FrameAspect - peek);
            var p = _rt.anchoredPosition;
            p.y = Mathf.Lerp(p.y, targetY, Time.unscaledDeltaTime * slideSpeed);
            _rt.anchoredPosition = p;

            if (waves == null) return;
            bool building = waves.IsBuilding;

            if (building && !_prevBuilding) { _speedIdx = 0; Time.timeScale = 1f; _shown = true; }
            _prevBuilding = building;

            _moneyText.text = "$" + waves.Money;
            _dayTimeText.text = $"Dia {waves.Day} · {waves.ClockText}";
            if (_territory != null)
            {
                int vos = _territory.DominancePercent(Owner.Player);
                int pic = _territory.DominancePercent(Owner.Enemy);
                int cro = _territory.DominancePercent(Owner.Neutral);
                _clockText.text = $"Gastos ${waves.DailyFee()}  ·  Vos {vos}%  Pic {pic}%  Cro {cro}%";
            }
            else _clockText.text = $"Gastos ${waves.DailyFee()}";

            // pill de plata: visible solo con el celu escondido, clavado en la esquina de pantalla
            if (_moneyPill != null)
            {
                _moneyPill.gameObject.SetActive(!shown);
                if (!shown)
                {
                    _moneyPill.anchoredPosition = new Vector2(14f, 14f - _rt.anchoredPosition.y);
                    _moneyPillText.text = "$" + waves.Money;
                }
            }

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

        // el mouse esta sobre (o pegado a) la esquina donde vive el celu, usando su huella "desplegada"
        // asi no oscila cuando se retrae. Coords de mouse = pantalla; el celu ancla en (0,0) abajo-izq.
        bool MouseOverPhoneArea()
        {
            if (Mouse.current == null) return false;
            var mp = Mouse.current.position.ReadValue();
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            float w = width * scale + 40f;                 // + margen
            float h = width * FrameAspect * scale + 40f;
            return mp.x <= w && mp.y <= h;
        }

        static void Dim(Image img, bool enabled)
        {
            var b = img.GetComponent<Button>(); if (b != null) b.interactable = enabled;
            var c = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.7f);
            var fb = img.GetComponent<ButtonFeedback>();
            if (fb != null) fb.SetBase(c); else img.color = c;
        }
    }
}
