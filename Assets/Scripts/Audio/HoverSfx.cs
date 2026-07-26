using UnityEngine;
using UnityEngine.EventSystems;

namespace Salada.Audio
{
    /// <summary>Reproduce un SFX cuando el mouse entra al elemento (hover de UI).</summary>
    public class HoverSfx : MonoBehaviour, IPointerEnterHandler
    {
        public SfxId id = SfxId.UIHover;
        public void OnPointerEnter(PointerEventData eventData) => Sfx.Play(id);
    }
}
