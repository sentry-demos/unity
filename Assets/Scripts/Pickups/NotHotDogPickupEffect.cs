using System.Collections;
using Sentry;
using Sentry.Unity;
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

        // UnityWebRequest goes nowhere near SentryHttpMessageHandler, so the 404 this
        // component exists to demonstrate used to arrive as an error on a trace with no spans
        // at all. Instrumented by hand, the failing request shows up in the waterfall.
        var transaction = RunTrace.StartTransaction("load_shader_bundle", "resource.load");
        var requestSpan = transaction.StartChild("http.client", $"GET {_assetBundleUrl}");

        // The two headers SentryHttpMessageHandler injects for the score upload, by hand
        // because nothing instruments UnityWebRequest. They do not create the span above --
        // they hand this span's id to the service on the other end, so the backend parents its
        // work onto this request instead of opening a trace of its own.
        var traceHeader = new SentryTraceHeader(
            requestSpan.TraceId,
            requestSpan.SpanId,
            requestSpan.IsSampled
        );
        www.SetRequestHeader("sentry-trace", traceHeader.ToString());

        var baggage = SentrySdk.GetBaggage();
        if (baggage != null)
        {
            www.SetRequestHeader("baggage", baggage.ToString());
        }

        var started = System.Diagnostics.Stopwatch.StartNew();

        yield return www.SendWebRequest();

        var status = StatusFor(www);

        requestSpan.SetData("http.request.method", "GET");
        requestSpan.SetData("url.full", _assetBundleUrl);
        requestSpan.SetData("http.response.status_code", www.responseCode);
        requestSpan.SetData("http.response_content_length", www.downloadedBytes);
        requestSpan.Finish(status);

        // Finished here rather than at the end of the coroutine on purpose: the unguarded
        // download below throws, and a transaction that never finishes is never sent. The
        // request is done by this point, so there is nothing left to measure anyway.
        transaction.Finish(status);

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

    /// <summary>
    /// The span status for a finished request. A timeout never reaches a status code, so the
    /// result is checked before the code is.
    /// </summary>
    private static SpanStatus StatusFor(UnityWebRequest www)
    {
        if (www.result == UnityWebRequest.Result.Success)
        {
            return SpanStatus.Ok;
        }

        if (www.responseCode == 404)
        {
            return SpanStatus.NotFound;
        }

        return www.responseCode >= 500 ? SpanStatus.InternalError : SpanStatus.UnknownError;
    }

    private IEnumerator DestroyYourself()
    {
        yield return new WaitForSeconds(_lifeTime);
        Destroy(this);
    }
}
