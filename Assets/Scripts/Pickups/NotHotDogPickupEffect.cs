using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// INTENTIONAL: the missing error handling is the point - a failed bundle download
// throws for Sentry to capture. Gated on DemoConfiguration.NotHotDogParticleEffect.
// See CONTRIBUTING.md.
public class NotHotDogPickupEffect : MonoBehaviour
{
    [SerializeField] private string _assetBundleUrl = "https://aspnetcore.empower-plant.com/bundles/special-shaders-v2.bundle";
    [SerializeField] private string _shaderName = "SpecialShaderEffect";
    [SerializeField] private float _lifeTime = 2;

    private ParticleSystemRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<ParticleSystemRenderer>();
        _renderer.material.shader = Shader.Find("Resources/Default.shader");

        StartCoroutine(DestroyYourself());
        StartCoroutine(LoadMaterialFromBundle());
    }

    private IEnumerator LoadMaterialFromBundle()
    {
        using var www = UnityWebRequestAssetBundle.GetAssetBundle(_assetBundleUrl);
        www.timeout = 15;

        Debug.Log("Dynamic shader enabled! Loading...");

        var started = System.Diagnostics.Stopwatch.StartNew();

        yield return www.SendWebRequest();

        // Reads the result for the metric without acting on it -- the unguarded download
        // below is the fault this component exists to demonstrate, so it stays unguarded.
        GameMetrics.Count(
            GameMetrics.BundleDownload,
            1,
            (GameMetrics.ResultKey, www.result.ToString())
        );
        GameMetrics.Distribution(
            GameMetrics.BundleDownloadDuration,
            started.Elapsed.TotalMilliseconds,
            Sentry.MeasurementUnit.Duration.Millisecond,
            (GameMetrics.ResultKey, www.result.ToString())
        );

        Debug.Log("Success! Applying dynamic shader.");

        var bundle = DownloadHandlerAssetBundle.GetContent(www);
        var bundledShader = bundle.LoadAsset<Shader>(_shaderName);
        _renderer.material.shader = bundledShader;
        bundle.Unload(false);
    }

    private IEnumerator DestroyYourself()
    {
        yield return new WaitForSeconds(_lifeTime);
        Destroy(this);
    }
}
