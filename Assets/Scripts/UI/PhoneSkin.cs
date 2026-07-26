using UnityEngine;

namespace Salada.UI
{
    /// <summary>
    /// Sprites y fuente del "celular" (skin). Se arma un asset y el PhoneUI lo referencia,
    /// asi no hay que cablear 20 sprites a mano en el componente.
    /// </summary>
    [CreateAssetMenu(fileName = "PhoneSkin", menuName = "Salada/Phone Skin")]
    public class PhoneSkin : ScriptableObject
    {
        [Header("Marco / fondo")]
        public Sprite frame;         // celu fondo (marco + pantalla vacia)

        [Header("Construir / demoler")]
        public Sprite build1, build2, build3, demolish;

        [Header("Otros botones")]
        public Sprite zones;    // boton de ver zonas

        [Header("Medidores (vacio = icono, color = relleno)")]
        public Sprite hostilVacio, hostilColor;         // hostilidad (rojo)
        public Sprite reputacionVacio, reputacionColor; // opiniones (azul)
        public Sprite felicidadVacio, felicidadColor;   // clima laboral (amarillo)
        public Sprite profitVacio, profitColor;         // profit (verde)

        [Header("Boton de oleada (cuadrado, cambia por estado)")]
        public Sprite wavePlay;      // empezar oleada
        public Sprite waveV1, waveV2, waveV3; // velocidades x1 / x2 / x5
        public Sprite waveSkipDay;   // avanzar de dia

        [Header("Fuente")]
        public Font font;            // ComicHelvetic
    }
}
