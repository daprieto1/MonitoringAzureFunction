using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using MonitoringPaaS.Models;
using Newtonsoft.Json;

namespace MonitoringPaaS
{
    public static class EngineHealthCheckCall
    {
        private static TraceWriter _log;
        private static DateTime _executionTime;
        private static CloudTable _incidentsTable;
        private static CloudTable _componentsTable;

        [FunctionName("EngineHealthCheckCall")]
        public static async Task Run(
            [TimerTrigger("*/15 * * * * *")]TimerInfo myTimer,
            [Table("StatusReportEngineIncidents")] CloudTable incidentsTable,
            [Table("EngineComponentHealth")] CloudTable componentsTable,
            TraceWriter log)
        {
            _log = log;
            _executionTime = DateTime.UtcNow;
            _incidentsTable = incidentsTable;
            _componentsTable = componentsTable;

            //await InsertExamples();
            //await CallEngineHealthCheck();
        }

        private static async Task CallEngineHealthCheck()
        {
            var timeout = Convert.ToDouble(Environment.GetEnvironmentVariable("timeout"));
            var healthCheckUri = new Uri(Environment.GetEnvironmentVariable("healthCheckUri"));

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) })
            {
                client
                    .GetAsync(healthCheckUri)
                    .ContinueWith(async (task) =>
                    {
                        StatusCode statusCode = GetStatusCode(task);
                        try
                        {
                            await SaveHealthCheckResponse(task.Result);
                        }
                        catch (Exception e)
                        {
                            _log.Error(e.Message);
                        }

                        await IncidentEngine(statusCode);
                    })
                    .Wait();
            }
        }

        private static async Task SaveHealthCheckResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                var batch = new TableBatchOperation();
                var body = JsonConvert.DeserializeObject<HealthCheckBody>(await response.Content.ReadAsStringAsync());

                foreach (HealthCheckComponent c in body.data)
                {
                    c.PartitionKey = _executionTime.Ticks.ToString();
                    c.RowKey = c.TestType;
                    batch.Insert(c);
                }

                await _componentsTable.ExecuteBatchAsync(batch);
            }
        }

        private static async Task IncidentEngine(StatusCode statusCode)
        {
            var pk = "ENGINE";
            var gt = $"{_executionTime.Year}-{_executionTime.Month}-{_executionTime.Day}";
            var rk = $"{gt}-{_executionTime.Ticks}";

            var query = new TableQuery<Incident>().Where($"(PartitionKey eq '{pk}') and ((RowKey gt '{gt}') and (RowKey le '{rk}')) ");
            var results = (await _incidentsTable.ExecuteQuerySegmentedAsync(query, null)).Results;

            var incident = GetIncident(results, ((int)statusCode).ToString(), pk, rk);
            await _incidentsTable.ExecuteAsync(TableOperation.InsertOrReplace(incident));
        }

        private static Incident GetIncident(IEnumerable<Incident> results, string statusCode, string pk, string rk)
        {
            if (results.Any())
            {
                Incident lastResult = results.OrderByDescending(r => r.Timestamp).FirstOrDefault();

                if (lastResult.StatusCode == statusCode)
                {
                    lastResult.FinalDate = _executionTime;
                    return lastResult;
                }
                else
                    return new Incident(_executionTime) { PartitionKey = pk, RowKey = rk, StatusCode = statusCode, FinalDate = _executionTime };

            }
            else
                return new Incident(_executionTime) { PartitionKey = pk, RowKey = rk, StatusCode = statusCode, FinalDate = _executionTime };

        }

        private static StatusCode GetStatusCode(Task<HttpResponseMessage> task)
        {
            StatusCode statusCode = StatusCode.OK;

            if (task.IsFaulted)
                statusCode = StatusCode.ERROR;
            else if (task.IsCanceled)
                statusCode = StatusCode.TIMEOUT;
            else
                statusCode = task.Result.StatusCode == HttpStatusCode.OK ? StatusCode.OK : StatusCode.ERROR;

            return statusCode;
        }

        private static async Task InsertExamples()
        {
            var batch = new TableBatchOperation();
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T00:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291986", StatusCode = "200", FinalDate = DateTimeOffset.Parse("2018-08-11T05:00:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T05:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291987", StatusCode = "500", FinalDate = DateTimeOffset.Parse("2018-08-11T11:00:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T11:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291988", StatusCode = "408", FinalDate = DateTimeOffset.Parse("2018-08-11T11:30:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T12:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291989", StatusCode = "200", FinalDate = DateTimeOffset.Parse("2018-08-11T14:00:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T14:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291910", StatusCode = "500", FinalDate = DateTimeOffset.Parse("2018-08-11T20:16:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T20:16:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291911", StatusCode = "408", FinalDate = DateTimeOffset.Parse("2018-08-11T22:00:16.329Z").UtcDateTime });
            batch.Insert(new Incident(DateTimeOffset.Parse("2018-08-11T22:00:16.329Z").UtcDateTime) { PartitionKey = "ENGINE", RowKey = "2018-8-11-636695305363291912", StatusCode = "200", FinalDate = DateTimeOffset.Parse("2018-08-11T23:59:16.329Z").UtcDateTime });

            await _incidentsTable.ExecuteBatchAsync(batch);
        }

        public class HealthCheckBody
        {
            public string Result { get; set; }
            public IList<HealthCheckComponent> data { get; set; }
        }

        public class HealthCheckComponent : TableEntity
        {
            public string TestType { get; set; }
            public string TestDetail { get; set; }
            public bool ServiceAvailable { get; set; }
            public double Time { get; set; }
        }
    }
}
