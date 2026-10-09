using System.Collections.Generic;
using UnityEngine;

namespace LeoVR.Weapons
{
    /// <summary>
    /// Sons gerados por código (sem ficheiros de áudio): tiro, clique em seco, slide, carregador, impacto...
    /// Mais tarde podes trocar por gravações reais.
    /// </summary>
    public static class WeaponAudio
    {
        const int SR = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        static AudioClip Create(string key, float[] data)
        {
            Normalize(data, 0.95f);
            var clip = AudioClip.Create(key, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            cache[key] = clip;
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Tiro: estalo supersónico + "boom" grave + cauda de eco filtrada.</summary>
        public static AudioClip Gunshot(float pitch, float body)
        {
            string key = $"shot_{pitch:F2}_{body:F2}";
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * 0.9f);
            var data = new float[n];
            var rnd = new System.Random(1234);
            float lp = 0, lp2 = 0, phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                float crack = noise * Mathf.Exp(-t * 90f * pitch);
                float a = Mathf.Lerp(0.6f, 0.03f, Mathf.Clamp01(t * 3f));
                lp += a * (noise - lp);
                lp2 += a * (lp - lp2);
                float tail = lp2 * Mathf.Exp(-t * 6.5f) * 2.4f;
                float f = Mathf.Lerp(140f, 45f, Mathf.Clamp01(t * 6f)) * pitch;
                phase += 2f * Mathf.PI * f / SR;
                float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 13f) * body;
                float attack = Mathf.Clamp01(t * 2000f);
                data[i] = attack * (crack * 0.9f + tail + boom);
            }
            return Create(key, data);
        }

        static AudioClip Metallic(string key, float seconds, float[] clickTimes, float f1, float f2, float decay, float noiseAmt)
        {
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * seconds);
            var data = new float[n];
            var rnd = new System.Random(key.GetHashCode());
            foreach (var start in clickTimes)
            {
                int s0 = (int)(start * SR);
                for (int i = s0; i < n; i++)
                {
                    float t = (i - s0) / (float)SR;
                    float noise = (float)(rnd.NextDouble() * 2 - 1);
                    float v = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f;
                    data[i] += (v * Mathf.Exp(-t * decay) + noise * noiseAmt * Mathf.Exp(-t * decay * 3f));
                }
            }
            return Create(key, data);
        }

        public static AudioClip DryClick() => Metallic("dry", 0.08f, new[] { 0f }, 3200f, 5100f, 260f, 0.5f);
        public static AudioClip RackBack() => Metallic("rackback", 0.15f, new[] { 0f, 0.035f }, 1700f, 2600f, 70f, 0.8f);
        public static AudioClip RackForward() => Metallic("rackfwd", 0.18f, new[] { 0f }, 1400f, 2300f, 45f, 1.0f);
        public static AudioClip MagIn() => Metallic("magin", 0.12f, new[] { 0f, 0.02f }, 1100f, 2900f, 80f, 0.7f);
        public static AudioClip MagOut() => Metallic("magout", 0.12f, new[] { 0f }, 900f, 2100f, 60f, 0.6f);
        public static AudioClip Selector() => Metallic("selector", 0.06f, new[] { 0f }, 2600f, 4200f, 300f, 0.4f);
        public static AudioClip Holster() => Metallic("holster", 0.15f, new[] { 0f }, 300f, 700f, 40f, 0.9f);

        public static AudioClip Tink()
        {
            const string key = "tink";
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * 0.3f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 4100f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 6300f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 8900f * t)) * Mathf.Exp(-t * 22f);
            }
            return Create(key, d);
        }

        /// <summary>Bala a acertar em aço: "ding" metálico longo.</summary>
        public static AudioClip Ding()
        {
            const string key = "ding";
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * 1.2f);
            var d = new float[n];
            var rnd = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 200f);
                d[i] = noise * 0.6f
                     + Mathf.Sin(2f * Mathf.PI * 1180f * t) * Mathf.Exp(-t * 3.5f)
                     + 0.6f * Mathf.Sin(2f * Mathf.PI * 2930f * t) * Mathf.Exp(-t * 5f)
                     + 0.35f * Mathf.Sin(2f * Mathf.PI * 4410f * t) * Mathf.Exp(-t * 8f);
            }
            return Create(key, d);
        }

        public static AudioClip Thud()
        {
            const string key = "thud";
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * 0.3f);
            var d = new float[n];
            var rnd = new System.Random(7);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                lp += 0.15f * (noise - lp);
                d[i] = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 20f) + lp * 2.5f * Mathf.Exp(-t * 35f);
            }
            return Create(key, d);
        }

        public static AudioClip Impact()
        {
            const string key = "impact";
            if (cache.TryGetValue(key, out var c)) return c;
            int n = (int)(SR * 0.15f);
            var d = new float[n];
            var rnd = new System.Random(11);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                lp += 0.3f * (noise - lp);
                d[i] = lp * Mathf.Exp(-t * 60f) + Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 50f) * 0.5f;
            }
            return Create(key, d);
        }

        /// <summary>Toca um som 3D numa posição do mundo.</summary>
        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitchJitter = 0.04f, float maxDistance = 40f)
        {
            if (clip == null) return;
            var go = new GameObject("SFX_" + clip.name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, s.pitch) + 0.1f);
        }
    }
}
