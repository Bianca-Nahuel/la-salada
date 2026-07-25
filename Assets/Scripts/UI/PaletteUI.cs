using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salada.Placement;

namespace Salada.UI
{
    /// <summary>
    /// Panel izquierdo: construye un boton por footprint de la paleta (-> SelectStall)
    /// mas un boton "Demoler" (-> EnterDemolishMode). Resalta el modo activo.
    /// Debe colgar de un GameObject con VerticalLayoutGroup.
    /// </summary>
    public class PaletteUI : MonoBehaviour
    {
        [SerializeField] private PlacementController placement;
        [SerializeField] private StallData[] palette;
        [SerializeField] private Color stallButtonColor = new Color(0.10f, 0.85f, 0.95f);
        [SerializeField] private Color demolishButtonColor = new Color(0.90f, 0.30f, 0.20f);

        private readonly List<Button> _buttons = new List<Button>();

        void Start()
        {
            if (placement == null) placement = FindAnyObjectByType<PlacementController>();
            BuildButtons();
        }

        void BuildButtons()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (palette != null)
            {
                foreach (var stall in palette)
                {
                    if (stall == null) continue;
                    var s = stall; // captura local para el closure
                    var label = $"{s.displayName} ({s.footprintWidth}x{s.footprintHeight})";
                    CreateButton(label, stallButtonColor, font, () => placement.SelectStall(s));
                }
            }

            CreateButton("Demoler", demolishButtonColor, font, () => placement.EnterDemolishMode());
        }

        void CreateButton(string label, Color color, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            go.GetComponent<Image>().color = color;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 48;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            _buttons.Add(btn);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 18;
        }
    }
}
