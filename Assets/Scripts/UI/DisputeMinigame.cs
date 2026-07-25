using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Salada.Placement;

namespace Salada.UI
{
    /// <summary>
    /// Minijuego de disputa: tira-y-afloja. Un marcador va de 0 (gana el rival) a 1 (gana el
    /// jugador), arranca en 0.5. El jugador empuja con clicks (o Espacio); el rival tira solo,
    /// con fuerza segun la diferencia de poder (mas duro si el rival tiene mas poder). "Retirarse"
    /// = el jugador se rinde. Congela el juego (Time.timeScale=0) y usa tiempo no escalado.
    /// </summary>
    public class DisputeMinigame : MonoBehaviour
    {
        [Header("Balance del empuje (config)")]
        [Tooltip("Clicks/seg promedio para EMPATAR con poder igualado. Mas que esto = ganas; menos = perdes.")]
        [SerializeField] private float clicksToWinEqual = 4f;
        [Tooltip("Cuanto sube/baja ese umbral por cada punto de diferencia de poder (rival - vos). Ej 0.5 = +0.5 clicks/seg por punto en contra.")]
        [SerializeField] private float clicksPerPowerDiff = 0.5f;
        [Tooltip("Cuanto avanza el dial por click (mas alto = pelea mas corta). No cambia la dificultad relativa.")]
        [SerializeField] private float pushPerClick = 0.06f;
        [Tooltip("Piso del umbral de clicks/seg (con mucha ventaja igual pedis al menos esto).")]
        [SerializeField] private float minRequiredCPS = 0.5f;
        [SerializeField] private float barPixels = 440f;

        private GameObject _root;
        private GameObject _intro;
        private Text _introText;
        private RectTransform _marker;
        private Text _title;
        private Font _font;

        private bool _showing;
        private bool _started;        // el tira-y-afloja arranca despues del mensaje
        private float _t;
        private float _enemyPull;      // tiron del rival por seg = requiredCPS * pushPerClick (empate a requiredCPS clicks/seg)
        private Owner _rival;
        private int _playerPower, _rivalPower;
        private Action<Owner, float> _onResolved;
        private float _prevTimeScale;

        public bool IsShowing => _showing;

        void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _root.SetActive(false);
        }

