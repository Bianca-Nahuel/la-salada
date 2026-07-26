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

        // cartelito "+$X" que se muestra recien cuando el cliente LLEGA al puesto (no al concretar)
        private string _arrivalText; private Color _arrivalColor; private bool _hasArrivalText;

        /// <summary>Deja preparado un cartelito para mostrar cuando el cliente llegue al puesto.</summary>
        public void SetArrivalText(string text, Color color) { _arrivalText = text; _arrivalColor = color; _hasArrivalText = true; }

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
            bodyGo.transform.localScale = new Vector3(size, size, 1f);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = PlaceholderSprite.Unit;
            _body.color = baseColor;
            _body.sortingOrder = 20;

            _barWidth = Mathf.Max(size, 0.5f);
            _barY = size * 0.5f + 0.18f;

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
            _body.color = Color.Lerp(_baseColor, leaderCol, p);
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
            _body.color = _grid.ColorFor(winner);
            LastSaleStall = BestStallOf(winner);   // antes del invoke: lo usa el detector de robos
            _onConverted?.Invoke(this, winner);

            var stall = LastSaleStall;
            if (stall == null) { BeginLeaving(); return; }

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
            // el cliente llego al puesto: ahora si mostramos el cartelito de la venta
            if (_hasArrivalText) { FloatingText.Spawn(transform.position, _arrivalText, _arrivalColor); _hasArrivalText = false; }
        }

        void BeginLeaving()
        {
            Vector2 pos = transform.position;
            var aisleCell = _grid.Model.NearestAisleCell(pos);
            var opening = _grid.Model.NearestEntrance(pos);
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
