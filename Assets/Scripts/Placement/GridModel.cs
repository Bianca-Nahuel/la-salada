using System.Collections.Generic;
using UnityEngine;

namespace Salada.Placement
{
    /// <summary>
    /// Datos + logica pura de la grilla de colocacion. Sin rendering ni MonoBehaviour.
    /// Mantiene la capa de tipo de celda (Grass/Aisle) y la capa de ocupacion, mas
    /// utilidades de pathfinding sobre los pasillos.
    /// </summary>
    public class GridModel
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public Vector2 Origin { get; }

        private readonly CellType[,] _cells;        // [x, y]
        private readonly PlacedStall[,] _occupancy; // [x, y], null = vacio
        private readonly List<PlacedStall> _stalls = new List<PlacedStall>();
        private readonly List<Vector2Int> _aisleCells = new List<Vector2Int>();
        private readonly List<Vector2Int> _entrances = new List<Vector2Int>();

        public IReadOnlyList<PlacedStall> Stalls => _stalls;
        public IReadOnlyList<Vector2Int> AisleCells => _aisleCells;
        /// <summary>Celdas de entrada/salida (spawn/exit de clientes).</summary>
        public IReadOnlyList<Vector2Int> EntranceCells => _entrances;

        private static readonly Vector2Int[] Card =
            { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        public GridModel(int width, int height, CellType[,] cells, Vector2 origin, float cellSize, List<Vector2Int> entrances = null)
        {
            Width = width;
            Height = height;
            _cells = cells;
            Origin = origin;
            CellSize = cellSize;
            _occupancy = new PlacedStall[width, height];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (cells[x, y] == CellType.Aisle)
                        _aisleCells.Add(new Vector2Int(x, y));

            if (entrances != null)
                foreach (var e in entrances)
                    if (InBounds(e) && _cells[e.x, e.y] == CellType.Aisle)
                        _entrances.Add(e);
        }

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public CellType GetCell(Vector2Int cell) => _cells[cell.x, cell.y];

        public PlacedStall GetOccupant(Vector2Int cell) =>
            InBounds(cell) ? _occupancy[cell.x, cell.y] : null;

        /// <summary>
        /// True si un puesto con el footprint dado ancla-en-origin puede colocarse:
        /// todas las celdas en limites, todas Grass, todas desocupadas.
        /// </summary>
        public bool CanPlace(Vector2Int origin, Vector2Int footprint, out string reason)
        {
            for (int dx = 0; dx < footprint.x; dx++)
            {
                for (int dy = 0; dy < footprint.y; dy++)
                {
                    var c = new Vector2Int(origin.x + dx, origin.y + dy);
                    if (!InBounds(c)) { reason = $"Fuera de limites en {c}"; return false; }
                    if (_cells[c.x, c.y] == CellType.Aisle) { reason = $"Celda {c} es pasillo"; return false; }
                    if (_occupancy[c.x, c.y] != null) { reason = $"Celda {c} ocupada"; return false; }
                }
            }
            reason = null;
            return true;
        }

        /// <summary>Escribe el puesto en cada celda cubierta. No valida (llamar CanPlace antes).</summary>
        public PlacedStall Place(Vector2Int origin, Vector2Int footprint, Owner owner, GameObject view, Vector2Int facing)
        {
            var stall = new PlacedStall(origin, footprint, owner, view, facing);
            for (int dx = 0; dx < footprint.x; dx++)
                for (int dy = 0; dy < footprint.y; dy++)
                    _occupancy[origin.x + dx, origin.y + dy] = stall;
            _stalls.Add(stall);
            return stall;
        }

        /// <summary>
        /// Remueve el puesto del JUGADOR que ocupa la celda dada. Limpia todas sus celdas
        /// y devuelve el registro (el caller destruye la vista). Neutral/rival/vacio -> null.
        /// </summary>
        public PlacedStall Remove(Vector2Int cell)
        {
            var stall = GetOccupant(cell);
            if (stall == null || stall.Owner != Owner.Player) return null;
            for (int dx = 0; dx < stall.Footprint.x; dx++)
                for (int dy = 0; dy < stall.Footprint.y; dy++)
                    _occupancy[stall.OriginCell.x + dx, stall.OriginCell.y + dy] = null;
            _stalls.Remove(stall);
            return stall;
        }

        // ---- Conversion mundo <-> celda ----

        public Vector2Int WorldToCell(Vector2 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - Origin.x) / CellSize),
                Mathf.FloorToInt((world.y - Origin.y) / CellSize));
        }

        /// <summary>Centro de una celda en coordenadas de mundo.</summary>
        public Vector2 CellToWorldCenter(Vector2Int cell)
        {
            return new Vector2(
                Origin.x + (cell.x + 0.5f) * CellSize,
                Origin.y + (cell.y + 0.5f) * CellSize);
        }

        /// <summary>Centro del area cubierta por un footprint (para ubicar sprites multi-celda).</summary>
        public Vector2 FootprintCenterWorld(Vector2Int origin, Vector2Int footprint)
        {
            return new Vector2(
                Origin.x + (origin.x + footprint.x * 0.5f) * CellSize,
                Origin.y + (origin.y + footprint.y * 0.5f) * CellSize);
        }

        // ---- Pathfinding sobre pasillos ----

        public bool IsAisle(Vector2Int c) => InBounds(c) && _cells[c.x, c.y] == CellType.Aisle;

        /// <summary>Celdas de pasillo que tocan el borde del mapa: bocas de entrada/salida.</summary>
        public List<Vector2Int> GetBorderAisleOpenings()
        {
            var list = new List<Vector2Int>();
            foreach (var c in _aisleCells)
                if (c.x == 0 || c.x == Width - 1 || c.y == 0 || c.y == Height - 1)
                    list.Add(c);
            return list;
        }

        /// <summary>BFS por pasillos. Devuelve la lista de celdas desde start hasta goal, o null.</summary>
        public List<Vector2Int> FindAislePath(Vector2Int start, Vector2Int goal)
        {
            if (!IsAisle(start) || !IsAisle(goal)) return null;
            if (start == goal) return new List<Vector2Int> { start };

            var came = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            came[start] = start;

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == goal) return Reconstruct(came, start, goal);
                foreach (var d in Card)
                {
                    var n = cur + d;
                    if (IsAisle(n) && !came.ContainsKey(n))
                    {
                        came[n] = cur;
                        q.Enqueue(n);
                    }
                }
            }
            return null;
        }

        private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> came, Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var c = goal;
            while (c != start) { path.Add(c); c = came[c]; }
            path.Add(start);
            path.Reverse();
            return path;
        }

        /// <summary>Direccion cardinal hacia el pasillo mas cercano desde un centro de mundo (para orientar precolocados).</summary>
        public Vector2Int NearestAisleDirection(Vector2 worldCenter)
        {
            float best = float.MaxValue;
            Vector2Int bestCell = new Vector2Int(-1, -1);
            foreach (var a in _aisleCells)
            {
                var wc = CellToWorldCenter(a);
                float d = (wc - worldCenter).sqrMagnitude;
                if (d < best) { best = d; bestCell = a; }
            }
            if (bestCell.x < 0) return new Vector2Int(0, -1);
            var delta = CellToWorldCenter(bestCell) - worldCenter;
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(delta.x >= 0 ? 1 : -1, 0)
                : new Vector2Int(0, delta.y >= 0 ? 1 : -1);
        }

        /// <summary>Celda de pasillo mas cercana a un punto de mundo.</summary>
        public Vector2Int NearestAisleCell(Vector2 world)
        {
            float best = float.MaxValue;
            Vector2Int bc = _aisleCells.Count > 0 ? _aisleCells[0] : new Vector2Int(0, 0);
            foreach (var a in _aisleCells)
            {
                float d = (CellToWorldCenter(a) - world).sqrMagnitude;
                if (d < best) { best = d; bc = a; }
            }
            return bc;
        }

        /// <summary>Entrada/salida mas cercana; si no hay entradas, cae a la boca de borde mas cercana.</summary>
        public Vector2Int NearestEntrance(Vector2 world)
        {
            if (_entrances.Count == 0) return NearestBorderOpening(world);
            float best = float.MaxValue;
            Vector2Int bo = _entrances[0];
            foreach (var e in _entrances)
            {
                float d = (CellToWorldCenter(e) - world).sqrMagnitude;
                if (d < best) { best = d; bo = e; }
            }
            return bo;
        }

        /// <summary>Boca de borde mas cercana a un punto de mundo.</summary>
        public Vector2Int NearestBorderOpening(Vector2 world)
        {
            var openings = GetBorderAisleOpenings();
            float best = float.MaxValue;
            Vector2Int bo = openings.Count > 0 ? openings[0] : new Vector2Int(0, 0);
            foreach (var o in openings)
            {
                float d = (CellToWorldCenter(o) - world).sqrMagnitude;
                if (d < best) { best = d; bo = o; }
            }
            return bo;
        }
    }
}