        void Build()
        {
            // limpiar cualquier hijo residual guardado en la escena (evita que aparezca el cartel al iniciar)
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch(_root.GetComponent<RectTransform>());

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(_root.transform, false);
            Stretch(bd.GetComponent<RectTransform>());
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(520, 300);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

            _title = MakeText(panel.transform, "", 22, new Color(1f, 0.85f, 0.5f), FontStyle.Bold);
            SetRect(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(480, 40));

            var sub = MakeText(panel.transform, "CLICK / ESPACIO = empujar (tu lado: derecha)   ·   Click der / Esc = retirarse", 13, new Color(0.8f, 0.83f, 0.9f), FontStyle.Normal);
            SetRect(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(480, 28));

            // barra
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0, 30); brt.sizeDelta = new Vector2(barPixels + 20, 44);
            bar.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 1f);

            // mitades tintadas: izquierda rival (rojo), derecha jugador (cyan)
            MakeHalf(bar.transform, -1, new Color(0.55f, 0.2f, 0.2f, 0.5f));
            MakeHalf(bar.transform, 1, new Color(0.2f, 0.5f, 0.55f, 0.5f));

            var mk = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            mk.transform.SetParent(bar.transform, false);
            _marker = mk.GetComponent<RectTransform>();
            _marker.anchorMin = _marker.anchorMax = new Vector2(0.5f, 0.5f); _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(16, 56);
            mk.GetComponent<Image>().color = Color.white;

            MakeButton(panel.transform, "¡EMPUJA!", new Color(0.2f, 0.55f, 0.6f), new Vector2(0, -60), new Vector2(220, 60), Push);
            MakeButton(panel.transform, "Retirarse", new Color(0.4f, 0.3f, 0.3f), new Vector2(0, -128), new Vector2(220, 40), Retreat);

            // ---- overlay de mensaje ANTES de la pelea ----
            _intro = new GameObject("Intro", typeof(RectTransform), typeof(Image));
            _intro.transform.SetParent(_root.transform, false); // ultimo hijo -> se dibuja arriba
            Stretch(_intro.GetComponent<RectTransform>());
            _intro.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            var ipanel = new GameObject("IntroPanel", typeof(RectTransform), typeof(Image));
            ipanel.transform.SetParent(_intro.transform, false);
            var iprt = ipanel.GetComponent<RectTransform>();
            iprt.anchorMin = iprt.anchorMax = new Vector2(0.5f, 0.5f); iprt.pivot = new Vector2(0.5f, 0.5f);
            iprt.sizeDelta = new Vector2(520, 220);
            ipanel.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.16f, 1f);

            _introText = MakeText(ipanel.transform, "", 20, new Color(1f, 0.85f, 0.55f), FontStyle.Bold);
            SetRect(_introText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(480, 120));
            MakeButton(ipanel.transform, "¡Empezar!", new Color(0.25f, 0.6f, 0.35f), new Vector2(0, -80), new Vector2(240, 56), BeginFight);
        }

        public void Play(char zone, Owner attacker, Owner defender, int attackerPower, int defenderPower, Action<Owner, float> onResolved)
        {
            if (_root == null) { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); Build(); }
            _onResolved = onResolved;

            bool playerIsAttacker = attacker == Owner.Player;
            _rival = playerIsAttacker ? defender : attacker;
            _playerPower = playerIsAttacker ? attackerPower : defenderPower;
            _rivalPower = playerIsAttacker ? defenderPower : attackerPower;

            int gap = _rivalPower - _playerPower; // >0 = estas en desventaja
            // umbral de clicks/seg para empatar; a ese ritmo el dial no se mueve
            float requiredCPS = Mathf.Max(minRequiredCPS, clicksToWinEqual + clicksPerPowerDiff * gap);
            _enemyPull = requiredCPS * pushPerClick;

            _t = 0.5f;
            _title.text = $"Disputa por la Zona {zone}  (vos {_playerPower} vs {_rival} {_rivalPower})";
            UpdateMarker();

            // mensaje antes de arrancar
            string quien = playerIsAttacker ? $"Vas a atacar la Zona {zone}." : $"¡Los rivales de la Zona {zone} te atacan!";
            _introText.text = $"{quien}\n(vos {_playerPower} vs {_rival} {_rivalPower})\n\nTOCA o ESPACIO para EMPEZAR.\nDespues macha para llevar el marcador a tu lado (derecha).";
            _intro.SetActive(true);
            _started = false;

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _showing = true;
            _root.SetActive(true);
        }

        void BeginFight() { _started = true; if (_intro != null) _intro.SetActive(false); }

        void Update()
        {
            if (!_showing) return;

            bool clickOrSpace =
                (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            // durante el mensaje: arrancar con click/espacio directo (no depende del EventSystem con timeScale=0)
            if (!_started)
            {
                if (clickOrSpace) BeginFight();
                return;
            }

            // retirarse con click derecho o Escape (el boton del EventSystem no responde con timeScale=0)
            if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            { Retreat(); return; }

            if (clickOrSpace) Push(); // empujar: click en cualquier lado o Espacio

            _t -= _enemyPull * Time.unscaledDeltaTime;

            // chequear ANTES de re-clampear: si no, el clamp topa t en 1 y el tiron lo baja justo
            // antes del chequeo -> nunca se ganaba. Ahora t puede pasar de 1 y la victoria cuenta.
            if (_t >= 1f) { Resolve(Owner.Player); return; }
            if (_t <= 0f) { Resolve(_rival); return; }
            UpdateMarker();
        }

        void Push() { if (_showing && _started) { _t += pushPerClick; UpdateMarker(); } }

        void Retreat() { if (_showing) Resolve(_rival); } // rendirse = gana el rival

        void Resolve(Owner winner)
        {
            _showing = false;
            _root.SetActive(false);
            Time.timeScale = _prevTimeScale;

            int winnerPow = winner == Owner.Player ? _playerPower : _rivalPower;
            int loserPow = winner == Owner.Player ? _rivalPower : _playerPower;
            float margin = Mathf.Clamp01(0.4f + 0.5f * (winnerPow - loserPow) / 6f);
            _onResolved?.Invoke(winner, margin);
        }

        void UpdateMarker()
        {
            if (_marker != null) _marker.anchoredPosition = new Vector2((Mathf.Clamp01(_t) - 0.5f) * barPixels, 0f);
        }

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void MakeHalf(Transform bar, int side, Color color)
        {
            var go = new GameObject(side < 0 ? "HalfL" : "HalfR", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(bar, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(barPixels * 0.5f, 40); rt.anchoredPosition = new Vector2(side * barPixels * 0.25f, 0);
            go.GetComponent<Image>().color = color;
        }

        Text MakeText(Transform parent, string text, int size, Color color, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        void MakeButton(Transform parent, string label, Color color, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var t = MakeText(go.transform, label, 18, Color.white, FontStyle.Bold);
            Stretch(t.rectTransform);
        }
    }
}
