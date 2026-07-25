using UnityEngine;

namespace Salada.Util
{
    /// <summary>
    /// Crea la vista placeholder de un puesto: un root sin escalar con un "Body"
    /// escalado al footprint + un marcador "Front" que indica hacia donde ataca.
    /// El root sin escalar evita distorsionar al marcador (hijo).
    /// </summary>
    public static class StallVisual
    {
        public static GameObject Create(string name, Transform parent, Vector2 worldCenter, Vector2 size,
            Color color, Vector2Int facing, int bodyOrder, int markerOrder, float cellSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = PlaceholderSprite.Unit;
            bsr.color = color;
            bsr.sortingOrder = bodyOrder;

            if (facing != Vector2Int.zero)
                AddFrontMarker(root.transform, size, facing, markerOrder, cellSize);

            return root;
        }

        public static void AddFrontMarker(Transform root, Vector2 size, Vector2Int facing, int order, float cellSize)
        {
            float half = (Mathf.Abs(facing.x) > 0 ? size.x : size.y) * 0.5f;
            var off = new Vector2(facing.x, facing.y) * (half + 0.06f);
            var m = new GameObject("Front");
            m.transform.SetParent(root, false);
            m.transform.localPosition = new Vector3(off.x, off.y, 0f);
            float ms = 0.22f * cellSize;
            m.transform.localScale = new Vector3(ms, ms, 1f);
            var msr = m.AddComponent<SpriteRenderer>();
            msr.sprite = PlaceholderSprite.Unit;
            msr.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            msr.sortingOrder = order;
        }
    }
}
