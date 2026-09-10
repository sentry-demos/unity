using DG.Tweening;
using SceneManagers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The choreography of the game-over screen, from the dim coming up to the name field taking
/// focus.
/// </summary>
/// <remarks>
/// <para>
/// It used to be three one-second waits with things switching on between them, which read as a
/// list rather than a moment. Everything overlaps now: the title is still settling as the score
/// starts counting, and the whole thing lands in roughly half the time.
/// </para>
/// <para>
/// The score counting up rather than appearing is the point of the sequence. It is the run's
/// result, and watching it climb is worth more than reading it.
/// </para>
/// <para>
/// Every tween is <c>SetUpdate(true)</c>. This screen runs at <c>Time.timeScale = 0</c>, so
/// anything on scaled time would simply never move. The numbers are all serialized because feel
/// is tuned by watching it, not by reasoning about it.
/// </para>
/// <para>
/// Only DOTween's core is visible from this assembly -- the UI module compiles into
/// Assembly-CSharp-firstpass -- so alpha goes through the generic <c>DOTween.To</c> rather than
/// the <c>DOFade</c> shortcut. Same tween, reachable name.
/// </para>
/// </remarks>
public class GameOverReveal : MonoBehaviour
{
    [Header("What moves")]
    [SerializeField] private Image _dim;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _score;
    [SerializeField] private CanvasGroup _panel;
    [SerializeField] private CanvasGroup _choices;
    [SerializeField] private ScorePoster _scorePoster;
    [SerializeField] private HUDManager _hudManager;

    [Header("Timing (seconds)")]
    [SerializeField] private float _dimFade = 0.22f;
    [SerializeField] private float _titleIn = 0.38f;
    [SerializeField] private float _scoreIn = 0.34f;
    [SerializeField] private float _countUp = 0.6f;
    [SerializeField] private float _panelIn = 0.3f;
    [SerializeField] private float _choicesIn = 0.24f;

    [Header("Feel")]
    [Tooltip("Title starts this much smaller and overshoots on the way in")]
    [SerializeField] private float _titleFromScale = 0.55f;

    [Tooltip("Score starts this much larger and snaps down onto the number")]
    [SerializeField] private float _scoreFromScale = 1.8f;

    [Tooltip("How far the posting panel rises as it fades in")]
    [SerializeField] private float _panelRise = 45f;

    [Tooltip("Kick the score gives when the count lands")]
    [SerializeField] private float _scorePunch = 0.35f;

    [Tooltip("Gap between the two buttons arriving")]
    [SerializeField] private float _choiceStagger = 0.07f;

    private Sequence _sequence;
    private float _dimAlpha = 1f;
    private Vector3 _panelHome;
    private bool _homeCaptured;

    /// <summary>
    /// Runs the whole reveal. Safe to call again; anything still running is dropped and every
    /// animated value is put back to its resting state first.
    /// </summary>
    public void Play(int finalScore)
    {
        CaptureHome();
        Stop();

        // Start states, so a replay after "Try Again" does not inherit a half-finished pose.
        SetDimAlpha(0f);
        _title.alpha = 0f;
        _title.transform.localScale = Vector3.one * _titleFromScale;
        _score.alpha = 0f;
        _score.transform.localScale = Vector3.one * _scoreFromScale;
        _score.text = "0";
        _score.gameObject.SetActive(true);
        _panel.alpha = 0f;
        _choices.alpha = 0f;
        _choices.transform.localScale = Vector3.one;
        _panel.transform.localPosition = _panelHome - new Vector3(0f, _panelRise, 0f);

        var shown = 0f;

        _sequence = DOTween.Sequence().SetUpdate(true);

        // The dim comes up under everything else rather than before it.
        _sequence.Insert(0f, DOTween.To(() => 0f, SetDimAlpha, _dimAlpha, _dimFade));

        // Title overshoots and settles.
        _sequence.Insert(0.04f, DOTween.To(() => _title.alpha, a => _title.alpha = a, 1f, _titleIn * 0.5f));
        _sequence.Insert(0.04f, _title.transform.DOScale(1f, _titleIn).SetEase(Ease.OutBack));

        // Score snaps down from oversized while the title is still moving, then counts.
        var scoreAt = 0.04f + _titleIn * 0.55f;
        _sequence.Insert(scoreAt, DOTween.To(() => _score.alpha, a => _score.alpha = a, 1f, _scoreIn * 0.5f));
        _sequence.Insert(scoreAt, _score.transform.DOScale(1f, _scoreIn).SetEase(Ease.OutBack));
        _sequence.Insert(
            scoreAt,
            DOTween.To(() => shown, v => { shown = v; _score.text = Mathf.RoundToInt(v).ToString(); },
                finalScore, _countUp).SetEase(Ease.OutCubic)
        );

        // The kick when the number lands is what sells it.
        var landed = scoreAt + _countUp;
        _sequence.Insert(landed, _score.transform.DOPunchScale(Vector3.one * _scorePunch, 0.3f, 6, 0.7f));

        // Posting panel rises in just after, so the two do not compete.
        var panelAt = landed - 0.1f;
        _sequence.InsertCallback(panelAt, () => _scorePoster.Enable(finalScore));
        _sequence.Insert(panelAt, DOTween.To(() => _panel.alpha, a => _panel.alpha = a, 1f, _panelIn));
        _sequence.Insert(panelAt, _panel.transform.DOLocalMoveY(_panelHome.y, _panelIn).SetEase(Ease.OutCubic));

        // Buttons arrive last, one after the other.
        var choicesAt = panelAt + _panelIn * 0.6f;
        _sequence.InsertCallback(choicesAt, () => _choices.gameObject.SetActive(true));
        _sequence.Insert(choicesAt, DOTween.To(() => _choices.alpha, a => _choices.alpha = a, 1f, _choicesIn));

        for (var i = 0; i < _choices.transform.childCount; i++)
        {
            var button = _choices.transform.GetChild(i);
            var at = choicesAt + i * _choiceStagger;
            _sequence.InsertCallback(at, () => button.localScale = Vector3.one * 0.8f);
            _sequence.Insert(at, button.DOScale(1f, _choicesIn).SetEase(Ease.OutBack));
        }

        // Only once the screen has settled, so focus does not fight the animation.
        _sequence.AppendCallback(() =>
        {
            if (_hudManager != null)
            {
                _hudManager.FocusNameField();
            }
        });
    }

    /// <summary>Drops any reveal in flight and leaves the screen where it stands.</summary>
    public void Stop()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    private void OnDestroy()
    {
        Stop();
    }

    // The panel's resting position is where the scene put it, read once before anything moves it.
    private void CaptureHome()
    {
        if (_homeCaptured)
        {
            return;
        }

        _homeCaptured = true;
        _panelHome = _panel.transform.localPosition;
        _dimAlpha = _dim != null ? _dim.color.a : 1f;
    }

    private void SetDimAlpha(float alpha)
    {
        if (_dim == null)
        {
            return;
        }

        var c = _dim.color;
        c.a = alpha;
        _dim.color = c;
    }
}
