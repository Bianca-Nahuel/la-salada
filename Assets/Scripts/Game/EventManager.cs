using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Salada.Combat;
using Salada.Placement;
using Salada.UI;
using Salada.Audio;

namespace Salada.Game
{
    /// <summary>
    /// Los eventos siempre se disparan al final de un dia. Justo antes de elegir el evento del
    /// dia se actualizan: (1) la cola de obligatorios pendientes (eventos obligatorios cuyas
    /// condiciones ya se cumplen) y (2) el pool de aleatorios disponibles (no obligatorios cuyas
    /// condiciones ya se cumplen). Si hay obligatorios pendientes se disparan hasta 2 ese dia (el
    /// resto queda pendiente para los dias siguientes); si no hay ninguno, se elige 1 evento al
    /// azar del pool. Se aplican las consecuencias de la opcion elegida y se muestra su dialogo.
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        const int MaxMandatoryPerDay = 2;

        [SerializeField] private GameEvent[] events;

        private WaveManager _waves;
        private BusinessMeters _meters;
        private GameEffects _effects;
        private GridManager _grid;
        private EventPopup _popup;
        private GameOverController _gameOver;

        private readonly HashSet<GameEvent> _happened = new HashSet<GameEvent>();
        private readonly Dictionary<GameEvent, string> _chosenOptionId = new Dictionary<GameEvent, string>();

        private readonly Queue<GameEvent> _mandatoryPending = new Queue<GameEvent>();
        private readonly HashSet<GameEvent> _mandatoryPendingSet = new HashSet<GameEvent>();

        private readonly List<GameEvent> _pool = new List<GameEvent>(); // aleatorios disponibles

        private readonly Queue<GameEvent> _showQueue = new Queue<GameEvent>(); // eventos del dia a mostrar
        private bool _showing;

        public bool IsEventPending => _showing || _showQueue.Count > 0;
        public GameEvent[] Events { get => events; set => events = value; }

        void Start()
        {
            _waves = FindFirstObjectByType<WaveManager>();
            _meters = FindFirstObjectByType<BusinessMeters>();
            _effects = FindFirstObjectByType<GameEffects>();
            _grid = FindFirstObjectByType<GridManager>();
            _popup = FindFirstObjectByType<EventPopup>(FindObjectsInactive.Include);
            _gameOver = FindFirstObjectByType<GameOverController>();
            if (_gameOver == null) _gameOver = gameObject.AddComponent<GameOverController>();
            if (_popup != null) _popup.Init(_waves, _meters, _gameOver);
            if (_waves != null) _waves.DayPassed += OnDayPassed;
            StartCoroutine(TriggerInitialDay());
        }

        /// <summary>
        /// WaveManager solo dispara DayPassed al completar las primeras wavesPerDay oleadas
        /// (para entonces Day ya paso a 2). Sin esto, el dia 1 nunca se evalua. Se espera un
        /// frame para que todos los Start() (WaveManager incluido) ya hayan corrido.
        /// </summary>
        IEnumerator TriggerInitialDay()
        {
            yield return null;
            OnDayPassed();
        }

        void OnDestroy()
        {
            if (_waves != null) _waves.DayPassed -= OnDayPassed;
        }

        void OnDayPassed()
        {
            if (events == null || _popup == null) return;
            int day = _waves != null ? _waves.Day : 1; // 1) el dia ya avanzo (WaveManager.Day++ corre antes de disparar DayPassed)

            UpdatePool(day);            // 2) sumar al pool los aleatorios que recien cumplen condiciones
            UpdateMandatoryQueue(day);   // 3) sumar a la cola los obligatorios que recien cumplen condiciones

            int taken = 0;
            while (taken < MaxMandatoryPerDay && _mandatoryPending.Count > 0)
            {
                var ev = _mandatoryPending.Dequeue();
                _mandatoryPendingSet.Remove(ev);
                _showQueue.Enqueue(ev);
                taken++;
            }

            if (taken == 0)
            {
                var pick = PickFromPool();
                if (pick != null) _showQueue.Enqueue(pick);
            }

            if (!_showing) ShowNext();
        }

        // ---- Pool / cola de obligatorios ----

        void UpdateMandatoryQueue(int day)
        {
            foreach (var ev in events)
            {
                if (ev == null || !ev.mandatory) continue;
                if (!ev.repeatable && _happened.Contains(ev)) continue;
                if (_mandatoryPendingSet.Contains(ev)) continue;
                if (ConditionsPass(ev, day))
                {
                    _mandatoryPending.Enqueue(ev);
                    _mandatoryPendingSet.Add(ev);
                }
            }
        }

        void UpdatePool(int day)
        {
            foreach (var ev in events)
            {
                if (ev == null || ev.mandatory) continue;
                if (!ev.repeatable && _happened.Contains(ev)) continue;
                if (_pool.Contains(ev)) continue;
                if (ConditionsPass(ev, day))
                    _pool.Add(ev);
            }
        }

        GameEvent PickFromPool()
        {
            if (_pool.Count == 0) return null;
            return _pool[UnityEngine.Random.Range(0, _pool.Count)];
        }

        // ---- Condiciones ----

