using RRC.Abstractions;

namespace RRC.Student;

public sealed class SimpleAdaptivePolicy : IRequestPolicy
{
    private const int Window = 200;       
    private const int MinSamples = 30;   

    private readonly Queue<(TimeSpan Elapsed, bool TimedOut)> recent = new();
    private TimeSpan timeout = TimeSpan.FromSeconds(1);

    public TimeSpan GetTimeout(RequestContext context, Random random) => timeout;

    public RetryDecision OnResult(RequestObservation obs, Random random)
    {
        recent.Enqueue((obs.Elapsed, obs.Outcome == RequestOutcome.Timeout));
        if (recent.Count > Window) recent.Dequeue();
        if (recent.Count >= MinSamples) UpdateTimeout();

        if (obs.Outcome == RequestOutcome.Success) return RetryDecision.NoRetry();

        if (obs.Attempt >= 3) return RetryDecision.NoRetry();

        double timeoutShare = recent.Count(x => x.TimedOut) / (double)recent.Count;
        if (timeoutShare > 0.2) return RetryDecision.NoRetry();

        return RetryDecision.RetryAfter(timeout * (0.1 * random.NextDouble()));
    }

    private void UpdateTimeout()
    {
        var sorted = recent.Select(x => x.Elapsed.Ticks).OrderBy(t => t).ToArray();
        int index = (int)Math.Ceiling(0.99 * sorted.Length) - 1;
        var proposed = TimeSpan.FromTicks((long)(sorted[index] * 1.5)); // запас 1.5

        timeout = TimeSpan.FromTicks(Math.Clamp(proposed.Ticks,
            TimeSpan.FromMilliseconds(1).Ticks, TimeSpan.FromSeconds(30).Ticks));
    }
}