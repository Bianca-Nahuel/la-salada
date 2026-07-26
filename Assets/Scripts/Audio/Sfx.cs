using UnityEngine.Events;

namespace Salada.Audio
{
    /// <summary>Fachada corta para reproducir SFX desde cualquier script: Sfx.Play(SfxId.X).</summary>
    public static class Sfx
    {
        public static void Play(SfxId id, float volumeScale = 1f) => AudioManager.Play(id, volumeScale);

        /// <summary>Envuelve un click de UI para que primero suene un SFX y despues ejecute la accion.</summary>
        public static UnityAction WithClick(UnityAction action, SfxId id = SfxId.UIClick) =>
            () => { Play(id); action?.Invoke(); };
    }
}
