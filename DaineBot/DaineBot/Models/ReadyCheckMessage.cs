using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Models
{
    public class ReadyCheckMessage
    {
        public int Id { get; set; }
        public ulong MessageId { get; set; }
        public ReadyCheck Check { get; set; } = null!;
        public int CheckId { get; set; }
    }
}
