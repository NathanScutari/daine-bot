using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DaineBotDbContext>
    {
        public DaineBotDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DaineBotDbContext>();
            optionsBuilder.UseSqlite("Data Source=dainebotdata.db");

            return new DaineBotDbContext(optionsBuilder.Options);
        }
    }
}
