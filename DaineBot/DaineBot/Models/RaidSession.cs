using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Models
{
    public class RaidSession
    {
        public int Id { get; set; }
        public required Roster Roster { get; set; }
        public string? ReportCode { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public ReadyCheck? Check { get; set; }
    }
}
