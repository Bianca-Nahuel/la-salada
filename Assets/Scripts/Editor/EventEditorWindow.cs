using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Salada.Game;
using Salada.Combat;

namespace Salada.EditorTools
{
    /// <summary>
    /// Herramienta para construir arboles de eventos completos: crea/edita personajes y eventos
    /// (texto, personaje, opciones con sus consecuencias, obligatorio/repetible y condiciones de
    /// disparo) directamente en esta ventana, y sincroniza la lista con el EventManager de la
    /// escena. Los eventos siempre se disparan al final del dia: los obligatorios apenas cumplen
    /// condiciones (maximo 2 por dia, el resto queda pendiente), y si no hay ninguno se elige 1
    /// evento del pool de aleatorios disponibles.
    /// </summary>
    public class EventEditorWindow : EditorWindow
    {
        const string EventsFolder = "Assets/Data/Events";
        const string CharsFolder = "Assets/Data/Characters";

        Vector2 listScroll;
        Vector2 editScroll;
        Object selected;

        [MenuItem("Salada/Event Editor")]
        static void Open()
        {
            var w = GetWindow<EventEditorWindow>("Event Editor");
            w.minSize = new Vector2(680, 420);
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            DrawList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            DrawEditor();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // ---- Panel izquierdo: listas ----

        void DrawList()
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            EditorGUILayout.LabelField("Personajes", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Nuevo personaje"))
                Select(CreateAsset<EventCharacter>(CharsFolder, "Personaje"));
            foreach (var ch in Load<EventCharacter>(CharsFolder))
            {
                EditorGUILayout.BeginHorizontal();
                var prev = GUI.color; GUI.color = ch.color;
                GUILayout.Label("■", GUILayout.Width(18)); GUI.color = prev;
                if (GUILayout.Button(ch.characterName, RowStyle(ch))) Select(ch);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Eventos", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Nuevo evento"))
            {
                var ev = CreateAsset<GameEvent>(EventsFolder, "Evento");
                ev.EnsureIds();
                EditorUtility.SetDirty(ev);
                Select(ev);
            }
            foreach (var ev in Load<GameEvent>(EventsFolder))
            {
                string tag = ev.mandatory ? "[OBL] " : (ev.repeatable ? "[REP] " : "");
                int nopt = ev.options != null ? ev.options.Length : 0;
                int ncond = ev.conditions != null ? ev.conditions.Count : 0;
                if (GUILayout.Button($"{tag}{ev.title}  ({nopt} op, {ncond} cond)", RowStyle(ev))) Select(ev);
            }

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Sincronizar eventos con el EventManager de la escena"))
                SyncManager();

            EditorGUILayout.HelpBox(
                "Los eventos siempre se disparan al final del dia. Los obligatorios se disparan " +
                "apenas cumplen condiciones (maximo 2 por dia; el resto queda pendiente para los " +
                "dias siguientes). Si ninguno es obligatorio ese dia, se elige 1 evento al azar " +
                "entre los aleatorios cuyas condiciones ya se cumplen (pool). Sincroniza al terminar.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        GUIStyle RowStyle(Object o) => selected == o ? EditorStyles.whiteBoldLabel : EditorStyles.linkLabel;

        void Select(Object o)
        {
            selected = o;
            EditorGUIUtility.PingObject(o);
            GUI.FocusControl(null);
        }

        // ---- Panel derecho: edicion ----

        void DrawEditor()
        {
            editScroll = EditorGUILayout.BeginScrollView(editScroll);
            if (selected is EventCharacter ch) DrawCharacter(ch);
            else if (selected is GameEvent ev) DrawEvent(ev);
            else EditorGUILayout.HelpBox("Selecciona un personaje o evento de la izquierda, o crea uno nuevo.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void DrawCharacter(EventCharacter ch)
        {
            EditorGUILayout.LabelField("Personaje", EditorStyles.boldLabel);
            var so = new SerializedObject(ch);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("characterName"));
            EditorGUILayout.PropertyField(so.FindProperty("color"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(ch);
        }

        void DrawEvent(GameEvent ev)
        {
            ev.EnsureIds();
            var so = new SerializedObject(ev);
            so.Update();

            EditorGUILayout.LabelField("Evento", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id", ev.id, EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(so.FindProperty("title"));
            EditorGUILayout.PropertyField(so.FindProperty("speaker"));
            EditorGUILayout.PropertyField(so.FindProperty("description"));

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(so.FindProperty("mandatory"));
            EditorGUILayout.PropertyField(so.FindProperty("repeatable"));
            EditorGUILayout.HelpBox(
                ev.mandatory
                    ? "Obligatorio: se dispara apenas cumple condiciones (maximo 2 obligatorios por dia; el resto queda pendiente para los dias siguientes)."
                    : "No obligatorio: entra al pool de eventos aleatorios apenas cumple condiciones; se elige 1 del pool cuando no hay obligatorios pendientes ese dia.",
                MessageType.None);

            EditorGUILayout.Space(10);
            DrawConditions(so.FindProperty("conditions"));

            EditorGUILayout.Space(10);
            DrawOptions(so.FindProperty("options"));

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(ev);
        }

        // ---- Condiciones ----

        void DrawConditions(SerializedProperty conditions)
        {
            EditorGUILayout.LabelField($"Condiciones ({conditions.arraySize}) - todas deben cumplirse (AND)", EditorStyles.boldLabel);

            for (int i = 0; i < conditions.arraySize; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawCondition(conditions.GetArrayElementAtIndex(i));
                bool removed = GUILayout.Button("x Eliminar condicion", GUILayout.Width(150));
                EditorGUILayout.EndVertical();
                if (removed) { conditions.DeleteArrayElementAtIndex(i); break; }
            }

            if (GUILayout.Button("+ Agregar condicion"))
                conditions.InsertArrayElementAtIndex(conditions.arraySize);
        }

        void DrawCondition(SerializedProperty cond)
        {
            var type = cond.FindPropertyRelative("type");
            var intValue = cond.FindPropertyRelative("intValue");
            var value = cond.FindPropertyRelative("value");
            var meter = cond.FindPropertyRelative("meter");
            var mode = cond.FindPropertyRelative("mode");
            var events = cond.FindPropertyRelative("events");
            var options = cond.FindPropertyRelative("options");

            EditorGUILayout.PropertyField(type, new GUIContent("Tipo"));

            switch ((ConditionType)type.enumValueIndex)
            {
                case ConditionType.DayAtLeast:
                    EditorGUILayout.PropertyField(intValue, new GUIContent("Dia >="));
                    break;
                case ConditionType.MoneyAbove:
                    EditorGUILayout.PropertyField(value, new GUIContent("Dinero >"));
                    break;
                case ConditionType.MoneyBelow:
                    EditorGUILayout.PropertyField(value, new GUIContent("Dinero <"));
                    break;
                case ConditionType.MeterAbove:
                    EditorGUILayout.PropertyField(meter, new GUIContent("Medidor"));
                    EditorGUILayout.PropertyField(value, new GUIContent("Valor >"));
                    break;
                case ConditionType.MeterBelow:
                    EditorGUILayout.PropertyField(meter, new GUIContent("Medidor"));
                    EditorGUILayout.PropertyField(value, new GUIContent("Valor <"));
                    break;
                case ConditionType.EventsHappened:
                    EditorGUILayout.PropertyField(mode, new GUIContent("Modo (lista de eventos)"));
                    DrawEventList(events);
                    break;
                case ConditionType.OptionsChosen:
                    EditorGUILayout.PropertyField(mode, new GUIContent("Modo (lista de opciones)"));
                    DrawOptionRefList(options);
                    break;
            }
        }

        void DrawEventList(SerializedProperty events)
        {
            EditorGUILayout.LabelField("Eventos:");
            for (int i = 0; i < events.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(events.GetArrayElementAtIndex(i), GUIContent.none);
                bool removed = GUILayout.Button("x", GUILayout.Width(22));
                EditorGUILayout.EndHorizontal();
                if (removed) { events.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("+ Agregar evento", GUILayout.Width(140)))
                events.InsertArrayElementAtIndex(events.arraySize);
        }

        void DrawOptionRefList(SerializedProperty options)
        {
            EditorGUILayout.LabelField("Opciones:");
            for (int i = 0; i < options.arraySize; i++)
            {
                var r = options.GetArrayElementAtIndex(i);
                var gameEventProp = r.FindPropertyRelative("gameEvent");
                var optionIdProp = r.FindPropertyRelative("optionId");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(gameEventProp, GUIContent.none, GUILayout.Width(160));

                var targetEv = gameEventProp.objectReferenceValue as GameEvent;
                if (targetEv != null && targetEv.options != null && targetEv.options.Length > 0)
                {
                    var labels = targetEv.options
                        .Select(o => string.IsNullOrEmpty(o.label) ? "(sin nombre)" : o.label)
                        .ToArray();
                    int current = System.Array.FindIndex(targetEv.options, o => o.id == optionIdProp.stringValue);
                    if (current < 0) current = 0;
                    int picked = EditorGUILayout.Popup(current, labels);
                    optionIdProp.stringValue = targetEv.options[picked].id;
                }
                else
                {
                    EditorGUILayout.LabelField("(elegir un evento con opciones)");
                }

                bool removed = GUILayout.Button("x", GUILayout.Width(22));
                EditorGUILayout.EndHorizontal();
                if (removed) { options.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("+ Agregar opcion", GUILayout.Width(140)))
                options.InsertArrayElementAtIndex(options.arraySize);
        }

        // ---- Opciones del evento ----

        void DrawOptions(SerializedProperty options)
        {
            EditorGUILayout.LabelField($"Opciones ({options.arraySize})", EditorStyles.boldLabel);

            for (int i = 0; i < options.arraySize; i++)
            {
                var opt = options.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");

                var idProp = opt.FindPropertyRelative("id");
                EditorGUILayout.LabelField("Id", string.IsNullOrEmpty(idProp.stringValue) ? "(se asigna al guardar)" : idProp.stringValue, EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("label"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("outcome"));
                EditorGUILayout.LabelField("Cambios en balanzas (+/-)");
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("hostility"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("reputation"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("happiness"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("profit"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("money"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("moneyPerStall"));

                EditorGUILayout.LabelField("Mecanicas especiales (opcional)");
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("salaryIncreasePercent"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("destroyBiggestStall"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("triggerGameOver"));

                EditorGUILayout.LabelField("Efecto especial (opcional, hasta 2)");
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("special"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("specialMagnitude"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("special2"));
                EditorGUILayout.PropertyField(opt.FindPropertyRelative("specialMagnitude2"));
                var oneDayProp = opt.FindPropertyRelative("specialOneDayOnly");
                var permanentProp = opt.FindPropertyRelative("specialPermanent");
                EditorGUILayout.PropertyField(oneDayProp, new GUIContent("Solo por hoy"));
                EditorGUILayout.PropertyField(permanentProp, new GUIContent("Permanente"));
                using (new EditorGUI.DisabledScope(oneDayProp.boolValue || permanentProp.boolValue))
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("specialWaves"));

                bool removed = GUILayout.Button("x Eliminar opcion", GUILayout.Width(140));
                EditorGUILayout.EndVertical();
                if (removed) { options.DeleteArrayElementAtIndex(i); break; }
            }

            if (GUILayout.Button("+ Agregar opcion"))
                options.InsertArrayElementAtIndex(options.arraySize);
        }

        // ---- Helpers de assets ----

        static List<T> Load<T>(string folder) where T : Object
        {
            var list = new List<T>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (var g in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
                list.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));
            return list;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static T CreateAsset<T>(string folder, string baseName) where T : ScriptableObject
        {
            EnsureFolder(folder);
            var asset = ScriptableObject.CreateInstance<T>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        void SyncManager()
        {
            var em = Object.FindAnyObjectByType<EventManager>();
            if (em == null) { EditorUtility.DisplayDialog("Event Editor", "No hay un EventManager en la escena.", "Ok"); return; }

            var all = Load<GameEvent>(EventsFolder);
            foreach (var ev in all) { ev.EnsureIds(); EditorUtility.SetDirty(ev); }
            AssetDatabase.SaveAssets();

            var arr = all.ToArray();
            var so = new SerializedObject(em);
            var prop = so.FindProperty("events");
            prop.arraySize = arr.Length;
            for (int i = 0; i < arr.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(em);
            EditorUtility.DisplayDialog("Event Editor", $"Sincronizados {arr.Length} eventos con el EventManager.", "Ok");
        }
    }
}
