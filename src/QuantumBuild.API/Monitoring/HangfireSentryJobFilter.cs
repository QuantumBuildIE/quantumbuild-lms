using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Sentry;

namespace QuantumBuild.API.Monitoring;

/// <summary>
/// Reports Hangfire jobs that reach the Failed state to Sentry. Background jobs run outside
/// the ASP.NET request pipeline, so Sentry's web integration never sees them without this.
/// Implemented as IApplyStateFilter (not IElectStateFilter) so it only fires on the state that
/// is actually applied after every filter — including [AutomaticRetry] — has had a chance to
/// re-elect the candidate state. That means a transient failure that still has retries left is
/// NOT reported here (AutomaticRetry re-elects it to Scheduled); only the final, exhausted
/// failure reaches this filter as FailedState.
///
/// Jobs whose own catch blocks swallow their exceptions (log-and-continue) never transition to
/// Failed at all, so this filter cannot see them — those call SentrySdk.CaptureException
/// directly from the catch block instead (see CLAUDE.md background-job Sentry note).
/// </summary>
public sealed class HangfireSentryJobFilter : IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
        {
            return;
        }

        var job = context.BackgroundJob.Job;

        SentrySdk.CaptureException(failedState.Exception, scope =>
        {
            scope.SetTag("hangfire.job_id", context.BackgroundJob.Id);
            scope.SetTag("hangfire.job_type", job.Type.FullName ?? job.Type.Name);
            scope.SetTag("hangfire.job_method", job.Method.Name);

            var tenantId = TryGetTenantId(job);
            if (tenantId is { } id)
            {
                scope.SetTag("tenant.id", id.ToString());
            }
        });
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    /// <summary>
    /// Best-effort tenant tag: matches a job method parameter literally named "tenantId" (Guid
    /// or Guid?), which covers per-tenant dispatch jobs (e.g. MissingTranslationsJob). Jobs that
    /// loop over all tenants internally (the daily cron jobs) take no such parameter and are
    /// reported without a tenant tag.
    /// </summary>
    private static Guid? TryGetTenantId(Job job)
    {
        var parameters = job.Method.GetParameters();
        for (var i = 0; i < parameters.Length && i < job.Args.Count; i++)
        {
            if (!string.Equals(parameters[i].Name, "tenantId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A boxed Nullable<Guid> with a value unwraps to a boxed Guid, so the Guid pattern
            // below already covers both "Guid" and "Guid? with value" arguments; a null Guid?
            // argument falls through to the discard.
            return job.Args[i] switch
            {
                Guid guid => guid,
                _ => null
            };
        }

        return null;
    }
}
