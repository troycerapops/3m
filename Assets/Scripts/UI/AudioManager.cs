using System.Collections;
using System.Collections.Generic;
using ThreeMusketeers.Core;
using ThreeMusketeers.Theming;
using UnityEngine;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Singleton, survives scene loads. Owns two music AudioSources (for a
    /// simple crossfade between the normal playlist and an "intense" track)
    /// plus one AudioSource for one-shot SFX. Entirely theme-driven -- it
    /// never hardcodes a clip, always reading from whatever ThemeDefinition
    /// is currently applied (see ApplyTheme). A theme with no clips assigned
    /// just plays silently; nothing else needs to change.
    ///
    /// Volume is the only thing this class persists itself (PlayerPrefs) --
    /// everything else (current playlist, intensity) is per-session and
    /// reset by ApplyTheme.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string MusicVolumeKey = "Audio.MusicVolume";
        private const string SfxVolumeKey = "Audio.SfxVolume";
        private const float CrossfadeDuration = 1.5f;

        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _activeMusicSource;
        private AudioSource _sfxSource;

        private ThemeDefinition _theme;
        private readonly List<AudioClip> _shuffledPlaylist = new List<AudioClip>();
        private int _playlistIndex;
        private bool _intense;
        private Coroutine _musicRoutine;

        public float MusicVolume { get; private set; } = 0.6f;
        public float SfxVolume { get; private set; } = 0.8f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicSourceA = gameObject.AddComponent<AudioSource>();
            _musicSourceB = gameObject.AddComponent<AudioSource>();
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _musicSourceA.playOnAwake = false;
            _musicSourceB.playOnAwake = false;
            _sfxSource.playOnAwake = false;
            _activeMusicSource = _musicSourceA;

            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        }

        /// <summary>
        /// Call whenever the active theme changes (Theme Select, or a fresh
        /// game start) -- resets and starts that theme's playlist from
        /// scratch, shuffled.
        /// </summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            _theme = theme;
            _intense = false;
            if (_musicRoutine != null) StopCoroutine(_musicRoutine);

            _shuffledPlaylist.Clear();
            if (theme != null && theme.soundtrackPlaylist != null)
                _shuffledPlaylist.AddRange(theme.soundtrackPlaylist);
            Shuffle(_shuffledPlaylist);
            _playlistIndex = 0;

            if (_shuffledPlaylist.Count > 0)
                _musicRoutine = StartCoroutine(PlaylistRoutine());
        }

        /// <summary>
        /// Crossfades to the theme's intense track, or back to the normal
        /// playlist -- safe to call every turn, it only acts on a change.
        /// GameManager decides what "intense" means; this class just plays
        /// whatever it's told.
        /// </summary>
        public void SetIntense(bool intense)
        {
            if (intense == _intense) return;
            _intense = intense;

            if (intense && _theme != null && _theme.intenseTrack != null)
                StartCoroutine(CrossfadeTo(_theme.intenseTrack, loop: true));
            else if (_shuffledPlaylist.Count > 0)
                StartCoroutine(CrossfadeTo(_shuffledPlaylist[_playlistIndex % _shuffledPlaylist.Count], loop: false));
        }

        public void PlayCaptureSound()
        {
            if (_theme != null && _theme.captureSound != null)
                _sfxSource.PlayOneShot(_theme.captureSound, SfxVolume);
        }

        public void PlayWinSound(GameResult result)
        {
            if (_theme == null) return;
            var clip = result == GameResult.OffenseWin ? _theme.offenseWinSound : _theme.defenseWinSound;
            if (clip != null) _sfxSource.PlayOneShot(clip, SfxVolume);
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            _musicSourceA.volume = MusicVolume;
            _musicSourceB.volume = MusicVolume;
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        }

        private IEnumerator PlaylistRoutine()
        {
            _activeMusicSource.clip = _shuffledPlaylist[_playlistIndex];
            _activeMusicSource.volume = MusicVolume;
            _activeMusicSource.loop = false;
            _activeMusicSource.Play();

            while (true)
            {
                yield return new WaitUntil(() => !_activeMusicSource.isPlaying);
                if (_intense) yield break; // SetIntense's crossfade owns playback now

                _playlistIndex = (_playlistIndex + 1) % _shuffledPlaylist.Count;
                if (_playlistIndex == 0) Shuffle(_shuffledPlaylist); // reshuffle after a full pass
                _activeMusicSource.clip = _shuffledPlaylist[_playlistIndex];
                _activeMusicSource.Play();
            }
        }

        private IEnumerator CrossfadeTo(AudioClip clip, bool loop)
        {
            var incoming = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;
            var outgoing = _activeMusicSource;

            incoming.clip = clip;
            incoming.loop = loop;
            incoming.volume = 0f;
            incoming.Play();

            float t = 0f;
            while (t < CrossfadeDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / CrossfadeDuration);
                incoming.volume = Mathf.Lerp(0f, MusicVolume, p);
                outgoing.volume = Mathf.Lerp(MusicVolume, 0f, p);
                yield return null;
            }
            outgoing.Stop();
            _activeMusicSource = incoming;

            if (!loop)
            {
                // Crossfaded back to a normal playlist track -- hand control
                // back to the playlist loop.
                if (_musicRoutine != null) StopCoroutine(_musicRoutine);
                _musicRoutine = StartCoroutine(PlaylistRoutine());
            }
        }

        private static void Shuffle(List<AudioClip> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
