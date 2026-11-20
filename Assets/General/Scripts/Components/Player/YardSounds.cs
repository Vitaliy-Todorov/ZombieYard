using UnityEngine;

namespace ZombieYard
{
    internal enum YardCue { Swing, Shot, Hurt, Take }

    internal sealed class YardSounds : MonoBehaviour
    {
        private readonly AudioClip[] clips = new AudioClip[4];
        private AudioSource source, sawMotor;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var noise = new System.Random(137);
            for (int i = 0; i < clips.Length; i++)
            {
                const int rate = 22050;
                float duration = i == 1 ? .26f : .15f;
                var samples = new float[(int)(rate * duration)];
                float lowPass = 0;
                for (int j = 0; j < samples.Length; j++)
                {
                    float t = (float)j / rate;
                    lowPass = Mathf.Lerp(lowPass, (float)noise.NextDouble() * 2 - 1, i == 1 ? .62f : .12f);
                    float tone = i == 3 ? Mathf.Sin(t * (2600 + 4000 * t)) : Mathf.Sin(t * (550 - 800 * t));
                    samples[j] = (i == 3 ? tone * .3f : lowPass + tone * .2f) * Mathf.Exp(-t * 22) * Mathf.Clamp01(t * 900);
                }
                clips[i] = AudioClip.Create(((YardCue)i).ToString(), samples.Length, 1, rate, false);
                clips[i].SetData(samples, 0);
            }
            sawMotor = gameObject.AddComponent<AudioSource>();
            sawMotor.playOnAwake = false;
            sawMotor.loop = true;
            sawMotor.volume = .18f;
            const int sampleRate = 22050;
            var buzz = new float[sampleRate];
            for (int i = 0; i < buzz.Length; i++)
            {
                float t = (float)i / sampleRate;
                float phase = t * 96 + .07f * Mathf.Sin(2 * Mathf.PI * 12 * t);
                buzz[i] = (.8f * Mathf.Repeat(phase, 1) - .4f) + .11f * Mathf.Sin(4 * Mathf.PI * phase);
            }
            sawMotor.clip = AudioClip.Create("Chainsaw motor", buzz.Length, 1, sampleRate, false);
            sawMotor.clip.SetData(buzz, 0);
        }

        public void RunSaw(bool active)
        {
            if (active == sawMotor.isPlaying) return;
            if (active) sawMotor.Play();
            else sawMotor.Stop();
        }

        public void Play(YardCue cue) => source.PlayOneShot(clips[(int)cue], .48f);
        private void OnDestroy()
        {
            foreach (var clip in clips) if (clip) Destroy(clip);
            if (sawMotor && sawMotor.clip) Destroy(sawMotor.clip);
        }
    }
}
