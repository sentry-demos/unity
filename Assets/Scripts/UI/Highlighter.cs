using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// The pop a button makes when navigation lands on it, and the colour that says it is the
    /// one Confirm will press.
    /// </summary>
    /// <remarks>
    /// Every call starts from a clean slate: the running bounce is killed and the scale put
    /// back before anything else happens. It used to skip the bounce whenever one was already
    /// playing, and leave the tween running on the button being left, so moving quickly along a
    /// row gave the colour without the pop and left the button behind still swelling.
    /// </remarks>
    public class Highlighter : MonoBehaviour
    {
        [SerializeField] private float bounceStrength = 1.5f;
        [SerializeField] private float bounceDuration = 0.1f;
        [SerializeField] private Ease bounceEase = Ease.InSine;

        private Selectable _selectable;
        private Tween _currentTween;
        private Vector3 _originalScale;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _originalScale = transform.localScale;
        }

        public void Highlight(bool highlight = true)
        {
            // Whichever way this goes, the button starts from where it belongs rather than from
            // wherever the last bounce had got to.
            Settle();

            if (!highlight)
            {
                // Un-highlight the button.
                _selectable?.OnPointerExit(null);
                return;
            }

            // If there is an actual button: show the highlight colour.
            _selectable?.OnPointerEnter(null);

            _currentTween = transform.DOScale(_originalScale * bounceStrength, bounceDuration)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(bounceEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _currentTween = null;
                });
        }

        private void Settle()
        {
            if (_currentTween == null)
            {
                return;
            }

            if (_currentTween.IsActive())
            {
                _currentTween.Kill();
            }

            _currentTween = null;
            transform.localScale = _originalScale;
        }

        private void OnDestroy()
        {
            Settle();
        }
    }
}
