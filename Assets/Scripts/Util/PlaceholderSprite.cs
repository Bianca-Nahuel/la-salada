using UnityEngine;

namespace Salada.Util
{
    /// <summary>
    /// Provee un Sprite blanco 1x1 (1 unidad de mundo) cacheado, reutilizado para
    /// todo el arte placeholder. Se tinta y escala segun haga falta.
    /// </summary>
    public static class PlaceholderSprite
    {
        private static Sprite _sprite;

        public static Sprite Unit
        {
            get
            {
                if (_sprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point
                    };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    // pixelsPerUnit = 1 => el sprite mide 1x1 unidad de mundo.
                    _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _sprite.name = "PlaceholderUnit";
                }
                return _sprite;
            }
        }
    }
}
