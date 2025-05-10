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
        public required RaidSession Session { get; set; }
        public List<ulong> AcceptedPlayers { get; set; } = [];
        public List<ulong> DeniedPlayers { get; set; } = [];
    }
}
