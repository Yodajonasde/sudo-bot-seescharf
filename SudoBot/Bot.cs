﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Net;
using DSharpPlus.Lavalink;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SudoBot.Attributes;
using SudoBot.Commands;
using SudoBot.Database;
using SudoBot.Models;
using SudoBot.Handlers;

namespace SudoBot
{
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public InteractivityExtension Interactivity { get; private set; }
        
        private MessageHandler _messageHandler = new MessageHandler();
        private ReactionRolesHandler _reactionRolesHandler = new ReactionRolesHandler();
        private MemberUpdateHandler _memberUpdateHandler = new MemberUpdateHandler();
        public async Task RunAsync()
        {

            // Bot
            var config = new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("BOTTOKEN"),
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Debug
            };

            Client = new DiscordClient(config);
            Client.Ready += OnClientReady;
            Client.GuildAvailable += OnGuildAvailable;
            Client.GuildCreated += OnGuildCreated;
            
            Client.ClientErrored += OnClientError;
            Client.MessageCreated += MessageCreated;
            Client.MessageReactionAdded += OnMessageReactionAdded;
            Client.MessageReactionRemoved += OnMessageReactionRemoved;
            Client.GuildDeleted += OnGuildDeleted;

            Client.GuildMemberUpdated += OnMemberUpdated;
            
            //DI
            var services = new ServiceCollection()
                .AddSingleton<Translation>()
                .BuildServiceProvider();

