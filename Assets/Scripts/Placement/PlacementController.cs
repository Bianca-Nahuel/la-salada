using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Salada.Combat;

namespace Salada.Placement
{
    /// <summary>
    /// Colocacion/demolicion de puestos del jugador. Solo funciona en fase Building (no
    /// durante una oleada). Comprar descuenta plata; demoler reembolsa una parte. Q/E rotan
    /// el puesto a colocar; click derecho cancela la accion actual (construir o demoler) y
    /// vuelve al cursor libre. La creacion se delega a GridManager.SpawnStall.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        // Idle = cursor libre (sin ghost). Se entra a Place/Demolish desde el celular.
        public enum Mode { Idle, Place, Demolish }

        [SerializeField] private GridManager grid;
        [SerializeField] private GhostPreview ghost;
        [SerializeField] private Camera cam;
        [SerializeField] private Salada.Game.TerritoryManager territory;

        public Mode CurrentMode { get; private set; } = Mode.Idle;
        public StallData SelectedStall { get; private set; }

        private static readonly Vector2Int[] Dirs =
            { new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) };
        private int _rot;
        private WaveManager _waves;

        public Vector2Int Facing => Dirs[_rot];

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            _waves = FindAnyObjectByType<WaveManager>();
            if (territory == null) territory = FindAnyObjectByType<Salada.Game.TerritoryManager>();
            if (ghost != null) ghost.SetCellSize(grid.CellSize);
        }

        float AttackRange => SelectedStall != null ? SelectedStall.attackRange : 2.5f;
        bool CanBuild => _waves == null || _waves.IsBuilding;

        // ---- API publica (el celular llama esto) ----

        public void SelectStall(StallData data)
        {
            SelectedStall = data;
            CurrentMode = Mode.Place;
        }

        public void EnterDemolishMode() => CurrentMode = Mode.Demolish;

        /// <summary>Vuelve al cursor libre (sin ghost).</summary>
        public void Cancel() => CurrentMode = Mode.Idle;

        // ---- Loop ----

        void Update()
        {
            if (grid == null || grid.Model == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Idle o durante la oleada -> cursor libre, sin ghost.
            if (CurrentMode == Mode.Idle || !CanBuild) { ghost.Hide(); return; }

            if (mouse.rightButton.wasPressedThisFrame) { Cancel(); ghost.Hide(); return; } // clic derecho cancela la accion actual

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2Int cell = grid.Model.WorldToCell(ScreenToWorld(mouse.position.ReadValue()));

            if (CurrentMode == Mode.Place)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.qKey.wasPressedThisFrame) _rot = (_rot + 3) % 4; // Q: rota hacia un lado
                    if (kb.eKey.wasPressedThisFrame) _rot = (_rot + 1) % 4; // E: rota hacia el otro
                }
                UpdatePlace(cell, overUI, mouse);
            }
            else UpdateDemolish(cell, overUI, mouse);
        }

        Vector2Int EffectiveFootprint(StallData data)
        {
            var f = data.Footprint;
            return (_rot % 2 == 0) ? f : new Vector2Int(f.y, f.x);
        }

        void UpdatePlace(Vector2Int cell, bool overUI, Mouse mouse)
        {
            if (SelectedStall == null || overUI || !grid.Model.InBounds(cell))
            {
                ghost.Hide();
                return;
            }
            var footprint = EffectiveFootprint(SelectedStall);
            bool affordable = _waves == null || _waves.CanAfford(SelectedStall.cost);
            bool facesAisle = grid.Model.HasAisleInFront(cell, footprint, Facing); // solo mirando a un camino
            bool inZone = territory == null || territory.CanBuild(Owner.Player, cell, footprint, out _); // zona propia/adyacente
            bool valid = grid.Model.CanPlace(cell, footprint, out _) && affordable && facesAisle && inZone;
            var sprite = footprint == Vector2Int.one ? grid.StallSpriteFor(Owner.Player, Facing) : null;
            ghost.Show(grid.Model.FootprintCenterWorld(cell, footprint), StallSize(footprint), valid, Facing, AttackRange, sprite);

            if (valid && mouse.leftButton.wasPressedThisFrame)
                DoPlace(cell, footprint, Facing, SelectedStall);
        }

        void UpdateDemolish(Vector2Int cell, bool overUI, Mouse mouse)
        {
            var occupant = overUI ? null : grid.Model.GetOccupant(cell);
            bool demolishable = occupant != null && occupant.Owner == Owner.Player;

            if (demolishable)
                ghost.Show(grid.Model.FootprintCenterWorld(occupant.OriginCell, occupant.Footprint),
                    StallSize(occupant.Footprint), false, Vector2Int.zero, 0f);
            else
                ghost.Hide();

            if (demolishable && mouse.leftButton.wasPressedThisFrame)
                DoRemove(cell);
        }

        Vector2 StallSize(Vector2Int footprint)
        {
            return new Vector2(
                footprint.x * grid.CellSize - grid.stallInset,
                footprint.y * grid.CellSize - grid.stallInset);
        }

        void DoPlace(Vector2Int cell, Vector2Int footprint, Vector2Int facing, StallData data)
        {
            if (_waves != null && !_waves.CanAfford(data.cost)) return;
            if (grid.SpawnStall(cell, footprint, Owner.Player, facing, data) != null)
            {
                _waves?.Spend(data.cost);
                CurrentMode = Mode.Idle; // una colocacion por seleccion -> vuelve al control normal
            }
        }

        void DoRemove(Vector2Int cell)
        {
            var removed = grid.Model.Remove(cell);
            if (removed == null) return;
            Salada.Combat.DustBurst.Spawn(grid.Model.CellToWorldCenter(removed.OriginCell)); // polvo al demoler
            if (removed.View != null) Destroy(removed.View);
            _waves?.RefundStall(removed.Cost);
            CurrentMode = Mode.Idle; // una demolicion -> vuelve al control normal
        }

        Vector2 ScreenToWorld(Vector2 screen)
        {
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        // ---- Hooks de debug (MCP): bypassean fase y economia ----

        public bool DebugPlace(Vector2Int cell, StallData data, int rot = 0)
        {
            if (data == null) { Debug.LogError("[Debug] StallData null"); return false; }
            var f = data.Footprint;
            var footprint = (rot % 2 == 0) ? f : new Vector2Int(f.y, f.x);
            var facing = Dirs[((rot % 4) + 4) % 4];
            if (grid.SpawnStall(cell, footprint, Owner.Player, facing, data) == null)
            {
                Debug.Log($"[Debug] Place RECHAZADO en {cell} ({data.displayName})");
                return false;
            }
            return true;
        }

        public bool DebugRemove(Vector2Int cell)
        {
            var occ = grid.Model.GetOccupant(cell);
            var removed = grid.Model.Remove(cell);
            if (removed != null && removed.View != null) Destroy(removed.View);
            bool ok = removed != null;
            Debug.Log($"[Debug] Remove en {cell}: {(ok ? "removido" : (occ == null ? "vacio" : $"no removible (owner={occ.Owner})"))}");
            return ok;
        }
    }
}
