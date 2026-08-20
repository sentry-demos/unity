using UnityEngine;

/// <summary>
/// The battle's background music, started and stopped as the game pauses and resumes.
/// </summary>
/// <remarks>
/// Distinct from <see cref="SoundEffects"/>, which plays one-shot hits and pickups. They must
/// not share an <see cref="AudioSource"/>: SoundEffects reassigns its source's clip on every
/// hit, which would cut the music off.
/// </remarks>
public class BattleAudioManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Background music source")]
    private AudioSource _backgroundMusic;

    [SerializeField]
    [Tooltip("Uncheck to silence the background music -- useful when playtesting")]
    private bool _musicEnabled = true;

    /// <summary>
    /// Whether music should play. Toggling this at runtime takes effect immediately, so it can
    /// be flipped in the inspector mid-play without restarting.
    /// </summary>
    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            if (_musicEnabled == value)
            {
                return;
            }

            _musicEnabled = value;
            Apply();
        }
    }

    // Tracks what the game asked for, so re-enabling music mid-run only resumes it if the
    // game is actually in a state that wants it playing (not paused, not dead).
    private bool _shouldPlay;

    public void PlayMusic()
    {
        _shouldPlay = true;
        Apply();
    }

    public void StopMusic()
    {
        _shouldPlay = false;
        Apply();
    }

    private void Apply()
    {
        if (_shouldPlay && _musicEnabled)
        {
            if (!_backgroundMusic.isPlaying)
            {
                _backgroundMusic.Play();
            }
        }
        else if (_backgroundMusic.isPlaying)
        {
            _backgroundMusic.Stop();
        }
    }

    private void Awake()
    {
        // The music source is set to play on awake, so the default intent is "playing".
        // Seeding from isPlaying does not work here: PlayOnAwake has not taken effect yet
        // when Awake runs, so it would read false and stop the track before it ever started.
        _shouldPlay = _backgroundMusic.playOnAwake;
        Apply();
    }

    private void OnValidate()
    {
        // Reflect an inspector toggle straight away while playing.
        if (Application.isPlaying && _backgroundMusic != null)
        {
            Apply();
        }
    }
}
