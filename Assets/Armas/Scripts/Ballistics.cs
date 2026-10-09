using System.Collections.Generic;
using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Balas reais: cada bala tem velocidade (m/s), sofre gravidade e resistência do ar,
    /// e é simulada por segmentos de raycast (não é tiro instantâneo).
    /// </summary>
    public class Ballistics : MonoBehaviour
    {
        class Bullet
        {
            public Vector3 pos, vel;
            public float damage, life, holeSize;
            public GameObject hole, fx;
        }

        static Ballistics instance;
        readonly List<Bullet> bullets = new List<Bullet>();
        const float DragPerSecond = 0.12f;

        public static void Fire(Vector3 origin, Vector3 velocity, float damage, float holeSize, GameObject holePrefab, GameObject impactFx)
        {
            if (instance == null)
            {
                var go = new GameObject("[Ballistics]");
                instance = go.AddComponent<Ballistics>();
            }
            instance.bullets.Add(new Bullet
            {
                pos = origin, vel = velocity, damage = damage, life = 3f,
                holeSize = holeSize, hole = holePrefab, fx = impactFx
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                Vector3 newVel = (b.vel + Physics.gravity * dt) * (1f - DragPerSecond * dt);
                Vector3 step = (b.vel + newVel) * 0.5f * dt;
                float dist = step.magnitude;
                if (dist > 0f && Physics.Raycast(b.pos, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    OnHit(b, hit);
                    bullets.RemoveAt(i);
                    continue;
                }
                b.pos += step;
                b.vel = newVel;
                b.life -= dt;
                if (b.life <= 0f) bullets.RemoveAt(i);
            }
        }

        static void OnHit(Bullet b, RaycastHit hit)
        {
            var dir = b.vel.normalized;
            var target = WeaponUtils.FindDamageable(hit.collider);
            if (target != null) target.TakeDamage(b.damage, hit.point, dir);

            var rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
                rb.AddForceAtPosition(dir * b.damage * 0.015f, hit.point, ForceMode.Impulse);

            var surf = hit.collider.GetComponentInParent<SurfaceImpact>();
            var holePrefab = surf != null && surf.holePrefab != null ? surf.holePrefab : b.hole;
            var fxPrefab = surf != null && surf.fxPrefab != null ? surf.fxPrefab : b.fx;
            float scale = b.holeSize * (surf != null ? surf.holeScale : 1f);

            if (holePrefab != null)
            {
                var rot = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                var h = Instantiate(holePrefab, hit.point + hit.normal * 0.0015f, rot);
                h.transform.localScale = Vector3.one * scale;
                if (rb != null) h.transform.SetParent(rb.transform, true);
                Destroy(h, 120f);
            }
            if (fxPrefab != null)
            {
                var fx = Instantiate(fxPrefab, hit.point, Quaternion.LookRotation(Vector3.Reflect(dir, hit.normal) * 0.5f + hit.normal));
                Destroy(fx, 2f);
            }
            bool metal = surf != null && surf.kind == SurfaceImpact.Kind.Metal;
            WeaponAudio.PlayAt(metal ? WeaponAudio.Ding() : WeaponAudio.Impact(), hit.point, metal ? 0.9f : 0.5f, 0.1f, metal ? 120f : 30f);
        }
    }
}
