using System.Collections.Generic;
using UnityEngine;

namespace Salada.Audio
{
    /// <summary>
    /// Reproduce SFX y musica de fondo. Los clips se cargan con Resources.Load desde
    /// Assets/Sounds/Resources/, mapeados por SfxId en ResourcePath. Si un clip todavia no
    /// existe (placeholder), Play() no hace nada -- no tira error ni rompe nada. Se autoinstancia
    /// (no hace falta agregarlo a la escena a mano); usar siempre a traves de Sfx.Play(...).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [SerializeField] private int poolSize = 8;

        // Ruta (sin extension) dentro de cualquier carpeta "Resources". Cambiar aca si se
        // reorganizan los archivos; no hace falta tocar el resto del codigo.
        static readonly Dictionary<SfxId, string> ResourcePath = new Dictionary<SfxId, string>
        {
            { SfxId.PhonePullOut,    "celular/sacar_celular" },
            { SfxId.PhonePutAway,    "celular/sacar_celular" }, // mismo clip para sacar y guardar
            { SfxId.UIHover,         "celular/hover" },
            { SfxId.UIClick,         "celular/click" },
            { SfxId.NewWaveOrEvent,  "celular/nueva_oleada_evento" },
            { SfxId.StallPlace,      "puestos/colocar_puesto" },
            { SfxId.StallRotate,     "puestos/rotar_puesto" },
            { SfxId.StallInvalid,    "puestos/colocar_lugar_invalido" },
            { SfxId.StallDemolish,   "puestos/demoler_puesto" },
            { SfxId.StallDestroyed,  "puestos/destruccion_puesto" },
            { SfxId.StallAttack,     "puestos/ataque_puesto" },
            { SfxId.ClientConvinced, "puestos/convencer_cliente" },
            { SfxId.MoneyGain,       "puestos/dinero" },
            { SfxId.GameOver,        "general/game_over" },
            { SfxId.GameWin,         "general/ganar" },
            { SfxId.Warning,         "advertencia" },
        };
        const string MusicPath = "musica";

        static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioManager>();
                    if (_instance == null) _instance = new GameObject("AudioManager").AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        readonly Dictionary<SfxId, AudioClip> _cache = new Dictionary<SfxId, AudioClip>();
        AudioSource[] _pool;
        int _poolIdx;
        AudioSource _music;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _pool = new AudioSource[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool[i] = src;
            }

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform, false);
            _music = musicGo.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = musicVolume;
            var musicClip = Resources.Load<AudioClip>(MusicPath);
            if (musicClip != null) { _music.clip = musicClip; _music.Play(); }
        }

        AudioClip GetClip(SfxId id)
        {
            if (_cache.TryGetValue(id, out var cached)) return cached;
            AudioClip clip = null;
            if (ResourcePath.TryGetValue(id, out var path)) clip = Resources.Load<AudioClip>(path);
            _cache[id] = clip; // cachea null tambien: no reintenta Resources.Load en cada llamada
            return clip;
        }

        /// <summary>Reproduce un SFX (PlayOneShot, se pueden superponer). No hace nada si el clip no existe.</summary>
        public static void Play(SfxId id, float volumeScale = 1f)
        {
            var inst = Instance;
            var clip = inst.GetClip(id);
            if (clip == null) return; // placeholder sin audio todavia
            var src = inst._pool[inst._poolIdx];
            inst._poolIdx = (inst._poolIdx + 1) % inst._pool.Length;
            src.PlayOneShot(clip, inst.sfxVolume * volumeScale);
        }
    }
}
