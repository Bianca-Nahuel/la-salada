using UnityEngine;
using UnityEditor;
using Salada.Game;

namespace Salada.EditorTools
{
    /// <summary>Dibuja una EventCondition mostrando solo los campos relevantes segun el tipo.</summary>
    [CustomPropertyDrawer(typeof(EventCondition))]
    public class EventConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2 + 6;

        public override void OnGUI(Rect rect, SerializedProperty prop, GUIContent label)
        {
            var type = prop.FindPropertyRelative("type");
            var intValue = prop.FindPropertyRelative("intValue");
            var value = prop.FindPropertyRelative("value");
            var meter = prop.FindPropertyRelative("meter");
            var other = prop.FindPropertyRelative("otherEvent");

            float line = EditorGUIUtility.singleLineHeight;
            var r1 = new Rect(rect.x, rect.y, rect.width, line);
            var r2 = new Rect(rect.x, rect.y + line + 4, rect.width, line);
            var r2a = new Rect(r2.x, r2.y, r2.width * 0.55f - 3, line);
            var r2b = new Rect(r2.x + r2.width * 0.55f + 3, r2.y, r2.width * 0.45f - 3, line);

            EditorGUI.PropertyField(r1, type, GUIContent.none);

            switch ((ConditionType)type.enumValueIndex)
            {
                case ConditionType.DayAtLeast:
                    EditorGUI.PropertyField(r2, intValue, new GUIContent("Dia >=")); break;
                case ConditionType.DayEvery:
                    EditorGUI.PropertyField(r2, intValue, new GUIContent("Cada X dias")); break;
                case ConditionType.Probability:
                    value.floatValue = EditorGUI.Slider(r2, "Probabilidad", value.floatValue, 0f, 1f); break;
                case ConditionType.OptionChosen:
                    EditorGUI.PropertyField(r2a, other, GUIContent.none);
                    EditorGUI.PropertyField(r2b, intValue, new GUIContent("Opcion #")); break;
                case ConditionType.EventHappened:
                case ConditionType.EventNotHappened:
                    EditorGUI.PropertyField(r2, other, new GUIContent("Evento")); break;
                case ConditionType.MeterAbove:
                    EditorGUI.PropertyField(r2a, meter, GUIContent.none);
                    EditorGUI.PropertyField(r2b, value, new GUIContent(">")); break;
                case ConditionType.MeterBelow:
                    EditorGUI.PropertyField(r2a, meter, GUIContent.none);
                    EditorGUI.PropertyField(r2b, value, new GUIContent("<")); break;
            }
        }
    }
}
