using UnityEngine;

namespace Salada.Placement
{
    /// <summary>Registro en runtime de un puesto colocado en la grilla.</summary>
    public class PlacedStall
    {
        public Vector2Int OriginCell;   // celda inferior-izquierda (ancla)
        public Vector2Int Footprint;    // (ancho, alto) en celdas (ya rotado)
        public Owner Owner;
        public GameObject View;         // referencia a la vista en escena (con StallCombat)
        public Vector2Int Facing;       // direccion de ataque (una cardinal: up/right/down/left)
        public int Cost;                // precio pagado (para reembolso al demoler)

        public PlacedStall(Vector2Int originCell, Vector2Int footprint, Owner owner, GameObject view, Vector2Int facing)
        {
            OriginCell = originCell;
            Footprint = footprint;
            Owner = owner;
            View = view;
            Facing = facing;
        }
    }
}
