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

        private Highlighter _tryAgainHighlighter;
        private Highlighter _quitHighlighter;

        private GameObject _highlightedButton;

        private void Awake()
        {
            _navigateAction = InputSystem.actions.FindAction("Navigate");
            _submitAction = InputSystem.actions.FindAction("Submit");

            // Subscribe to input events
            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;

            _tryAgainHighlighter = tryAgainButton.GetComponent<Highlighter>();
            _quitHighlighter = quitButton.GetComponent<Highlighter>();
        }

        private void OnDestroy()
        {
            // Unsubscribe from input events
            _navigateAction.performed -= OnNavigatePerformed;
            _submitAction.performed -= OnSubmitPerformed;
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            // Only invoke if the highlighted button is actually active
            if (_highlightedButton != null && _highlightedButton.activeSelf)
            {
                _highlightedButton.GetComponent<Button>().onClick.Invoke();
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (!_quitHighlighter.isActiveAndEnabled)
            {
                return;
            }

            var direction = context.ReadValue<Vector2>();

            // Simple navigation between try again and quit buttons
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

        public void SetHighlightedButton(Highlighter highlighted)
        {
            _tryAgainHighlighter.Highlight(false);
            _quitHighlighter.Highlight(false);

            highlighted.Highlight();
            _highlightedButton = highlighted.gameObject;
        }

        public void ClearHighlightedButton()
        {
            _tryAgainHighlighter.Highlight(false);
            _quitHighlighter.Highlight(false);
            _highlightedButton = null;
        }
    }
}
