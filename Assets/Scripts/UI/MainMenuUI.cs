using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Salada.UI
{
    /// <summary>
    /// Menu principal: fondo de cangrejitos que se mueve en diagonal (45º) e infinito, el celular
    /// (sprite) en el centro y los botones Jugar / Salir (sprites con texto integrado, sin texto
    /// aparte). De vez en cuando se asoma un personaje desde abajo. Se arma todo en runtime.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Texture bgTexture;    // fondo menu (cangrejitos), tileado
        [SerializeField] private Sprite phoneSprite;   // CEL menu
        [SerializeField] private Sprite playSprite;    // BOTON JUGAR
        [SerializeField] private Sprite quitSprite;    // BOTON SLAIR
        [SerializeField] private Sprite[] characters;  // personajes que se asoman
        [SerializeField] private string gameScene = "SampleScene";

        [Header("Fondo animado (scroll diagonal)")]
        [SerializeField] private float scrollSpeed = 0.06f;         // velocidad del scroll (unidades uv/seg)
        [SerializeField] private Vector2 scrollDir = new Vector2(1f, 1f); // 45º; invertir signos para cambiar sentido
        [SerializeField] private float tilePixels = 400f;          // tamaño de cada baldosa del fondo en px (mas alto = cangrejos mas grandes)

        [Header("Celular / botones")]
        [SerializeField, Range(0.4f, 1f)] private float phoneHeightFrac = 0.94f;
        [SerializeField] private Vector2 playPos = new Vector2(0.5f, 0.45f); // centro del boton JUGAR (fraccion del celu)
        [SerializeField] private Vector2 quitPos = new Vector2(0.5f, 0.30f);
        [SerializeField] private float buttonWidthFrac = 0.62f;    // ancho del boton (fraccion del ancho del celu)

        [Header("Personajes que se asoman (desde abajo)")]
        [SerializeField] private Vector2 gapRange = new Vector2(1.6f, 4.5f);
        [SerializeField] private float slideDur = 0.5f;
        [SerializeField] private Vector2 holdRange = new Vector2(1.0f, 2.6f);
        [SerializeField] private float charHeight = 700f;
        [SerializeField, Range(0.1f, 0.95f)] private float peekFrac = 0.62f;

        const float FrameAspect = 2047f / 1069f;

        private RawImage _bg;
        private float _uvT;
        private RectTransform _charsRoot;
        private int _lastChar = -1;

        void Start()
        {
            Build();
            if (characters != null && characters.Length > 0) StartCoroutine(PeekLoop());
        }

        void Build()
        {
            // fondo scrolleable (cangrejitos en diagonal, infinito)
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(RawImage));
            bgGo.transform.SetParent(transform, false);
            Stretch(bgGo.GetComponent<RectTransform>());
            _bg = bgGo.GetComponent<RawImage>();
            _bg.raycastTarget = false;
            if (bgTexture != null) { bgTexture.wrapMode = TextureWrapMode.Repeat; _bg.texture = bgTexture; }
            else _bg.color = new Color(0.45f, 0.78f, 0.96f);

            // raiz de personajes (detras del celu, delante del fondo)
            _charsRoot = new GameObject("Chars", typeof(RectTransform)).GetComponent<RectTransform>();
            _charsRoot.SetParent(transform, false);
            Stretch(_charsRoot);

            // celular centrado
            var phone = MakeImage(transform, "Phone");
            var prt = phone.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            float h = 1080f * phoneHeightFrac;
            if (phoneSprite != null)
            {
                phone.sprite = phoneSprite; phone.color = Color.white; phone.preserveAspect = true;
                float aspect = phoneSprite.rect.height > 0 ? phoneSprite.rect.width / phoneSprite.rect.height : 1f / FrameAspect;
                prt.sizeDelta = new Vector2(h * aspect, h);
            }
            else { phone.color = new Color(0.1f, 0.1f, 0.13f); prt.sizeDelta = new Vector2(h / FrameAspect, h); }

            // botones (sprites con texto integrado)
            MakeSpriteButton(phone.transform, playSprite, playPos, buttonWidthFrac, Play);
            MakeSpriteButton(phone.transform, quitSprite, quitPos, buttonWidthFrac, Quit);
        }

        void Update()
        {
            if (_bg == null || bgTexture == null) return;
            var r = _bg.rectTransform.rect;
            float tilesX = tilePixels > 1f ? r.width / tilePixels : 4f;
            float tilesY = tilePixels > 1f ? r.height / tilePixels : 4f;
            _uvT += Time.deltaTime * scrollSpeed;
            _bg.uvRect = new Rect(_uvT * scrollDir.x, _uvT * scrollDir.y, tilesX, tilesY); // 45º con dir (1,1)
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

        // ---- botones con sprite ----

        void MakeSpriteButton(Transform parent, Sprite sprite, Vector2 centerFrac, float widthFrac, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = centerFrac; rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true;
            // tamaño segun el ancho del celu y el aspect del sprite
            var parentRt = parent as RectTransform;
            float w = (parentRt != null ? parentRt.rect.width : 400f) * widthFrac;
            float aspect = (sprite != null && sprite.rect.height > 0) ? sprite.rect.width / sprite.rect.height : 3f;
            rt.sizeDelta = new Vector2(w, aspect > 0 ? w / aspect : w * 0.33f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            go.AddComponent<MenuButtonFeedback>().img = img; // hover/click
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
            bool left = Random.value < 0.5f;
            float x = left ? Random.Range(0.05f, 0.23f) : Random.Range(0.77f, 0.95f);
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            float peek = charHeight * peekFrac;

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

        // feedback simple del boton (hover crece, click se hunde)
        private class MenuButtonFeedback : MonoBehaviour,
            UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
            UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
        {
            public Image img;
            bool _h, _p;
            void Apply() { float s = _p ? 0.95f : _h ? 1.07f : 1f; if (img != null) img.rectTransform.localScale = new Vector3(s, s, 1f); }
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { _h = true; Apply(); }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { _h = false; _p = false; Apply(); }
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { _p = true; Apply(); }
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { _p = false; Apply(); }
        }
    }
}
