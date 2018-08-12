using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using MonitoringPaaS.Models;

namespace MonitoringPaaS
{
    public static class SummaryDailyStatusReport
    {
        private static CloudTable _incidentsTable;
        private static CloudTable _dailyTable;

        [FunctionName("SummaryDailyStatusReport")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req,
            [Table("StatusReportEngineIncidents")] CloudTable incidentsTable,
            [Table("DailyStatusReport")] CloudTable dailyTable,
            TraceWriter log)
        {
            _incidentsTable = incidentsTable;
            _dailyTable = dailyTable;

            var date = DateTime.UtcNow;
            var pk = "ENGINE";
            var query = new TableQuery<Incident>().Where($"(PartitionKey eq '{pk}') and (ExecutionDay eq {date.Day}) and (ExecutionMonth eq {date.Month}) and (ExecutionYear eq {date.Year})");
            var results = (await _incidentsTable.ExecuteQuerySegmentedAsync(query, null)).Results;

            var dailyReport = new DailyStatusReport { PartitionKey = "ENGINE", RowKey = $"{date.Year}-{date.Month}-{date.Day}" };

            dailyReport.Uptime = results
                .Where(r => (int)StatusCode.OK == Int32.Parse(r.StatusCode))
                .Sum(r => (r.FinalDate - r.InitialDate).TotalMilliseconds);

            dailyReport.MaintenanceTime = results
                .Where(r => (int)StatusCode.TIMEOUT == Int32.Parse(r.StatusCode))
                .Sum(r => (r.FinalDate - r.InitialDate).TotalMilliseconds);

            dailyReport.Downtime = results
                .Where(r => (int)StatusCode.OK != Int32.Parse(r.StatusCode) && (int)StatusCode.TIMEOUT != Int32.Parse(r.StatusCode))
                .Sum(r => (r.FinalDate - r.InitialDate).TotalMilliseconds);

            var total = results.Sum(r => (r.FinalDate - r.InitialDate).TotalMilliseconds);

            await _dailyTable.ExecuteAsync(TableOperation.Insert(dailyReport));

        }
    }

    public class DailyStatusReport : TableEntity
    {
        public double Downtime { get; set; }
        public double Uptime { get; set; }
        public double MaintenanceTime { get; set; }
    }
}
