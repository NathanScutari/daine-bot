using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace DaineBot.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DaineBotDbContext>
    {
        public DaineBotDbContext CreateDbContext(string[] args)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "dainebotdata.db");

            var optionsBuilder = new DbContextOptionsBuilder<DaineBotDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new DaineBotDbContext(optionsBuilder.Options);
        }
    }
}
