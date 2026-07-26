using System;
using System.Collections.Generic;
using UnityEngine;

namespace Salada.Game
{
    /// <summary>Tipo de condicion para que un evento se dispare (obligatorio) o entre al pool (aleatorio).</summary>
    public enum ConditionType
    {
        DayAtLeast,      // Day >= intValue
        MoneyAbove,      // dinero > value
        MoneyBelow,      // dinero < value
        MeterAbove,      // medidor 'meter' > value
        MeterBelow,      // medidor 'meter' < value
        EventsHappened,  // eventos de 'events' ocurrieron (segun 'mode': alguno o todos)
        OptionsChosen,   // opciones de 'options' fueron elegidas (segun 'mode': alguna o todas)
        TerritoryAbove,  // % de dominio del jugador (TerritoryManager) > value
        TerritoryBelow   // % de dominio del jugador (TerritoryManager) < value
    }

    /// <summary>Para condiciones de lista (EventsHappened/OptionsChosen): alguno o todos.</summary>
    public enum ConditionMode { Any, All }

    /// <summary>Referencia a una opcion especifica de un evento (por id unico, no por indice).</summary>
    [Serializable]
    public class EventOptionRef
    {
        public GameEvent gameEvent;
        public string optionId;
    }

    /// <summary>Una condicion de disparo. Todas las condiciones de un evento deben cumplirse (AND).</summary>
    [Serializable]
    public class EventCondition
    {
        public ConditionType type;
        public int intValue;      // dia (DayAtLeast)
        public float value = 1f;  // umbral de dinero o medidor
        public MeterType meter;   // para MeterAbove/Below
        public ConditionMode mode = ConditionMode.All; // para EventsHappened/OptionsChosen
        public List<GameEvent> events = new List<GameEvent>();          // para EventsHappened
        public List<EventOptionRef> options = new List<EventOptionRef>(); // para OptionsChosen
    }

    /// <summary>Una opcion/decision de un evento: consecuencias + dialogo final. Id unico y estable.</summary>
    [Serializable]
    public class EventOption
    {
        [HideInInspector] public string id;

        public string label = "Opcion";
        [TextArea] public string outcome = ""; // dialogo final tras elegir

        [Header("Cambios en balanzas (+/-, 0-100)")]
        public float hostility;
        public float reputation;
        public float happiness;
        public float profit;

        [Header("Plata (+/-)")]
        public int money;
        [Tooltip("Costo/ingreso adicional multiplicado por la cantidad de puestos del jugador (se suma a 'money'). Ej -25 = -$25 por cada puesto.")]
        public int moneyPerStall;

        [Header("Mecanicas especiales (opcional)")]
        [Tooltip("Sube permanentemente el sueldo diario por puesto (WaveManager.salaryPerStall). Ej 0.1 = +10%.")]
        public float salaryIncreasePercent;
        [Tooltip("Destruye el puesto del jugador con mayor superficie (footprint).")]
        public bool destroyBiggestStall;
        [Tooltip("Termina la partida en derrota (Game Over) al elegir esta opcion.")]
        public bool triggerGameOver;
        [Tooltip("Termina la partida en victoria (final bueno) al elegir esta opcion.")]
        public bool triggerGameWin;

        [Header("Efecto especial (opcional, hasta 2)")]
        public EffectType special = EffectType.None;
        public float specialMagnitude = 1f;
        public EffectType special2 = EffectType.None;
        public float specialMagnitude2 = 1f;
        [Tooltip("Duracion en oleadas de los efectos especiales (ignorado si 'Solo por hoy' o 'Permanente' estan tildados).")]
        public int specialWaves = 0;
        [Tooltip("El/los efecto/s especial/es dura/n solo lo que resta del dia de hoy.")]
        public bool specialOneDayOnly;
        [Tooltip("El/los efecto/s especial/es no vence/n nunca.")]
        public bool specialPermanent;
    }

    /// <summary>
    /// Un evento: quien lo dice, condiciones de disparo y opciones con consecuencias.
    /// Los eventos siempre se disparan al final de un dia. Los obligatorios se disparan apenas
    /// cumplen condiciones (maximo 2 por dia, el resto queda pendiente). Los no obligatorios
    /// entran a un pool de eventos aleatorios apenas cumplen condiciones, y se elige 1 por dia
    /// entre los disponibles cuando no hay obligatorios pendientes.
    /// </summary>
    [CreateAssetMenu(fileName = "Event", menuName = "Salada/Game Event")]
    public class GameEvent : ScriptableObject
    {
        [HideInInspector] public string id;

        public string title = "Evento";
        public EventCharacter speaker;
        [TextArea] public string description = "";

        [Header("Disparo")]
        [Tooltip("Obligatorio: se dispara apenas se cumplen sus condiciones (maximo 2 obligatorios por dia; el resto queda pendiente para los dias siguientes). Si no es obligatorio, entra al pool de eventos aleatorios apenas se cumplen sus condiciones.")]
        public bool mandatory;
        [Tooltip("Puede repetirse (por defecto pasa una sola vez y se remueve del pool / de la cola).")]
        public bool repeatable;
        [Tooltip("Todas las condiciones deben cumplirse (AND) para que el evento se dispare (obligatorio) o entre al pool (aleatorio).")]
        public List<EventCondition> conditions = new List<EventCondition>();

        [Header("Opciones")]
        public EventOption[] options;

        void OnValidate() => EnsureIds();

        /// <summary>Asigna un id unico al evento y a cada una de sus opciones si no lo tienen.</summary>
        public void EnsureIds()
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (options != null)
                foreach (var o in options)
                    if (o != null && string.IsNullOrEmpty(o.id))
                        o.id = Guid.NewGuid().ToString("N");
        }
    }
}
