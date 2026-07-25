using System.Collections.Generic;
using UnityEngine;

namespace Salada.Game
{
    /// <summary>Efecto especial que puede disparar un evento, con duracion en oleadas.</summary>
    public enum EffectType
    {
        None,
        PlayerDamageMult,   // multiplica el dano de nuestros puestos
        ClientCountMult,    // multiplica la cantidad de clientes por oleada
        SaleRewardMult,     // multiplica la plata por venta
        BlockRivalExpansion // los rivales no se expanden
    }

    /// <summary>
    /// Efectos especiales activos a lo largo de las oleadas (con duracion). Los sistemas
    /// (WaveManager, StallCombat) consultan los multiplicadores agregados. Se decrementan
    /// al final de cada oleada.
    /// </summary>
    public class GameEffects : MonoBehaviour
    {
        private class Active { public EffectType type; public float magnitude; public int wavesLeft; }
        private readonly List<Active> _active = new List<Active>();

        public void AddEffect(EffectType type, float magnitude, int waves)
        {
            if (type == EffectType.None || waves <= 0) return;
            _active.Add(new Active { type = type, magnitude = magnitude, wavesLeft = waves });
        }

        /// <summary>Descontar una oleada a cada efecto; remover los vencidos.</summary>
        public void OnWaveEnded()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].wavesLeft--;
                if (_active[i].wavesLeft <= 0) _active.RemoveAt(i);
            }
        }

        private float Product(EffectType t)
        {
            float m = 1f;
            foreach (var a in _active) if (a.type == t) m *= a.magnitude;
            return m;
        }

        public float PlayerDamageMult => Product(EffectType.PlayerDamageMult);
        public float ClientCountMult => Product(EffectType.ClientCountMult);
        public float SaleRewardMult => Product(EffectType.SaleRewardMult);
        public bool ExpansionBlocked
        {
            get { foreach (var a in _active) if (a.type == EffectType.BlockRivalExpansion) return true; return false; }
        }

        public List<string> ActiveDescriptions()
        {
            var list = new List<string>();
            foreach (var a in _active)
                list.Add($"{Label(a.type, a.magnitude)} ({a.wavesLeft})");
            return list;
        }

        public static string Label(EffectType t, float mag)
        {
            switch (t)
            {
                case EffectType.PlayerDamageMult: return $"x{mag:0.##} dano";
                case EffectType.ClientCountMult: return $"x{mag:0.##} clientes";
                case EffectType.SaleRewardMult: return $"x{mag:0.##} plata/venta";
                case EffectType.BlockRivalExpansion: return "rivales no se expanden";
                default: return "";
            }
        }
    }
}
