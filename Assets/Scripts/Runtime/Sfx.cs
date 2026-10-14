using UnityEngine;

namespace FifMan
{
    /// <summary>Procedurally generated sound effects, so the project needs no audio assets.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        AudioSource source;
        public AudioClip Pellet, Power, EatGhost, Hit, Win, Lose;

        public static Sfx Create(GameObject host)
        {
            var sfx = host.AddComponent<Sfx>();
            sfx.source = host.AddComponent<AudioSource>();
            sfx.source.spatialBlend = 0f;
            sfx.Pellet = Tone("pellet", 520f, 700f, 0.06f);
            sfx.Power = Tone("power", 180f, 900f, 0.35f);
            sfx.EatGhost = Tone("eat", 500f, 1400f, 0.25f);
            sfx.Hit = Tone("hit", 420f, 70f, 0.6f);
            sfx.Win = Tone("win", 300f, 1300f, 0.9f);
            sfx.Lose = Tone("lose", 500f, 60f, 1.2f);
            return sfx;
        }

        public void Play(AudioClip clip, float volume = 0.6f)
        {
            if (clip != null) source.PlayOneShot(clip, volume);
        }

        static AudioClip Tone(string name, float f0, float f1, float seconds)
        {
            const int rate = 44100;
            int n = (int)(rate * seconds);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / rate;
                float envelope = Mathf.Clamp01(Mathf.Min(t * 30f, (1f - t) * 6f));
                float wave = 0.6f * Mathf.Sin(phase) + 0.4f * Mathf.Sign(Mathf.Sin(phase * 0.5f));
                data[i] = wave * envelope * 0.35f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
