using System.Collections;
using UnityEngine;

public class GameStatePlaceSentry : GameState
{
    private readonly GameStateMachine _stateMachine;
    private readonly PlayerInput _input;
    private readonly GameData _data;
    private readonly Transform _mouseTransform;

    private GameObject _sentryGameObject;
    
    public GameStatePlaceSentry(GameStateMachine stateMachine) : base(stateMachine)
    {
        _stateMachine = stateMachine;
        _input = PlayerInput.Instance;
        _data = GameData.Instance;
        _mouseTransform = stateMachine.MouseTransform;
    }

    public override void Tick()
    {
        base.Tick();

        if (_input.GetMouseDown() && !Helpers.IsMouseOverUI())
        {
            _sentryGameObject = GameObject.Instantiate(_data.SentryPrefab, _mouseTransform.position, Quaternion.identity, _mouseTransform);
            var sentry = _sentryGameObject.GetComponent<SentryTower>();
            sentry.Wiggle();
            return;
        }
        
        // Checking for tower because the up from the button click gets read here too
        if (_sentryGameObject != null && _input.GetMouseUp())
        {
            var sentry = _sentryGameObject.GetComponent<SentryTower>();
            sentry.Drop();
            
            _sentryGameObject.transform.parent = null;
            _sentryGameObject = null;
            
            StateTransition(GameStates.Fight);
        }
    }

    public override void OnEnter()
    {
        base.OnEnter();
        
        if (_data.UnattendedMode)
        {
            var spawnPosition = Random.insideUnitSphere;
            spawnPosition.z = 0;
            _sentryGameObject = GameObject.Instantiate(_data.SentryPrefab, spawnPosition, Quaternion.identity);
            var sentry = _sentryGameObject.GetComponent<SentryTower>();
            sentry.Wiggle();
            
            _stateMachine.StartCoroutine(ContinuePlaying());
        }
    }

    private IEnumerator ContinuePlaying()
    {
        yield return new WaitForSeconds(Random.value);
        
        var sentry = _sentryGameObject.GetComponent<SentryTower>();
        sentry.Drop();
            
        _sentryGameObject.transform.parent = null;
        _sentryGameObject = null;
            
        StateTransition(GameStates.Fight);
    }
}
