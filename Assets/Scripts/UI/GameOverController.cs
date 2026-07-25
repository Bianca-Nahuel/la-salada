using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Salada.UI
{
    /// <summary>
    /// Pantalla de Game Over: overlay simple con mensaje y boton para reiniciar la escena.
    /// Se autoconstruye la primera vez que se llama a Show(). No hace falta agregarla a la
    /// escena manualmente: el EventManager la crea (AddComponent) si no encuentra una.
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        private GameObject _root;
        private Transform _content;
        private Font _font;

        void EnsureBuilt()
        {
            if (_root != null) return;
            _font = UIFont.Get();

            var canvasGo = new GameObject("GameOverCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // por encima de todo lo demas, incluido EventPopup

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            var rrt = _root.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(_root.transform, false);
            var brt = bd.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(480, 100);
            panel.GetComponent<Image>().color = new Color(0.16f, 0.05f, 0.05f, 1f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20); vlg.spacing = 12;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _content = panel.transform;

            _root.SetActive(false);
        }

        public void Show(string message)
        {
            EnsureBuilt();
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

            AddText("Game Over", 28, new Color(0.95f, 0.35f, 0.3f), FontStyle.Bold, 36);
            AddText(string.IsNullOrEmpty(message) ? "Se acabo la salada para vos." : message, 17, new Color(0.9f, 0.9f, 0.92f), FontStyle.Normal, 50);
            AddButton("Reiniciar", () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });

            _root.SetActive(true);
            Time.timeScale = 0f;
        }

        void AddText(string text, int size, Color color, FontStyle style, float minHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_content, false);
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void AddButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f);
            go.AddComponent<LayoutElement>().minHeight = 50;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
            var t = textGo.GetComponent<Text>();
            t.font = _font; t.text = label; t.color = Color.white; t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
