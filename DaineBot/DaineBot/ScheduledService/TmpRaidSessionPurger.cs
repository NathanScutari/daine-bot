using DaineBot.Data;
using DaineBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.ScheduledService
{
    public class TmpRaidSessionPurger : BackgroundService
    {
        private readonly DaineBotDbContext _db;

        public TmpRaidSessionPurger(DaineBotDbContext db)
        {
            _db = db;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 🔁 1. Purge des sessions temporaires trop vieilles
                    var cutoff = DateTime.UtcNow.AddMinutes(-60);
                    List<TmpRaidSession> tmpRaidSessions = _db.TmpSessions.Where(s => s.CreatedAt < cutoff).ToList();

                    if (tmpRaidSessions.Count > 0)
                    {
                        Debug.WriteLine($"{tmpRaidSessions.Count()} sessions en cours de création supprimées.");
                    }
                    _db.TmpSessions.RemoveRange(tmpRaidSessions);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                // ⏱️ Attente de 1 minute
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
