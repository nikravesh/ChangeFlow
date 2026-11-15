using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using ChangeFlow.Tests.Infrastructure;
using Xunit.Abstractions;

namespace ChangeFlow.Tests.Users.Performance;

public sealed class CreateUserPerformanceTests(ITestOutputHelper output)
{
    private const int WarmupRequestCount = 20;
    private const int MeasuredRequestCount = 100;
    private const int Concurrency = 10;
    private static readonly TimeSpan MaximumP95 = TimeSpan.FromSeconds(2);

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Valid_create_requests_meet_the_recorded_release_latency_target()
    {
        using var factory = new ChangeFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Setup", factory.SetupCode);

        for (var index = 0; index < WarmupRequestCount; index++)
        {
            using var response = await SendCreate(client, "warmup", index);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var measurements = new List<Measurement>(MeasuredRequestCount);
        for (var batch = 0; batch < MeasuredRequestCount / Concurrency; batch++)
        {
            var requests = Enumerable.Range(0, Concurrency)
                .Select(offset => Measure(
                    client,
                    "measured",
                    (batch * Concurrency) + offset))
                .ToArray();
            measurements.AddRange(await Task.WhenAll(requests));
        }

        var orderedDurations = measurements
            .Select(measurement => measurement.Duration)
            .Order()
            .ToArray();
        var p95Index = (int)Math.Ceiling(orderedDurations.Length * 0.95) - 1;
        var p95 = orderedDurations[p95Index];
        var failureCount = measurements.Count(measurement => measurement.Status != HttpStatusCode.Created);

        output.WriteLine($"Configuration: warm-up={WarmupRequestCount}, measured={MeasuredRequestCount}, concurrency={Concurrency}");
        output.WriteLine($"Environment: OS={RuntimeInformation.OSDescription}; Framework={RuntimeInformation.FrameworkDescription}; Architecture={RuntimeInformation.ProcessArchitecture}; Processors={Environment.ProcessorCount}");
        output.WriteLine($"Results: p95={p95.TotalMilliseconds:F2} ms; failures={failureCount}; maximum={MaximumP95.TotalMilliseconds:F0} ms");

        Assert.Equal(0, failureCount);
        Assert.True(
            p95 < MaximumP95,
            $"Expected p95 below {MaximumP95.TotalMilliseconds:F0} ms but measured {p95.TotalMilliseconds:F2} ms.");
    }

    private static async Task<Measurement> Measure(HttpClient client, string prefix, int index)
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendCreate(client, prefix, index);
        stopwatch.Stop();
        return new Measurement(stopwatch.Elapsed, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendCreate(HttpClient client, string prefix, int index) =>
        client.PostAsJsonAsync("/users", new
        {
            displayName = $"Performance User {index}",
            loginIdentifier = $"{prefix}.{index}.{Guid.NewGuid():N}",
            password = TestSecretFactory.CreateValidPassword(),
            roles = new[] { "Requester", "Reviewer" }
        });

    private sealed record Measurement(TimeSpan Duration, HttpStatusCode Status);
}
