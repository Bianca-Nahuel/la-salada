using UnityEngine;
using Salada.Util;

namespace Salada.Placement
{
    /// <summary>
    /// Fantasma de colocacion: cuerpo semitransparente (verde/rojo), marcador del frente y
    /// un semicirculo que muestra el rango de ataque hacia adelante. El root no se escala.
    /// </summary>
    public class GhostPreview : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 10;
        [SerializeField] private Color validColor = new Color(0.1f, 1f, 0.1f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.1f, 0.1f, 0.5f);
        [SerializeField] private Color rangeColor = new Color(0.2f, 0.9f, 1f, 0.16f);

        private SpriteRenderer _body;
        private Transform _marker;
        private SpriteRenderer _markerSr;
        private Transform _range;
        private SpriteRenderer _rangeSr;
        private float _cellSize = 1f;

        void Awake()
        {
            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null) rootSr.enabled = false;

            _range = NewChild("Range", out _rangeSr, ShapeSprites.HalfDisc, sortingOrder - 1);
            _rangeSr.color = rangeColor;

            var bodyT = NewChild("Body", out _body, PlaceholderSprite.Unit, sortingOrder);

            _marker = NewChild("Front", out _markerSr, PlaceholderSprite.Unit, sortingOrder + 1);
            _markerSr.color = new Color(0.1f, 0.1f, 0.12f, 0.8f);

            Hide();
        }

        Transform NewChild(string name, out SpriteRenderer sr, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go.transform;
        }

        public void SetCellSize(float cellSize) => _cellSize = cellSize;

        public void Show(Vector2 worldCenter, Vector2 size, bool isValid, Vector2Int facing, float range)
        {
            transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
            _body.transform.localScale = new Vector3(size.x, size.y, 1f);
            _body.color = isValid ? validColor : invalidColor;
            _body.enabled = true;

            if (facing != Vector2Int.zero)
            {
                float half = (Mathf.Abs(facing.x) > 0 ? size.x : size.y) * 0.5f;
                var off = new Vector2(facing.x, facing.y) * (half + 0.06f);
                _marker.localPosition = new Vector3(off.x, off.y, 0f);
                float ms = 0.22f * _cellSize;
                _marker.localScale = new Vector3(ms, ms, 1f);
                _markerSr.enabled = true;
            }
            else _markerSr.enabled = false;

            if (range > 0f && facing != Vector2Int.zero)
            {
                _range.localScale = new Vector3(range, range, 1f);
                _range.localRotation = Quaternion.Euler(0f, 0f, ShapeSprites.FacingAngle(facing));
                _rangeSr.enabled = true;
            }
            else _rangeSr.enabled = false;
        }

        public void Hide()
        {
            if (_body != null) _body.enabled = false;
            if (_markerSr != null) _markerSr.enabled = false;
            if (_rangeSr != null) _rangeSr.enabled = false;
        }
    }
}
