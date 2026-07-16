namespace QuantumBuild.Modules.ToolboxTalks.Application.Common;

public static class DateTimeUtils
{
    /// <summary>
    /// Normalises a DateTime to Kind=Utc so it can be written to a timestamptz column.
    /// Unspecified (e.g. from a date-only request string) is treated as already-UTC wall
    /// time rather than local time, matching how the frontend sends fully-qualified UTC
    /// ISO strings for time-precise values.
    /// </summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
