using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Models
{
    public class AdminRole
    {
        public int Id { get; set; }
        public ulong Guild { get; set; }
        public List<ulong> RoleList { get; set; } = [];
    }
}
