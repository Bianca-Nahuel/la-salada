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
        private float _threshold;
        private float _pauseDuration;
        private float _total;
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
        private Vector3 _approachPoint;
        private float _pauseTimer;

        public bool IsTargetable => !_sold;
        public float TotalConvince => _total;

        public void Init(GridManager grid, List<Vector3> path, float speed, float threshold, float pauseDuration,
            float size, Color baseColor, Action<Client, Owner> onConverted, Action<Client> onEscaped)
        {
            _grid = grid;
            _path = path;
            _idx = 0;
            _speed = speed;
            _threshold = threshold;
            _pauseDuration = pauseDuration;
            _baseColor = baseColor;
            _onConverted = onConverted;
            _onEscaped = onEscaped;

            transform.position = path[0];
            BuildVisuals(size, baseColor);
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
            switch (_state)
            {
                case State.Traveling: FollowPath(Escape); break;
                case State.Approaching: MoveTo(_approachPoint, StartPause); break;
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

        void MoveTo(Vector3 target, Action onArrive) { if (MoveStep(target)) onArrive(); }

        bool MoveStep(Vector3 target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, _speed * Time.deltaTime);
            return (transform.position - target).sqrMagnitude < 0.0004f;
        }

        /// <summary>Un puesto convence al cliente en 'amount'; roba 'steal' de ese valor a las demas facciones.</summary>
        public void TakeDamage(PlacedStall stall, float amount, float steal)
        {
            if (_sold) return;

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
            _onConverted?.Invoke(this, winner);

            var stall = BestStallOf(winner);
            if (stall != null) { _approachPoint = StallFrontPoint(stall); _state = State.Approaching; }
            else BeginLeaving();
        }

        Vector3 StallFrontPoint(PlacedStall s)
        {
            Vector2 c = _grid.Model.FootprintCenterWorld(s.OriginCell, s.Footprint);
            var f = s.Facing;
            float cs = _grid.CellSize;
            float halfDepth = (Mathf.Abs(f.x) > 0 ? s.Footprint.x : s.Footprint.y) * 0.5f * cs;
            var p = c + new Vector2(f.x, f.y) * (halfDepth + 0.35f * cs);
            return new Vector3(p.x, p.y, 0f);
        }

        void StartPause() { _state = State.Pausing; _pauseTimer = _pauseDuration; }

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
