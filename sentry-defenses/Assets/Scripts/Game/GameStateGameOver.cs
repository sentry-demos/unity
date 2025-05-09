using System.Collections;
using Manager;
using UnityEngine;

namespace Game
{
    public class GameStateGameOver : GameState
    {
        private readonly GameStateMachine _stateMachine;
        private GameData _gameData;
        private readonly EventManager _eventManager;
        private GameOverMenu _gameOverMenu;
        
        public GameStateGameOver(GameStateMachine stateMachine) : base(stateMachine)
        {
            _stateMachine = stateMachine;
            _gameData = GameData.Instance;
            _eventManager = EventManager.Instance;
            _eventManager.OnFight += OnFight;

            _gameOverMenu = stateMachine.GameOverMenu;
        }

        private void OnFight()
        {
            if (!IsActive)
            {
                return;
            }
            
            StateTransition(GameStates.Fight);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _eventManager.Reset();
            _gameOverMenu.SetBugCount(_gameData.BugCount);
            _gameOverMenu.Show(() =>
            {
                if (_gameData.UnattendedMode)
                {
                    _stateMachine.StartCoroutine(ContinuePlaying());
                }    
            });
        }
        
        private IEnumerator ContinuePlaying()
        {
            yield return new WaitForSeconds(Random.value);
            OnFight();
        }

        public override void OnExit()
        {
            base.OnExit();
            _gameOverMenu.Hide(null);
        }
    }
}