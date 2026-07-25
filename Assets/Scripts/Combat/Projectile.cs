using UnityEngine;

namespace Salada.Combat
{
    /// <summary>
    /// "Disparo" placeholder que vuela desde el puesto hacia el cliente. Sigue al cliente
    /// si sigue vivo; si no, va a su ultima posicion. Se autodestruye al llegar o por lifetime.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _fallback;
        private float _speed;
        private float _life;

        public void Init(Transform target, Vector3 fallback, float speed, float maxLife)
        {
            _target = target;
            _fallback = fallback;
            _speed = speed;
            _life = maxLife;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            Vector3 dst = _target != null ? _target.position : _fallback;
            transform.position = Vector3.MoveTowards(transform.position, dst, _speed * Time.deltaTime);
            if (_life <= 0f || (transform.position - dst).sqrMagnitude < 0.01f)
                Destroy(gameObject);
        }
    }
}
