using System;
using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;
using Salada.Util;

namespace Salada.Combat
{
    /// <summary>
    /// Cliente que recorre los pasillos de una boca a otra. Los puestos lo convencen por
    /// faccion; al golpear, cada faccion tambien le baja un poco el progreso a las demas
    /// (clientes en disputa cuestan mas). Al llegar al umbral, la faccion con mas progreso
    /// hace la venta: el cliente camina al puesto ganador, se detiene y sale por la boca mas
    /// cercana. Si llega al final sin comprar, se va (venta perdida). Tiene barra de progreso.
    /// </summary>
    public class Client : MonoBehaviour
    {
        public static readonly List<Client> Active = new List<Client>();

        private enum State { Traveling, Approaching, Pausing, Leaving }

        private GridManager _grid;
        private List<Vector3> _path;
        private int _idx;
        private float _speed;
        private float _rushSpeed;      // velocidad en el tramo "rush" (hasta el centro)
        private int _rushUntil;        // waypoints [0.._rushUntil) se recorren a _rushSpeed
        private float _threshold;
        private float _pauseDuration;
        private float _total;
        private float _convinceDecay;  // atencion que pierde por segundo si nadie le pega
        private float _hitTimer;       // gracia desde el ultimo golpe antes de empezar a decaer
        private const float HitGrace = 0.5f;
        private readonly Dictionary<Owner, float> _byFaction = new Dictionary<Owner, float>();
        private readonly Dictionary<PlacedStall, float> _byStall = new Dictionary<PlacedStall, float>();

        private SpriteRenderer _body;
        private Transform _barFill;
        private SpriteRenderer _barFillSr;
        private float _barWidth;
        private float _barY;
        private Color _baseColor;

        private Action<Client, Owner> _onConverted;
        private Action<Client> _onEscaped;

        private State _state = State.Traveling;
        private bool _sold;
        private float _pauseTimer;

        // cartelito "+$X" (y lo que dependa de el, ej. el sonido de venta) que se muestra recien
        // cuando el cliente LLEGA al puesto (no al concretar la venta).
        private string _arrivalText; private Color _arrivalColor; private bool _hasArrivalText;
        private Action _onArrival;

        /// <summary>
        /// Deja preparado un cartelito (y opcionalmente una accion, ej. sonido) para cuando el
        /// cliente llegue al puesto.
        /// </summary>
        public void SetArrivalText(string text, Color color, Action onArrival = null)
        {
            _arrivalText = text; _arrivalColor = color; _hasArrivalText = true; _onArrival = onArrival;
        }

        // arte del cliente (frames a/b por estado). Setear ANTES de Init.
        private ClientSkinSet.Skin _skin;
        private Transform _bodyTf;
        private float _targetHeight;    // alto fijo del cliente (para que no se achique al cambiar de sprite)
        private bool _bought;           // ya compro (usa la ropa de la faccion)
        private Owner _boughtFaction;   // faccion a la que le compro
        private Owner _soldWinner;
        private bool _frameB;           // frame actual de la caminata (a/b)
        private float _walkTimer, _wobblePhase;
        private const float WalkFrameTime = 0.22f; // cada cuanto cambia el frame de caminata
        private const float WobbleSpeed = 9f, WobbleAmp = 6f; // tambaleo (grados)
        private List<Vector2Int> _exits; // salidas posibles (sale por cualquiera)

        public void SetSkin(ClientSkinSet.Skin skin) { _skin = skin; }
        public void SetExits(List<Vector2Int> exits) { _exits = exits; }

        public bool IsTargetable => !_sold;
        public float TotalConvince => _total;

        /// <summary>Progreso de venta [0..1] (que tan cerca esta de venderse). Para el targeting rival.</summary>
        public float SaleProgress => _threshold > 0f ? Mathf.Clamp01(_total / _threshold) : 0f;

