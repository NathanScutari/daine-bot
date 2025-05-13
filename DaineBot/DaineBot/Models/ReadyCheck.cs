using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Models
{
    public class ReadyCheck
    {
        public int Id { get; set; }
        public RaidSession Session { get; set; } = null!;
        public int SessionId { get; set; }
        public List<ulong> AcceptedPlayers { get; set; } = [];
        public List<ulong> DeniedPlayers { get; set; } = [];
        public bool ReminderSent { get; set; }
        public bool Complete { get; set; }
        public List<ReadyCheckMessage> Messages { get; set; } = [];
    }
}
