using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Salada.Combat;
using Salada.Game;
using Salada.UI;
using Salada.Util;

namespace Salada.Placement
{
    /// <summary>
    /// Modo "ver zonas" del celular. Mientras esta activo pinta cada zona con el color de su
    /// dueño (disputadas = mezcla de las dos facciones) y, al clickear una zona, abre el modal
    /// con dueño/poder/paciencia. Las batallas SOLO se pueden iniciar desde aca (ya no clickeando
    /// una zona suelta). Click derecho sale del modo.
    /// </summary>
    public class TerritoryController : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Camera cam;
        [SerializeField] private PlacementController placement;
        [SerializeField] private WaveManager waves;
        [SerializeField] private TerritoryManager territory;
        [SerializeField] private DisputePopup popup;
        [SerializeField] private int sortingOrder = 3;

        [SerializeField, Range(0f, 1f)] private float baseAlpha = 0.40f;   // opacidad normal de cada zona
        [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.72f;  // opacidad de la zona bajo el mouse

        private readonly List<(Vector2Int cell, SpriteRenderer sr)> _cells = new List<(Vector2Int, SpriteRenderer)>();
        private GameObject _root;
        private bool _active;
        private char _hoverZone = '.'; // zona bajo el mouse (se resalta)

        /// <summary>True mientras el modo "ver zonas" esta activo (lo lee el celular para retraerse).</summary>
        public bool Active => _active;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            if (waves == null) waves = FindAnyObjectByType<WaveManager>();
            if (territory == null) territory = FindAnyObjectByType<TerritoryManager>();
            if (popup == null) popup = FindAnyObjectByType<DisputePopup>(FindObjectsInactive.Include);
            BuildOverlay();
            SetOverlayVisible(false);
        }

        void BuildOverlay()
        {
            if (grid == null || grid.Model == null) return;
            var m = grid.Model;
            _root = new GameObject("ZoneViewCells");
            _root.transform.SetParent(transform, false);
            float s = m.CellSize * 0.96f;

            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (m.GetCell(cell) != CellType.Grass || m.ZoneOf(cell) == '.') continue;
                    var go = new GameObject($"zv_{x}_{y}");
                    go.transform.SetParent(_root.transform, false);
                    var wc = m.CellToWorldCenter(cell);
                    go.transform.position = new Vector3(wc.x, wc.y, 0f);
                    go.transform.localScale = new Vector3(s, s, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = PlaceholderSprite.Unit;
                    sr.sortingOrder = sortingOrder;
                    _cells.Add((cell, sr));
                }
        }

        // ---- API publica (el celular llama esto) ----

        /// <summary>Entra/sale del modo ver-zonas.</summary>
        public void ToggleZoneView()
        {
            if (_active) ExitZoneView();
            else EnterZoneView();
        }

        public void EnterZoneView()
        {
            if (placement != null) placement.Cancel(); // salir de construir/demoler
            _active = true;
            SetOverlayVisible(true);
            RefreshColors();
        }

        public void ExitZoneView()
        {
            _active = false;
            SetOverlayVisible(false);
            if (popup != null) popup.Hide();
        }

        void Update()
        {
            if (!_active) return;
            if (grid == null || grid.Model == null || territory == null) return;

            // si se entra a construir/demoler (ej. tocaste un puesto en el celu), salir del modo
            if (placement != null && placement.CurrentMode != PlacementController.Mode.Idle) { ExitZoneView(); return; }

            var mouse = Mouse.current;
            // overUI = sobre el celu o un modal (el backdrop del popup/minijuego tapa la pantalla)
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // zona bajo el mouse (para el hover)
            _hoverZone = '.';
            if (mouse != null && !overUI)
                _hoverZone = grid.Model.ZoneOf(grid.Model.WorldToCell(ScreenToWorld(mouse.position.ReadValue())));

            RefreshColors();

            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame && !overUI) { ExitZoneView(); return; } // click derecho en el mapa sale
            if (!mouse.leftButton.wasPressedThisFrame || overUI) return;

            char zone = grid.Model.ZoneOf(grid.Model.WorldToCell(ScreenToWorld(mouse.position.ReadValue())));
            if (zone != '.' && popup != null) popup.Show(zone, territory, waves);
        }

        void SetOverlayVisible(bool v) { if (_root != null) _root.SetActive(v); }

        void RefreshColors()
        {
            foreach (var (cell, sr) in _cells)
            {
                char z = grid.Model.ZoneOf(cell);
                Color c = ZonePalette(z);
                if (z == _hoverZone) { c = Color.Lerp(c, Color.white, 0.35f); c.a = hoverAlpha; } // brilla en hover
                else c.a = baseAlpha;
                sr.color = c;
            }
        }

        /// <summary>Color propio y distinto de cada zona (hue deterministico por su id).</summary>
        static Color ZonePalette(char zone)
        {
            float hue = (zone * 0.61803398875f) % 1f; // razon aurea: reparte los tonos bien separados
            if (hue < 0f) hue += 1f;
            return Color.HSVToRGB(hue, 0.6f, 0.95f);
        }

        Vector2 ScreenToWorld(Vector2 screen)
        {
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(world.x, world.y);
        }
    }
}
