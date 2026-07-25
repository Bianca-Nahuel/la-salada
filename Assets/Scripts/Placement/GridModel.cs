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
        private readonly char[,] _zones;            // [x, y], '.' = sin zona
        private readonly PlacedStall[,] _occupancy; // [x, y], null = vacio
        private readonly List<PlacedStall> _stalls = new List<PlacedStall>();
        private readonly List<Vector2Int> _aisleCells = new List<Vector2Int>();
        private readonly List<Vector2Int> _entrances = new List<Vector2Int>();

        // Zonas (capa de territorio): celdas por zona + grafo de adyacencia entre zonas.
        private readonly List<char> _zoneIds = new List<char>();
        private readonly Dictionary<char, List<Vector2Int>> _zoneCells = new Dictionary<char, List<Vector2Int>>();
        private readonly Dictionary<char, HashSet<char>> _zoneAdj = new Dictionary<char, HashSet<char>>();

        public IReadOnlyList<PlacedStall> Stalls => _stalls;
        public IReadOnlyList<Vector2Int> AisleCells => _aisleCells;
        /// <summary>Celdas de entrada/salida (spawn/exit de clientes).</summary>
        public IReadOnlyList<Vector2Int> EntranceCells => _entrances;
        /// <summary>Ids de zona pintados (sin '.').</summary>
        public IReadOnlyList<char> Zones => _zoneIds;

        private static readonly Vector2Int[] Card =
            { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        public GridModel(int width, int height, CellType[,] cells, Vector2 origin, float cellSize,
            List<Vector2Int> entrances = null, char[,] zones = null)
        {
            Width = width;
            Height = height;
            _cells = cells;
            Origin = origin;
            CellSize = cellSize;
            _occupancy = new PlacedStall[width, height];

            _zones = new char[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    _zones[x, y] = zones != null ? zones[x, y] : '.';
                    if (cells[x, y] == CellType.Aisle)
                        _aisleCells.Add(new Vector2Int(x, y));
                }

            BuildZoneIndex();

            if (entrances != null)
                foreach (var e in entrances)
                    if (InBounds(e) && _cells[e.x, e.y] == CellType.Aisle)
                        _entrances.Add(e);
        }

        void BuildZoneIndex()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    char z = _zones[x, y];
                    if (z == '.') continue;
                    if (!_zoneCells.TryGetValue(z, out var cells)) { cells = new List<Vector2Int>(); _zoneCells[z] = cells; _zoneIds.Add(z); }
                    cells.Add(new Vector2Int(x, y));

                    // adyacencia: si un vecino ortogonal es otra zona pintada, enlazar.
                    foreach (var d in Card)
                    {
                        var n = new Vector2Int(x + d.x, y + d.y);
                        if (!InBounds(n)) continue;
                        char nz = _zones[n.x, n.y];
                        if (nz == '.' || nz == z) continue;
                        Link(z, nz); Link(nz, z);
                    }
                }
        }

        void Link(char a, char b)
        {
            if (!_zoneAdj.TryGetValue(a, out var set)) { set = new HashSet<char>(); _zoneAdj[a] = set; }
            set.Add(b);
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

        /// <summary>
        /// Remueve el puesto de CUALQUIER dueño que ocupa la celda (para disputas de territorio).
        /// Limpia sus celdas y devuelve el registro; el caller destruye la vista.
        /// </summary>
        public PlacedStall RemoveAny(Vector2Int cell)
        {
            var stall = GetOccupant(cell);
            if (stall == null) return null;
            for (int dx = 0; dx < stall.Footprint.x; dx++)
                for (int dy = 0; dy < stall.Footprint.y; dy++)
                    _occupancy[stall.OriginCell.x + dx, stall.OriginCell.y + dy] = null;
            _stalls.Remove(stall);
            return stall;
        }

        // ---- Zonas (territorio) ----

        /// <summary>Id de zona de una celda; '.' si no tiene o está fuera de límites.</summary>
        public char ZoneOf(Vector2Int cell) => InBounds(cell) ? _zones[cell.x, cell.y] : '.';

        public bool HasZone(Vector2Int cell) => ZoneOf(cell) != '.';

        public IReadOnlyList<Vector2Int> CellsOfZone(char zone) =>
            _zoneCells.TryGetValue(zone, out var c) ? c : System.Array.Empty<Vector2Int>();

        /// <summary>Zonas directamente adyacentes (distancia 1).</summary>
        public IReadOnlyCollection<char> ZoneNeighbors(char zone) =>
            _zoneAdj.TryGetValue(zone, out var s) ? (IReadOnlyCollection<char>)s : System.Array.Empty<char>();

        /// <summary>Distancia en el grafo de zonas (0 si igual, int.MaxValue si no conectadas).</summary>
        public int ZoneDistance(char a, char b)
        {
            if (a == b) return a == '.' ? int.MaxValue : 0;
            if (a == '.' || b == '.' || !_zoneAdj.ContainsKey(a)) return int.MaxValue;
            var dist = new Dictionary<char, int> { [a] = 0 };
            var q = new Queue<char>();
            q.Enqueue(a);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == b) return dist[cur];
                if (!_zoneAdj.TryGetValue(cur, out var neigh)) continue;
                foreach (var n in neigh)
                    if (!dist.ContainsKey(n)) { dist[n] = dist[cur] + 1; q.Enqueue(n); }
            }
            return int.MaxValue;
        }

        /// <summary>Centro (mundo) de una zona = promedio de los centros de sus celdas.</summary>
        public Vector2 ZoneCentroidWorld(char zone)
        {
            var cells = CellsOfZone(zone);
            if (cells.Count == 0) return Origin;
            Vector2 sum = Vector2.zero;
            foreach (var c in cells) sum += CellToWorldCenter(c);
            return sum / cells.Count;
        }

        /// <summary>Cantidad de puestos de un dueño cuya celda origen cae en la zona.</summary>
        public int StallCountInZone(char zone, Owner owner)
        {
            int n = 0;
            foreach (var s in _stalls)
                if (s.Owner == owner && ZoneOf(s.OriginCell) == zone) n++;
            return n;
        }

        /// <summary>Dueños distintos con al menos un puesto en la zona.</summary>
        public void OwnersInZone(char zone, HashSet<Owner> into)
        {
            into.Clear();
            foreach (var s in _stalls)
                if (ZoneOf(s.OriginCell) == zone) into.Add(s.Owner);
        }

        /// <summary>Puestos de un dueño en una zona (para destruirlos en una disputa).</summary>
        public List<PlacedStall> StallsInZone(char zone, Owner owner)
        {
            var list = new List<PlacedStall>();
            foreach (var s in _stalls)
                if (s.Owner == owner && ZoneOf(s.OriginCell) == zone) list.Add(s);
            return list;
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

        /// <summary>Celda de piso justo frente al borde-frontal del footprint (segun facing).</summary>
        public Vector2Int FrontCell(Vector2Int origin, Vector2Int footprint, Vector2Int facing)
        {
            Vector2Int edge;
            if (facing.x > 0) edge = origin + new Vector2Int(footprint.x - 1, (footprint.y - 1) / 2);
            else if (facing.x < 0) edge = origin + new Vector2Int(0, (footprint.y - 1) / 2);
            else if (facing.y > 0) edge = origin + new Vector2Int((footprint.x - 1) / 2, footprint.y - 1);
            else edge = origin + new Vector2Int((footprint.x - 1) / 2, 0);
            return edge + facing;
        }

        /// <summary>True si al menos una celda adyacente al borde frontal (en direccion facing) es piso.</summary>
        public bool HasAisleInFront(Vector2Int origin, Vector2Int footprint, Vector2Int facing)
        {
            if (facing.x != 0)
            {
                int x = (facing.x > 0 ? origin.x + footprint.x - 1 : origin.x) + facing.x;
                for (int dy = 0; dy < footprint.y; dy++)
                    if (IsAisle(new Vector2Int(x, origin.y + dy))) return true;
            }
            else
            {
                int y = (facing.y > 0 ? origin.y + footprint.y - 1 : origin.y) + facing.y;
                for (int dx = 0; dx < footprint.x; dx++)
                    if (IsAisle(new Vector2Int(origin.x + dx, y))) return true;
            }
            return false;
        }

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
