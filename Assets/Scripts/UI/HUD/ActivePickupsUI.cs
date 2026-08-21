using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

class ActivePickupsUI : MonoBehaviour
{
    struct ActivePickupState
    {
        public GameObject GameObject;
        public float ExpirationTime;

        public ActivePickupState(GameObject gameObject, float expirationTime)
        {
            this.GameObject = gameObject;
            this.ExpirationTime = expirationTime;
        }

        public ActivePickupState Extend(float duration)
        {
            return new ActivePickupState(this.GameObject, this.ExpirationTime + duration);
        }
    }

    private readonly Dictionary<EntityId, ActivePickupState> _activePickups = new();

    public void Add(Sprite icon, float duration)
    {
        var iconInstanceId = icon.GetEntityId();
        if (_activePickups.ContainsKey(iconInstanceId))
        {
            // if the pickup is already active, extend its duration
            _activePickups[iconInstanceId] = _activePickups[iconInstanceId].Extend(duration);
            return;
        }

        // get in game height of this object
        float containerHeight = GetComponent<RectTransform>().rect.height;

        GameObject pickupObject = new GameObject("PickupIcon");

        Image imageComponent = pickupObject.AddComponent<Image>();
        imageComponent.sprite = icon;
        imageComponent.rectTransform.sizeDelta = new Vector2(containerHeight, containerHeight);

        pickupObject.transform.SetParent(transform);

        _activePickups.Add(
            icon.GetEntityId(),
            new ActivePickupState(pickupObject, Time.time + duration)
        );
    }

    private void Update()
    {
        var expiredPickupStateIds = (from kvp in _activePickups where Time.time > kvp.Value.ExpirationTime 
            select kvp.Key).ToList();

        foreach (var expiredPickupId in expiredPickupStateIds)
        {
            Destroy(_activePickups[expiredPickupId].GameObject);
            _activePickups.Remove(expiredPickupId);
        }
    }
}
