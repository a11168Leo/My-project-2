using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>Qualquer coisa que pode levar dano (alvos, jogadores, objetos).</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point, Vector3 direction);
    }
}
