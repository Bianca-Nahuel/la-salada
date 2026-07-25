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
        [SerializeField] private float playerPush = 0.075f;  // avance por click/tecla
        [SerializeField] private float basePull = 0.11f;     // tiron base del rival por segundo
        [SerializeField] private float pullGapK = 0.06f;     // cuanto pesa la diferencia de poder
        [SerializeField] private float minPullMult = 0.4f;   // tope minimo/maximo del tiron segun poder
        [SerializeField] private float maxPullMult = 1.6f;   // (asi siempre es ganable machacando)
        [SerializeField] private float barPixels = 440f;

        private GameObject _root;
        private RectTransform _marker;
        private Text _title;
        private Font _font;

        private bool _showing;
        private float _t;
        private float _enemyPull;
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

            var sub = MakeText(panel.transform, "Empuja con CLICK o ESPACIO. Tu lado: derecha.", 14, new Color(0.8f, 0.83f, 0.9f), FontStyle.Normal);
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
        }

        public void Play(char zone, Owner attacker, Owner defender, int attackerPower, int defenderPower, Action<Owner, float> onResolved)
        {
            if (_root == null) { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); Build(); }
            _onResolved = onResolved;

            bool playerIsAttacker = attacker == Owner.Player;
            _rival = playerIsAttacker ? defender : attacker;
            _playerPower = playerIsAttacker ? attackerPower : defenderPower;
            _rivalPower = playerIsAttacker ? defenderPower : attackerPower;

            // el rival tira mas fuerte si tiene mas poder que vos (dureza segun diferencia, acotada)
            _enemyPull = basePull * Mathf.Clamp(1f + pullGapK * (_rivalPower - _playerPower), minPullMult, maxPullMult);

            _t = 0.5f;
            _title.text = $"Disputa por la Zona {zone}  (vos {_playerPower} vs {_rival} {_rivalPower})";
            UpdateMarker();

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _showing = true;
            _root.SetActive(true);
        }

        void Update()
        {
            if (!_showing) return;

            // empujar: click izquierdo en cualquier lado, o Espacio (asi no depende de acertarle al boton)
            bool push = false;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) push = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) push = true;
            if (push) Push();

            _t -= _enemyPull * Time.unscaledDeltaTime;

            // chequear ANTES de re-clampear: si no, el clamp topa t en 1 y el tiron lo baja justo
            // antes del chequeo -> nunca se ganaba. Ahora t puede pasar de 1 y la victoria cuenta.
            if (_t >= 1f) { Resolve(Owner.Player); return; }
            if (_t <= 0f) { Resolve(_rival); return; }
            UpdateMarker();
        }

        void Push() { if (_showing) { _t += playerPush; UpdateMarker(); } } // sin tope: el chequeo de victoria usa t>=1

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
