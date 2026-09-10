using System;
using System.Collections.Generic;
using Sentry;
using Sentry.Unity;
using UnityEngine.Networking;

/// <summary>
/// What <c>SentryHttpMessageHandler</c> would have done, for requests that cannot go through it.
/// </summary>
/// <remarks>
/// <para>
/// <c>HttpClient</c> and <see cref="Sentry.Unity.SentryHttpMessageHandler"/> do not work on
/// Switch or Switch 2, so those platforms send their requests through
/// <see cref="UnityWebRequest"/> instead and lose everything the handler would have attached:
/// the <c>http.client</c> child span, trace-header propagation to the backend, the breadcrumb,
/// and the event for a failed response. These helpers put each of those back.
/// </para>
/// <para>
/// Only the callers are platform-specific; this class deliberately is not. Kept behind the
/// platform guard it would compile on no CI target and be invisible to <c>dotnet format</c>,
/// which is a bad place for the code that decides whether a console build reports anything at
/// all. Compiled everywhere, the five player builds in CI at least prove it still builds.
/// </para>
/// </remarks>
public static class SentryWebRequest
{
    /// <summary>
    /// Propagates the sentry-trace and baggage headers to the outgoing request, so the backend
    /// joins the distributed trace. Mimics <c>SentryMessageHandler.PropagateTraceHeaders</c>.
    /// </summary>
    public static void PropagateTraceHeaders(UnityWebRequest request, ISpan span)
    {
        var traceHeader = span?.GetTraceHeader() ?? SentrySdk.GetTraceHeader();
        if (traceHeader != null)
        {
            request.SetRequestHeader("sentry-trace", traceHeader.ToString());
        }

        var baggage = SentrySdk.GetBaggage();
        if (baggage != null)
        {
            request.SetRequestHeader("baggage", baggage.ToString());
        }
    }

    /// <summary>The breadcrumb the handler would have left for the request.</summary>
    public static void AddHttpBreadcrumb(string url, int statusCode)
    {
        SentrySdk.AddBreadcrumb(
            message: string.Empty,
            category: "http",
            type: "http",
            data: new Dictionary<string, string>
            {
                { "url", url },
                { "method", "POST" },
                { "status_code", statusCode.ToString() },
            }
        );
    }

    /// <summary>
    /// Maps HTTP status codes to span statuses. Mimics
    /// <c>SpanStatusConverter.FromHttpStatusCode</c>.
    /// </summary>
    public static SpanStatus GetSpanStatusFromHttpCode(int code)
    {
        return code switch
        {
            < 400 => SpanStatus.Ok,
            400 => SpanStatus.FailedPrecondition,
            401 => SpanStatus.Unauthenticated,
            403 => SpanStatus.PermissionDenied,
            404 => SpanStatus.NotFound,
            409 => SpanStatus.AlreadyExists,
            429 => SpanStatus.ResourceExhausted,
            499 => SpanStatus.Cancelled,
            < 500 => SpanStatus.FailedPrecondition,
            500 => SpanStatus.InternalError,
            501 => SpanStatus.Unimplemented,
            503 => SpanStatus.Unavailable,
            504 => SpanStatus.DeadlineExceeded,
            < 600 => SpanStatus.InternalError,
            _ => SpanStatus.UnknownError,
        };
    }

    /// <summary>
    /// Captures a 4xx/5xx response as an event. Mimics <c>SentryHttpFailedRequestHandler</c>.
    /// </summary>
    public static void CaptureFailedRequest(string method, string url, int statusCode, string error)
    {
        if (statusCode < 400)
        {
            return;
        }

        var exception = new System.Net.Http.HttpRequestException(
            $"Response status code does not indicate success: {statusCode} ({error})"
        );

        var sentryEvent = new SentryEvent(exception)
        {
            Request = new SentryRequest
            {
                Url = url,
                Method = method,
                QueryString = new Uri(url).Query,
            },
        };
        sentryEvent.Contexts["response"] = new Dictionary<string, object>
        {
            { "status_code", statusCode },
        };

        SentrySdk.CaptureEvent(sentryEvent);
    }
}
