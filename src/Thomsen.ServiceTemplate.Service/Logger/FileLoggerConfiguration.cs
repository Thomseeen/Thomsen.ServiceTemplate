using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Service.Logger
{
    public class FileLoggerConfiguration
    {
        public string LogFileDirectory { get; set; } = "";

        public int MaxLogFileSizeKB { get; set; } = 2000;

        public int MaxOldLogFilesCnt { get; set; } = 10;
    }
}
