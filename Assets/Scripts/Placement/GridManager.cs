using UnityEngine;
using Salada.Combat;

namespace Salada.Placement
{
    /// <summary>
    /// Dueno del GridModel en la escena. Guarda cellSize + origen (su transform) y los
    /// colores por estado. Construye el modelo desde el MapLayout en Awake.
    /// Unica fuente de verdad.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [SerializeField] private MapLayout mapLayout;
        [SerializeField] private float cellSize = 1f;

        [Header("Estetica de puestos")]
        [Tooltip("Margen (en unidades) que se resta al tamano del puesto para que se vea el pasto entre puestos.")]
        public float stallInset = 0.08f;

        [Header("Colores")]
        public Color grassColor = new Color(0.85f, 0.76f, 0.52f); // arena
        public Color aisleColor = new Color(0.36f, 0.20f, 0.16f);
        public Color playerColor = new Color(0.10f, 0.85f, 0.95f); // cyan = vos
        public Color neutralColor = new Color(0.95f, 0.85f, 0.15f); // amarillo = rival "distinto"
        public Color enemyColor = new Color(0.90f, 0.10f, 0.10f);   // rojo = rival

        [Header("Spawn")]
        [SerializeField] private int spawnedStallSortingOrder = 3;
        private Transform _spawnedRoot;

        public GridModel Model { get; private set; }
        public MapLayout Layout => mapLayout;
        public float CellSize => cellSize;

        void Awake()
        {
            BuildModel();
        }

        public void BuildModel()
        {
            if (mapLayout == null)
            {
                Debug.LogError("[GridManager] MapLayout no asignado.");
                return;
            }
            var origin = (Vector2)transform.position;
            var cells = mapLayout.BuildCells();
            Model = new GridModel(mapLayout.width, mapLayout.height, cells, origin, cellSize, mapLayout.EntranceCells());
        }

        public Color ColorFor(Owner owner)
        {
            switch (owner)
            {
                case Owner.Player: return playerColor;
                case Owner.Neutral: return neutralColor;
                case Owner.Enemy: return enemyColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// Crea la vista, registra el puesto en el modelo y le engancha su StallCombat
        /// (cada puesto gestiona sus propios ataques). Devuelve null si no se puede colocar.
        /// </summary>
        public PlacedStall SpawnStall(Vector2Int cell, Vector2Int footprint, Owner owner, Vector2Int facing, StallData data)
        {
            if (Model == null || !Model.CanPlace(cell, footprint, out _)) return null;
            if (_spawnedRoot == null) _spawnedRoot = new GameObject("SpawnedStalls").transform;

            var center = Model.FootprintCenterWorld(cell, footprint);
            var size = new Vector2(footprint.x * cellSize - stallInset, footprint.y * cellSize - stallInset);
            var view = Salada.Util.StallVisual.Create($"Stall_{owner}_{cell.x}_{cell.y}", _spawnedRoot,
                center, size, ColorFor(owner), facing, spawnedStallSortingOrder, spawnedStallSortingOrder + 1, cellSize);

            var stall = Model.Place(cell, footprint, owner, view, facing);
            if (data != null)
            {
                stall.Cost = data.cost;
                view.AddComponent<StallCombat>().Init(this, stall, data);
            }
            return stall;
        }
    }
}