        bool ConditionsPass(GameEvent ev, int day)
        {
            if (ev.conditions == null) return true;
            foreach (var c in ev.conditions)
                if (!Pass(c, day)) return false;
            return true;
        }

        bool Pass(EventCondition c, int day)
        {
            switch (c.type)
            {
                case ConditionType.DayAtLeast: return day >= c.intValue;
                case ConditionType.MoneyAbove: return _waves != null && _waves.Money > c.value;
                case ConditionType.MoneyBelow: return _waves != null && _waves.Money < c.value;
                case ConditionType.MeterAbove: return _meters != null && _meters.Get(c.meter) > c.value;
                case ConditionType.MeterBelow: return _meters != null && _meters.Get(c.meter) < c.value;
                case ConditionType.EventsHappened: return PassEventsHappened(c);
                case ConditionType.OptionsChosen: return PassOptionsChosen(c);
                default: return true;
            }
        }

        bool PassEventsHappened(EventCondition c)
        {
            if (c.events == null || c.events.Count == 0) return true;
            if (c.mode == ConditionMode.All)
            {
                foreach (var e in c.events)
                    if (e == null || !_happened.Contains(e)) return false;
                return true;
            }
            foreach (var e in c.events)
                if (e != null && _happened.Contains(e)) return true;
            return false;
        }

        bool PassOptionsChosen(EventCondition c)
        {
            if (c.options == null || c.options.Count == 0) return true;
            if (c.mode == ConditionMode.All)
            {
                foreach (var r in c.options)
                    if (!OptionWasChosen(r)) return false;
                return true;
            }
            foreach (var r in c.options)
                if (OptionWasChosen(r)) return true;
            return false;
        }

        bool OptionWasChosen(EventOptionRef r)
        {
            if (r == null || r.gameEvent == null || string.IsNullOrEmpty(r.optionId)) return false;
            return _chosenOptionId.TryGetValue(r.gameEvent, out var chosenId) && chosenId == r.optionId;
        }

        // ---- Mostrar / aplicar ----

        void ShowNext()
        {
            if (_showQueue.Count == 0) { _showing = false; return; }
            _showing = true;
            var ev = _showQueue.Dequeue();
            _popup.Show(ev, idx => Apply(ev, idx), ShowNext);
        }

        void Apply(GameEvent ev, int optionIndex)
        {
            if (ev.options == null || optionIndex < 0 || optionIndex >= ev.options.Length) return;
            var opt = ev.options[optionIndex];

            if (_meters != null)
            {
                _meters.Add(MeterType.Hostility, opt.hostility);
                _meters.Add(MeterType.Reputation, opt.reputation);
                _meters.Add(MeterType.Happiness, opt.happiness);
                _meters.Add(MeterType.Profit, opt.profit);
            }

            int totalMoney = opt.money;
            if (opt.moneyPerStall != 0 && _waves != null) totalMoney += opt.moneyPerStall * _waves.PlayerStallCount();
            if (totalMoney != 0 && _waves != null) _waves.AddMoney(totalMoney);

            if (opt.salaryIncreasePercent != 0f && _waves != null) _waves.IncreaseSalaryPerStall(opt.salaryIncreasePercent);
            if (opt.destroyBiggestStall) DestroyBiggestPlayerStall();

            if (_effects != null)
            {
                int duration = DurationFor(opt);
                _effects.AddEffect(opt.special, opt.specialMagnitude, duration);
                _effects.AddEffect(opt.special2, opt.specialMagnitude2, duration);
            }

            _happened.Add(ev);
            _chosenOptionId[ev] = opt.id;

            if (!ev.mandatory && !ev.repeatable) _pool.Remove(ev);
        }

        int DurationFor(EventOption opt)
        {
            if (opt.specialPermanent) return int.MaxValue;
            if (opt.specialOneDayOnly) return _waves != null ? _waves.wavesPerDay : opt.specialWaves;
            return opt.specialWaves;
        }

        void DestroyBiggestPlayerStall()
        {
            if (_grid == null || _grid.Model == null) return;
            PlacedStall biggest = null;
            int bestArea = -1;
            foreach (var s in _grid.Model.Stalls)
            {
                if (s.Owner != Owner.Player) continue;
                int area = s.Footprint.x * s.Footprint.y;
                if (area > bestArea || (area == bestArea && biggest != null && s.Cost > biggest.Cost))
                {
                    bestArea = area;
                    biggest = s;
                }
            }
            if (biggest == null) return;
            var removed = _grid.Model.Remove(biggest.OriginCell);
            if (removed?.View != null) Destroy(removed.View);
            Sfx.Play(SfxId.StallDestroyed);
        }

        // ---- Debug ----

        public void DebugTrigger(int index)
        {
            if (events == null || events.Length == 0 || _popup == null) return;
            _showQueue.Enqueue(events[Mathf.Clamp(index, 0, events.Length - 1)]);
            if (!_showing) ShowNext();
        }

        public void DebugTriggerEvent(GameEvent ev)
        {
            if (ev == null || _popup == null) return;
            _showQueue.Enqueue(ev);
            if (!_showing) ShowNext();
        }
    }
}