        /// <summary>Convencimiento acumulado por una faccion (para detectar robos de venta).</summary>
        public float ConvinceBy(Owner o) => _byFaction.TryGetValue(o, out var v) ? v : 0f;

        /// <summary>Puesto de 'faction' que mas convencio a este cliente (para atribuir zona de golpes/ventas).</summary>
        public PlacedStall TopStallOf(Owner faction) => BestStallOf(faction);

        /// <summary>Puesto ganador de la venta (seteado al vender); null si aun no vendio.</summary>
        public PlacedStall LastSaleStall { get; private set; }

        public bool Rushing => _rushUntil > 0;                  // para testeo
        public int PathLength => _path != null ? _path.Count : 0; // para testeo
        /// <summary>Corre el decaimiento por 'seconds' saltando la gracia (para testeo).</summary>
        public void DebugForceDecay(float seconds)
        {
            _hitTimer = -1f;
            float dec = _convinceDecay * seconds;
            var keys = new List<Owner>(_byFaction.Keys);
            foreach (var f in keys) if (_byFaction[f] > 0f) _byFaction[f] = Mathf.Max(0f, _byFaction[f] - dec);
            _total = 0f;
            foreach (var kv in _byFaction) _total += kv.Value;
            if (_body != null) UpdateBar();
        }

        public void Init(GridManager grid, List<Vector3> path, float speed, float threshold, float pauseDuration,
            float size, Color baseColor, float convinceDecay, Action<Client, Owner> onConverted, Action<Client> onEscaped)
        {
            _grid = grid;
            _path = path;
            _idx = 0;
            _speed = speed;
            _rushSpeed = speed;
            _rushUntil = 0;
            _threshold = threshold;
            _pauseDuration = pauseDuration;
            _convinceDecay = convinceDecay;
            _baseColor = baseColor;
            _onConverted = onConverted;
            _onEscaped = onEscaped;

            transform.position = path[0];
            BuildVisuals(size, baseColor);
        }

        /// <summary>Cliente que va rapido hasta cierto waypoint (el centro) y despues mas lento.</summary>
        public void SetRush(int rushUntil, float rushSpeed, float slowSpeed)
        {
            _rushUntil = rushUntil;
            _rushSpeed = rushSpeed;
            _speed = slowSpeed;
        }

        void BuildVisuals(float size, Color baseColor)
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sortingOrder = 20;

            _bodyTf = bodyGo.transform;
            float topY;
            if (_skin != null && _skin.normalA != null)
            {
                _body.color = Color.white; // el arte ya viene coloreado, no se tinta
                _targetHeight = size * 1.8f; // alto fijo del cliente (ajustable con clientSize)
                _body.sprite = _skin.normalA;
                RescaleBody();
                topY = _targetHeight * 0.5f;
            }
            else
            {
                _body.sprite = PlaceholderSprite.Unit;
                _body.color = baseColor;
                bodyGo.transform.localScale = new Vector3(size, size, 1f);
                topY = size * 0.5f;
            }

            _barWidth = Mathf.Max(size, 0.5f);
            _barY = topY + 0.18f;

            MakeBar("BarBg", new Color(0f, 0f, 0f, 0.6f), 21, _barWidth, out _);
            _barFill = MakeBar("BarFill", Color.white, 22, 0.0001f, out _barFillSr);
        }

        Transform MakeBar(string name, Color color, int order, float width, out SpriteRenderer sr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _barY, 0f);
            go.transform.localScale = new Vector3(width, 0.11f, 1f);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Unit;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Update()
        {
            if (_grid == null || _grid.Model == null) return;
            if (!_sold) DecayConvince(); // la atencion baja si nadie le pega

            bool walking = _state != State.Pausing;
            AnimateWalk(walking);

            switch (_state)
            {
                case State.Traveling: FollowPath(Escape); break;
                case State.Approaching: FollowPath(StartPause); break;
                case State.Pausing:
                    _pauseTimer -= Time.deltaTime;
                    if (_pauseTimer <= 0f) BeginLeaving();
                    break;
                case State.Leaving: FollowPath(() => Destroy(gameObject)); break;
            }
        }

