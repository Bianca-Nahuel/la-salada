using UnityEngine;
using Salada.Util;

namespace Salada.Placement
{
    /// <summary>
    /// Renderiza la grilla estatica (pasto/pasillo) y los puestos precolocados
    /// (neutrales/rivales) una sola vez al inicio con sprites placeholder. Los
    /// precolocados se orientan hacia el pasillo mas cercano para que ataquen.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class GridView : MonoBehaviour
    {
        [SerializeField] private int cellSortingOrder = 0;

        [Header("Tiles base (llenan cada celda)")]
        [Tooltip("Tile de MADERA para TODO el piso/camino (Aisle).")]
        [SerializeField] private Sprite woodTile;
        [Tooltip("Tile de ARENA para TODA la arena (Grass).")]
        [SerializeField] private Sprite sandTile;
        [Tooltip("Cuantas repeticiones del tile por celda (modo Tiled). 1 = estirado.")]
        [SerializeField] private float tilesPerCell = 2f;

        [Header("Tiles de pasillo (legacy, fallback si no hay woodTile)")]
        [SerializeField] private MapTileSet tileSet;                 // tiles elegibles por celda (digitos)
        [SerializeField] private Sprite[] aisleTilesHorizontal;      // auto (P): orientacion horizontal
        [SerializeField] private Sprite[] aisleTilesVertical;        // auto (P): orientacion vertical

        [Header("Entradas / rivales")]
        [SerializeField] private Color entranceColor = new Color(0.2f, 0.9f, 1f, 0.45f);
        [SerializeField] private StallData rivalStallData; // 1x1 para los rivales pintados

        private GridManager _grid;

        void Start()
        {
            _grid = GetComponent<GridManager>();
            if (_grid.Model == null) _grid.BuildModel();
            RenderCells();
            RenderPrePlaced();
        }

        void RenderCells()
        {
            var model = _grid.Model;
            var parent = new GameObject("Cells").transform;
            parent.SetParent(transform, false);
            for (int x = 0; x < model.Width; x++)
            {
                for (int y = 0; y < model.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var wc = model.CellToWorldCenter(cell);
                    var go = new GameObject($"Cell_{x}_{y}");
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(wc.x, wc.y, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = cellSortingOrder;

                    bool isAisle = model.GetCell(cell) == CellType.Aisle;
                    Sprite tile;
                    if (isAisle)
                    {
                        // TODO el piso = madera; si no hay woodTile, cae al sistema viejo (digitos/planks)
                        tile = woodTile;
                        if (tile == null)
                        {
                            int fi = _grid.Layout.FloorTileIndex(x, y);
                            tile = (fi >= 0 && tileSet != null && tileSet.tiles != null && fi < tileSet.tiles.Length && tileSet.tiles[fi] != null)
                                ? tileSet.tiles[fi]
                                : PickAisleTile(cell);
                        }
                    }
                    else
                    {
                        tile = sandTile; // TODA la arena = tile de arena
                    }

                    bool baseTile = tile != null && (tile == woodTile || tile == sandTile);
                    if (baseTile && tilesPerCell > 1f)
                    {
                        // modo Tiled: repite el tile a su tamano natural, tilesPerCell veces por celda
                        sr.sprite = tile;
                        var ns = tile.bounds.size;
                        float rep = Mathf.Max(1f, tilesPerCell);
                        go.transform.localScale = new Vector3(
                            ns.x > 0 ? (_grid.CellSize / rep) / ns.x : 1f,
                            ns.y > 0 ? (_grid.CellSize / rep) / ns.y : 1f, 1f);
                        sr.drawMode = SpriteDrawMode.Tiled;
                        sr.size = new Vector2(ns.x * rep, ns.y * rep);
                    }
                    else if (tile != null)
                    {
                        sr.sprite = tile; // estirado: un tile agrandado a la celda
                        var b = tile.bounds.size;
                        go.transform.localScale = new Vector3(b.x > 0 ? _grid.CellSize / b.x : 1f, b.y > 0 ? _grid.CellSize / b.y : 1f, 1f);
                    }
                    else
                    {
                        sr.sprite = PlaceholderSprite.Unit;
                        sr.color = isAisle ? _grid.aisleColor : _grid.grassColor;
                        go.transform.localScale = new Vector3(_grid.CellSize, _grid.CellSize, 1f);
                    }

                    if (_grid.Layout.CharAt(x, y) == 'E') // marcar entrada/salida
                    {
                        var mk = new GameObject($"Entrance_{x}_{y}");
                        mk.transform.SetParent(parent, false);
                        mk.transform.position = new Vector3(wc.x, wc.y, 0f);
                        mk.transform.localScale = new Vector3(_grid.CellSize, _grid.CellSize, 1f);
                        var msr = mk.AddComponent<SpriteRenderer>();
                        msr.sprite = PlaceholderSprite.Unit;
                        msr.color = entranceColor;
                        msr.sortingOrder = cellSortingOrder + 1;
                    }
                }
            }
        }

        /// <summary>Elige un tile segun la orientacion del pasillo (vertical/horizontal) en esa celda.</summary>
        Sprite PickAisleTile(Vector2Int cell)
        {
            var m = _grid.Model;
            bool vert = m.IsAisle(cell + Vector2Int.up) || m.IsAisle(cell + Vector2Int.down);
            bool horz = m.IsAisle(cell + Vector2Int.left) || m.IsAisle(cell + Vector2Int.right);

            Sprite[] set;
            if (vert && !horz) set = aisleTilesVertical;
            else if (horz && !vert) set = aisleTilesHorizontal;
            else set = (aisleTilesHorizontal != null && aisleTilesHorizontal.Length > 0) ? aisleTilesHorizontal : aisleTilesVertical;

            if (set == null || set.Length == 0)
                set = (aisleTilesVertical != null && aisleTilesVertical.Length > 0) ? aisleTilesVertical : aisleTilesHorizontal;
            if (set == null || set.Length == 0) return null;

            int idx = Mathf.Abs(cell.x * 7 + cell.y * 13) % set.Length;
            return set[idx];
        }

        void RenderPrePlaced()
        {
            var model = _grid.Model;
            foreach (var pre in _grid.Layout.prePlaced)
            {
                if (pre.stall == null) continue;
                var footprint = pre.stall.Footprint;
                if (!model.CanPlace(pre.originCell, footprint, out string reason))
                {
                    Debug.LogWarning($"[GridView] No se pudo precolocar {pre.stall.displayName} en {pre.originCell}: {reason}");
                    continue;
                }
                var facing = model.NearestAisleDirection(model.FootprintCenterWorld(pre.originCell, footprint));
                _grid.SpawnStall(pre.originCell, footprint, pre.owner, facing, pre.stall);
            }

            // rivales pintados en el mapa (1x1 sobre arena)
            if (rivalStallData != null)
                foreach (var (cell, owner) in _grid.Layout.RivalStarts())
                {
                    if (!model.CanPlace(cell, Vector2Int.one, out _)) continue;
                    var facing = model.FacingToAisle(cell);
                    _grid.SpawnStall(cell, Vector2Int.one, owner, facing, rivalStallData);
                }
        }
    }
}
