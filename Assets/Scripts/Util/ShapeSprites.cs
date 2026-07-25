using UnityEngine;

namespace Salada.Util
{
    /// <summary>Sprites de forma cacheados (generados en runtime). Sin assets de arte.</summary>
    public static class ShapeSprites
    {
        private static Sprite _halfDisc;

        /// <summary>
        /// Semicirculo relleno que apunta hacia +Y, con pivot en el centro de la base (borde plano).
        /// A escala 1 el radio = 1 unidad. Se escala por el rango y se rota hacia el frente.
        /// </summary>
        public static Sprite HalfDisc
        {
            get
            {
                if (_halfDisc == null)
                {
                    int w = 128, h = 64;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    var cols = new Color32[w * h];
                    float cx = w * 0.5f;
                    float r = h; // radio en px
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float dx = x + 0.5f - cx;
                            float dy = y + 0.5f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            byte a = (byte)(d <= r ? 255 : 0);
                            cols[y * w + x] = new Color32(255, 255, 255, a);
                        }
                    }
                    tex.SetPixels32(cols);
                    tex.Apply();
                    // pixelsPerUnit = h => alto = 1 unidad (radio), ancho = 2 unidades. Pivot base-centro.
                    _halfDisc = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
                    _halfDisc.name = "HalfDisc";
                }
                return _halfDisc;
            }
        }

        /// <summary>Rotacion Z (grados) para que el +Y local apunte hacia 'facing' (cardinal).</summary>
        public static float FacingAngle(Vector2Int facing)
        {
            return Mathf.Atan2(-facing.x, facing.y) * Mathf.Rad2Deg;
        }
    }
}
