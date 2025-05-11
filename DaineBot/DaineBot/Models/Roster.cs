using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Models
{
    public class Roster
    {
        public int Id { get; set; }
        public ulong Guild { get; set; }
        public ulong RosterRole { get; set; }
        public ulong RosterChannel { get; set; }
        public List<RaidSession> Sessions { get; set; } = new();
        public required string TimeZoneId { get; set; }
        public ulong RaidLeader { get; set; }
    }
}
