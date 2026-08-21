using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Sentry;
using Sentry.Unity;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Upgrades
{
    /**
 * Encapsulates behavior of LevelUpUI prefab
 */
    public class LevelUpUI : MonoBehaviour
    {
        private InputAction _navigateAction;
        private InputAction _submitAction;

        // fyi: title -> upgrade name, description -> level, stats -> description
        // leveling up an upgrade, changes the stats to new level, increases the level #

        [SerializeField] private LevelOptionUI _levelOption1;
        [SerializeField] private LevelOptionUI _levelOption2;
        [SerializeField] private BattleSceneManager _gameManager;

        private DemoConfiguration _demoConfig;

        private Button _option1Button;
        private Button _option2Button;
        private Button _highlightedButton;

        private void Awake()
        {
            _demoConfig = DemoConfiguration.Load();

            _navigateAction = InputSystem.actions.FindAction("Navigate");
            _submitAction = InputSystem.actions.FindAction("Submit");

            _option1Button = _levelOption1.GetComponent<Button>();
            _option2Button = _levelOption2.GetComponent<Button>();
        }

        private void OnEnable()
        {
            InputSystem.actions.FindActionMap("Player").Disable();
            InputSystem.actions.FindActionMap("UI").Enable();

            // Subscribe to input events
            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;

            // Pause the game
            Time.timeScale = 0;

            List<UpgradePathBase> paths = null;
            if (_demoConfig != null && _demoConfig.FetchUpgradeFromServer)
            {
                paths = GetUpgrades();
            }

            paths ??= UpgradeManager.Instance.GetRandomUpgradePaths(2);
            if (paths == null || paths.Count == 0)
            {
                Debug.LogWarning("No upgrade paths available. Everything fully upgraded?");
                GameMetrics.Count(GameMetrics.UpgradePoolExhausted, 1);
                Time.timeScale = 1;
                gameObject.SetActive(false);
                return;
            }


            var upgradeChoice1 = paths[0];
            var upgradeChoice2 = paths.Count > 1 ? paths[1] : paths[0]; // In case there is only one upgrade left

            SetLevelOptionUI(upgradeChoice1, upgradeChoice2);

            _option1Button.onClick.AddListener(() => SelectUpgrade(upgradeChoice1));
            _option2Button.onClick.AddListener(() => SelectUpgrade(upgradeChoice2));

            // Set initial highlighted button to option 1
            SetHighlightedButton(_option1Button);

            if (_demoConfig != null && _demoConfig.AutoPlay)
            {
                StartCoroutine(SelectSomething());
            }
        }

        private IEnumerator SelectSomething()
        {
            var delay = Random.value;
            Debug.Log($"Starting to select in {delay} seconds");
            yield return new WaitForSecondsRealtime(delay);

            Debug.Log("Done waiting");

            if (Random.value > 0.5f)
            {
                Debug.Log("Selected left");
                SetHighlightedButton(_option1Button);
            }
            else
            {
                Debug.Log("Selected right");
                SetHighlightedButton(_option2Button);
            }

            yield return new WaitForSecondsRealtime(Random.value);

            Debug.Log("Clicking the highlighted button");
            _highlightedButton?.GetComponent<Button>().onClick.Invoke();
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            var direction = context.ReadValue<Vector2>();
            if (direction.x < 0)
            {
                SetHighlightedButton(_option1Button);
            }
            else if (direction.x > 0)
            {
                SetHighlightedButton(_option2Button);
            }
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            _highlightedButton?.onClick.Invoke();
        }

        public void SetHighlightedButton(Button button)
        {
            _option1Button.GetComponent<Highlighter>().Highlight(false);
            _option2Button.GetComponent<Highlighter>().Highlight(false);

            button.GetComponent<Highlighter>().Highlight();
            _highlightedButton = button;
        }

        private void OnDisable()
        {
            // Unsubscribe from input events
            _navigateAction.performed -= OnNavigatePerformed;
            _submitAction.performed -= OnSubmitPerformed;

            _option1Button.onClick.RemoveAllListeners();
            _option2Button.onClick.RemoveAllListeners();
        }

        // Given a set of option choices, update the UI accordingly
        private void SetLevelOptionUI(UpgradePathBase option1, UpgradePathBase option2)
        {
            _levelOption1.Set(
                title: option1.Title,
                description: "Level " + option1.NextLevel,
                stats: option1.NextDescription,
                icon: option1.Icon
            );

            if (option1 == option2)
            {
                _levelOption2.SetMaxedOut();
            }
            else
            {
                _levelOption2.Set(
                    title: option2.Title,
                    description: "Level " + option2.NextLevel,
                    stats: option2.NextDescription,
                    icon: option2.Icon
                );
            }
        }

        private void SelectUpgrade(UpgradePathBase selectedUpgrade)
        {
            InputSystem.actions.FindActionMap("Player").Enable();
            InputSystem.actions.FindActionMap("UI").Disable();

            UpgradeManager.Instance.LevelUpUpgradePath(selectedUpgrade);

            // Resume the game and exit the level up popup
            Time.timeScale = 1;
            gameObject.SetActive(false);
        }

        // INTENTIONAL: the parsing is deliberately incomplete, to generate HTTP traffic
        // and failures for Sentry. Gated on DemoConfiguration.FetchUpgradeFromServer.
        // See CONTRIBUTING.md.
        //
        // Caveat: on failure this returns null and the caller's `??=` falls back to local
        // upgrades, but on success it returns an empty list, which skips the fallback and
        // costs the player that upgrade. Fix the caller, not the parsing.
        private List<UpgradePathBase> GetUpgrades()
        {
            // On the run's trace, so the level-up that triggered this fetch and the error it
            // throws read as one story. The op describes the fetch as a whole; the outgoing
            // request underneath it is the http.client span.
            var fetchTransaction = RunTrace.StartTransaction("fetch_upgrades", "ui.upgrade.fetch");
            RunTrace.SetScopeTransaction(fetchTransaction);

            // Emitted inside the transaction, so every one of these metrics is trace
            // connected: the count that goes up leads to the trace that explains it.
            var started = Stopwatch.StartNew();
            var result = "error";

            try
            {
                var responseContent = FetchUpgradeDataFromServer(fetchTransaction);
                var upgrades = ParseUpgradeData(responseContent, fetchTransaction);

                if (upgrades == null)
                {
                    Debug.LogWarning("Upgrade parsing failed, falling back to local upgrades");
                    result = "parse_failed";

                    // The parse span already finished InternalError. Finishing the parent Ok
                    // reported the transaction as a success whose child had failed.
                    fetchTransaction.Finish(SpanStatus.InternalError);
                    return null;
                }

                // Distinct from "ok" on purpose. A parse that succeeds but maps nothing skips
                // the caller's fallback and costs the player the upgrade, and the only thing
                // that separates it from a healthy fetch is the count being zero.
                result = upgrades.Count > 0 ? "ok" : "empty";

                fetchTransaction.Finish(SpanStatus.Ok);
                return upgrades;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error fetching upgrades from server: {ex.Message}");
                SentrySdk.CaptureException(ex);
                fetchTransaction.Finish(SpanStatus.InternalError);

                return null;
            }
            finally
            {
                GameMetrics.Count(GameMetrics.UpgradeFetch, 1, (GameMetrics.ResultKey, result));
                GameMetrics.Distribution(
                    GameMetrics.UpgradeFetchDuration,
                    started.Elapsed.TotalMilliseconds,
                    MeasurementUnit.Duration.Millisecond,
                    (GameMetrics.ResultKey, result)
                );

                RunTrace.ClearScopeTransaction();
            }
        }

        private string FetchUpgradeDataFromServer(ITransactionTracer transaction)
        {
            // Named for what it covers: this runs before the request goes out, so the old
            // "process_level_data" read as post-processing that had not happened yet.
            var prepareSpan = transaction.StartChild("task", "prepare_upgrade_request");

            var currentLevel = _gameManager.GetCurrentLevel();

            // Floored, because an unfloored Random.value put 0.02ms spans in the distribution
            // next to 90ms ones and made the span look broken rather than simulated.
            System.Threading.Tasks.Task.Delay((int)(20 + Random.value * 80)).Wait(); // Simulate some work to be done

            prepareSpan.Finish();

            const string domain = "https://aspnetcore.empower-plant.com";
            const string upgradesEndpoint = "/reviews";
            var upgradesURL = $"{domain}{upgradesEndpoint}?currentLevel={currentLevel}";

            var client = new System.Net.Http.HttpClient(new SentryHttpMessageHandler());
            client.Timeout = TimeSpan.FromSeconds(3);

            try
            {
                var response = client.GetAsync(upgradesURL).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Server returned error status: {response.StatusCode}");
                }

                Debug.Log("Successfully fetched upgrade data from server");
                var responseContent = response.Content.ReadAsStringAsync().Result;

                return responseContent;
            }
            finally
            {
                client.Dispose();
            }
        }

        private List<UpgradePathBase> ParseUpgradeData(string responseContent, ITransactionTracer transaction)
        {
            var parseSpan = transaction.StartChild("task", "parse_upgrade_data");

            try
            {
                var serverResponse = JsonUtility.FromJson<ServerUpgradeResponse>(responseContent);
                var upgradePaths = new List<UpgradePathBase>(serverResponse.upgrades.Length);

                for (var i = 0; i < serverResponse.upgrades.Length; i++)
                {
                    var serverUpgrade = serverResponse.upgrades[i];

                    // We need to map server upgrade data to actual UpgradePathBase instances
                    // We'd need a sort of factory method or some mapping logic here
                }

                parseSpan.Finish(SpanStatus.Ok);
                return upgradePaths;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to parse the upgrade data.");
                SentrySdk.CaptureException(ex);
                parseSpan.Finish(SpanStatus.InternalError);
                return null;
            }
        }

        [Serializable]
        public class ServerUpgradeResponse
        {
            public ServerUpgrade[] upgrades;
        }

        [Serializable]
        public class ServerUpgrade
        {
        }
    }
}
