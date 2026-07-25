using System.Collections.Generic;
using UnityEngine;

namespace Salada.Util
{
    /// <summary>
    /// Genera (y cachea) variantes del sprite del "globo" disparo con el borde azul
    /// recoloreado al color de la faccion que dispara. Deja el relleno blanco y el $ negro.
    /// Requiere que la textura base sea readable.
    /// </summary>
    public static class ProjectileArt
    {
        private static readonly Dictionary<(Sprite, int), Sprite> _cache = new Dictionary<(Sprite, int), Sprite>();

        public static Sprite Recolored(Sprite baseSprite, Color color)
        {
            if (baseSprite == null) return null;
            int key = (Mathf.RoundToInt(color.r * 255) << 16) | (Mathf.RoundToInt(color.g * 255) << 8) | Mathf.RoundToInt(color.b * 255);
            if (_cache.TryGetValue((baseSprite, key), out var cached)) return cached;

            var tex = baseSprite.texture;
            if (!tex.isReadable) return baseSprite; // fallback si no es legible

            var src = tex.GetPixels32();
            var dst = new Color32[src.Length];
            var col = (Color32)color;
            for (int i = 0; i < src.Length; i++)
            {
                var p = src[i];
                // "azul" = el azul domina claramente sobre rojo y verde
                bool bluish = p.b > 100 && p.b > p.r + 40 && p.b > p.g + 40;
                dst[i] = bluish ? new Color32(col.r, col.g, col.b, p.a) : p;
            }

            var outTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false) { filterMode = tex.filterMode };
            outTex.SetPixels32(dst);
            outTex.Apply();

            var s = Sprite.Create(outTex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), baseSprite.pixelsPerUnit);
            s.name = baseSprite.name + "_" + key;
            _cache[(baseSprite, key)] = s;
            return s;
        }
    }
}
