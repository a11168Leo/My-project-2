using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LeoVR.Weapons
{
    /// <summary>Vida de um alvo. Pisca a vermelho quando leva dano, cai quando morre e volta a levantar-se.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        public bool respawn = true;
        public float respawnDelay = 4f;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;

        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        float flash;
        Quaternion startRotation;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            Current = maxHealth;
            startRotation = transform.rotation;
            mpb = new MaterialPropertyBlock();
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
            }
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction)
        {
            if (IsDead) return;
            Current -= amount;
            flash = 1f;
            onDamaged?.Invoke();
            if (Current <= 0f) StartCoroutine(Die(direction));
        }

        IEnumerator Die(Vector3 dir)
        {
            IsDead = true;
            onDeath?.Invoke();
            var flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = -transform.forward;
            flat.Normalize();
            var axis = Vector3.Cross(Vector3.up, flat);
            var from = transform.rotation;
            var to = Quaternion.AngleAxis(88f, axis) * from;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.45f)
            {
                transform.rotation = Quaternion.Slerp(from, to, t * t);
                yield return null;
            }
            transform.rotation = to;
            if (!respawn) yield break;
            yield return new WaitForSeconds(respawnDelay);
            transform.rotation = startRotation;
            Current = maxHealth;
            IsDead = false;
        }

        void Update()
        {
            if (flash <= 0f) return;
            flash -= Time.deltaTime * 5f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (flash <= 0f)
                {
                    renderers[i].SetPropertyBlock(null);
                    continue;
                }
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, Color.Lerp(baseColors[i], Color.red, flash));
                renderers[i].SetPropertyBlock(mpb);
            }
        }
    }
}
