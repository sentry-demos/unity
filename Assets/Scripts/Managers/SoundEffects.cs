using UnityEngine;

/// <summary>
/// One-shot sound effects: enemy hits and pickups.
/// </summary>
/// <remarks>
/// A <see cref="SceneSingleton{T}"/> because its callers are spawned prefabs -- projectiles and
/// pickups instantiated at runtime, which cannot hold an inspector reference. Music lives in
/// <see cref="BattleAudioManager"/> on a separate AudioSource; see the note on PlayHitSound.
/// </remarks>
public class SoundEffects : SceneSingleton<SoundEffects>
{
    [SerializeField]
    [Tooltip("Source for sound effects -- must not be the background music source")]
    private AudioSource _audioSource;

    [SerializeField]
    private AudioClip _enemyHitSound;

    [SerializeField]
    private float _hitSoundCooldown = 0.1f;

    private float _timeOfLastHitSound = 0f;

    public void PlayPickupSound(AudioClip clip)
    {
        _audioSource.PlayOneShot(clip);
    }

    public void PlayHitSound()
    {
        // don't play the sound if it's too soon
        if (Time.time - _timeOfLastHitSound < _hitSoundCooldown)
        {
            return;
        }

        // NOTE: don't use PlayOneShot on hit sounds because so many hits can
        // happen that it can cause the audio source to die (no more sound for
        // remainder of game session). Better to play manually.
        //
        // That reassignment is why this source must be its own: pointing it at the music
        // source would replace the music with a hit sound.
        _audioSource.clip = _enemyHitSound;
        _audioSource.Play();
        _timeOfLastHitSound = Time.time;
    }
}
