using System;
using System.Collections.Generic;
using System.Text;

namespace MonitoringPaaS
{
    enum StatusCode
    {
        OK = 200,
        TIMEOUT = 408,
        ERROR = 500,
        MAINTENANCE = 503
    }
}
