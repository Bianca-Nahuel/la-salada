using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Salada.Combat;
using Salada.Game;
using Salada.UI;

namespace Salada.Placement
{
    /// <summary>
    /// Maneja el click sobre una zona DISPUTADA para abrir el modal de Atacar/Negociar. Solo
    /// actua cuando la colocacion esta en Idle (cursor libre) y estamos en fase Building, asi no
    /// pelea con la construccion/demolicion.
    /// </summary>
    public class TerritoryController : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Camera cam;
        [SerializeField] private PlacementController placement;
        [SerializeField] private WaveManager waves;
        [SerializeField] private TerritoryManager territory;
        [SerializeField] private DisputePopup popup;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            if (waves == null) waves = FindAnyObjectByType<WaveManager>();
            if (territory == null) territory = FindAnyObjectByType<TerritoryManager>();
            if (popup == null) popup = FindAnyObjectByType<DisputePopup>(FindObjectsInactive.Include);
        }

        void Update()
        {
            if (grid == null || grid.Model == null || territory == null || popup == null) return;
            if (placement != null && placement.CurrentMode != PlacementController.Mode.Idle) return;
            if (waves != null && !waves.IsBuilding) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var cell = grid.Model.WorldToCell(ScreenToWorld(mouse.position.ReadValue()));
            char zone = grid.Model.ZoneOf(cell);
            if (zone == '.') return;

            var st = territory.StatusOf(zone);
            bool playerDisputed = st.state == ZoneState.Disputed && (st.ownerA == Owner.Player || st.ownerB == Owner.Player);
            if (playerDisputed) popup.Show(zone, territory, waves);
        }

        Vector2 ScreenToWorld(Vector2 screen)
        {
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(world.x, world.y);
        }
    }
}
