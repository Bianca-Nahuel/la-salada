using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Salada.UI
{
    /// <summary>
    /// Menu principal (escena aparte). Titulo + Jugar (carga la escena del juego) + Salir.
    /// Se arma en runtime.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private string gameScene = "SampleScene";
        [SerializeField] private Color background = new Color(0.06f, 0.30f, 0.52f);

        private Font F => font != null ? font : UIFont.Get();

        void Start() { Build(); }

        void Build()
        {
            // fondo full-screen
            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = background;

            // banda oscura detras del titulo
            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(transform, false);
            var brt = band.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0.66f); brt.anchorMax = new Vector2(1f, 0.86f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            band.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);

            MakeText("LA SALADA", 72, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, 0.5f, 0.80f, 640, 96);
            MakeText("Crustaceo Store  ·  gestion de puestos", 22, new Color(0.9f, 0.94f, 1f), FontStyle.Normal, 0.5f, 0.70f, 640, 40);

            MakeButton("JUGAR", new Color(0.20f, 0.65f, 0.30f), 0.5f, 0.44f, 340, 78, Play);
            MakeButton("SALIR", new Color(0.55f, 0.28f, 0.28f), 0.5f, 0.30f, 340, 66, Quit);

            MakeText("v0.1", 16, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal, 0.5f, 0.05f, 200, 24);
        }

        void Play() => SceneManager.LoadScene(gameScene);

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        Text MakeText(string text, int size, Color color, FontStyle style, float cx, float cy, float w, float h)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(cx, cy); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            t.font = F; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void MakeButton(string label, Color color, float cx, float cy, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(cx, cy); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var t = textGo.GetComponent<Text>();
            t.font = F; t.text = label; t.color = Color.white; t.fontSize = 34; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        }
    }
}
