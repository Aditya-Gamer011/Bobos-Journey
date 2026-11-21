using System.Collections.Generic;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Tiny synthesiser: every sound effect is generated at startup, so there are no audio assets.</summary>
    public class Sfx : MonoBehaviour
    {
        public enum Id { Jump, Elastic, Land, Bounce, Checkpoint, Crumble, Respawn, Page, Whoosh, Thud, Zone }

        public static float Volume = 0.8f;

        static Sfx instance;
        readonly Dictionary<Id, AudioClip> clips = new Dictionary<Id, AudioClip>();
        AudioSource[] voices;
        int nextVoice;

        public static void Play(Id id, float volume = 1f, float pitch = 1f)
        {
            if (instance == null)
            {
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
            }
            instance.PlayInternal(id, volume, pitch);
        }

        void Awake()
        {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            const int sampleRate = 44100;
            clips[Id.Jump] = Make("jump", sampleRate, 0.22f, (t, d) => Sine(Mathf.Lerp(260f, 620f, Ease(t / d)) + Mathf.Sin(t * 60f) * 18f, t) * Env(t, d, 0.005f));
            clips[Id.Elastic] = Make("elastic", sampleRate, 0.32f, (t, d) => (Sine(Mathf.Lerp(330f, 990f, Ease(t / d)) + Mathf.Sin(t * 90f) * 40f, t) * 0.8f + Sine(Mathf.Lerp(660f, 1320f, t / d), t) * 0.25f) * Env(t, d, 0.004f));
            clips[Id.Land] = Make("land", sampleRate, 0.16f, (t, d) => (Sine(Mathf.Lerp(150f, 55f, t / d), t) * 0.9f + Noise() * 0.25f * Mathf.Exp(-t * 60f)) * Env(t, d, 0.002f));
            clips[Id.Bounce] = Make("bounce", sampleRate, 0.5f, (t, d) => Sine(220f + Mathf.Sin(t * 38f) * 140f * Mathf.Exp(-t * 5f) + t * 500f, t) * Env(t, d, 0.003f));
            clips[Id.Checkpoint] = Make("checkpoint", sampleRate, 0.9f, (t, d) => Chime(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f));
            clips[Id.Zone] = Make("zone", sampleRate, 1.4f, (t, d) => Chime(t, new[] { 392f, 523.25f, 659.25f, 783.99f, 987.77f }, 0.13f) * 0.9f);
            clips[Id.Crumble] = Make("crumble", sampleRate, 0.45f, (t, d) => Noise() * (0.5f + 0.5f * Mathf.Sin(t * 180f)) * Env(t, d, 0.01f) * 0.6f);
            clips[Id.Respawn] = Make("respawn", sampleRate, 0.6f, (t, d) => Sine(Mathf.Lerp(900f, 300f, t / d), t) * 0.6f * Env(t, d, 0.01f) + Sine(Mathf.Lerp(450f, 150f, t / d), t) * 0.3f * Env(t, d, 0.01f));
            clips[Id.Page] = Make("page", sampleRate, 0.25f, (t, d) => Noise() * Mathf.Sin(Mathf.PI * t / d) * 0.35f * (0.6f + 0.4f * Mathf.Sin(t * 70f)));
            clips[Id.Whoosh] = Make("whoosh", sampleRate, 0.7f, (t, d) => Noise() * Mathf.Sin(Mathf.PI * t / d) * 0.5f * Mathf.Sin(t * 30f + 1f));
            clips[Id.Thud] = Make("thud", sampleRate, 0.5f, (t, d) => (Sine(Mathf.Lerp(90f, 35f, t / d), t) + Noise() * 0.3f * Mathf.Exp(-t * 30f)) * Env(t, d, 0.002f));
        }

        void PlayInternal(Id id, float volume, float pitch)
        {
            if (!clips.TryGetValue(id, out var clip)) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * Volume);
        }

        // oscillator phase, shared by Sine() and reset by Make() for each clip
        static float phase;
        static System.Random rng = new System.Random(1);

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Ease(float x) => 1f - (1f - x) * (1f - x);
        static float Env(float t, float d, float attack) => Mathf.Clamp01(t / attack) * Mathf.Pow(1f - Mathf.Clamp01(t / d), 2f);

        static float Sine(float freq, float t)
        {
            phase += freq / 44100f;
            return Mathf.Sin(phase * Mathf.PI * 2f);
        }

        static float Chime(float t, float[] notes, float step)
        {
            float sum = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float noteTime = t - i * step;
                if (noteTime < 0) continue;
                float env = Mathf.Exp(-noteTime * 5f) * Mathf.Clamp01(noteTime / 0.004f);
                sum += (Mathf.Sin(noteTime * notes[i] * Mathf.PI * 2f) + 0.3f * Mathf.Sin(noteTime * notes[i] * 2f * Mathf.PI * 2f)) * env * 0.3f;
            }
            return sum;
        }

        static AudioClip Make(string name, int sampleRate, float duration, System.Func<float, float, float> f)
        {
            phase = 0;
            int n = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)sampleRate, duration), -1f, 1f) * 0.8f;
            var clip = AudioClip.Create(name, n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
