using Characters;
using DG.Tweening;
using UI;
using UnityEngine;
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

        // Wired straight from the scene. These used to be read back off ScorePoster, which
        // meant the panel had to expose its own widgets for someone else to drive.
        [SerializeField] private NameEntryField _nameEntry;
        [SerializeField] private Button _submitButton;

        private Highlighter _tryAgainHighlighter;
        private Highlighter _quitHighlighter;
        private Highlighter _submitHighlighter;

        // DOTween animation for the name field. Driving the highlight through
        // OnPointerEnter(null) on a TMP_InputField crashes on Switch (NintendoSDK TMP
        // integration), so the scale bounce is tweened directly instead.
        private Tween _nameFieldTween;

        [Tooltip(
            "How much the name field swells when navigation lands on it. It is far wider than "
                + "a button, so this wants to be much gentler than the Highlighter's bounce"
        )]
        [SerializeField] private float _nameFieldBounce = 1.06f;

        [SerializeField] private float _nameFieldBounceTime = 0.12f;

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

        // Navigate is a pass-through action bound to an analog stick, so it fires on every value
        // change -- every frame while the stick is off centre, and once more on the way back to
        // it. What arrives is the raw vector, which carries whatever the other axis happens to
        // be reading: a stick pushed left reports a little up or down along with it.
        //
        // Both facts are handled by quantising: the vector becomes one of four steps, and a step
        // only counts when it differs from the last one. This used to be a flat 0.2s cooldown,
        // armed before the direction was read, so letting go of the stick started a fresh dead
        // window and swallowed the next press. Same treatment as the level-up screen.
        private Vector2Int _navStep;

        // Entering a step takes a firm push; leaving it takes a fair return towards centre, so a
        // stick resting near the line does not chatter between two buttons.
        private const float NavEnter = 0.5f;
        private const float NavRelease = 0.35f;

        // Graphic.DOColor lives in DOTween's UI module, which compiles into
        // Assembly-CSharp-firstpass and is not visible from this asmdef assembly - only the
        // precompiled DOTween core is. The generic To() covers the same ground.
        private Tween TweenSubmitColor(Color target) =>
            DOTween.To(() => _submitGraphic.color, c => _submitGraphic.color = c, target, 0.1f)
                .SetUpdate(true);

        private void Awake()
        {
            _navigateAction = InputSystem.actions.FindAction("Navigate");
            _submitAction = InputSystem.actions.FindAction("Submit");

            // Subscribe to input events
            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;

            _tryAgainHighlighter = tryAgainButton.GetComponent<Highlighter>();
            _quitHighlighter = quitButton.GetComponent<Highlighter>();

            if (_nameEntry != null)
            {
                _nameEntry.VirtualKeyboardClosedWithText += OnVirtualKeyboardClosedWithText;
            }

            if (_submitButton != null)
            {
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

            if (_nameEntry != null)
            {
                _nameEntry.VirtualKeyboardClosedWithText -= OnVirtualKeyboardClosedWithText;
            }

            _nameFieldTween?.Kill();
            _submitColorTween?.Kill();
        }

        /// <summary>
        /// Whether this came from the pad the demo run drives itself with.
        /// </summary>
        /// <remarks>
        /// During a demo run both action maps are live: the synthetic pad needs the Player map,
        /// and a watching human needs the UI map to press Again. Without this the demo's own
        /// stick also worked the menu, and the highlight hopped between Again and Quit on its
        /// own while nobody was touching anything.
        /// </remarks>
        private static bool IsDemoInput(InputAction.CallbackContext context)
        {
            var device = context.control?.device;
            return device != null && device.name == DemoPlayerController.VirtualGamepadName;
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf || IsDemoInput(context))
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
                    if (_nameEntry.IsTyping && context.control.device is Keyboard kb
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
                _nameEntry.Focus();
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

        /// <summary>
        /// The raw stick vector as one of four steps, or zero for centred. The dominant axis
        /// wins, so a left push carrying a little upward bleed is a left and not an up.
        /// </summary>
        private Vector2Int ReadNavStep(Vector2 raw)
        {
            var horizontal = Mathf.Abs(raw.x) >= Mathf.Abs(raw.y);
            var axis = horizontal ? raw.x : raw.y;

            // Already standing in a step on this axis, so it takes less to stay in it.
            var held = horizontal ? _navStep.x != 0 : _navStep.y != 0;

            if (Mathf.Abs(axis) < (held ? NavRelease : NavEnter))
            {
                return Vector2Int.zero;
            }

            var step = axis > 0 ? 1 : -1;
            return horizontal ? new Vector2Int(step, 0) : new Vector2Int(0, step);
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                // Nobody is listening, so where the stick happens to be resting must not carry
                // over as a step already taken the next time this screen comes up.
                _navStep = Vector2Int.zero;
                return;
            }

            if (IsDemoInput(context))
            {
                return;
            }

            var step = ReadNavStep(context.ReadValue<Vector2>());
            if (step == _navStep)
            {
                return;
            }

            _navStep = step;

            // Back to centre. Nothing to move, but the next push now counts as a fresh one.
            if (step == Vector2Int.zero)
            {
                return;
            }

            var direction = (Vector2)step;

            if (_nameFieldFocused)
            {
                // While the field is actively focused, suppress keyboard-driven navigation
                // entirely (WASD keys like S must not trigger Navigate while typing).
                // Gamepad/d-pad input is still allowed downward to leave the field in one press.
                if (_nameEntry.IsTyping && (context.control.device is Keyboard || direction.y >= 0))
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
                else if (_nameEntry != null && _nameEntry.CanEdit)
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

        /// <summary>
        /// Gives the name field typing focus. <paramref name="announce"/> plays the bounce that
        /// says navigation landed here; pass false when something else already drew the eye,
        /// such as the game-over reveal, where it reads as a stray pop after everything settles.
        /// </summary>
        public void FocusNameField(bool announce = true)
        {
            if (_nameEntry == null || !_nameEntry.CanEdit)
            {
                return;
            }
            _nameFieldFocused = true;
            // Activate so the player can type immediately (opens the on-screen keyboard on
            // touch platforms; focuses the field for physical keyboard input on PC).
            _nameEntry.Focus();

            if (!announce)
            {
                return;
            }

            // Animate directly via DOTween - see the field comment for why not OnPointerEnter.
            _nameFieldTween?.Kill();
            _nameFieldTween = _nameEntry.transform
                .DOScale(_nameFieldBounce, _nameFieldBounceTime)
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
            if (_nameEntry != null)
            {
                _nameEntry.transform.localScale = Vector3.one;
                _nameEntry.Blur();
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
                _submitColorTween = TweenSubmitColor(_submitNormalColor);
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
                _submitColorTween = TweenSubmitColor(_submitHighlightColor);
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
                _submitColorTween = TweenSubmitColor(_submitNormalColor);
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
