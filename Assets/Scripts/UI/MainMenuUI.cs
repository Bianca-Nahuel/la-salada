using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Salada.UI
{
    /// <summary>
    /// Menu principal: fondo celeste, el celular en el centro con la pantalla de fondo vacia y los
    /// botones Jugar / Salir encima. De vez en cuando se asoma un personaje por un costado. Se arma
    /// todo en runtime.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Sprite phoneFrame;   // celu fondo (marco)
        [SerializeField] private Sprite phoneScreen;  // pantalla 1 fondo vacio
        [SerializeField] private Sprite[] characters; // personajes que se asoman
        [SerializeField] private string gameScene = "SampleScene";
        [SerializeField] private Color background = new Color(0.45f, 0.78f, 0.96f); // celeste

        [Header("Celular")]
        [SerializeField, Range(0.4f, 1f)] private float phoneHeightFrac = 0.96f; // alto del celu respecto a la pantalla
        const float FrameAspect = 2047f / 1069f;                                  // alto/ancho del marco
        static readonly Vector2 ScreenMin = new Vector2(0.085f, 0.105f);          // area util dentro del marco
        static readonly Vector2 ScreenMax = new Vector2(0.915f, 0.845f);

        [Header("Personajes que se asoman (desde abajo)")]
        [SerializeField] private Vector2 gapRange = new Vector2(1.6f, 4.5f); // segundos entre asomadas
        [SerializeField] private float slideDur = 0.5f;
        [SerializeField] private Vector2 holdRange = new Vector2(1.0f, 2.6f);
        [SerializeField] private float charHeight = 700f;   // alto del personaje (px de referencia)
        [SerializeField, Range(0.1f, 0.95f)] private float peekFrac = 0.62f; // cuanto asoma (fraccion de su alto)

        private Font F => font != null ? font : UIFont.Get();
        private RectTransform _charsRoot;
        private int _lastChar = -1; // ultimo personaje que salio (para no repetir)

        void Start()
        {
            Build();
            if (characters != null && characters.Length > 0) StartCoroutine(PeekLoop());
        }

        void Build()
        {
            // fondo celeste
            var bg = MakeImage(transform, "Bg");
            Stretch(bg.rectTransform);
            bg.color = background;
            bg.sprite = null;

            // raiz de personajes (detras del celu, delante del fondo)
            _charsRoot = new GameObject("Chars", typeof(RectTransform)).GetComponent<RectTransform>();
            _charsRoot.SetParent(transform, false);
            Stretch(_charsRoot);

            // celular centrado
            var phone = MakeImage(transform, "Phone");
            var prt = phone.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            float h = 1080f * phoneHeightFrac;
            prt.sizeDelta = new Vector2(h / FrameAspect, h);
            if (phoneFrame != null) { phone.sprite = phoneFrame; phone.color = Color.white; phone.preserveAspect = true; }
            else phone.color = new Color(0.1f, 0.1f, 0.13f);

            // pantalla de fondo vacio dentro del marco
            var screen = MakeImage(phone.transform, "Screen");
            var srt = screen.rectTransform;
            srt.anchorMin = ScreenMin; srt.anchorMax = ScreenMax; srt.offsetMin = srt.offsetMax = Vector2.zero;
            if (phoneScreen != null) { screen.sprite = phoneScreen; screen.color = Color.white; }
            else screen.color = new Color(0.9f, 0.94f, 1f);

            // titulo + botones sobre la pantalla
            MakeText(screen.transform, "LA SALADA", 50, new Color(1f, 0.86f, 0.35f), FontStyle.Bold, 0.5f, 0.80f, 0.92f, 0.13f);
            MakeButton(screen.transform, "JUGAR", new Color(0.20f, 0.65f, 0.30f), 0.5f, 0.51f, 0.86f, 0.17f, Play);
            MakeButton(screen.transform, "SALIR", new Color(0.62f, 0.28f, 0.28f), 0.5f, 0.31f, 0.86f, 0.17f, Quit);

            MakeText(screen.transform, "v0.1", 14, new Color(0.2f, 0.2f, 0.25f, 0.7f), FontStyle.Normal, 0.5f, 0.06f, 0.4f, 0.05f);
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

        // ---- personajes que se asoman ----

        IEnumerator PeekLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(gapRange.x, gapRange.y));
                yield return StartCoroutine(PeekOnce());
            }
        }

        IEnumerator PeekOnce()
        {
            // elegir un personaje distinto al ultimo (no repetir dos veces seguidas)
            int idx = Random.Range(0, characters.Length);
            if (characters.Length > 1) while (idx == _lastChar) idx = Random.Range(0, characters.Length);
            _lastChar = idx;
            var sprite = characters[idx];
            if (sprite == null) yield break;

            var img = MakeImage(_charsRoot, "Peek");
            img.sprite = sprite; img.color = Color.white; img.preserveAspect = true;
            float aspect = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 0.6f;
            float w = charHeight * aspect;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, charHeight);

            // salen por ABAJO, bien a los costados para no quedar cerca del celu (centrado).
            bool left = Random.value < 0.5f;
            float x = left ? Random.Range(0.05f, 0.23f) : Random.Range(0.77f, 0.95f);
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
            rt.pivot = new Vector2(0.5f, 1f);   // el tope del sprite queda en el borde inferior de la pantalla
            float peek = charHeight * peekFrac; // cuanto sube desde abajo

            yield return SlideY(rt, 0f, peek, slideDur);
            yield return new WaitForSeconds(Random.Range(holdRange.x, holdRange.y));
            yield return SlideY(rt, peek, 0f, slideDur);
            Destroy(img.gameObject);
        }

        IEnumerator SlideY(RectTransform rt, float fromY, float toY, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromY, toY, k));
                yield return null;
            }
            rt.anchoredPosition = new Vector2(0f, toY);
        }

        // ---- helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Image MakeImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        // cx,cy = centro (fraccion del padre); w,h = tamaño (fraccion del padre)
        Text MakeText(Transform parent, string text, int size, Color color, FontStyle style, float cx, float cy, float w, float h)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(cx - w * 0.5f, cy - h * 0.5f); rt.anchorMax = new Vector2(cx + w * 0.5f, cy + h * 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = F; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void MakeButton(Transform parent, string label, Color color, float cx, float cy, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(cx - w * 0.5f, cy - h * 0.5f); rt.anchorMax = new Vector2(cx + w * 0.5f, cy + h * 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bimg = go.GetComponent<Image>();
            bimg.color = color;
            bimg.sprite = RoundedSprite();       // esquinas redondeadas (9-slice)
            bimg.type = Image.Type.Sliced;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var t = textGo.GetComponent<Text>();
            t.font = F; t.text = label; t.color = Color.white; t.fontSize = 36; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        }

        // sprite blanco de rectangulo redondeado (9-slice), generado al vuelo y cacheado
        static Sprite _rounded;
        static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;
            const int s = 48, r = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float dx = fx - Mathf.Clamp(fx, r, s - r);
                    float dy = fy - Mathf.Clamp(fy, r, s - r);
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f); // dentro del radio + antialias
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r)); // border => se 9-slicea sin deformar las esquinas
            return _rounded;
        }
    }
}