            //Commands
            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new []{"$"},
                EnableMentionPrefix = true,
                EnableDms = false,
                Services =  services
            };

            Commands = Client.UseCommandsNext(commandsConfig);
            Commands.SetHelpFormatter<SudoHelpFormatter>();

            Commands.CommandErrored += OnCommandErrored;

            //Interactivity
            var interactivityConfig = new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(1)
            };

            Interactivity = Client.UseInteractivity(interactivityConfig);
            
            var lavalinkEndpoint = new ConnectionEndpoint
            {
                Hostname = "163.172.148.235",
                Port = 2334
            };
            
            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = Environment.GetEnvironmentVariable("LAVALINK"),
                RestEndpoint = lavalinkEndpoint,
                SocketEndpoint = lavalinkEndpoint
            };
            
            var lavalink = Client.UseLavalink();
            
            Commands.RegisterCommands<FunCommands>();
            Commands.RegisterCommands<UtilityCommands>();
            Commands.RegisterCommands<RankCommands>();
            Commands.RegisterCommands<CustomGamesCommands>();
            Commands.RegisterCommands<TestCommands>();
            Commands.RegisterCommands<AdminCommands>();
            Commands.RegisterCommands<ModCommands>();
            Commands.RegisterCommands<FileCommands>();
            Commands.RegisterCommands<GlobalCommands>();
            Commands.RegisterCommands<TagCommands>();
            Commands.RegisterCommands<ParserCommands>();
            Commands.RegisterCommands<SearchCommands>();
            Commands.RegisterCommands<ListCommands>();
            Commands.RegisterCommands<ApecCommands>();
            Commands.RegisterCommands<MusicCommands>();
            Commands.RegisterCommands<ReactionRoleCommands>();

            // scheduler
            var task = Task.Run(async () =>
            {
                await Task.Delay(1000*30);
                while (true)
                {
                    await Task.Delay(1000);
                    try
                    {
                        await Scheduled.RunSchedule();
                    }
                    catch (Exception e)
                    {
                        Client.Logger.Log(LogLevel.Critical, $"Error running Scheduler: {e.Message}");
                    }
                }
            });
            
            // Start Bot
            await Client.ConnectAsync();
            await lavalink.ConnectAsync(lavalinkConfig);
            await Task.Delay(-1);
        }

        private Task OnGuildDeleted(DiscordClient sender, GuildDeleteEventArgs e)
        {
            sender.Logger.Log(LogLevel.Information,  $"Bot Left: [{e.Guild.Id}] {e.Guild.Name}", DateTime.Now);

            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithThumbnail(e.Guild.IconUrl)
                .WithTitle("Bot Left")
                .WithDescription(e.Guild.Name)
                .AddField("ID", e.Guild.Id.ToString())
                .AddField("User Count", e.Guild.MemberCount.ToString());
            
            Globals.LogChannel.SendMessageAsync(embed: embed.Build());
            
            return Task.CompletedTask;
        }
        private Task OnClientReady(DiscordClient sender, ReadyEventArgs e)
        {
            Globals.Client = sender;
            sender.Logger.Log(LogLevel.Information,  $"Bot Started", DateTime.Now);

            sender.UpdateStatusAsync(new DiscordActivity("$invite", ActivityType.ListeningTo));
            
            
            
            return Task.CompletedTask;
        }

        private Task OnCommandErrored(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException)
            {
                var sentMessage = e.Context.Channel.SendMessageAsync($"Command nicht Gefunden").GetAwaiter().GetResult();
                Task.Delay(2000).GetAwaiter().GetResult();
                sentMessage.DeleteAsync();

            }
            
            else if (e.Command.Name == "list")
            {
                if (e.Exception.Message == "Value cannot be null. (Parameter 'source')")
                {
                    e.Context.Channel.SendMessageAsync("Keine Rollen im Ranking, füge eine mit `$rank set-role {@rolle} {punkte}` hinzu");
                } 
            }


            else if (e.Exception.Message == "NO LOG CHANNEL")
            {
                e.Context.Channel.SendMessageAsync("Es ist ein fehler aufgetreten, allerdings konnte dieser nicht gemeldet werden da kein Error Log channel festgelegt wurde, bitte lege einen mit `$admin set-log-channel #channel` fest");
            }
            
            else if (e.Exception is InvalidOperationException)
            {
                var commandName = e.Command.Name;
                
                e.Context.Channel.SendMessageAsync($"Invalide Ausführung: {e.Context.Message.Content} \n(`$help {e.Command.Name}`)").GetAwaiter().GetResult();
                //
                // var helpContext = e.Context.CommandsNext.CreateFakeContext(e.Context.User, e.Context.Channel,
                //     e.Context.Message.Content, e.Context.Prefix, help, e.Command.Name);
                //
                // help.ExecuteAsync(helpContext);
            }
            
            else if (e.Exception is ChecksFailedException)
            {
                var exception = (ChecksFailedException) e.Exception;
                var failed = exception.FailedChecks;
                foreach (var f in failed)
                {
                    if (f is CheckForPermissionsAttribute)
                    {
                        e.Context.RespondAsync("Keine Berechtigung diesen command zu verwenden!");
                    } else if (f is DSharpPlus.CommandsNext.Attributes.RequireOwnerAttribute)
                    {
                        e.Context.RespondAsync("Nix Da!");
                    } else if (f is DSharpPlus.CommandsNext.Attributes.CooldownAttribute)
                    {
                        var attr = (DSharpPlus.CommandsNext.Attributes.CooldownAttribute) f;
                        var reset = attr.GetRemainingCooldown(e.Context);
                        string remaining = "";
                        if (reset.Days > 0)
                        {
                            remaining += $"{reset.Days} Tage ";
                        }
                        if (reset.Hours > 0)
                        {
                            remaining += $"{reset.Hours} Stunden ";
                        }
                        if (reset.Minutes > 0)
                        {
                            remaining += $"{reset.Minutes} Minuten ";
                        }
                        if (reset.Seconds > 0)
                        {
                            remaining += $"{reset.Seconds} Sekunden ";
                        }

                        var sent = e.Context.RespondAsync($"Zurzeit im Cooldown, bitte {remaining}warten").GetAwaiter().GetResult();
                        Task.Delay(2000).GetAwaiter().GetResult();
                        e.Context.Message.DeleteAsync();
                        Task.Delay(1000).GetAwaiter().GetResult();
                        sent.DeleteAsync();
                    }
                    else
                    {
                        e.Context.RespondAsync($"Exception: ```{e.Exception.Message}``` Check: ```{f.TypeId}```Wenn dies ein unbekannter Fehler ist bitte auf den `$guild` Discord kommen und JMP#7777 kontaktieren."); 
                    }
                }
            }

            else if (e.Exception is ArgumentException)
            {
                e.Context.Channel.SendMessageAsync($"Invalide Argumente: {e.Context.Message.Content}").GetAwaiter().GetResult();
                var commandName = e.Command.Name;
                var help = e.Context.CommandsNext.FindCommand("help", out commandName);

                var helpContext = e.Context.CommandsNext.CreateFakeContext(e.Context.User, e.Context.Channel,
                    e.Context.Message.Content, e.Context.Prefix, help, $"{e.Command.QualifiedName}");
                
                help.ExecuteAsync(helpContext);
            }
            
            else if (e.Exception is NotImplementedException)
            {
                e.Context.Channel.SendMessageAsync("Dieses Feature ist noch nicht Fertig!, wenn es dringlich ist `$guild` beitreten und JMP#7777 kontaktieren").GetAwaiter().GetResult();
            }
            
            else if (e.Exception is ExternalException)
            {
                e.Context.Channel.SendMessageAsync(e.Exception.Message).GetAwaiter().GetResult();
            }

            else {
                e.Context.Channel.SendMessageAsync($"```{e.Exception.Message}```Wenn dies ein unbekannter Fehler ist bitte auf den `$guild` Discord kommen und JMP#7777 kontaktieren.").GetAwaiter().GetResult();
            }

            Task.Delay(2000).GetAwaiter().GetResult();
            e.Context.Message.DeleteAsync();

            return Task.CompletedTask;
            }

        private Task OnGuildCreated(DiscordClient sender, GuildCreateEventArgs e)
        {
            sender.Logger.Log(LogLevel.Information,  $"Bot Joined: [{e.Guild.Id}] {e.Guild.Name}", DateTime.Now);

            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Aquamarine)
                .WithThumbnail(e.Guild.IconUrl)
                .WithTitle("Bot Joined")
                .WithDescription(e.Guild.Name)
                .AddField("ID", e.Guild.Id.ToString())
                .AddField("User Count", e.Guild.MemberCount.ToString());
            
            Globals.LogChannel.SendMessageAsync(embed: embed.Build());
            
            Guild g = new Guild(e.Guild.Id);
            g.Name = e.Guild.Name;
            g.MemberCount = e.Guild.MemberCount;
            var foundGuild = Guild.GetGuild(e.Guild.Id).GetAwaiter().GetResult();
            if (foundGuild == null)
            {
                Mongo.Instance.InsertGuild(g).GetAwaiter().GetResult();
            }
            else
            {
                Mongo.Instance.UpdateGuild(g).GetAwaiter().GetResult();
            }
            
            
            return Task.CompletedTask;
        }
        
        private Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
        {
            sender.Logger.Log(LogLevel.Information,  $"Bot Logged in on: [{e.Guild.Id}] {e.Guild.Name}", DateTime.Now);

            var guild = Guild.GetGuild(e.Guild.Id).GetAwaiter().GetResult();
            if (guild == null)
            {
                Guild g = new Guild(e.Guild.Id);
                g.Name = e.Guild.Name;
                g.MemberCount = e.Guild.MemberCount;
                Mongo.Instance.InsertGuild(g).GetAwaiter().GetResult();
            }
            else
            {
                if (guild.MemberCount == 0 || guild.Name == null)
                {
                    guild.Name = e.Guild.Name;
                    guild.MemberCount = e.Guild.MemberCount;
                    guild.SaveGuild().GetAwaiter().GetResult();
                }

                if (guild.Name != e.Guild.Name || guild.MemberCount != e.Guild.MemberCount)
                {
                    guild.Name = e.Guild.Name;
                    guild.MemberCount = e.Guild.MemberCount;
                    guild.SaveGuild().GetAwaiter().GetResult();
                }
            }

            if (e.Guild.Id == 468835109844418575)
            {
                var c = Globals.LogChannel;
                c.SendMessageAsync("Bot Started");
            }
            
            return Task.CompletedTask;
        }

        private Task OnMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs e)
        {
            sender.Logger.Log(LogLevel.Information, $"Member Updated: [{e.Guild}] ({e.Member})", DateTime.Now);
            _memberUpdateHandler.HandleRoleChange(e).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
        
        private Task OnClientError(DiscordClient sender, ClientErrorEventArgs e)
        {
            sender.Logger.Log(LogLevel.Error,  $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            
            return Task.CompletedTask;
        }

        private Task OnMessageReactionAdded(DiscordClient client, MessageReactionAddEventArgs args)
        {
            try
            {
                _reactionRolesHandler.HandleReactionAdded(args).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                client.Logger.Log(LogLevel.Error, $"Reaction Added: [{args.Guild}: {args.Channel}] {e.Message}");
            }
            
            return Task.CompletedTask;
        }
        
        private Task OnMessageReactionRemoved(DiscordClient client, MessageReactionRemoveEventArgs args)
        {
            try
            {
                _reactionRolesHandler.HandleReactionRemoved(args).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                client.Logger.Log(LogLevel.Error, $"Reaction Removed: [{args.Guild}: {args.Channel}] {e.Message}");
            }
            
            return Task.CompletedTask;
        }
        
        private Task MessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            sender.Logger.Log(LogLevel.Information,  $"Message Created: [{e.Guild.Id} : {e.Channel.Id}] ({e.Author.Username}): {e.Message.Content}", DateTime.Now);
            try
            {
                _messageHandler.HandleMessage(e).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (ex.Message == "NO LOG CHANNEL")
                {
                }
                else
                {
                    throw ex;
                }
            }
            
            return Task.CompletedTask;
        }
    }
}
