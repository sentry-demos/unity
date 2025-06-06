using System.Collections;
using UnityEngine;

namespace Game
{
    public class GameStateApplyUpgrade : GameState
    {
        private readonly GameStateMachine _stateMachine;
        private readonly ApplyUpgradeMenu _applyUpgradeMenu;
        
        private readonly PlayerInput _input;
        private readonly Camera _camera;
        private readonly GameData _data;
        
        public GameStateApplyUpgrade(GameStateMachine stateMachine) : base(stateMachine)
        {
            _stateMachine = stateMachine;
            _applyUpgradeMenu = stateMachine.ApplyUpgradeMenu;
            
            _input = PlayerInput.Instance;
            _camera = Camera.main;
            _data = GameData.Instance;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _applyUpgradeMenu.Show();

            if (_data.UnattendedMode)
            {
                _stateMachine.StartCoroutine(ContinuePlaying());
            }
        }

        public override void Tick()
        {
            base.Tick();
                    
            if (_input.GetMouseDown() && !Helpers.IsMouseOverUI())
            {
                var ray = _camera.ScreenPointToRay(Input.mousePosition);
                var hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity);
                if (hit)
                {
                    var sentry = hit.collider.GetComponent<SentryTower>();
                    if (sentry == null)
                    {
                        return;
                    }

                    switch (_stateMachine.PickedUpgrade)
                    {
                        case UpgradeType.Damage:
                            sentry.Upgrades.Damage++;
                            break;
                        case UpgradeType.FireRate:
                            sentry.Upgrades.FireRate++;
                            break;
                        case UpgradeType.Range:
                            sentry.Upgrades.Range++;
                            break;
                    }

                    sentry.PostUpgrade();
                    
                    StateTransition(GameStates.Fight);
                }
            }
        }
        
        private IEnumerator ContinuePlaying()
        {
            yield return new WaitForSeconds(Random.value);
            
            var sentry = GameObject.FindFirstObjectByType<SentryTower>();
            switch (_stateMachine.PickedUpgrade)
            {
                case UpgradeType.Damage:
                    sentry.Upgrades.Damage++;
                    break;
                case UpgradeType.FireRate:
                    sentry.Upgrades.FireRate++;
                    break;
                case UpgradeType.Range:
                    sentry.Upgrades.Range++;
                    break;
            }

            sentry.PostUpgrade();
                    
            StateTransition(GameStates.Fight);
        }
        
        public override void OnExit()
        {
            base.OnExit();
            _applyUpgradeMenu.Hide(null);
        }
    }
}