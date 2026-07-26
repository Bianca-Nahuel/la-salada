using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;
using Salada.Util;

namespace Salada.Game
{
    /// <summary>
    /// Overlay que solo aparece en modo construccion (colocando un puesto) y pinta cada celda de
    /// arena segun si el jugador puede construir ahi: celeste = zonas propias, verde = adyacentes
    /// libres, amarillo = adyacentes en disputa, rojo = no se puede.
    /// </summary>
    public class ZoneBuildOverlay : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private PlacementController placement;
        [SerializeField] private TerritoryManager territory;
        [SerializeField] private int sortingOrder = 2;

        [SerializeField] private Color ownColor = new Color(0.30f, 0.80f, 1f, 0.55f);      // celeste: mis zonas
        [SerializeField] private Color adjFreeColor = new Color(0.30f, 1f, 0.35f, 0.55f);  // verde: adyacente libre
        [SerializeField] private Color adjDisputeColor = new Color(1f, 0.90f, 0.20f, 0.60f); // amarillo: adyacente en disputa
        [SerializeField] private Color blockedColor = new Color(1f, 0.30f, 0.30f, 0.50f);  // rojo: no se puede

        private readonly List<(Vector2Int cell, SpriteRenderer sr)> _cells = new List<(Vector2Int, SpriteRenderer)>();
        private GameObject _root;
        private bool _visible;

        void Start()
        {
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            if (territory == null) territory = FindAnyObjectByType<TerritoryManager>();
            Build();
            SetVisible(false);
        }

        void Build()
        {
            if (grid == null || grid.Model == null) return;
            var m = grid.Model;
            _root = new GameObject("ZoneOverlayCells").transform.gameObject;
            _root.transform.SetParent(transform, false);
            float s = m.CellSize * 0.94f;

            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (m.GetCell(cell) != CellType.Grass) continue; // solo arena (donde se construye)
                    var go = new GameObject($"z_{x}_{y}");
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

        void Update()
        {
            bool place = placement != null && placement.CurrentMode == PlacementController.Mode.Place;
            if (place != _visible) SetVisible(place);
            if (place) Refresh();
        }

        void SetVisible(bool v)
        {
            _visible = v;
            if (_root != null) _root.SetActive(v);
        }

        void Refresh()
        {
            if (grid == null || grid.Model == null || territory == null) return;
            var m = grid.Model;
            var tut = placement != null ? placement.TutorialCells : null;
            foreach (var (cell, sr) in _cells)
            {
                if (tut != null) { sr.color = tut.Contains(cell) ? adjFreeColor : blockedColor; continue; } // tutorial: solo la celda permitida en verde
                var cat = territory.ZoneBuildCategory(Owner.Player, m.ZoneOf(cell));
                sr.color = ColorFor(cat);
            }
        }

        Color ColorFor(TerritoryManager.BuildCategory cat)
        {
            switch (cat)
            {
                case TerritoryManager.BuildCategory.Own: return ownColor;
                case TerritoryManager.BuildCategory.AdjacentFree: return adjFreeColor;
                case TerritoryManager.BuildCategory.AdjacentDisputed: return adjDisputeColor;
                default: return blockedColor;
            }
        }
    }
}
