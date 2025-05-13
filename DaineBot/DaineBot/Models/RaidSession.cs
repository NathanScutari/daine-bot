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
        public int RosterId { get; set; }
        public string? ReportCode { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Day { get; set; }
        public TimeSpan Duration { get; set; }
        public ReadyCheck? Check { get; set; }
        public DateTime NextSession { get; set; }
    }
}
