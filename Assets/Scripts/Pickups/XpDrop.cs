using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class XpDrop : MonoBehaviour
{
    private int _xp = 10;
    private bool _moving = false;

    [SerializeField]
    private float _detectPlayerDistance = 3f;

    [SerializeField]
    private float _moveSpeed = 5f;

    public void SetMoving(bool moving = true)
    {
        _moving = moving;
    }

    public void SetXp(int xp)
    {
        _xp = xp;
    }

    private void Update()
    {
        var player = Player.Instance;
        if (player == null)
        {
            return;
        }
        // detect if player is with 1 unit of the pickup
        if (
            !_moving
            && Vector2.Distance(player.transform.position, transform.position)
                < _detectPlayerDistance
        )
        {
            _moving = true;
        }

        if (_moving)
        {
            // move towards the player
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.transform.position,
                _moveSpeed * Time.deltaTime
            );
        }
    }

    // on trigger handler
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only the player's magnet hitbox collects XP, not the body collider -- the body is
        // non-trigger, but OnTriggerEnter2D still fires for it because this drop is a trigger.
        if (other.gameObject.CompareTag(Tags.PlayerHitbox))
        {
            GameEvents.RaiseXpEarned(_xp);

            Destroy(gameObject);
        }
    }
}
