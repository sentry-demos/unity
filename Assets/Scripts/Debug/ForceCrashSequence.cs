using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// INTENTIONAL: the d-pad force-crash. Up Up Down Down Left Right Left Right.
/// </summary>
/// <remarks>
/// <para>
/// A wrong direction, a diagonal, or a pause longer than <see cref="SequenceTimeout"/> resets
/// progress, so it cannot be entered by accident. That is what makes it safe to ship armed,
/// which it must be: it exists so the native crash in <see cref="NativeScoreSaver"/> can be
/// demoed on a console build, and a console has no practical way to pass -demo or set
/// SENTRY_DEMO. <see cref="DemoConfiguration.KonamiCrash"/> is therefore deliberately not
/// covered by the demo master switch. See CONTRIBUTING.md.
/// </para>
/// <para>
/// Runs on its own Update, so it works while playing, while paused, and on the game-over
/// screen alike. Missing input actions simply disable the trigger.
/// </para>
/// </remarks>
public class ForceCrashSequence : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Supplies the score handed to the native call")]
    private BattleSceneManager _battle;

    private const float SequenceTimeout = 2f;

    // Direction codes: 0 = Up, 1 = Down, 2 = Left, 3 = Right
    private static readonly int[] Sequence = { 0, 0, 1, 1, 2, 3, 2, 3 };

    private DemoConfiguration _demoConfig;

    private InputAction _up;
    private InputAction _down;
    private InputAction _left;
    private InputAction _right;

    private int _index;
    private float _lastInputTime;

    private void Awake()
    {
        _demoConfig = DemoConfiguration.Load();
    }

    private void Start()
    {
        _up = InputSystem.actions.FindAction("CrashSeqUp");
        _down = InputSystem.actions.FindAction("CrashSeqDown");
        _left = InputSystem.actions.FindAction("CrashSeqLeft");
        _right = InputSystem.actions.FindAction("CrashSeqRight");
        _index = 0;
    }

    private void Update()
    {
        // No config asset at all leaves the sequence armed: that is how it ships, and it is
        // what a console build depends on.
        if (_demoConfig != null && !_demoConfig.KonamiCrash)
        {
            return;
        }

        var pressed = -1;
        var pressedCount = 0;
        if (_up != null && _up.WasPressedThisFrame())
        { pressed = 0; pressedCount++; }
        if (_down != null && _down.WasPressedThisFrame())
        { pressed = 1; pressedCount++; }
        if (_left != null && _left.WasPressedThisFrame())
        { pressed = 2; pressedCount++; }
        if (_right != null && _right.WasPressedThisFrame())
        { pressed = 3; pressedCount++; }

        if (pressedCount == 0)
        {
            return;
        }

        // More than one direction in the same frame (e.g. a diagonal) - treat as a miss and
        // start over.
        if (pressedCount > 1)
        {
            _index = 0;
            return;
        }

        // Reset progress if the player paused too long since the last input.
        if (Time.unscaledTime - _lastInputTime > SequenceTimeout)
        {
            _index = 0;
        }
        _lastInputTime = Time.unscaledTime;

        if (pressed == Sequence[_index])
        {
            _index++;
            if (_index >= Sequence.Length)
            {
                Debug.Log("ForceCrash triggered via d-pad sequence.");
                _index = 0;
                NativeScoreSaver.SaveScoreToDisk(_battle != null ? _battle.GetScore() : 0);
            }
        }
        else
        {
            // Wrong direction: reset, but let this press start a fresh attempt if it happens
            // to be the first input of the sequence.
            _index = (pressed == Sequence[0]) ? 1 : 0;
        }
    }
}