        // caminata: alterna los frames a/b y hace un leve tambaleo; parado se queda quieto y derecho
        void AnimateWalk(bool walking)
        {
            if (_bodyTf == null) return;
            if (walking && _skin != null)
            {
                _walkTimer += Time.deltaTime;
                if (_walkTimer >= WalkFrameTime) { _walkTimer -= WalkFrameTime; _frameB = !_frameB; ApplySprite(); }
                _wobblePhase += Time.deltaTime * WobbleSpeed;
                _bodyTf.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_wobblePhase) * WobbleAmp);
            }
            else _bodyTf.localRotation = Quaternion.identity;
        }

        // pone el sprite del frame/estado actual y reescala para mantener el alto fijo (no se achica)
        void ApplySprite()
        {
            if (_body == null || _skin == null) return;
            var s = _skin.Frame(_bought, _boughtFaction, _frameB);
            if (s != null) { _body.sprite = s; RescaleBody(); }
        }

        void RescaleBody()
        {
            if (_bodyTf == null || _body == null || _body.sprite == null) return;
            float bh = _body.sprite.bounds.size.y;
            float sc = bh > 0.0001f ? _targetHeight / bh : 1f;
            _bodyTf.localScale = new Vector3(sc, sc, 1f);
        }

        // aplica la "ropa" de la faccion (al llegar al puesto). Con placeholder, tinta.
        void ApplyBought()
        {
            if (_skin != null) { _bought = true; ApplySprite(); }
            else if (_body != null) _body.color = _grid.ColorFor(_soldWinner);
        }

        void FollowPath(Action onEnd)
        {
            if (_path == null || _idx >= _path.Count) { onEnd(); return; }
            if (MoveStep(_path[_idx])) { _idx++; if (_idx >= _path.Count) onEnd(); }
        }

        bool MoveStep(Vector3 target)
        {
            float sp = _idx < _rushUntil ? _rushSpeed : _speed; // rapido hasta el centro, despues lento
            transform.position = Vector3.MoveTowards(transform.position, target, sp * Time.deltaTime);
            return (transform.position - target).sqrMagnitude < 0.0004f;
        }

        /// <summary>Baja la atencion acumulada si paso la gracia sin recibir golpes.</summary>
        void DecayConvince()
        {
            if (_convinceDecay <= 0f || _total <= 0f) return;
            _hitTimer -= Time.deltaTime;
            if (_hitTimer > 0f) return; // recien golpeado: no decae todavia
            float dec = _convinceDecay * Time.deltaTime;
            var keys = new List<Owner>(_byFaction.Keys);
            foreach (var f in keys)
                if (_byFaction[f] > 0f) _byFaction[f] = Mathf.Max(0f, _byFaction[f] - dec);
            _total = 0f;
            foreach (var kv in _byFaction) _total += kv.Value;
            UpdateBar();
        }

        /// <summary>Un puesto convence al cliente en 'amount'; roba 'steal' de ese valor a las demas facciones.</summary>
        public void TakeDamage(PlacedStall stall, float amount, float steal)
        {
            if (_sold) return;
            _hitTimer = HitGrace; // lo golpearon: reinicia la gracia antes de decaer

            if (steal > 0f)
            {
                var keys = new List<Owner>(_byFaction.Keys);
                foreach (var f in keys)
                    if (f != stall.Owner)
                        _byFaction[f] = Mathf.Max(0f, _byFaction[f] - amount * steal);
            }

            _byFaction.TryGetValue(stall.Owner, out float cf); _byFaction[stall.Owner] = cf + amount;
            _byStall.TryGetValue(stall, out float cs); _byStall[stall] = cs + amount;

            _total = 0f;
            foreach (var kv in _byFaction) _total += kv.Value;

            UpdateBar();

            if (_total >= _threshold) Sell(LeaderFaction());
        }

        void UpdateBar()
        {
            float p = Mathf.Clamp01(_total / _threshold);
            var leaderCol = _grid.ColorFor(LeaderFaction());
            if (_skin == null) _body.color = Color.Lerp(_baseColor, leaderCol, p); // solo tinta el placeholder
            _barFillSr.color = leaderCol;
            _barFill.localScale = new Vector3(_barWidth * p, 0.11f, 1f);
            _barFill.localPosition = new Vector3(-_barWidth * 0.5f + _barWidth * p * 0.5f, _barY, 0f);
        }

        Owner LeaderFaction()
        {
            Owner best = Owner.Neutral; float bestVal = -1f;
            foreach (var kv in _byFaction)
                if (kv.Value > bestVal) { bestVal = kv.Value; best = kv.Key; }
            return best;
        }

        PlacedStall BestStallOf(Owner faction)
        {
            PlacedStall best = null; float bestVal = -1f;
            foreach (var kv in _byStall)
                if (kv.Key.Owner == faction && kv.Value > bestVal) { bestVal = kv.Value; best = kv.Key; }
            return best;
        }

        void Sell(Owner winner)
        {
            _sold = true;
            _soldWinner = _boughtFaction = winner;
            // sigue caminando con la ropa NORMAL; el cambio de ropa ocurre al LLEGAR al puesto (StartPause)
            LastSaleStall = BestStallOf(winner);   // antes del invoke: lo usa el detector de robos
            _onConverted?.Invoke(this, winner);

            var stall = LastSaleStall;
            if (stall == null) { ApplyBought(); BeginLeaving(); return; } // sin puesto: cambia ya y se va

            // Caminar por el PISO hasta la celda frente al puesto (no cruza arena).
            var front = _grid.Model.FrontCell(stall.OriginCell, stall.Footprint, stall.Facing);
            if (!_grid.Model.IsAisle(front)) { StartPause(); return; } // fallback raro
            var cur = _grid.Model.NearestAisleCell(transform.position);
            var cells = _grid.Model.FindAislePath(cur, front);

            var wp = new List<Vector3> { (Vector3)(Vector2)_grid.Model.CellToWorldCenter(cur) };
            if (cells != null)
                foreach (var c in cells) { var w = _grid.Model.CellToWorldCenter(c); wp.Add(new Vector3(w.x, w.y, 0f)); }

            _path = wp;
            _idx = 0;
            _state = State.Approaching;
        }

        void StartPause()
        {
            _state = State.Pausing; _pauseTimer = _pauseDuration;
            // el cliente llego al puesto: ahora si mostramos el cartelito de la venta (y su sonido)
            if (_hasArrivalText)
            {
                FloatingText.Spawn(transform.position, _arrivalText, _arrivalColor);
                _hasArrivalText = false;
                _onArrival?.Invoke();
            }
            // el cliente LLEGO al puesto: recien ahora cambia de ropa (se nota mas) y muestra el cartel
            if (_sold) ApplyBought();
        }

        void BeginLeaving()
        {
            Vector2 pos = transform.position;
            var aisleCell = _grid.Model.NearestAisleCell(pos);
            // sale por CUALQUIER salida (no siempre la mas cercana)
            Vector2Int opening = (_exits != null && _exits.Count > 0)
                ? _exits[UnityEngine.Random.Range(0, _exits.Count)]
                : _grid.Model.NearestEntrance(pos);
            var cells = _grid.Model.FindAislePath(aisleCell, opening);

            var wp = new List<Vector3> { (Vector3)(Vector2)_grid.Model.CellToWorldCenter(aisleCell) };
            if (cells != null)
                foreach (var c in cells) { var w = _grid.Model.CellToWorldCenter(c); wp.Add(new Vector3(w.x, w.y, 0f)); }

            _path = wp;
            _idx = 0;
            _state = State.Leaving;
        }

        void Escape() { _onEscaped?.Invoke(this); Destroy(gameObject); }
    }
}
