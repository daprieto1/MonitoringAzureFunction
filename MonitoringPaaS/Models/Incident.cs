using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonitoringPaaS.Models
{
    public class Incident : TableEntity
    {
        public string StatusCode { get; set; }
        public DateTime InitialDate { get; set; }
        public DateTime FinalDate { get; set; }
        public int ExecutionDay { get; set; }
        public int ExecutionMonth { get; set; }
        public int ExecutionYear { get; set; }

        public Incident() { }

        public Incident(DateTime executionDate)
        {
            this.ExecutionYear = executionDate.Year;
            this.ExecutionMonth = executionDate.Month;
            this.ExecutionDay = executionDate.Day;
            this.InitialDate = executionDate;
        }
    }
}
