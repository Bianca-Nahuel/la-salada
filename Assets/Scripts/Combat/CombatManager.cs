using UnityEngine;
using Salada.Placement;

namespace Salada.Combat
{
    /// <summary>
    /// Config compartida de combate. YA NO recorre ni ataca: cada puesto gestiona sus
    /// propios ataques (ver StallCombat). Aca viven los parametros globales: el robo de
    /// convencimiento en disputa, el visual del disparo y el multiplicador de dano por faccion.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [Header("Disputa")]
        [Tooltip("Fraccion del golpe que se le resta al progreso de las OTRAS facciones.")]
        [Range(0f, 1f)] public float stealFactor = 0.5f;

        [Header("Disparo")]
        public float projectileSpeed = 9f;
        public float projectileSize = 0.6f;
        [Tooltip("Variantes del sprite 'globo'; se recolorea el borde por faccion.")]
        public Sprite[] projectileSprites;

        [Header("Multiplicador de dano por faccion")]
        public float playerDamageMult = 1f;
        public float neutralDamageMult = 1f;
        public float enemyDamageMult = 1f;

        private Transform _shots;
        public Transform ShotsRoot
        {
            get { if (_shots == null) _shots = new GameObject("Shots").transform; return _shots; }
        }

        public float DamageMult(Owner owner)
        {
            switch (owner)
            {
                case Owner.Player: return playerDamageMult;
                case Owner.Neutral: return neutralDamageMult;
                case Owner.Enemy: return enemyDamageMult;
                default: return 1f;
            }
        }
    }
}
