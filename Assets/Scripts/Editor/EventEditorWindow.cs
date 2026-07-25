using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Salada.Game;
using Salada.Combat;

namespace Salada.EditorTools
{
    /// <summary>
    /// Ventana de gestion de eventos: crear/listar personajes y eventos (se editan en el
    /// Inspector), y sincronizar la lista de eventos del EventManager de la escena.
    /// </summary>
    public class EventEditorWindow : EditorWindow
    {
        const string EventsFolder = "Assets/Data/Events";
        const string CharsFolder = "Assets/Data/Characters";
        Vector2 scroll;

        [MenuItem("Salada/Event Editor")]
        static void Open() => GetWindow<EventEditorWindow>("Event Editor");

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Personajes", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Nuevo personaje"))
                CreateAsset<EventCharacter>(CharsFolder, "Personaje");
            foreach (var ch in Load<EventCharacter>(CharsFolder))
            {
                EditorGUILayout.BeginHorizontal();
                var prev = GUI.color; GUI.color = ch.color;
                GUILayout.Label("■", GUILayout.Width(18)); GUI.color = prev;
                if (GUILayout.Button(ch.characterName, EditorStyles.linkLabel)) Select(ch);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Eventos", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Nuevo evento"))
                CreateAsset<GameEvent>(EventsFolder, "Evento");
            foreach (var ev in Load<GameEvent>(EventsFolder))
            {
                EditorGUILayout.BeginHorizontal();
                string tag = ev.mandatory ? "[OBL] " : "";
                int nopt = ev.options != null ? ev.options.Length : 0;
                if (GUILayout.Button($"{tag}{ev.title}  ({nopt} op)", EditorStyles.linkLabel)) Select(ev);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Sincronizar eventos con el EventManager de la escena"))
                SyncManager();

            EditorGUILayout.HelpBox(
                "Crea personajes y eventos, tocalos para editarlos en el Inspector. " +
                "Al terminar, sincroniza para que el EventManager use todos los eventos.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

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

        static void CreateAsset<T>(string folder, string baseName) where T : ScriptableObject
        {
            EnsureFolder(folder);
            var asset = ScriptableObject.CreateInstance<T>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Select(asset);
        }

        static void Select(Object o)
        {
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }

        void SyncManager()
        {
            var em = Object.FindFirstObjectByType<EventManager>();
            if (em == null) { EditorUtility.DisplayDialog("Event Editor", "No hay un EventManager en la escena.", "Ok"); return; }
            var all = Load<GameEvent>(EventsFolder).ToArray();
            var so = new SerializedObject(em);
            var arr = so.FindProperty("events");
            arr.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(em);
            EditorUtility.DisplayDialog("Event Editor", $"Sincronizados {all.Length} eventos con el EventManager.", "Ok");
        }
    }
}
