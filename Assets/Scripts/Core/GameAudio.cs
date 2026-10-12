using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Original synthesized audio; no external assets or licenses required.</summary>
public class GameAudio : MonoBehaviour
{
    public enum Cue { Click, Swing, Hit, Hurt, Pickup, Coin, Chop, TreeFall, Death }
    private static GameAudio instance;
    private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
    private readonly HashSet<Button> wiredButtons = new HashSet<Button>();
    private AudioSource music;
    private AudioSource[] voices;
    private float sfxVolume;
    private float nextButtonScan;
    private Transform player;
    private const int Rate = 22050;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null) new GameObject("GameAudio").AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.clip = CreateMusic();
        voices = new AudioSource[8];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
        foreach (Cue cue in System.Enum.GetValues(typeof(Cue))) clips[cue] = CreateEffect(cue);
        SetVolumes(PlayerPrefs.GetFloat("RPG.Settings.Master", 1f),
            PlayerPrefs.GetFloat("RPG.Settings.Music", .7f), PlayerPrefs.GetFloat("RPG.Settings.SFX", .8f));
        music.Play();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void SetVolumes(float master, float musicVolume, float effects)
    {
        AudioListener.volume = Mathf.Clamp01(master);
        if (instance == null) return;
        instance.music.volume = Mathf.Clamp01(musicVolume) * .25f;
        instance.sfxVolume = Mathf.Clamp01(effects) * .55f;
    }

    public static void Play(Cue cue, Vector3? position = null)
    {
        if (instance == null || !instance.clips.ContainsKey(cue)) return;
        float gain = instance.sfxVolume;
        if (position.HasValue)
        {
            if (instance.player == null)
            {
                PlayerMovements actor = FindObjectOfType<PlayerMovements>();
                if (actor != null) instance.player = actor.transform;
            }
            if (instance.player != null)
                gain *= Mathf.Clamp01(1f - Vector2.Distance(position.Value, instance.player.position) / 14f);
        }
        if (gain <= .001f) return;
        // Never interrupt an existing effect; discard excess voices in large fights.
        foreach (AudioSource source in instance.voices)
        {
            if (source.isPlaying) continue;
            source.clip = instance.clips[cue];
            source.volume = gain;
            source.pitch = cue == Cue.Click ? 1f : Random.Range(.94f, 1.06f);
            source.Play();
            break;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        wiredButtons.RemoveWhere(button => button == null);
        player = null;
        nextButtonScan = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextButtonScan) return;
        nextButtonScan = Time.unscaledTime + 1f;
        foreach (Button button in FindObjectsOfType<Button>(true))
            if (wiredButtons.Add(button)) button.onClick.AddListener(Click);
    }

    private static void Click() { Play(Cue.Click); }

    private static AudioClip CreateEffect(Cue cue)
    {
        float duration = cue == Cue.TreeFall ? .8f : cue == Cue.Death ? .5f : .18f;
        float[] samples = new float[Mathf.RoundToInt(Rate * duration)];
        var noise = new System.Random(718 + (int)cue);
        float phase = 0f;
        float filtered = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)Rate;
            float p = t / duration;
            float frequency = 300f;
            float noiseMix = 0f;
            switch (cue)
            {
                case Cue.Click: frequency = 900f - 500f * p; break;
                case Cue.Swing: frequency = 450f - 300f * p; noiseMix = .85f; break;
                case Cue.Hit: frequency = 180f - 110f * p; noiseMix = .65f; break;
                case Cue.Hurt: frequency = 220f - 140f * p; noiseMix = .3f; break;
                case Cue.Pickup: frequency = p < .45f ? 660f : 880f; break;
                case Cue.Coin: frequency = p < .35f ? 1320f : 1760f; break;
                case Cue.Chop: frequency = 130f - 65f * p; noiseMix = .7f; break;
                case Cue.TreeFall: frequency = 90f - 55f * p; noiseMix = .9f; break;
                case Cue.Death: frequency = 260f - 200f * p; noiseMix = .25f; break;
            }
            phase += 2f * Mathf.PI * frequency / Rate;
            filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2f - 1f, .3f);
            float tone = Mathf.Sin(phase) * .75f + Mathf.Sin(phase * 2f) * .15f;
            float envelope = Mathf.Min(1f, t / .006f) * Mathf.Pow(1f - p, 2f);
            samples[i] = Mathf.Lerp(tone, filtered, noiseMix) * envelope * .65f;
        }
        AudioClip clip = AudioClip.Create(cue.ToString(), samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateMusic()
    {
        // Eight-bar, 90 BPM pentatonic melody with gentle plucked chords.
        float beat = 2f / 3f;
        float[] samples = new float[Mathf.RoundToInt(Rate * beat * 32f)];
        int[] roots = { 48, 53, 55, 48, 57, 53, 55, 48 };
        int[] melody = { 72, 76, 79, 76, 77, 81, 79, 77, 79, 83, 81, 79, 76, 74, 72, 67,
            76, 81, 84, 81, 77, 79, 81, 77, 79, 76, 74, 67, 72, 76, 74, 72 };
        for (int b = 0; b < 32; b++)
        {
            AddNote(samples, b * beat, melody[b], beat * .92f, .12f);
            if (b % 4 != 0) continue;
            int root = roots[b / 4];
            AddNote(samples, b * beat, root, beat * 3.9f, .09f);
            AddNote(samples, b * beat + .04f, root + (root == 57 ? 3 : 4), beat * 3.8f, .035f);
            AddNote(samples, b * beat + .08f, root + 7, beat * 3.7f, .035f);
        }
        AudioClip clip = AudioClip.Create("Village - original fantasy loop", samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void AddNote(float[] samples, float start, int midi, float duration, float gain)
    {
        float frequency = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        int offset = Mathf.RoundToInt(start * Rate);
        for (int i = 0; i < duration * Rate && offset + i < samples.Length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1f, t / .015f) * Mathf.Exp(-3f * t / duration)
                * Mathf.Clamp01((duration - t) / .08f);
            float phase = 2f * Mathf.PI * frequency * t;
            samples[offset + i] += (Mathf.Sin(phase) + .2f * Mathf.Sin(phase * 2f)) * envelope * gain;
        }
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (Button button in wiredButtons) if (button != null) button.onClick.RemoveListener(Click);
        foreach (AudioClip clip in clips.Values) Destroy(clip);
        if (music != null && music.clip != null) Destroy(music.clip);
        instance = null;
    }
}
