using System;
using System.Collections.Generic;
using UnityEngine;

namespace Salada.Game
{
    /// <summary>Tipo de condicion para que se dispare un evento.</summary>
    public enum ConditionType
    {
        DayAtLeast,        // Day >= intValue
        DayEvery,          // Day % intValue == 0 (cada X dias)
        Probability,       // roll: random(0..1) < value
        OptionChosen,      // en otherEvent se eligio la opcion intValue
        EventHappened,     // otherEvent ya paso
        EventNotHappened,  // otherEvent no paso
        MeterAbove,        // medidor 'meter' > value
        MeterBelow         // medidor 'meter' < value
    }

    /// <summary>Una condicion de disparo. Todas las condiciones de un evento deben cumplirse (AND).</summary>
    [Serializable]
    public class EventCondition
    {
        public ConditionType type;
        public int intValue;          // dia / indice de opcion
        public float value = 1f;      // probabilidad (0-1) o valor de medidor
        public MeterType meter;       // para MeterAbove/Below
        public GameEvent otherEvent;  // para OptionChosen/EventHappened/EventNotHappened
    }

    /// <summary>Una opcion/decision de un evento: consecuencias + dialogo final.</summary>
    [Serializable]
    public class EventOption
    {
        public string label = "Opcion";
        [TextArea] public string outcome = ""; // dialogo final tras elegir

        [Header("Cambios en balanzas (+/-)")]
        public float hostility;
        public float reputation;
        public float happiness;
        public float profit;

        [Header("Plata (+/-)")]
        public int money;

        [Header("Efecto especial (opcional)")]
        public EffectType special = EffectType.None;
        public float specialMagnitude = 1f;
        public int specialWaves = 0;
    }

    /// <summary>Un evento: quien lo dice, condiciones de disparo y opciones con consecuencias.</summary>
    [CreateAssetMenu(fileName = "Event", menuName = "Salada/Game Event")]
    public class GameEvent : ScriptableObject
    {
        public string title = "Evento";
        public EventCharacter speaker;
        [TextArea] public string description = "";

        [Header("Disparo")]
        [Tooltip("Obligatorio: siempre se dispara si cumple condiciones (no compite por la probabilidad del dia).")]
        public bool mandatory;
        [Tooltip("Puede repetirse (por defecto pasa una sola vez).")]
        public bool repeatable;
        [Tooltip("Todas deben cumplirse. La probabilidad decide entre eventos opcionales (1 por dia).")]
        public List<EventCondition> conditions = new List<EventCondition>();

        [Header("Opciones")]
        public EventOption[] options;
    }
}
