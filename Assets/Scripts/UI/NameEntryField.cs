using System;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The name box on the game-over screen, and the platform quirks that come with it.
/// </summary>
/// <remarks>
/// <para>
/// Three platform families enter a name three different ways: a physical keyboard on desktop,
/// a native on-screen keyboard on mobile and Switch, and the WinRT gamepad keyboard on Windows
/// handhelds. Gathered here, the rest of the game-over screen can just ask for the text.
/// </para>
/// <para>
/// It also owns how long a name may be, but does not decide it. The score store does, because
/// that is what differs between keeping a score on a backend and keeping it on the device.
/// </para>
/// <para>
/// Living on the input field itself matters: the field sits under a root that is inactive for
/// the whole battle, so <see cref="Update"/> runs only once the game-over panel is revealed.
/// Polled from the panel's own component it ran every frame of every run instead.
/// </para>
/// </remarks>
[RequireComponent(typeof(TMP_InputField))]
public class NameEntryField : MonoBehaviour
{
    private TMP_InputField _field;

    private TouchScreenKeyboard _keyboard;
    private bool _keyboardWasActive;

    // TMP_InputField reverts to its original text when the user cancels (ESC / keyboard
    // dismissed), which threw away a typed name. Kept here to restore it.
    private string _savedName = "";

    /// <summary>Raised whenever the text changes, including a restore after a cancel.</summary>
    public event Action TextChanged;

    /// <summary>Raised when the on-screen keyboard closes and the field has text.</summary>
    public event Action VirtualKeyboardClosedWithText;

    public string Text => _field.text;

    /// <summary>Whether the field currently has typing focus, as opposed to merely being selected.</summary>
    public bool IsTyping => _field.isFocused;

    /// <summary>Whether the player could type into it right now.</summary>
    public bool CanEdit => gameObject.activeInHierarchy && _field.interactable;

#if UNITY_STANDALONE_WIN
    // Windows handhelds (e.g. ROG Ally) report no touch keyboard support, so the WinRT
    // gamepad keyboard is driven explicitly through this helper. The DLL is optional;
    // every call is wrapped so its absence just means no on-screen keyboard.
    [DllImport("HandheldHelper")] private static extern bool ShowVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool HideVirtualKeyboard();
    [DllImport("HandheldHelper")] private static extern bool IsDeviceHandheld();
#endif

    private void Awake()
    {
        _field = GetComponent<TMP_InputField>();

        // Keep the Unity input field visible next to the native keyboard on platforms
        // that show one (mobile, Switch).
        _field.shouldHideMobileInput = false;

        _field.onValueChanged.AddListener(OnValueChanged);
        _field.onEndEdit.AddListener(OnEndEdit);

        if (TouchScreenKeyboard.isSupported)
        {
            // Platforms with a native on-screen keyboard (mobile, Switch) need it opened
            // explicitly when the field is selected via controller navigation.
            _field.onSelect.AddListener(OnSelected);
        }
#if UNITY_STANDALONE_WIN
        else
        {
            try
            {
                if (IsDeviceHandheld())
                {
                    _field.onSelect.AddListener(OnSelected);
                    _field.onDeselect.AddListener(OnDeselected);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HandheldHelper unavailable: {ex.Message}");
            }
        }
#endif
    }

    private void OnDestroy()
    {
        if (_field == null)
        {
            return;
        }

        _field.onSelect.RemoveListener(OnSelected);
        _field.onValueChanged.RemoveListener(OnValueChanged);
        _field.onEndEdit.RemoveListener(OnEndEdit);
#if UNITY_STANDALONE_WIN
        _field.onDeselect.RemoveListener(OnDeselected);
#endif
    }

    private void Update()
    {
        // Mirror the native on-screen keyboard's text into the input field while it is open,
        // and detect the close so navigation can move on to Submit.
        if (_keyboard != null && _keyboard.active)
        {
            _field.text = _keyboard.text;
            _keyboardWasActive = true;
        }
        else if (_keyboardWasActive)
        {
            if (_keyboard != null)
            {
                _field.text = _keyboard.text;
            }
            _keyboardWasActive = false;
            if (!string.IsNullOrEmpty(_field.text))
            {
                VirtualKeyboardClosedWithText?.Invoke();
            }
        }
    }

    /// <summary>How many characters a name may have. 0 means no limit, as TMP_InputField reads it.</summary>
    public void SetLengthLimit(int limit)
    {
        _field.characterLimit = limit;

        // A limit arriving after something was already typed still applies.
        if (limit > 0 && _field.text.Length > limit)
        {
            _field.SetTextWithoutNotify(_field.text.Substring(0, limit));
            _savedName = _field.text;
            TextChanged?.Invoke();
        }
    }

    /// <summary>Gives the field typing focus, opening an on-screen keyboard where there is one.</summary>
    public void Focus()
    {
        _field.ActivateInputField();
    }

    /// <summary>Takes focus away, closing the on-screen keyboard and clearing the selection.</summary>
    public void Blur()
    {
        _field.DeactivateInputField();

        // Clear the EventSystem selection so an in-flight Submit event has nowhere to land
        // after the input field is deactivated.
        EventSystem.current?.SetSelectedGameObject(null);
    }

    /// <summary>Stops further editing, once the name has been recorded.</summary>
    public void Lock()
    {
        _field.interactable = false;
    }

    private void OnValueChanged(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _savedName = text;
        }
        TextChanged?.Invoke();
    }

    private void OnEndEdit(string text)
    {
        // TMP_InputField reverts to its original text when the user cancels. If the field is
        // now empty but a name was typed earlier, restore it.
        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(_savedName))
        {
            _field.SetTextWithoutNotify(_savedName);
            TextChanged?.Invoke();
        }
    }

    private void OnSelected(string text)
    {
        if (TouchScreenKeyboard.isSupported)
        {
            _keyboard = TouchScreenKeyboard.Open(
                _field.text,
                TouchScreenKeyboardType.Default,
                false, // autocorrection
                false, // multiline
                false, // secure
                false, // alert
                _field.placeholder.GetComponent<TextMeshProUGUI>().text
            );
        }
#if UNITY_STANDALONE_WIN
        else
        {
            // Only reachable on a Windows handheld: the listener is not added otherwise.
            try
            {
                ShowVirtualKeyboard();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to show virtual keyboard: {ex.Message}");
            }
        }
#endif
    }

#if UNITY_STANDALONE_WIN
    private void OnDeselected(string text)
    {
        try
        {
            HideVirtualKeyboard();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to hide virtual keyboard: {ex.Message}");
        }
        if (!string.IsNullOrEmpty(_field.text))
        {
            VirtualKeyboardClosedWithText?.Invoke();
        }
    }
#endif
}
