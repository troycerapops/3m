using System.Collections;
using System.Collections.Generic;
using ThreeMusketeers.Core;
using ThreeMusketeers.Theming;
using UnityEngine;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Singleton, survives scene loads. Owns two music AudioSources (so
    /// consecutive tracks within a playlist can crossfade into each other --
    /// see PlaylistRoutine/CrossfadeToTrack) plus one AudioSource for
    /// one-shot SFX. This class never decides WHEN to play music or WHICH
    /// playlist -- it just plays whatever it's told, whenever it's told:
    /// - PlayMenuMusic(clips): the caller (MainMenuController) passes the
    ///   theme-independent menu playlist, since menu music is always the
    ///   same regardless of which ThemeDefinition is selected.
    /// - PlayGameplayMusic(): plays the active theme's soundtrackPlaylist
    ///   (set via ApplyTheme). Nothing starts this automatically -- the
    ///   caller (MainMenuController's Play button) must call it explicitly,
    ///   so gameplay music never starts before the player actually presses
    ///   Play.
    /// Switching FROM one playlist TO another (e.g. menu -> gameplay) is a
    /// hard cut, by design -- only track-to-track transitions WITHIN a
    /// running playlist crossfade.
    /// ApplyTheme itself only remembers the theme for SFX lookups
    /// (PlayCaptureSound/PlayMovementSound/PlaySelectPieceSound/PlayWinSound)
    /// -- it does not touch music.
    /// A theme/playlist with no clips assigned just plays silently; nothing
    /// else needs to change.
    ///
    /// Volume is the only thing this class persists itself (PlayerPrefs) --
    /// everything else (current playlist) is per-session.
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
        /// game start) -- just remembers it for PlayGameplayMusic/SFX
        /// lookups. Does not touch music playback; call PlayGameplayMusic()
        /// separately once a game is actually starting.
        /// </summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            _theme = theme;
        }

        /// <summary>
        /// Starts (or restarts) the theme-independent menu playlist, shuffled
        /// -- the same regardless of which ThemeDefinition is selected.
        /// Safe to call again while it's already playing (e.g. re-entering
        /// the Home Screen); it just restarts the shuffle.
        /// </summary>
        public void PlayMenuMusic(AudioClip[] menuPlaylist)
        {
            StartPlaylist(menuPlaylist);
        }

        /// <summary>
        /// Starts the active theme's soundtrack playlist, shuffled. Only
        /// call this when a game is actually beginning (e.g. the Play
        /// button) -- nothing starts gameplay music on its own.
        /// </summary>
        public void PlayGameplayMusic()
        {
            StartPlaylist(_theme != null ? _theme.soundtrackPlaylist : null);
        }

        private void StartPlaylist(AudioClip[] clips)
        {
            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            // Hard cut when switching playlists entirely (menu <-> gameplay)
            // -- only track-to-track transitions within a playlist crossfade.
            _musicSourceA.Stop();
            _musicSourceB.Stop();
            _activeMusicSource = _musicSourceA;

            _shuffledPlaylist.Clear();
            if (clips != null) _shuffledPlaylist.AddRange(clips);
            Shuffle(_shuffledPlaylist);
            _playlistIndex = 0;

            if (_shuffledPlaylist.Count > 0)
                _musicRoutine = StartCoroutine(PlaylistRoutine());
        }

        public void PlayCaptureSound()
        {
            if (_theme != null && _theme.captureSound != null)
                _sfxSource.PlayOneShot(_theme.captureSound, SfxVolume);
        }

        /// <summary>
        /// Plays on any piece move, capture or not -- layers under
        /// PlayCaptureSound on a capturing move (footstep/slide plus the
        /// capture stinger).
        /// </summary>
        public void PlayMovementSound()
        {
            if (_theme != null && _theme.movementSound != null)
                _sfxSource.PlayOneShot(_theme.movementSound, SfxVolume);
        }

        /// <summary>Plays when a piece is successfully tapped/selected.</summary>
        public void PlaySelectPieceSound()
        {
            if (_theme != null && _theme.selectPieceSound != null)
                _sfxSource.PlayOneShot(_theme.selectPieceSound, SfxVolume);
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
                // Start crossfading into the next track CrossfadeDuration
                // seconds before this one ends, so the two actually overlap
                // instead of just fading in from silence after a gap.
                float clipLength = _activeMusicSource.clip != null ? _activeMusicSource.clip.length : 0f;
                float fadeStartTime = Mathf.Max(0f, clipLength - CrossfadeDuration);
                yield return new WaitUntil(() => !_activeMusicSource.isPlaying || _activeMusicSource.time >= fadeStartTime);

                if (_shuffledPlaylist.Count == 0) yield break;

                _playlistIndex = (_playlistIndex + 1) % _shuffledPlaylist.Count;
                if (_playlistIndex == 0) Shuffle(_shuffledPlaylist); // reshuffle after a full pass

                yield return CrossfadeToTrack(_shuffledPlaylist[_playlistIndex]);
            }
        }

        private IEnumerator CrossfadeToTrack(AudioClip clip)
        {
            var incoming = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;
            var outgoing = _activeMusicSource;

            incoming.clip = clip;
            incoming.loop = false;
            incoming.volume = 0f;
            incoming.Play();

            float t = 0f;
            while (t < CrossfadeDuration && outgoing.isPlaying)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / CrossfadeDuration);
                incoming.volume = Mathf.Lerp(0f, MusicVolume, p);
                outgoing.volume = Mathf.Lerp(MusicVolume, 0f, p);
                yield return null;
            }
            outgoing.Stop();
            incoming.volume = MusicVolume;
            _activeMusicSource = incoming;
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
