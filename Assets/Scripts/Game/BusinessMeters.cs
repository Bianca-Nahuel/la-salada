using UnityEngine;

namespace Salada.Game
{
    public enum MeterType { Hostility, Reputation, Happiness, Profit }

    /// <summary>
    /// Las 4 "balanzas" del negocio (0-100, 50 = neutro) y sus efectos de juego. Otros
    /// sistemas leen los multiplicadores derivados. Los valores cambian por eventos,
    /// disputas de oleada o habilidades (via Add/Set).
    /// </summary>
    public class BusinessMeters : MonoBehaviour
    {
        [Header("Balanzas (0-100, 50 = neutro)")]
        [Tooltip("Cuanto nos odian los competidores -> mas probable que se expandan cerca nuestro.")]
        [Range(0, 100)] public float hostility = 50f;
        [Tooltip("Cuanto confian los clientes -> mas dano (convencimiento) de nuestros ataques.")]
        [Range(0, 100)] public float reputation = 50f;
        [Tooltip("Felicidad de empleados -> mayor velocidad de ataque (menor cooldown).")]
        [Range(0, 100)] public float happiness = 50f;
        [Tooltip("Cuanto le sacamos a los productos -> mas plata por venta.")]
        [Range(0, 100)] public float profit = 50f;

        [Header("Amplitud de efecto (alrededor de 50 = x1.0)")]
        public float reputationDamageRange = 0.4f;   // dano x0.6 .. x1.4
        public float happinessCooldownRange = 0.3f;  // cooldown x1.3 .. x0.7
        public float profitRewardRange = 0.5f;        // plata/venta x0.5 .. x1.5

        /// <summary>Multiplicador de dano de nuestros ataques (reputacion).</summary>
        public float PlayerDamageMult => Mathf.Lerp(1f - reputationDamageRange, 1f + reputationDamageRange, reputation / 100f);
        /// <summary>Multiplicador de cooldown de nuestros ataques (felicidad; menor = mas rapido).</summary>
        public float PlayerCooldownMult => Mathf.Lerp(1f + happinessCooldownRange, 1f - happinessCooldownRange, happiness / 100f);
        /// <summary>Multiplicador de plata por venta (profit).</summary>
        public float SaleRewardMult => Mathf.Lerp(1f - profitRewardRange, 1f + profitRewardRange, profit / 100f);
        /// <summary>Probabilidad (0..1) de que un rival se expanda cerca nuestro (hostilidad).</summary>
        public float HostilityChance => hostility / 100f;

        public float Get(MeterType t)
        {
            switch (t)
            {
                case MeterType.Hostility: return hostility;
                case MeterType.Reputation: return reputation;
                case MeterType.Happiness: return happiness;
                case MeterType.Profit: return profit;
                default: return 0f;
            }
        }

        public void Set(MeterType t, float value)
        {
            value = Mathf.Clamp(value, 0f, 100f);
            switch (t)
            {
                case MeterType.Hostility: hostility = value; break;
                case MeterType.Reputation: reputation = value; break;
                case MeterType.Happiness: happiness = value; break;
                case MeterType.Profit: profit = value; break;
            }
        }

        public void Add(MeterType t, float delta) => Set(t, Get(t) + delta);

        /// <summary>Deja las 4 balanzas en el mismo valor (ej. 50 al terminar el tutorial).</summary>
        public void SetAll(float value)
        {
            Set(MeterType.Hostility, value); Set(MeterType.Reputation, value);
            Set(MeterType.Happiness, value); Set(MeterType.Profit, value);
        }
    }
}
