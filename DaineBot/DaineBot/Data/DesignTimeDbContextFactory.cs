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
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');

            var connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.TrimStart('/')};SSL Mode=Require;Trust Server Certificate=true;";

            var optionsBuilder = new DbContextOptionsBuilder<DaineBotDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new DaineBotDbContext(optionsBuilder.Options);
        }
    }
}
