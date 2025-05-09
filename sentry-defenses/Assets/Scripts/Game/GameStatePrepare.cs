using System.Collections;
using Manager;
using UnityEngine;

namespace Game
{
    public class GameStatePrepare : GameState
    {
        private GameStateMachine _stateMachine;
        private EventManager _eventManager;
        private GameData _gameData;
        private readonly StartMenu _startMenu;
        private readonly PickUpgradeMenu _pickUpgradeMenu;
        private readonly ApplyUpgradeMenu _applyUpgradeMenu;
        
        public GameStatePrepare(GameStateMachine stateMachine) : base(stateMachine)
        {
            _stateMachine = stateMachine;
            _eventManager = EventManager.Instance;
            _gameData = GameData.Instance;
            _pickUpgradeMenu = stateMachine.PickUpgradeMenu;
            _applyUpgradeMenu = stateMachine.ApplyUpgradeMenu;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _pickUpgradeMenu.Prepare();
            _applyUpgradeMenu.Prepare();

            if (_gameData.UnattendedMode)
            {
                _stateMachine.StartCoroutine(ContinuePlaying());    
            }
            else
            {
                StateTransition(GameStates.Fight);
            }
        }

        private IEnumerator ContinuePlaying()
        {
            yield return new WaitForSeconds(Random.value);
            StateTransition(GameStates.Fight);
        }
    }
}