using UnityEngine;

namespace Salada.Game
{
    /// <summary>Personaje que puede decir un evento (nombre + color identificatorio).</summary>
    [CreateAssetMenu(fileName = "Character", menuName = "Salada/Event Character")]
    public class EventCharacter : ScriptableObject
    {
        public string characterName = "Personaje";
        public Color color = new Color(0.9f, 0.9f, 0.95f);
        [Tooltip("Retrato opcional mostrado por encima del popup de eventos. Si esta vacio, no se muestra nada.")]
        public Sprite sprite;
    }
}
