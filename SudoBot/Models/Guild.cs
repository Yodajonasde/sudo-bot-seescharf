using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SudoBot.Database;

namespace SudoBot.Models
{
    public class Guild
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public ulong GuildId { get; set; }

        public int TicketCount { get; set; }
        
        public ulong CustomsRole { get; private set; }

        public bool HasLeveling { get; set; }
        public bool HasCustoms { get; set; }
        public bool HasSupport { get; set; }

        public Guild(ulong guildId)
        {
            GuildId = guildId;
            HasLeveling = false;
            HasCustoms = true;
            HasSupport = false;
            TicketCount = 1;
        }
        
        private async Task SaveGuild()
        {
            await MongoCrud.Instance.UpdateGuild(this);
        }

        public async Task RemoveAllCustomsRole(CommandContext ctx)
        {
            if (CustomsRole == 0) return;
            
            var role = ctx.Guild.GetRole(CustomsRole);
            var allMembers = await ctx.Guild.GetAllMembersAsync();
            var customsMembers = allMembers.Where(user => user.Roles.Contains(role));
            
            foreach (DiscordMember member in customsMembers)
            {
                await member.RevokeRoleAsync(role);
            }
        }

        public async Task SetCustomsRole(ulong roleId)
        {
            CustomsRole = roleId;
            await SaveGuild();
        }

        public async Task ResetTickets()
        {
            var users = await MongoCrud.Instance.GetUsersWithoutTicket(GuildId);
            foreach (var user in users)
            {
                await user.AddTickets(TicketCount);
            }
        }
    }
}