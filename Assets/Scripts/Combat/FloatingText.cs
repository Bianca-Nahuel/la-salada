using UnityEngine;

namespace Salada.Combat
{
    /// <summary>Texto de mundo que sube y se desvanece (ej. "+$10" al concretar una venta).</summary>
    public class FloatingText : MonoBehaviour
    {
        private TextMesh _tm;
        private float _life;
        private float _maxLife;
        private float _riseSpeed;
        private Color _color;

        public static void Spawn(Vector3 pos, string text, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = pos + new Vector3(0f, 0.2f, 0f);
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.GetComponent<MeshRenderer>().sortingOrder = 40;
            tm.text = text;
            tm.color = color;
            tm.fontSize = 60;
            tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            var ft = go.AddComponent<FloatingText>();
            ft._tm = tm;
            ft._color = color;
            ft._maxLife = ft._life = 1f;
            ft._riseSpeed = 1.2f;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            float t = Mathf.Clamp01(_life / _maxLife);
            var c = _color; c.a = t;
            _tm.color = c;
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
