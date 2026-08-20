using UnityEngine;

public class PickupBase : MonoBehaviour
{
    [SerializeField]
    protected AudioClip _pickupSound;

    [SerializeField]
    [Tooltip("How much score this pickup is worth")]
    protected int _scoreValue = 50;

    [SerializeField]
    [Tooltip("How long the effect lasts (instants have 0 duration)")]
    protected float _effectDuration = 0;

    [SerializeField]
    [Tooltip("The text that will be displayed in the UI when this pickup is collected")]
    protected string _effectText;

    public Sprite Icon => GetComponent<SpriteRenderer>().sprite;

    protected virtual void OnCollect(Player player) { }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // if the player touches the pickup, destroy the pickup
        if (other.gameObject.TryGetComponent<Player>(out var player))
        {
            OnCollect(player);

            if (_pickupSound != null)
            {
                SoundEffects.Instance.PlayPickupSound(_pickupSound);
            }

            Player.Instance.SpawnPlayerText(GetEffectText());

            // Each pickup is its own component, so the type name is the pickup's name and
            // the attribute stays bounded by the class list.
            GameMetrics.RecordPickupCollected(GetType().Name);

            // Read the icon off this object before destroying it
            var collected = new PickupCollected(_scoreValue, Icon, _effectDuration);

            // Destroy the pickup
            Destroy(gameObject);

            GameEvents.RaisePickupGrabbed(collected);
        }
    }

    protected virtual string GetEffectText()
    {
        return _effectText;
    }
}
