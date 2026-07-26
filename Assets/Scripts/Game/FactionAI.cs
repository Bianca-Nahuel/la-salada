using System;
using UnityEngine;
using Salada.Placement;

namespace Salada.Game
{
    /// <summary>
    /// Config CENTRAL de la IA de las facciones rivales (un solo lugar, un perfil por faccion).
    /// La leen WaveManager (expansion + targeting), StallCombat y TerritoryManager. Todos los
    /// numeros son editables desde el Inspector, por faccion.
    /// </summary>
    public class FactionAI : MonoBehaviour
    {
        [Serializable]
        public class Profile
        {
            [Header("Expansion: costo (en ventas)")]
            [Tooltip("Ventas para la PRIMERA expansion.")] public int baseCost = 3;
            [Tooltip("Cuantas ventas mas cuesta cada expansion siguiente.")] public int growth = 2;

            [Header("Targeting")]
            [Tooltip("0 = pegan al mas cercano; mas alto = priorizan al cliente casi-vendido.")]
            public float almostSoldWeight = 1f;

            [Header("Expansion: pesos de decision (por celda candidata)")]
            [Tooltip("Reforzar zonas donde ya estan pero venden poco (pierden clientes que golpearon).")]
            public float wReinforce = 1f;
            [Tooltip("Ocupar zonas libres (sin puestos de nadie).")]
            public float wUnoccupied = 0.4f;
            [Tooltip("Invadir a un competidor amenazante: te roba clientes disputados, domina la salada, o capta muchos clientes.")]
            public float wInvade = 1f;
            [Tooltip("Ir contra el jugador (escala con tu hostilidad). Secundario a las ventas.")]
            public float wAntiPlayer = 0.3f;
            [Tooltip("Agruparse cerca de lo propio (cohesion).")]
            public float wCohesion = 0.4f;
            [Tooltip("Ruido aleatorio para variar la eleccion.")]
            public float randomJitter = 0.15f;
        }

        [Header("Perfil rojo (Picantes / Enemy) - agresivo/expansivo")]
        public Profile picantes = new Profile
        {
            baseCost = 3, growth = 2, almostSoldWeight = 1f,
            wReinforce = 0.8f, wUnoccupied = 0.4f, wInvade = 1.0f, wAntiPlayer = 0.6f, wCohesion = 0.3f, randomJitter = 0.15f
        };

        [Header("Perfil amarillo (Crowned / Neutral) - economico/defensivo")]
        public Profile crowned = new Profile
        {
            baseCost = 3, growth = 3, almostSoldWeight = 1f,
            wReinforce = 1.2f, wUnoccupied = 0.4f, wInvade = 0.9f, wAntiPlayer = 0.2f, wCohesion = 0.5f, randomJitter = 0.15f
        };

        [Header("Global")]
        [Tooltip("Cada partida usa una semilla distinta (random). Apagalo para reproducir con fixedSeed.")]
        public bool useRandomSeed = true;
        [Tooltip("Semilla fija cuando useRandomSeed = off (para pruebas deterministas).")]
        public int fixedSeed = 12345;
        [Tooltip("Cuanto se 'olvida' por dia la fuerza-de-ventas y los golpes-perdidos (0.5 = a la mitad).")]
        [Range(0f, 1f)] public float statDayDecay = 0.5f;
        [Tooltip("Maximo de dias hasta que dos rivales pelean en una zona que se disputan.")]
        public int rivalDisputeMaxDays = 2;

        public Profile Of(Owner o) => o == Owner.Enemy ? picantes : crowned;

        /// <summary>Semilla raiz de la partida (random o fija). 'salt' separa los streams (Wave vs Territory).</summary>
        public int SeedFor(int salt)
        {
            int root = useRandomSeed ? Environment.TickCount : fixedSeed;
            return root * 31 + salt;
        }
    }
}
