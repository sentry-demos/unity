using Sentry;
using Sentry.Unity;

/// <summary>
/// The trace every signal from one run hangs off: its errors, its logs, its metrics, and the
/// handful of transactions the run starts along the way.
/// </summary>
/// <remarks>
/// <para>
/// Without this, every <c>SentrySdk.StartTransaction</c> call starts a trace of its own, and
/// the errors and logs in between land on whatever propagation context the scope happened to
/// be holding. One run came out as five unrelated traces -- startup, the scene load, the
/// gameplay the errors sit on, one per upgrade fetch, and the crash -- so there was no single
/// trace that showed a run.
/// </para>
/// <para>
/// <see cref="Begin"/> mints an id and points the scope at it, which is what carries errors,
/// logs and metrics. Transactions do not read the scope, so they take the header explicitly
/// via <see cref="StartTransaction"/>. Deliberately not one long root span: a run is a minute
/// of coroutines, and short sibling transactions on a shared trace give the same single
/// waterfall without a span held open across all of it.
/// </para>
/// <para>
/// "Try Again" reloads the scene rather than the process, so every attempt calls
/// <see cref="Begin"/> again and gets its own trace. Sharing one across attempts would pile
/// every run of a session into a single unreadable waterfall.
/// </para>
/// </remarks>
public static class RunTrace
{
    private static SentryTraceHeader _current;

    /// <summary>The current run's trace, or null before the first run of the process.</summary>
    public static SentryTraceHeader Current => _current;

    /// <summary>
    /// Starts a new trace and points the scope at it. Called from
    /// <see cref="BattleMetrics.RunStarted"/>, ahead of anything the run reports.
    /// </summary>
    public static void Begin()
    {
        if (!SentrySdk.IsEnabled)
        {
            return;
        }

        _current = new SentryTraceHeader(SentryId.Create(), SpanId.Create(), true);

        // Rewrites the scope's propagation context. Everything that reads the scope -- errors,
        // structured logs, metrics -- picks the trace up from here without the caller knowing.
        SentrySdk.ContinueTrace(_current, null);
    }

    /// <summary>
    /// A transaction on the run's trace, where <c>SentrySdk.StartTransaction(name, operation)</c>
    /// would have started a new one.
    /// </summary>
    public static ITransactionTracer StartTransaction(string name, string operation)
    {
        if (_current is null)
        {
            return SentrySdk.StartTransaction(name, operation);
        }

        // ContinueTrace builds the context from the run's header and re-points the scope at
        // the same trace, which is the documented way to put a transaction on an existing one.
        // Passing the header to TransactionContext directly does not work: its third parameter
        // is a parent SpanId, not a trace.
        var context = SentrySdk.ContinueTrace(_current, null, name, operation);

        return SentrySdk.StartTransaction(context);
    }

    /// <summary>
    /// Points the scope at a transaction, so errors captured while it is open attach to it.
    /// Pair every call with <see cref="ClearScopeTransaction"/>.
    /// </summary>
    public static void SetScopeTransaction(ITransactionTracer transaction)
    {
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);
    }

    /// <summary>
    /// Drops the scope's pointer to a transaction that has finished.
    /// </summary>
    /// <remarks>
    /// The call sites here set <c>scope.Transaction</c> and never unset it, which left every
    /// later event in the run attributed to a transaction that had already ended.
    /// </remarks>
    public static void ClearScopeTransaction()
    {
        SentrySdk.ConfigureScope(scope => scope.Transaction = null);

        // Defensive: finishing a scope transaction can regenerate the scope's propagation
        // context, which scope-sync mirrors to the native layer -- a native crash between two
        // transactions would then land on a fresh random trace instead of the run's. Re-point
        // at the run so the window never opens, whatever the SDK's finish behavior is.
        if (_current is not null)
        {
            SentrySdk.ContinueTrace(_current, null);
        }
    }
}
