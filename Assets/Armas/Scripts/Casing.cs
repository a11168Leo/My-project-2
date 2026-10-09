using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>Cápsula ejetada: faz "tlim" ao bater no chão.</summary>
    public class Casing : MonoBehaviour
    {
        int hits;

        void OnCollisionEnter(Collision c)
        {
            if (hits >= 3 || c.relativeVelocity.magnitude < 0.4f) return;
            hits++;
            WeaponAudio.PlayAt(WeaponAudio.Tink(), transform.position, 0.25f / hits, 0.15f, 15f);
        }
    }
}
