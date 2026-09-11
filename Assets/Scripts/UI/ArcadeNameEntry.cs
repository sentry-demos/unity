using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Letter slots wound up and down with a stick, the way a cabinet asks for initials.
/// </summary>
/// <remarks>
/// <para>
/// The entry for machines with no keyboard to offer. WebGL reports no on-screen keyboard, and a
/// kiosk cabinet has no physical one either, so a text box there is a dead end. This needs
/// nothing but the directions and the confirm button that already work every other screen.
/// </para>
/// <para>
/// Up and down wind the letter under the caret, left and right move between slots. Confirm
/// steps to the next slot, and past the last one hands the screen back a finished name -- as
/// does pushing right off the end. Pushing left off the front does not: walking out that way
/// would read as a way back to something, and there is nothing that way.
/// </para>
/// <para>
/// A tap works it too, through <see cref="IPointerClickHandler"/>. Navigate is bound to sticks,
/// d-pads and keys but not to touch, so on a phone this would otherwise be a row of letters
/// nothing could reach.
/// </para>
/// <para>
/// Slots are cloned from a template in the scene, so the type is authored next to the rest of
/// the panel rather than described here, and the count follows whatever limit the score store
/// asks for.
/// </para>
/// <para>
/// The blink runs on <see cref="Time.unscaledTime"/>. This screen sits at
/// <c>Time.timeScale = 0</c>, where anything on scaled time simply never moves.
/// </para>
/// </remarks>
public class ArcadeNameEntry : MonoBehaviour, INameEntry, IPointerClickHandler
{
    // Letters, then digits, then blank. Blank sits at the end so winding down from A reaches it
    // in one step, and lives in code rather than in a serialized field because a string whose
    // last character is a space is one inspector round-trip away from losing it.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";

    [Tooltip("Cloned once per slot. Stays in the scene as the thing to copy, and is never shown")]
    [SerializeField] private TextMeshProUGUI _slotTemplate;

    [Tooltip("Distance between slot centres, in canvas units")]
    [SerializeField] private float _slotSpacing = 64f;

    [Tooltip("How many letters, before the score store says otherwise")]
    [SerializeField] private int _slotCount = 3;

    [Tooltip("A slot that is not being edited")]
    [SerializeField] private Color _idleColor = new Color(0.196f, 0.196f, 0.196f);

    [Tooltip("The slot under the caret, on the lit half of the blink")]
    [SerializeField] private Color _activeColor = new Color(0.85f, 0.28f, 0.1f);

    [Tooltip("Seconds for a full blink of the slot under the caret")]
    [SerializeField] private float _blinkPeriod = 0.7f;

    private readonly List<TextMeshProUGUI> _slots = new();

    private char[] _letters = Array.Empty<char>();
    private int _index;
    private bool _focused;
    private bool _locked;

    public event Action TextChanged;

    /// <summary>Trailing blanks are not part of a name, so "A__" posts as "A".</summary>
    public string Text => new string(_letters).Trim();

    /// <summary>Never. There is nothing here to type into.</summary>
    public bool IsTyping => false;

    public bool CanEdit => gameObject.activeInHierarchy && !_locked;

    public Transform Transform => transform;

    private void Awake()
    {
        BuildSlots(_slotCount);
    }

    private void Update()
    {
        if (!_focused || _slots.Count == 0)
        {
            return;
        }

        var lit = Mathf.Repeat(Time.unscaledTime, _blinkPeriod) < _blinkPeriod * 0.5f;
        _slots[_index].color = lit ? _activeColor : _idleColor;
    }

    public void SetLengthLimit(int limit)
    {
        // 0 is "no limit", which is not a thing a row of slots can be. Whoever chose this entry
        // did so because the limit was a short one, so an unlimited one just leaves it as built.
        if (limit <= 0)
        {
            return;
        }

        BuildSlots(limit);
    }

    public void Focus()
    {
        if (_locked)
        {
            return;
        }

        _focused = true;
        Render();
    }

    public void Blur()
    {
        _focused = false;
        Render();
    }

