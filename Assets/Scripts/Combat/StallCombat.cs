using UnityEngine;
using Salada.Placement;
using Salada.Util;
using Salada.Game;

namespace Salada.Combat
{
    /// <summary>
    /// Componente de ataque de UN puesto (va en su GameObject). Cada puesto gestiona su
    /// propio cooldown, targeting y disparo. Los stats base salen del StallData (por tipo de
    /// puesto) y se ajustan por el multiplicador de faccion del CombatManager.
    /// </summary>
    public class StallCombat : MonoBehaviour
    {
        [Header("Stats resueltos (solo lectura en runtime)")]
        public float damage = 25f;
        public float range = 2.5f;
        public float cooldown = 0.7f;

        private GridManager _grid;
        private PlacedStall _stall;
        private CombatManager _combat;
        private BusinessMeters _meters;
        private GameEffects _effects;
        private float _steal, _projSpeed, _projSize;
        private float _nextAttack;

        public void Init(GridManager grid, PlacedStall stall, StallData data)
        {
            _grid = grid;
            _stall = stall;
            _combat = FindFirstObjectByType<CombatManager>();
            _meters = FindFirstObjectByType<BusinessMeters>();
            _effects = FindFirstObjectByType<GameEffects>();

            float mult = _combat != null ? _combat.DamageMult(stall.Owner) : 1f;
            damage = data.attackDamage * mult;
            range = data.attackRange;
            cooldown = data.attackCooldown;

            _steal = _combat != null ? _combat.stealFactor : 0.5f;
            _projSpeed = _combat != null ? _combat.projectileSpeed : 9f;
            _projSize = _combat != null ? _combat.projectileSize : 0.18f;
        }

        void Update()
        {
            if (_grid == null || _grid.Model == null || _stall == null) return;
            if (Time.time < _nextAttack || Client.Active.Count == 0) return;

            Vector2 center = _grid.Model.FootprintCenterWorld(_stall.OriginCell, _stall.Footprint);
            Vector2 facing = new Vector2(_stall.Facing.x, _stall.Facing.y);
            float rangeSqr = range * range;

            Client target = null;
            float bestSqr = rangeSqr;
            foreach (var c in Client.Active)
            {
                if (!c.IsTargetable) continue;
                Vector2 to = (Vector2)c.transform.position - center;
                float dSqr = to.sqrMagnitude;
                if (dSqr > rangeSqr) continue;
                if (Vector2.Dot(to, facing) < 0f) continue; // atras -> no alcanza
                if (dSqr < bestSqr) { bestSqr = dSqr; target = c; }
            }

            if (target != null)
            {
                SpawnShot(center, target);
                target.TakeDamage(_stall, EffectiveDamage(), _steal);
                _nextAttack = Time.time + EffectiveCooldown();
            }
        }

        // Los puestos del jugador reciben reputacion (dano) y felicidad (cooldown) en vivo.
        public float EffectiveDamage()
        {
            float d = damage;
            if (_stall != null && _stall.Owner == Owner.Player)
            {
                if (_meters != null) d *= _meters.PlayerDamageMult;   // reputacion
                if (_effects != null) d *= _effects.PlayerDamageMult; // efecto especial de evento
            }
            return d;
        }

        public float EffectiveCooldown()
        {
            if (_stall != null && _stall.Owner == Owner.Player && _meters != null)
                return Mathf.Max(0.05f, cooldown * _meters.PlayerCooldownMult);
            return cooldown;
        }

        private static int _shotVariant;

        void SpawnShot(Vector2 from, Client target)
        {
            var go = new GameObject("Shot");
            if (_combat != null) go.transform.SetParent(_combat.ShotsRoot, false);
            go.transform.position = new Vector3(from.x, from.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 25;

            var sprites = _combat != null ? _combat.projectileSprites : null;
            Sprite baseSprite = (sprites != null && sprites.Length > 0) ? sprites[_shotVariant++ % sprites.Length] : null;
            if (baseSprite != null)
            {
                // borde azul recoloreado al color de la faccion que dispara
                sr.sprite = ProjectileArt.Recolored(baseSprite, _grid.ColorFor(_stall.Owner));
                float maxB = Mathf.Max(baseSprite.bounds.size.x, baseSprite.bounds.size.y);
                float s = maxB > 0f ? _projSize / maxB : _projSize;
                go.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                sr.sprite = PlaceholderSprite.Unit;
                sr.color = _grid.ColorFor(_stall.Owner);
                go.transform.localScale = new Vector3(_projSize, _projSize, 1f);
            }
            go.AddComponent<Projectile>().Init(target.transform, target.transform.position, _projSpeed, 1.5f);
        }
    }
}
