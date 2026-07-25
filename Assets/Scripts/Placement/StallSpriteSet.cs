using UnityEngine;

namespace Salada.Placement
{
    /// <summary>
    /// Sprites de puestos 1x1 por faccion y direccion (frente/espalda/izq/der).
    /// El "frente" mira hacia abajo (Facing 0,-1). Azul=Player, Rojo=Enemy, Amarillo=Neutral.
    /// </summary>
    [CreateAssetMenu(fileName = "StallSpriteSet", menuName = "Salada/Stall Sprite Set")]
    public class StallSpriteSet : ScriptableObject
    {
        [System.Serializable]
        public class Dirs
        {
            public Sprite front;   // frente  (mira abajo)
            public Sprite back;    // espalda (mira arriba)
            public Sprite left;    // izquierda
            public Sprite right;   // derecha
        }

        public Dirs player;   // azul
        public Dirs enemy;    // rojo
        public Dirs neutral;  // amarillo

        public Sprite Get(Owner owner, Vector2Int facing)
        {
            var d = owner == Owner.Player ? player : owner == Owner.Enemy ? enemy : neutral;
            if (d == null) return null;
            if (facing == new Vector2Int(0, 1)) return d.back;
            if (facing == new Vector2Int(-1, 0)) return d.left;
            if (facing == new Vector2Int(1, 0)) return d.right;
            return d.front; // (0,-1) o default
        }
    }
}
