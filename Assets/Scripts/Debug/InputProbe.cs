// Disabled by default. To bring the overlay back, add SENTAUR_INPUT_PROBE to
// Project Settings > Player > Scripting Define Symbols for the target platform.
#if SENTAUR_INPUT_PROBE
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Debug overlay that names whichever control is currently held, in the exact form an
/// Input Action binding expects.
/// </summary>
/// <remarks>
/// <para>
/// For discovering which physical button to map an action to. WebGL routes gamepads through
/// the browser's Gamepad API, whose button order does not always match the same pad read
/// natively -- so the mapping has to be read on the device that will run the build, not in
/// the Editor.
/// </para>
/// <para>
/// Temporary: delete this file once the binding is known. It self-registers, so nothing else
/// references it and removing the file is enough.
/// </para>
/// </remarks>
public sealed class InputProbe : MonoBehaviour
{
    // Controls that are always "pressed" alongside a real one, or that restate a control
    // already listed, would bury the answer in noise.
    private static readonly HashSet<string> Ignored = new() { "anyKey", "press" };

    private readonly List<string> m_Held = new();
    private string m_LastLogged = "";
    private GUIStyle m_Style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject(nameof(InputProbe));
        go.AddComponent<InputProbe>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        m_Held.Clear();

        foreach (var device in InputSystem.devices)
        {
            foreach (var control in device.allControls)
            {
                if (control is not ButtonControl button || !button.isPressed)
                {
                    continue;
                }

                if (Ignored.Contains(control.name))
                {
                    continue;
                }

                // The form an Input Action binding wants, e.g. <Gamepad>/buttonSouth.
                var suffix = control.path.Substring(device.path.Length);
                m_Held.Add($"<{device.layout}>{suffix}");
            }
        }

        var line = m_Held.Count == 0 ? "" : string.Join("  ", m_Held);
        if (line != m_LastLogged)
        {
            m_LastLogged = line;
            Debug.Log(line.Length == 0 ? "[InputProbe] (nothing pressed)" : $"[InputProbe] {line}");
        }
    }

    private void OnGUI()
    {
        m_Style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };

        var text = new StringBuilder();
        text.AppendLine(DescribeDevices());
        text.AppendLine();
        text.Append(m_Held.Count == 0 ? "(nothing pressed)" : string.Join("\n", m_Held));

        var area = new Rect(12, 12, Screen.width - 24, Screen.height - 24);
        m_Style.normal.textColor = Color.black;
        GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), text.ToString(), m_Style);
        m_Style.normal.textColor = m_Held.Count == 0 ? Color.grey : Color.green;
        GUI.Label(area, text.ToString(), m_Style);
    }

    private static string DescribeDevices()
    {
        var devices = new List<string>();
        foreach (var device in InputSystem.devices)
        {
            devices.Add($"{device.displayName} [{device.layout}]");
        }

        return devices.Count == 0 ? "devices: none" : "devices: " + string.Join(", ", devices);
    }
}
#endif
