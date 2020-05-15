using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SudoBot.Attributes;

namespace SudoBot.Commands
{
    public class TestCommands : BaseCommandModule
    {
        [CheckForPermissions(SudoPermission.Any, GuildPermission.TestCommands)]
        [Command("test")]
        public async Task T(CommandContext ctx, DiscordEmoji e)
        {
            await ctx.Channel.SendMessageAsync(e.Url);
        }
    }
}