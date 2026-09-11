using System;
using System.Collections;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SceneManagers
{
    public class TitleSceneManager : MonoBehaviour
    {
        private DemoConfiguration _demoConfig;
        private InputAction _navigateAction;

        [SerializeField] private GameObject startButton;
        [SerializeField] private GameObject quitButton;

        private Highlighter _startHighlighter;
        private Highlighter _quitHighlighter;

        private GameObject _highlightedButton;

        // How far sideways counts as a sideways push. Matches the game-over screen.
        private const float NavThreshold = 0.5f;

        private void Awake()
        {
            _demoConfig = DemoConfiguration.Load();
            _navigateAction = InputSystem.actions.FindAction("Navigate");
            _startHighlighter = startButton.GetComponent<Highlighter>();
            _quitHighlighter = quitButton.GetComponent<Highlighter>();
        }

        private void Start()
        {
            // Pre-select Start so a controller has a focused button without touching the stick.
            SetHighlightedButton(_startHighlighter);

            if (_demoConfig != null && _demoConfig.AutoPlay)
            {
                StartCoroutine(AutoStartGame());
            }
        }

        private IEnumerator AutoStartGame()
        {
            yield return new WaitForSecondsRealtime(Random.value);

            SetHighlightedButton(_startHighlighter);

            yield return new WaitForSecondsRealtime(Random.value);

            StartGame();
        }

        public void OnNavigate()
        {
            if (!_navigateAction.IsPressed())
            {
                return;
            }

            // A firm push, not any push. The raw stick vector carries whatever the other axis
            // reads, so an upward shove with a little sideways bleed used to move the highlight.
            var direction = _navigateAction.ReadValue<Vector2>();
            if (Mathf.Abs(direction.x) < NavThreshold || Mathf.Abs(direction.x) < Mathf.Abs(direction.y))
            {
                return;
            }

            SetHighlightedButton(direction.x < 0 ? _startHighlighter : _quitHighlighter);
        }

        public void OnSubmit() => _highlightedButton?.GetComponent<Button>().onClick.Invoke();

        public void SetHighlightedButton(Highlighter highlighted)
        {
            // Navigate fires every frame the stick is off centre, and this is reached on each
            // one. Landing on the button that is already lit has to be nothing at all, or the
            // bounce restarts every frame and never gets far enough to be seen.
            if (_highlightedButton == highlighted.gameObject)
            {
                return;
            }

            _startHighlighter.Highlight(false);
            _quitHighlighter.Highlight(false);

            highlighted.Highlight();
            _highlightedButton = highlighted.gameObject;
        }

        public void StartGame()
        {
            // CI's demo run proves the build reached gameplay from this line.
            // Mirrored in .github/scripts/lib/DemoRun.psm1.
            Debug.Log("Start Game");
            SceneManager.LoadScene("BattleScene", LoadSceneMode.Single);
        }

        public void QuitGame()
        {
            Debug.Log("Quit (Note this won't quit in the editor)");
            Application.Quit();
        }

        private void UnityOfBugs()
        {
            Debug.Log("Loading UnityOfBugs");
            SceneManager.LoadScene("1_Bugfarm", LoadSceneMode.Single);
        }
    }
}
