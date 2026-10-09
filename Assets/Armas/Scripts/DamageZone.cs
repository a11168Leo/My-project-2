using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>Zona do corpo com multiplicador (cabeça x4, tronco x1, membros x0.6).</summary>
    public class DamageZone : MonoBehaviour, IDamageable
    {
        public Health owner;
        public float multiplier = 1f;

        public void TakeDamage(float amount, Vector3 point, Vector3 direction)
        {
            if (owner == null) owner = GetComponentInParent<Health>();
            if (owner != null) owner.TakeDamage(amount * multiplier, point, direction);
        }
    }
}
