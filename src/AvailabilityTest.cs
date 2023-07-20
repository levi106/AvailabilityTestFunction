using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class AvailabilityTest
    {
        private static TelemetryClient telemetryClient;

        public async static Task RunAvailabilityTestAsync(ILogger log, string url)
        {
            using (var httpClient = new HttpClient())
            {
                // TODO: Replace with your business logic 
                await httpClient.GetStringAsync(url);
            }
        }

        [FunctionName("AvailabilityTest")]
        public async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext executionContext)
        {
            if (telemetryClient == null)
            {
                // Initializing a telemetry configuration for Application Insights based on connection string 
                var telemetryConfiguration = new TelemetryConfiguration();
                telemetryConfiguration.ConnectionString = Environment.GetEnvironmentVariable( "APPLICATIONINSIGHTS_CONNECTION_STRING" );
                telemetryConfiguration.TelemetryChannel = new InMemoryChannel();
                telemetryClient = new TelemetryClient( telemetryConfiguration );
            }

            string url = Environment.GetEnvironmentVariable("WEB_TEST_URL");
            log.LogInformation($"WebTest URL: {url}");

            string testName = Environment.GetEnvironmentVariable("WEB_TEST_NAME");
            log.LogInformation($"WebTest Name: {testName}");
            string location = Environment.GetEnvironmentVariable("REGION_NAME");
            log.LogInformation($"Region Name: {location}");
            
            var availability = new AvailabilityTelemetry
            {
                Name = testName,
                RunLocation = location,
                Success = false,
            };

            availability.Context.Operation.ParentId = Activity.Current.SpanId.ToString();
            availability.Context.Operation.Id = Activity.Current.RootId;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var activity = new Activity( "AvailabilityContext" ))
                {
                    activity.Start();
                    availability.Id = Activity.Current.SpanId.ToString();
                    // Run business logic 
                    await RunAvailabilityTestAsync(log, url);
                }
                availability.Success = true;
            }

            catch (Exception ex)
            {
                availability.Message = ex.Message;
                throw;
            }

            finally
            {
                stopwatch.Stop();
                availability.Duration = stopwatch.Elapsed;
                availability.Timestamp = DateTimeOffset.UtcNow;
                telemetryClient.TrackAvailability( availability );
                telemetryClient.Flush();
            }
        }
    }
}
