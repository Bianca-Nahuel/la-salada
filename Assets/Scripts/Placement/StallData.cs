using UnityEngine;

namespace Salada.Placement
{
    /// <summary>
    /// Definicion de un tipo de puesto: su footprint (tamano en celdas), nombre y stats de
    /// combate. Cada puesto colocado gestiona sus propios ataques con estos valores base.
    /// </summary>
    [CreateAssetMenu(fileName = "Stall", menuName = "Salada/Stall Data")]
    public class StallData : ScriptableObject
    {
        public string displayName = "Puesto";
        [Min(1)] public int footprintWidth = 1;
        [Min(1)] public int footprintHeight = 1;

        [Header("Economia")]
        public int cost = 20;

        [Header("Combate (base por tipo de puesto)")]
        public float attackDamage = 25f;
        public float attackRange = 2.5f;
        public float attackCooldown = 0.7f;

        public Vector2Int Footprint => new Vector2Int(footprintWidth, footprintHeight);
    }
}
