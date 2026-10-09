using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Cassetete / arma corpo-a-corpo: o dano depende da velocidade real do golpe.
    /// Pancada fraca = nada; golpe com força = muito dano + empurrão + vibração forte.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MeleeWeapon : MonoBehaviour
    {
        public Transform tip;
        [Tooltip("Velocidade mínima (m/s) para contar como golpe")]
        public float minSpeed = 2f;
        public float damagePerMeterPerSecond = 9f;
        public float maxDamage = 70f;
        public float hitCooldown = 0.2f;
        public float knockback = 1.5f;

        XRGrabInteractable grab;
        Vector3 lastTip, tipVel;
        float lastHit;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (tip) lastTip = tip.position;
        }

        void FixedUpdate()
        {
            if (!tip) return;
            tipVel = (tip.position - lastTip) / Time.fixedDeltaTime;
            lastTip = tip.position;
        }

        XRNode? HeldHand()
        {
            if (grab == null || !grab.isSelected) return null;
            foreach (var i in grab.interactorsSelecting)
                if (!(i is XRSocketInteractor)) return WeaponUtils.HandOf(i.transform);
            return null;
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < lastHit + hitCooldown) return;
            float speed = Mathf.Max(c.relativeVelocity.magnitude, tipVel.magnitude);
            if (speed < minSpeed) return;
            lastHit = Time.time;

            var cp = c.GetContact(0);
            Vector3 dir = tipVel.sqrMagnitude > 0.01f ? tipVel.normalized : -cp.normal;
            float dmg = Mathf.Min(maxDamage, (speed - minSpeed + 1f) * damagePerMeterPerSecond);

            var target = WeaponUtils.FindDamageable(c.collider);
            if (target != null) target.TakeDamage(dmg, cp.point, dir);
            if (c.rigidbody && !c.rigidbody.isKinematic)
                c.rigidbody.AddForceAtPosition(dir * knockback * speed * 0.2f, cp.point, ForceMode.Impulse);

            WeaponAudio.PlayAt(WeaponAudio.Thud(), cp.point, Mathf.Clamp01(speed / 8f));
            WeaponUtils.Haptic(HeldHand(), Mathf.Clamp01(speed / 7f), 0.08f);
        }
    }
}