    public void Lock()
    {
        _locked = true;
        _focused = false;
        Render();
    }

    public bool Navigate(Vector2Int step, bool fromKeyboard)
    {
        if (!_focused || _locked)
        {
            return false;
        }

        if (step.y != 0)
        {
            Wind(step.y);
            return true;
        }

        if (step.x > 0)
        {
            // Off the right-hand end is the way out, to the Submit button.
            if (_index >= _slots.Count - 1)
            {
                return false;
            }

            _index++;
            Render();
            return true;
        }

        if (step.x < 0)
        {
            _index = Mathf.Max(0, _index - 1);
            Render();
        }

        return true;
    }

    public bool Confirm()
    {
        if (!_focused || _locked)
        {
            return false;
        }

        if (_index < _slots.Count - 1)
        {
            _index++;
            Render();
            return true;
        }

        // The last slot confirmed is a finished name. What happens to it is the screen's call.
        return false;
    }

    /// <summary>
    /// A tap in the top half of a slot winds it up and one in the bottom half winds it down,
    /// after moving the caret there. Slots are close together, so a tap that lands between two
    /// of them takes the nearer one rather than being dropped.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_locked || _slots.Count == 0)
        {
            return;
        }

        var nearest = 0;
        var shortest = float.MaxValue;
        var above = true;

        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i].rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    slot, eventData.position, eventData.pressEventCamera, out var local))
            {
                continue;
            }

            var distance = Mathf.Abs(local.x);
            if (distance >= shortest)
            {
                continue;
            }

            shortest = distance;
            nearest = i;
            above = local.y >= 0f;
        }

        if (shortest == float.MaxValue)
        {
            return;
        }

        _focused = true;
        _index = nearest;
        Wind(above ? 1 : -1);
    }

    private void Wind(int direction)
    {
        var at = Alphabet.IndexOf(_letters[_index]);
        if (at < 0)
        {
            at = 0;
        }

        _letters[_index] = Alphabet[(at + direction + Alphabet.Length) % Alphabet.Length];
        Render();
        TextChanged?.Invoke();
    }

    private void BuildSlots(int count)
    {
        count = Mathf.Max(1, count);
        if (_slots.Count == count)
        {
            return;
        }

        foreach (var slot in _slots)
        {
            Destroy(slot.gameObject);
        }
        _slots.Clear();

        // The template is the authored look, not one of the slots.
        _slotTemplate.gameObject.SetActive(false);

        var home = _slotTemplate.rectTransform.anchoredPosition;
        _letters = new char[count];

        for (var i = 0; i < count; i++)
        {
            var slot = Instantiate(_slotTemplate, _slotTemplate.transform.parent);
            slot.name = $"Slot{i}";
            slot.gameObject.SetActive(true);
            slot.rectTransform.anchoredPosition =
                home + new Vector2((i - (count - 1) * 0.5f) * _slotSpacing, 0f);

            _slots.Add(slot);
            _letters[i] = Alphabet[0];
        }

        _index = 0;
        Render();
    }

    private void Render()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var under = _focused && i == _index;
            _slots[i].text = Glyph(_letters[i], under);
            _slots[i].color = under ? _activeColor : _idleColor;
        }
    }

    /// <summary>
    /// A slot is three lines whether or not it is wearing the carets, so the letters keep one
    /// baseline across the row. The blank lines carry a non-breaking space because an empty one
    /// collapses.
    /// </summary>
    /// <remarks>
    /// The carets are <c>^</c> and <c>v</c> rather than proper triangles: the pixel font this
    /// is set in has no glyph for those and no fallback, so they would draw as missing-character
    /// boxes.
    /// </remarks>
    private static string Glyph(char letter, bool under)
    {
        const string up = "<size=50%>^</size>";
        const string down = "<size=50%>v</size>";
        const string gap = "<size=50%>\u00A0</size>";

        // A blank slot still needs somewhere to look.
        var shown = letter == ' ' ? '_' : letter;

        return under ? $"{up}\n{shown}\n{down}" : $"{gap}\n{shown}\n{gap}";
    }
}
