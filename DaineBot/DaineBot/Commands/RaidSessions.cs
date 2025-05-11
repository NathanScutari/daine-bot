using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace DaineBot.Commands
{
    public class Raid : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaineBotDbContext _db;
        private readonly IAdminService _adminService;
        private readonly RaidService _raidService;

        public Raid(DaineBotDbContext db, IAdminService adminService, RaidService raidService)
        {
            _db = db;
            _adminService = adminService;
            _raidService = raidService;
        }

        [SlashCommand("raid-session", "Affiche les infos des sessions de raid du roster")]
        public async Task RaidSession()
        {
            var user = Context.User as SocketGuildUser;

            if (user == null)
            {
                await RespondAsync("La commande doit être lancée depuis un serveur", ephemeral: true);
                return;
            }

            Models.Roster? roster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == Context.Guild.Id);

            if (roster == null)
            {
                await RespondAsync("Il n'y a pas encore de roster configuré sur ce serveur.", ephemeral: true);
                return;
            }

            List<RaidSession> sessions = _db.RaidSessions.Where(rs => rs.Roster == roster).ToList();

            ComponentBuilder? builder = null;
            if (await _adminService.HasAdminRoleAsync(Context))
            {
                builder = new ComponentBuilder()
                    .WithButton("Ajouter un jour de raid", "raid-session-create");
            }

            if (sessions.Count == 0)
            {
                await RespondAsync("Il n'y a pas encore de session de raid configurée pour le roster.", ephemeral: true, components: builder?.Build());
            }
            else
            {
                string response = $"Il y a **{sessions.Count} sessions par semaine :**";
                foreach (var session in sessions)
                {
                    response += $"\n  * {CultureInfo.CurrentCulture.DateTimeFormat.DayNames[session.Day]} : {session.Hour}H{session.Minute}, Durée : {session.Duration.ToString(@"hh\:mm")}";
                }

                await RespondAsync(response, ephemeral: true, components: builder?.Build());
            }
        }

        [ComponentInteraction("raid-session-create")]
        public async Task RaidSessionCreate()
        {
            var tmpRaidSession = _db.TmpSessions.FirstOrDefault(trs => trs.UserId == Context.User.Id && trs.GuildId == Context.Guild.Id);

            if (tmpRaidSession != null)
            {
                _db.TmpSessions.Remove(tmpRaidSession);
                await _db.SaveChangesAsync();
            }

            var builder = new ComponentBuilder()
                        .WithButton("Lundi", customId: "raid_day_1")
                        .WithButton("Mardi", customId: "raid_day_2")
                        .WithButton("Mercredi", customId: "raid_day_3")
                        .WithButton("Jeudi", customId: "raid_day_4")
                        .WithButton("Vendredi", customId: "raid_day_5")
                        .WithButton("Samedi", customId: "raid_day_6")
                        .WithButton("Dimanche", customId: "raid_day_0");

            await RespondAsync("Choisis le jour du raid", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("raid_day_*")]
        public async Task RaidSessionCreate(string id)
        {
            var dayOfWeek = int.Parse(id.Replace("raid_day_", ""));

            var tmpRaidSession = new TmpRaidSession()
            {
                CreatedAt = DateTime.UtcNow,
                Day = dayOfWeek,
                GuildId = Context.Guild.Id,
                UserId = Context.User.Id
            };

            await _db.TmpSessions.AddAsync(tmpRaidSession);
            await _db.SaveChangesAsync();

            await RespondWithModalAsync<RaidSessionModal>("raid_time_input");
        }


        [ModalInteraction("raid_time_input")]
        public async Task RaidSessionFinaliseCreation(RaidSessionModal raidModal)
        {
            var tmpRaidSession = await _db.TmpSessions.FirstOrDefaultAsync(trs => trs.UserId == Context.User.Id && trs.GuildId == Context.Guild.Id);

            if (tmpRaidSession == null)
            {
                await RespondAsync("Erreur pendant la création de la session, une autre commande de création de session a peut être été lancée entre temps ?", ephemeral: true);
                return;
            }

            var hourStr = raidModal.Hour;
            var minuteStr = raidModal.Minute;
            var durationStr = raidModal.Duration;

            if (!int.TryParse(hourStr, out var hour) || hour < 0 || hour > 23 ||
                !int.TryParse(minuteStr, out var minute) || minute < 0 || minute > 59 ||
                !int.TryParse(durationStr, out var duration) || duration <= 0)
            {
                await RespondAsync("⛔ Valeurs invalides.", ephemeral: true);
                return;
            }

            var roster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == Context.Guild.Id);

            if (roster == null)
            {
                await RespondAsync("Erreur dans la récupération du roster, il a peut être été supprimé entre temps.", ephemeral: true);
                return;
            }

            var raidSession = new RaidSession()
            {
                Day = tmpRaidSession.Day,
                Hour = hour,
                Minute = minute,
                Duration = TimeSpan.FromMinutes(duration),
                Roster = roster,
            };

            _db.TmpSessions.Remove(tmpRaidSession);
            await _db.RaidSessions.AddAsync(raidSession);
            await _db.SaveChangesAsync();

            await RespondAsync("La session de raid a bien été ajoutée.", ephemeral: true);
        }

        [SlashCommand("modifier-prochaine-session", "Permet de modifier une prochaine session de raid")]
        public async Task RaidSessionTmpEdit()
        {
            if (!await _adminService.HasAdminRoleAsync(Context)) { return; }

            var guild = Context.Guild;
            if (guild == null) { return; }

            var builder = new ComponentBuilder();

            Models.Roster? roster = _db.Rosters.FirstOrDefault(r => r.Guild == guild.Id);

            if (roster == null)
            {
                await RespondAsync("Il n'y a pas encore de roster configuré sur le serveur.");
                return;
            }

            var sessionTuple = _raidService.GetAllSessionsForRoster(roster);

            foreach (var session in sessionTuple)
            {
                builder.WithButton(session.sessionStr, "session_edit:" + session.id);
            }

            await RespondAsync("Choisis une session à modifier", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("session_edit:*")]
        public async Task RaidSessionChoiceEdit(string id)
        {
            int sessionId = int.Parse(id.Replace("session_edit:", ""));

            await RespondWithModalAsync<RaidSessionModal>($"session_edit_modal:{sessionId}");
        }

        [ModalInteraction("session_edit_modal:*")]
        public async Task RaidSessionConfirmEdit(string id, RaidSessionModal modal)
        {
            int sessionId = int.Parse(id.Replace("session_edit_modal:", ""));
        }

    }

    public class RaidSessionModal : IModal
    {
        public string Title => "Heure de début et durée du raid";

        [InputLabel("Heure de début")]
        [ModalTextInput("hour", TextInputStyle.Short)]
        public string Hour { get; set; }

        [InputLabel("Minute")]
        [ModalTextInput("minute", TextInputStyle.Short)]
        public string Minute { get; set; }

        [InputLabel("Durée de la session")]
        [ModalTextInput("duration", TextInputStyle.Short)]
        public string Duration { get; set; }
    }
}
