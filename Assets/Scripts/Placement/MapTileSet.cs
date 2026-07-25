using UnityEngine;

namespace Salada.Placement
{
    /// <summary>
    /// Set ordenado de sprites de piso. El indice se usa para pintar un tile especifico
    /// por celda (char '0'..'9' en el mapa). Lo usan GridView (render) y el Map Painter (paleta).
    /// </summary>
    [CreateAssetMenu(fileName = "MapTileSet", menuName = "Salada/Map Tile Set")]
    public class MapTileSet : ScriptableObject
    {
        public Sprite[] tiles;
    }
}
