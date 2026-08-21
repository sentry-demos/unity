using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Checks that every constant on <see cref="Tags"/> still names a tag that exists in the
/// TagManager.
/// </summary>
/// <remarks>
/// Unity offers no runtime API for listing tags, so <see cref="Tags"/> is maintained by hand
/// and nothing stops a tag being renamed in the Editor. That failure is quiet in the worst
/// way: <c>CompareTag</c> throws on an unknown tag, and only on the frame the code path first
/// runs. This turns it into a console error on load instead.
/// </remarks>
[InitializeOnLoad]
public static class TagsValidator
{
    static TagsValidator()
    {
        // Delay past the domain reload that InitializeOnLoad runs in, so the TagManager is
        // fully loaded before it is read.
        EditorApplication.delayCall += Validate;
    }

    [MenuItem("Tools/Sentaur/Validate Tags")]
    private static void Validate()
    {
        var declared = typeof(Tags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => new KeyValuePair<string, string>(f.Name, (string)f.GetRawConstantValue()));

        var existing = new HashSet<string>(InternalEditorUtility.tags);

        foreach (var (name, value) in declared.Select(p => (p.Key, p.Value)))
        {
            if (!existing.Contains(value))
            {
                Debug.LogError(
                    $"Tags.{name} is \"{value}\", which is not in the TagManager. "
                        + "Add the tag back or update the constant -- CompareTag throws on an "
                        + "unknown tag."
                );
            }
        }
    }
}
