using UnityEngine;

namespace Salada.UI
{
    /// <summary>Fuente unica para toda la UI del juego (Text legado, no TMP).</summary>
    public static class UIFont
    {
        const string ResourcePath = "ComicHelvetic_Medium";
        private static Font _font;

        public static Font Get()
        {
            if (_font == null) _font = Resources.Load<Font>(ResourcePath);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // fallback
            return _font;
        }
    }
}
