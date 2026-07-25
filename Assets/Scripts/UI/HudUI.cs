using UnityEngine;
using UnityEngine.UI;
using Salada.Combat;

namespace Salada.UI
{
    /// <summary>
    /// Barra superior: muestra plata, oleada y contadores de ventas. Construye el Text
    /// en runtime y lo actualiza cada frame desde el WaveManager.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        [SerializeField] private WaveManager waves;
        private Text _text;

        void Start()
        {
            if (waves == null) waves = FindFirstObjectByType<WaveManager>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HudText", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 40);
            rt.anchoredPosition = new Vector2(0, -6);

            _text = go.GetComponent<Text>();
            _text.font = font;
            _text.fontSize = 22;
            _text.color = Color.white;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void Update()
        {
            if (waves == null || _text == null) return;
            _text.text = $"Plata: ${waves.Money}    Oleada: {waves.Wave}    " +
                         $"Ventas: {waves.SalesWon}   Perdidas: {waves.SalesLost}   Escaparon: {waves.Escaped}";
        }
    }
}
