using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SudoBot.Attributes;
using SudoBot.Database;
using SudoBot.Models;
using SudoBot.Handlers;

namespace SudoBot.Commands
{
    [Group("ranking"), Aliases("r")]
    public class RankCommands: BaseCommandModule
    {
        [Command("givePoints"), Aliases("give")]
        [CheckForPermissions(SudoPermission.Mod, GuildPermission.Any)]
        [Description("Einem User Bonus Punkte Geben")]
        public async Task GiveSp(CommandContext ctx, [Description("Der User der die Punkte Erhalten Soll")]DiscordMember member, [Description("Anzahl der Punkte")]int count)
        {
            var user = await User.GetOrCreateUser(member);
            var guild = await Guild.GetGuild(user.GuildId);
            await user.AddSpecialPoints(count);
            await ctx.Channel.SendMessageAsync($"{member.Mention} hat {count.ToString()} {guild.RankingPointName ?? "XP"} erhalten");
        }

        [Command("setRole")]
        [Aliases("sr")]
        [Description("Eine Rolle fürs Ranking System Festlegen")]
        [CheckForPermissions(SudoPermission.Admin, GuildPermission.Any)]
        public async Task SetRankingRole(CommandContext ctx, DiscordRole role, int points)
        {
            var guild = await Guild.GetGuild(ctx.Guild.Id);
            await guild.AddRankingRole(role, points);
            await ctx.Channel.SendMessageAsync($"Die Rolle {role.Name} ist mit {points.ToString()} IQ zu Erreichen!");
        }
        
        [Command("removeRole")]
        [Aliases("rr")]
        [Description("Eine Rolle aus dem Ranking System Entfernen")]
        [CheckForPermissions(SudoPermission.Admin, GuildPermission.Any)]
        public async Task RemoveRankingRole(CommandContext ctx, DiscordRole role)
        {
            var guild = await Guild.GetGuild(ctx.Guild.Id);
            var success = await guild.RemoveRankingRole(role);
            if (success)
            {
                await ctx.Channel.SendMessageAsync($"Die Rolle {role.Name} wurde aus dem Ranking entfernt!");
            }
            else
            {
                await ctx.Channel.SendMessageAsync($"Die Rolle {role.Name} ist nicht im Ranking!");
            }
        }

        [Command("setName")]
        [Description("Den Namen der Punkte setzen (Default: XP)")]
        [CheckForPermissions(SudoPermission.Admin, GuildPermission.Any)]
        public async Task SetRankingName(CommandContext ctx, string name)
        {
            var guild = await Guild.GetGuild(ctx.Guild.Id);
            var oldname = guild.RankingPointName;
            await guild.SetRankingPointsName(name);
            await ctx.Channel.SendMessageAsync($"Der Name wurde von {oldname} auf {guild.RankingPointName} geändert!");
        }
        
        [Command("setTimeMultiplier")]
        [Description("Setzen wie viel ein Tag als Nachrichten zählt (Default: 10 Nachrichten Pro Tag seit Join Date)")]
        [CheckForPermissions(SudoPermission.Admin, GuildPermission.Ranking)]
        public async Task SetTimeMultiplier(CommandContext ctx, int ammount)
        {
            var guild = await Guild.GetGuild(ctx.Guild.Id);
            await guild.SetRankingTimeMultipier(ammount);
            await ctx.Channel.SendMessageAsync($"Der Zeit Multiplikator wurde auf {guild.RankingTimeMultiplier.ToString()} gesetzt!");
        }

        [Command("list")]
        [Description("Auflistung aller Rollen im Ranking System")]
        [CheckForPermissions(SudoPermission.Any, GuildPermission.Any)]
        public async Task ListRankingRoles(CommandContext ctx)
        {
            var guild = await Guild.GetGuild(ctx.Guild.Id);
            var roles = guild.RankingRoles.OrderBy(x => x.Points).ToList();

            if (roles == null || roles.Count == 0)
            {
                await ctx.Channel.SendMessageAsync("Es wurden noch keine Rollen festgelegt, siehe `$help ranking setRole`");
                return;
            } 
            
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Aquamarine)
                .WithTitle("Rollen");
            
            foreach (var r in roles)
            {
                var drole = ctx.Guild.GetRole(r.Role);
                embed.AddField(drole.Name, $"{r.Points.ToString()} {guild.RankingPointName ?? "XP"}", true);
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}