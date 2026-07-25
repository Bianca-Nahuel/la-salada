using System;
using System.Collections.Generic;
using UnityEngine;
using Salada.Combat;
using Salada.UI;

namespace Salada.Game
{
    /// <summary>
    /// Cada dia evalua las condiciones de todos los eventos y muestra los que corresponden:
    /// todos los obligatorios que cumplan, y como maximo 1 opcional (elegido por probabilidad).
    /// Aplica las consecuencias de la opcion elegida y muestra su dialogo final.
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        [SerializeField] private GameEvent[] events;

        private WaveManager _waves;
        private BusinessMeters _meters;
        private GameEffects _effects;
        private EventPopup _popup;

        private readonly HashSet<GameEvent> _happened = new HashSet<GameEvent>();
        private readonly Dictionary<GameEvent, int> _chosen = new Dictionary<GameEvent, int>();
        private readonly Queue<GameEvent> _queue = new Queue<GameEvent>();
        private bool _showing;
        private int _rng = 987654;

        public bool IsEventPending => _showing || _queue.Count > 0;
        public GameEvent[] Events { get => events; set => events = value; }

        void Start()
        {
            _waves = FindAnyObjectByType<WaveManager>();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _effects = FindAnyObjectByType<GameEffects>();
            _popup = FindAnyObjectByType<EventPopup>(FindObjectsInactive.Include);
            if (_waves != null) _waves.DayPassed += OnDayPassed;
        }

        void OnDestroy()
        {
            if (_waves != null) _waves.DayPassed -= OnDayPassed;
        }

        void OnDayPassed()
        {
            if (events == null || _popup == null) return;
            int day = _waves != null ? _waves.Day : 1;

            var mandatory = new List<GameEvent>();
            var optional = new List<GameEvent>();
            foreach (var ev in events)
            {
                if (ev == null) continue;
                if (!ev.repeatable && _happened.Contains(ev)) continue;
                if (!ConditionsPassExceptProbability(ev, day)) continue;
                (ev.mandatory ? mandatory : optional).Add(ev);
            }

            foreach (var ev in mandatory) _queue.Enqueue(ev);       // todos los obligatorios
            var pick = PickOptional(optional);                       // 1 opcional por probabilidad
            if (pick != null) _queue.Enqueue(pick);

            if (!_showing) ShowNext();
        }

        // ---- Condiciones ----

        bool ConditionsPassExceptProbability(GameEvent ev, int day)
        {
            if (ev.conditions == null) return true;
            foreach (var c in ev.conditions)
                if (c.type != ConditionType.Probability && !Pass(c, day))
                    return false;
            return true;
        }

        bool Pass(EventCondition c, int day)
        {
            switch (c.type)
            {
                case ConditionType.DayAtLeast: return day >= c.intValue;
                case ConditionType.DayEvery: return c.intValue > 0 && day % c.intValue == 0;
                case ConditionType.Probability: return true; // se maneja aparte
                case ConditionType.OptionChosen:
                    return c.otherEvent != null && _chosen.TryGetValue(c.otherEvent, out int i) && i == c.intValue;
                case ConditionType.EventHappened:
                    return c.otherEvent != null && _happened.Contains(c.otherEvent);
                case ConditionType.EventNotHappened:
                    return c.otherEvent == null || !_happened.Contains(c.otherEvent);
                case ConditionType.MeterAbove:
                    return _meters != null && _meters.Get(c.meter) > c.value;
                case ConditionType.MeterBelow:
                    return _meters != null && _meters.Get(c.meter) < c.value;
                default: return true;
            }
        }

        float Probability(GameEvent ev)
        {
            float p = 1f;
            if (ev.conditions != null)
                foreach (var c in ev.conditions)
                    if (c.type == ConditionType.Probability) p *= Mathf.Clamp01(c.value);
            return p;
        }

        GameEvent PickOptional(List<GameEvent> list)
        {
            if (list.Count == 0) return null;
            // orden aleatorio y devolver el primero que gane su tirada -> a lo sumo 1 por dia
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            foreach (var ev in list)
                if (NextFloat() < Probability(ev)) return ev;
            return null;
        }

        // ---- Mostrar / aplicar ----

        void ShowNext()
        {
            if (_queue.Count == 0) { _showing = false; return; }
            _showing = true;
            var ev = _queue.Dequeue();
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
            if (opt.money != 0 && _waves != null) _waves.AddMoney(opt.money);
            if (_effects != null) _effects.AddEffect(opt.special, opt.specialMagnitude, opt.specialWaves);

            _happened.Add(ev);
            _chosen[ev] = optionIndex;
        }

        // ---- Debug ----

        public void DebugTrigger(int index)
        {
            if (events == null || events.Length == 0 || _popup == null) return;
            _queue.Enqueue(events[Mathf.Clamp(index, 0, events.Length - 1)]);
            if (!_showing) ShowNext();
        }

        public void DebugTriggerEvent(GameEvent ev)
        {
            if (ev == null || _popup == null) return;
            _queue.Enqueue(ev);
            if (!_showing) ShowNext();
        }

        int NextInt(int max)
        {
            _rng = _rng * 1103515245 + 12345;
            int v = (_rng >> 16) & 0x7fff;
            return max <= 0 ? 0 : v % max;
        }

        float NextFloat() => NextInt(10000) / 10000f;
    }
}
