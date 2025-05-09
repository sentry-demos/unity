using System;
using System.Collections;
using Game;
using Manager;
using Sentry;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameStatePickUpgrade : GameState
{
    private readonly PickUpgradeMenu _pickUpgradeMenu;
    private readonly GameStateMachine _stateMachine;
    private readonly GameData _data;

    public GameStatePickUpgrade(GameStateMachine stateMachine) : base(stateMachine)
    {
        _stateMachine = stateMachine;
        _pickUpgradeMenu = _stateMachine.PickUpgradeMenu;
        _data = GameData.Instance;
    }
    
    private void SetUpgrade(UpgradeType upgradeType)
    {
        _stateMachine.PickedUpgrade = upgradeType;
        
        _pickUpgradeMenu.Hide(() =>
        {
            if (upgradeType == UpgradeType.NewTower)
            {
                CheckoutNewSentry();
                StateTransition(GameStates.PlaceSentry);
            }
            else
            {
                StateTransition(GameStates.UpgradeSentry);
            }    
        });
    }
    
    public override void OnEnter()
    {
        base.OnEnter();
        _stateMachine.PickedUpgrade = UpgradeType.None;
        
        EventManager.Instance.PauseGame();

        var upgradeCount = Enum.GetNames(typeof(UpgradeType)).Length;
        var firstUpgrade = Random.Range(1, upgradeCount);
        var secondUpgrade = Random.Range(1, upgradeCount - 1);
        if (secondUpgrade >= firstUpgrade) // because we don't want the same upgrade twice
        {
            secondUpgrade++;
        }

        _pickUpgradeMenu.CreateButton((UpgradeType)firstUpgrade, SetUpgrade);
        _pickUpgradeMenu.CreateButton((UpgradeType)secondUpgrade, SetUpgrade);
        
        _pickUpgradeMenu.Show(() =>
        {
            if (_data.UnattendedMode)
            {
                var selectedUpgrade = Random.value <= 0 ? firstUpgrade : secondUpgrade;
                _stateMachine.StartCoroutine(ContinuePlaying((UpgradeType)selectedUpgrade));
            }    
        });
    }

    private IEnumerator ContinuePlaying(UpgradeType selectedUpgrade)
    {
        yield return new WaitForSeconds(Random.value);
        SetUpgrade(selectedUpgrade);
    }
    
    private static void CheckoutNewSentry()
    {
        var checkoutTransaction = SentrySdk.StartTransaction("checkout", "http.client");
        SentrySdk.ConfigureScope(scope => scope.Transaction = checkoutTransaction);
        
        var processDataSpan = checkoutTransaction.StartChild("task", "process_upgrade_data");
        var upgradeData = new UpgradeData
        {
            UpgradeName = nameof(UpgradeType.NewTower),
            PlayerEmail = "player@sentry-defenses.com"
        };
        var jsonString = JsonUtility.ToJson(upgradeData);
        
        System.Threading.Tasks.Task.Delay(100).Wait();
        
        processDataSpan.Finish();
        
        var domain = "https://aspnetcore.empower-plant.com";
        var checkoutEndpoint = "/checkout";
        var checkoutURL = domain + checkoutEndpoint;
        
        var client = new System.Net.Http.HttpClient(new SentryHttpMessageHandler());
        var content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = client.PostAsync(checkoutURL, content).Result;
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Successfully updated the upgrade");
            }
            else
            {
                
                Debug.LogError($"Checkout failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during checkout: {ex.Message}");
            SentrySdk.CaptureException(ex);
            checkoutTransaction.Finish(SpanStatus.InternalError);
            return;
        }
        
        checkoutTransaction.Finish(SpanStatus.Ok);
    }
    
    [Serializable]
    public class UpgradeData
    {
        public string UpgradeName;
        public string PlayerEmail;
    }
}
