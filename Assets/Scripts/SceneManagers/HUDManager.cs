using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SceneManagers
{
    public class HUDManager : MonoBehaviour
    {
        private InputAction _navigateAction;
        private InputAction _submitAction;

        [SerializeField] private GameObject tryAgainButton;
        [SerializeField] private GameObject quitButton;
        [SerializeField] private ScorePoster _scorePoster;

        private Highlighter _tryAgainHighlighter;
        private Highlighter _quitHighlighter;
        private Highlighter _submitHighlighter;
        private TMP_InputField _nameField;
        private Button _submitButton;

        // DOTween animation for the name field. Driving the highlight through
        // OnPointerEnter(null) on a TMP_InputField crashes on Switch (NintendoSDK TMP
        // integration), so the scale bounce is tweened directly instead.
        private Tween _nameFieldTween;

        // Direct color tween for the submit button - mirrors the name field approach because
        // a Highlighter added at runtime doesn't reliably run DoStateTransition when the
        // button's parent was inactive at AddComponent time.
        private Tween _submitColorTween;
        private Graphic _submitGraphic;
        private Color _submitHighlightColor;
        private Color _submitNormalColor;
        private bool _submitNormalColorCaptured;

        private GameObject _highlightedButton;
        private bool _nameFieldFocused;

        // Prevent rapid double-fire from analog sticks or composite bindings.
        private float _lastNavTime;
        private const float NavCooldown = 0.2f;

        private void Awake()
        {
            _navigateAction = InputSystem.actions.FindAction("Navigate");
            _submitAction = InputSystem.actions.FindAction("Submit");

            // Subscribe to input events
            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;

            _tryAgainHighlighter = tryAgainButton.GetComponent<Highlighter>();
            _quitHighlighter = quitButton.GetComponent<Highlighter>();

            if (_scorePoster != null)
            {
                _nameField = _scorePoster.NameField;
                _submitButton = _scorePoster.SubmitButton;
                _scorePoster.OnVirtualKeyboardClosedWithText += OnVirtualKeyboardClosedWithText;
                // Reuse the navigation buttons' highlight color for the direct tween.
                _submitHighlightColor = tryAgainButton.GetComponent<Button>().colors.highlightedColor;
                _submitGraphic = _submitButton.targetGraphic != null
                    ? _submitButton.targetGraphic
                    : _submitButton.GetComponent<Graphic>();
                // The Highlighter handles the bounce scale animation; color is driven
                // separately above.
                _submitHighlighter = _submitButton.GetComponent<Highlighter>()
                    ?? _submitButton.gameObject.AddComponent<Highlighter>();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from input events
            _navigateAction.performed -= OnNavigatePerformed;
            _submitAction.performed -= OnSubmitPerformed;

            if (_scorePoster != null)
            {
                _scorePoster.OnVirtualKeyboardClosedWithText -= OnVirtualKeyboardClosedWithText;
            }

            _nameFieldTween?.Kill();
            _submitColorTween?.Kill();
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (_nameFieldFocused)
            {
                // With text present, Confirm fires the submit button directly - whether or not
                // the on-screen keyboard is still open. ClearNameFieldFocus() deactivates the
                // input field, which closes the keyboard.
                if (_submitButton != null && _submitButton.interactable)
                {
                    // While actively typing, only Enter should submit - Space and other
                    // Submit-bound keys must remain typeable characters in the field.
                    if (_nameField.isFocused && context.control.device is Keyboard kb
                        && context.control != kb.enterKey && context.control != kb.numpadEnterKey)
                    {
                        return;
                    }

                    ClearNameFieldFocus();
                    _submitButton.onClick.Invoke();
                    SetHighlightedButton(_tryAgainHighlighter);
                    return;
                }

                // No text yet - (re-)activate the input field / open the on-screen keyboard.
                // Avoid calling Select() before ActivateInputField - on Switch, Select()
                // triggers TMP's OnSelect internally, opening two keyboard instances at once.
                _nameField.ActivateInputField();
                return;
            }

            // Only invoke if the highlighted button is actually active
            if (_highlightedButton != null && _highlightedButton.activeSelf)
            {
                var isSubmit = _highlightedButton == _submitButton?.gameObject;
                _highlightedButton.GetComponent<Button>().onClick.Invoke();
                // After submitting, immediately highlight Again so the player can retry.
                if (isSubmit)
                {
                    SetHighlightedButton(_tryAgainHighlighter);
                }
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _lastNavTime < NavCooldown)
            {
                return;
            }
            _lastNavTime = Time.realtimeSinceStartup;

            var direction = context.ReadValue<Vector2>();

            if (_nameFieldFocused)
            {
                // While the field is actively focused, suppress keyboard-driven navigation
                // entirely (WASD keys like S must not trigger Navigate while typing).
                // Gamepad/d-pad input is still allowed downward to leave the field in one press.
                if (_nameField.isFocused && (context.control.device is Keyboard || direction.y >= 0))
                {
                    return;
                }

                // Navigate down away from the name field
                if (direction.y < 0)
                {
                    ClearNameFieldFocus();
                    if (_submitButton != null && _submitButton.interactable
                        && _submitHighlighter != null && _submitHighlighter.isActiveAndEnabled)
                    {
                        SetHighlightedButton(_submitHighlighter);
                    }
                    else if (_tryAgainHighlighter.isActiveAndEnabled)
                    {
                        SetHighlightedButton(_tryAgainHighlighter);
                    }
                    else if (_quitHighlighter.isActiveAndEnabled)
                    {
                        SetHighlightedButton(_quitHighlighter);
                    }
                }
                return;
            }

            if (_highlightedButton == _submitButton?.gameObject)
            {
                if (direction.y > 0)
                {
                    // Navigate up from submit to the name field
                    ClearHighlightedButtonInternal();
                    FocusNameField();
                }
                else if (direction.y < 0)
                {
                    // Navigate down from submit to the buttons row
                    ClearHighlightedButtonInternal();
                    if (_tryAgainHighlighter.isActiveAndEnabled)
                    {
                        SetHighlightedButton(_tryAgainHighlighter);
                    }
                    else if (_quitHighlighter.isActiveAndEnabled)
                    {
                        SetHighlightedButton(_quitHighlighter);
                    }
                }
                return;
            }

            // At the buttons row (tryAgain / quit / nothing highlighted)
            if (direction.y > 0)
            {
                // Stop at Submit first if it's available, otherwise go to the name field.
                if (_submitButton != null && _submitButton.interactable
                    && _submitHighlighter != null && _submitHighlighter.isActiveAndEnabled)
                {
                    SetHighlightedButton(_submitHighlighter);
                }
                else if (_nameField != null && _nameField.gameObject.activeInHierarchy && _nameField.interactable)
                {
                    ClearHighlightedButtonInternal();
                    FocusNameField();
                }
                return;
            }

            if (!_quitHighlighter.isActiveAndEnabled)
            {
                return;
            }

            // Simple left/right navigation between try again and quit buttons
            if (_highlightedButton == null)
            {
                // Default to try again button if available, otherwise quit button
                if (direction.x < 0 && _tryAgainHighlighter.isActiveAndEnabled)
                {
                    SetHighlightedButton(_tryAgainHighlighter);
                }
                else if (direction.x > 0)
                {
                    SetHighlightedButton(_quitHighlighter);
                }
            }
            else if (_highlightedButton == quitButton && direction.x < 0 && _tryAgainHighlighter.isActiveAndEnabled)
            {
                // Navigate from quit to try again
                SetHighlightedButton(_tryAgainHighlighter);
            }
            else if (_highlightedButton == tryAgainButton && direction.x > 0)
            {
                // Navigate from try again to quit
                SetHighlightedButton(_quitHighlighter);
            }
        }

        private void OnVirtualKeyboardClosedWithText()
        {
            if (_submitButton != null && _submitButton.interactable
                && _submitHighlighter != null && _submitHighlighter.isActiveAndEnabled)
            {
                SetHighlightedButton(_submitHighlighter);
            }
        }

        public void FocusNameField()
        {
            if (_nameField == null || !_nameField.gameObject.activeInHierarchy || !_nameField.interactable)
            {
                return;
            }
            _nameFieldFocused = true;
            // Activate so the player can type immediately (opens the on-screen keyboard on
            // touch platforms; focuses the field for physical keyboard input on PC).
            _nameField.ActivateInputField();
            // Animate directly via DOTween - see the field comment for why not OnPointerEnter.
            _nameFieldTween?.Kill();
            _nameFieldTween = _nameField.transform
                .DOScale(1.5f, 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.InSine)
                .SetUpdate(true) // runs during Time.timeScale = 0
                .OnComplete(() => _nameFieldTween = null);
        }

        private void ClearNameFieldFocus()
        {
            if (!_nameFieldFocused)
            {
                return;
            }
            _nameFieldFocused = false;
            _nameFieldTween?.Kill();
            _nameFieldTween = null;
            if (_nameField != null)
            {
                _nameField.transform.localScale = Vector3.one;
                _nameField.DeactivateInputField();
                // Clear the EventSystem selection so the in-flight Submit event has nowhere
                // to land after the input field is deactivated.
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        public void SetHighlightedButton(Highlighter highlighted)
        {
            ClearNameFieldFocus();
            _tryAgainHighlighter.Highlight(false);
            _quitHighlighter.Highlight(false);
            _submitHighlighter?.Highlight(false);

            // Restore the submit button color when leaving it (e.g. post-submit -> Again).
            if (_highlightedButton == _submitButton?.gameObject && _submitNormalColorCaptured && _submitGraphic != null)
            {
                _submitColorTween?.Kill();
                _submitColorTween = _submitGraphic.DOColor(_submitNormalColor, 0.1f).SetUpdate(true);
            }

            highlighted.Highlight();
            _highlightedButton = highlighted.gameObject;

            // Directly tween the submit button's color (same pattern as the name field).
            // Capture the normal color lazily so it is read after the button is fully active.
            if (highlighted == _submitHighlighter && _submitGraphic != null)
            {
                if (!_submitNormalColorCaptured)
                {
                    _submitNormalColor = _submitGraphic.color;
                    _submitNormalColorCaptured = true;
                }
                _submitColorTween?.Kill();
                _submitColorTween = _submitGraphic.DOColor(_submitHighlightColor, 0.1f).SetUpdate(true);
            }
        }

        // Clears button highlight state without touching name field focus.
        private void ClearHighlightedButtonInternal()
        {
            _tryAgainHighlighter.Highlight(false);
            _quitHighlighter.Highlight(false);
            _submitHighlighter?.Highlight(false);

            if (_submitNormalColorCaptured && _submitGraphic != null)
            {
                _submitColorTween?.Kill();
                _submitColorTween = _submitGraphic.DOColor(_submitNormalColor, 0.1f).SetUpdate(true);
            }

            _highlightedButton = null;
        }

        public void ClearHighlightedButton()
        {
            ClearNameFieldFocus();
            ClearHighlightedButtonInternal();
        }
    }
}
