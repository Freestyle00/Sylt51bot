﻿using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Classes;
using System.Globalization;

namespace Sylt51bot
{
    class Program
    {
		public static CommandsNextExtension commands;
		public static DiscordClient discord;
		public static DiscordActivity g1 = new DiscordActivity("");
		public static ulong LastHb = 0; // Last heartbeat message
		public static SetupInfo cInf; // The setup info
        
		public static CommandsNextConfiguration cNcfg; // The commanddsnext config
		public static DiscordConfiguration dCfg; // The discord config
		public static List<RegisteredServer> servers; // The list registered of servers
		static void Main(string[] args)
        {
            try
            {
                if (File.Exists("config/mconfig.json"))
                {
                    cInf = Newtonsoft.Json.JsonConvert.DeserializeObject<SetupInfo>(File.ReadAllText("config/mconfig.json"));
                    if(File.Exists("config/RegServers.json"))
                    {
                        servers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RegisteredServer>>(File.ReadAllText("config/RegServers.json"));
                    }
                }
                else
                {
                    Console.WriteLine("Missing setup info");
                    Environment.Exit(0);
                }
                cNcfg = new CommandsNextConfiguration
                {
                    StringPrefixes = cInf.Prefixes,
                    CaseSensitive = false,
                    EnableDefaultHelp = true,
                    DefaultHelpChecks = new List<CheckBaseAttribute>()
                };
                dCfg = new DiscordConfiguration
                {
                    Token = cInf.Token,
                    TokenType = TokenType.Bot
                };
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                Environment.Exit(0);
            }
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            try
            {
                discord = new DiscordClient(dCfg);
                commands = discord.UseCommandsNext(cNcfg);

                commands.CommandErrored += CmdErrorHandler;
                commands.SetHelpFormatter<CustomHelpFormatter>();
				commands.RegisterCommands<LevelCommands>();
				commands.RegisterCommands<BotAdminCommands>();
				commands.RegisterCommands<GenCommands>();
                discord.MessageCreated += async (client, e) =>
                {
					if(!servers.Exists(x => x.Id == e.Guild.Id))
					{
						servers.Add(new RegisteredServer { Id = e.Guild.Id} );
						File.WriteAllText("config/RegServers.json", Newtonsoft.Json.JsonConvert.SerializeObject(servers));
					}
					if(servers.Find(x => x.Id == e.Guild.Id).EnabledModules.HasFlag(Modules.Rechenknecht))
					{
						if(e.Message.Content.Contains("€"))
						{
							double i = 0;
							long Schulden = 86300000000;
							string[] split = e.Message.Content.Split('€');
							foreach(string sS in split)
							{
								string euroamt = sS.Substring(sS.LastIndexOf(" ") + 1);

								if(euroamt.Contains(","))
								{
									euroamt = euroamt.Replace(",", ".");
								}
								if(double.TryParse(euroamt, out double amt) && amt > 0 && !double.IsNaN(amt))
								{
									i += amt;
								}
							}
							if(i > 0 && i <= 1000)
							{
								cInf.SchuldenDerDDR -= i * 1.95583;
								await e.Message.RespondAsync($"Das sind {Math.Round(i * 1.95583, 1)} Mark. {Math.Round(i * 1.95583 * 2, 1)} Ostmark. {Math.Round(i * 1.95583 * 2 * 10, 1)} Ostmark aufm Schwarzmarkt.\nVon den bisherigen Zwietracht-Pfostierungen hätte man {(1 - (double)cInf.SchuldenDerDDR/(double)Schulden).ToString("##0.00000%") } der DDR entschulden können.");
								File.WriteAllText("config/mconfig.json", Newtonsoft.Json.JsonConvert.SerializeObject(cInf));
							}
						}
					}
                };

                discord.SocketClosed += async (client, e) =>
                {
                    await discord.ReconnectAsync();
                };

				discord.GuildCreated += async (client, e) =>
				{
					if(!servers.Exists(x => x.Id == e.Guild.Id))
					{
						RegisteredServer newJoinedServer = new RegisteredServer
						{
							Id = e.Guild.Id
						};
						servers.Add(newJoinedServer);
						File.WriteAllText("config/RegServers.json", Newtonsoft.Json.JsonConvert.SerializeObject(servers));
					}
				};


                await discord.ConnectAsync();
                await SendHeartbeatAsync().ConfigureAwait(false);
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                try
				{
					Console.WriteLine("CONNECTION TERMINATED\nAttempting automatic restart...");
					File.WriteAllText("Error.log", ex.ToString());
					Main(new string[]{});
				}
				catch
				{
					Console.WriteLine("Automatic restart failed.");
				}
            }
        }
        static async  Task CmdErrorHandler(CommandsNextExtension _m, CommandErrorEventArgs e)
        {
            try
			{
				var failedChecks = ((DSharpPlus.CommandsNext.Exceptions.ChecksFailedException)e.Exception).FailedChecks;
				DiscordEmbedBuilder embed = new DiscordEmbedBuilder { Color = DiscordColor.Red, Description = "Der Befehl konnte nicht ausgeführt werden :c" };
				bool canSend = false;
				if(e.Context.Channel.PermissionsFor(await e.Context.Guild.GetMemberAsync(discord.CurrentUser.Id)).HasPermission(Permissions.SendMessages))
				{
					canSend = true;
				}
				foreach (var failedCheck in failedChecks)
				{
					if (failedCheck is CAttributes.RequireBotPermissions2Attribute)
					{
						var botperm = (CAttributes.RequireBotPermissions2Attribute)failedCheck;
						embed.AddField("Deine nötigen Berechtigungen", $"```{botperm.Permissions.ToPermissionString()}```");
						if (botperm.Permissions.HasFlag(Permissions.SendMessages))
						{
							canSend = false;
						}
					}
					if (failedCheck is CAttributes.RequireUserPermissions2Attribute)
					{
						var botperm = (CAttributes.RequireUserPermissions2Attribute)failedCheck;
						embed.AddField("Meine nötigen Berechtigungen", $"```{botperm.Permissions.ToPermissionString()}```");
					}
					if (failedCheck is RequireGuildAttribute)
					{
						RequireGuildAttribute guild = (RequireGuildAttribute)failedCheck;
						embed.AddField("Server only", "This command can not be used in DMs.");
					}
					if(failedCheck is CAttributes.ModuleAttribute)
					{
						CAttributes.ModuleAttribute mod = (CAttributes.ModuleAttribute)failedCheck;
						embed.AddField("Das folgende Modul muss aktiviert sein:", $"```{mod.module}```");
					}
					embed.AddField("Error:", $"```{e.Exception.ToString()}```");
				}
				if (canSend == true)
				{
					await e.Context.Message.RespondAsync(embed);
				}
				else
				{
					await e.Context.Guild.Owner.SendMessageAsync("I can't send messages in your server but I'm lacking perms to work so have this list in DMs instead", embed);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(ex));
				Console.WriteLine(e.Exception.ToString());
			}
        }
		public static async Task AlertException(CommandContext e, Exception ex)
		{
			await e.Message.RespondAsync(new DiscordEmbedBuilder { Color = DiscordColor.Red, Description = "Ein Fehler ist aufgetreten" });
			Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(ex));
			await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), Newtonsoft.Json.JsonConvert.SerializeObject(ex));
		}

		public static async Task AlertException(MessageCreateEventArgs e, Exception ex)
		{
			await e.Message.RespondAsync(new DiscordEmbedBuilder { Color = DiscordColor.Red, Description = "An error occured" });
			Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(ex));
			await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), Newtonsoft.Json.JsonConvert.SerializeObject(ex));
		}

		public static async Task AlertException(MessageReactionAddEventArgs e, Exception ex)
		{
			await e.Message.RespondAsync(new DiscordEmbedBuilder { Color = DiscordColor.Red, Description = "Ein Fehler ist aufgetreten" });
			Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(ex));
			await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), Newtonsoft.Json.JsonConvert.SerializeObject(ex));
		}

		public static async Task AlertException(Exception ex)
		{
			Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(ex));
			await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), Newtonsoft.Json.JsonConvert.SerializeObject(ex));
		}
		public static async Task SendHeartbeatAsync()
		{
			while (true)
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
					DiscordEmbedBuilder embed = new DiscordEmbedBuilder { Description = $"Heartbeat received!\n{discord.Ping.ToString()}ms" };
					int ping = discord.Ping;
					embed.WithFooter($"Today at [{System.DateTime.UtcNow.ToShortTimeString()}]");
					if (ping < 200)
					{
						embed.Color = DiscordColor.Green;
					}
					else if (ping < 500)
					{
						embed.Color = DiscordColor.Orange;
					}
					else
					{
						embed.Color = DiscordColor.Red;
					}
					DiscordMessage msghb = null;
					msghb = await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), embed);


					await discord.UpdateStatusAsync(g1);
					Console.WriteLine($"{System.DateTime.UtcNow.ToShortTimeString()} Ping: {discord.Ping}ms ");
					if (LastHb != 0)
					{
						try
						{
							DiscordChannel hbch = await discord.GetChannelAsync(cInf.ErrorHbChannel);
							DiscordMessage hbmsg = await hbch.GetMessageAsync(LastHb);
							await hbmsg.DeleteAsync();
						}
						catch { }
					}
					LastHb = msghb.Id;
					foreach (RegisteredServer e in servers)
					{
						try
						{
							foreach (KeyValuePair<ulong, DateTime> kvp in e.timedoutedusers)
							{
								if (DateTime.Now - kvp.Value >= e.CoolDown)
								{
									servers[servers.FindIndex(x => x.Id == e.Id)].timedoutedusers.Remove(kvp.Key);
								}
							}
						}
						catch { }
					}
                    File.WriteAllText("config/RegServers.json", Newtonsoft.Json.JsonConvert.SerializeObject(servers));
				}
				catch (Exception ex)
				{
					await discord.SendMessageAsync(await discord.GetChannelAsync(cInf.ErrorHbChannel), $"Failed to heartbeat\n\n{ex.ToString()}");
				}
				await Task.Delay(TimeSpan.FromMinutes(10));
			}
		}
    }
	
}

namespace CAttributes
{
	[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
	public class CommandClassAttribute : System.Attribute
	{
		public string classname { get; set; }
		public CommandClassAttribute(string e)
		{
			classname = e;
		}
	}
	
	[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
	public class ModuleAttribute : CheckBaseAttribute
	{
		public Modules module { get; set; }
		public ModuleAttribute(Modules e)
		{
			module = e;
		}
		public override Task<bool> ExecuteCheckAsync(CommandContext e, bool help)
		{
			return Task.FromResult(Sylt51bot.Program.servers.Find(x => x.Id == e.Guild.Id).EnabledModules.HasFlag(module));
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class RequireAuthAttribute : CheckBaseAttribute
	{
		public override Task<bool> ExecuteCheckAsync(CommandContext e, bool help)
		{
			return Task.FromResult(Sylt51bot.Program.cInf.AuthUsers.Contains(e.User.Id));
		}
	}

	[AttributeUsage( AttributeTargets.All, AllowMultiple = false, Inherited = false)]
	public sealed class RequireUserPermissions2Attribute : CheckBaseAttribute
    {
        /// <summary>
        /// Gets the permissions required by this attribute.
        /// </summary>
        public Permissions Permissions { get; }

        /// <summary>
        /// Gets this check's behaviour in DMs. True means the check will always pass in DMs, whereas false means that it will always fail.
        /// </summary>
        public bool IgnoreDms { get; } = true;

        /// <summary>
        /// Defines that usage of this command is restricted to members with specified permissions.
        /// </summary>
        /// <param name="permissions">Permissions required to execute this command.</param>
        /// <param name="ignoreDms">Sets this check's behaviour in DMs. True means the check will always pass in DMs, whereas false means that it will always fail.</param>
        public RequireUserPermissions2Attribute(Permissions permissions, bool ignoreDms = true)
        {
            this.Permissions = permissions;
            this.IgnoreDms = ignoreDms;
        }

        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
			if(ctx.Command.Name == "help")
			{
				return Task.FromResult(true);
			}
            if (ctx.Guild == null)
                return Task.FromResult(this.IgnoreDms);

            var usr = ctx.Member;
            if (usr == null)
                return Task.FromResult(false);

            if (usr.Id == ctx.Guild.OwnerId)
                return Task.FromResult(true);

            var pusr = ctx.Channel.PermissionsFor(usr);

            if ((pusr & Permissions.Administrator) != 0)
                return Task.FromResult(true);

            return (pusr & this.Permissions) == this.Permissions ? Task.FromResult(true) : Task.FromResult(false);
        }
    }

	[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    public sealed class RequireBotPermissions2Attribute : CheckBaseAttribute
    {
        /// <summary>
        /// Gets the permissions required by this attribute.
        /// </summary>
        public Permissions Permissions { get; }

        /// <summary>
        /// Gets this check's behaviour in DMs. True means the check will always pass in DMs, whereas false means that it will always fail.
        /// </summary>
        public bool IgnoreDms { get; } = true;

        /// <summary>
        /// Defines that usage of this command is only possible when the bot is granted a specific permission.
        /// </summary>
        /// <param name="permissions">Permissions required to execute this command.</param>
        /// <param name="ignoreDms">Sets this check's behaviour in DMs. True means the check will always pass in DMs, whereas false means that it will always fail.</param>
        public RequireBotPermissions2Attribute(Permissions permissions, bool ignoreDms = true)
        {
            this.Permissions = permissions;
            this.IgnoreDms = ignoreDms;
        }

        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
			if(ctx.Command.Name == "help")
			{
				return true;
			}
            if (ctx.Guild == null)
                return this.IgnoreDms;

            var bot = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (bot == null)
                return false;

            if (bot.Id == ctx.Guild.OwnerId)
                return true;

            var pbot = ctx.Channel.PermissionsFor(bot);

            if ((pbot & Permissions.Administrator) != 0)
                return true;

            return (pbot & this.Permissions) == this.Permissions;
        }
    }
}

namespace Classes
{
	public class SetupInfo
	{
        // Main Info
		public string Token { get; set; }
		public ulong ErrorHbChannel { get; set; }
		public List<string> Prefixes { get; set; }
        // Links
		public string DiscordInvite { get; set; } = null;
		public string GitHub { get; set; } = null;
        public List<ulong> AuthUsers { get; set; } = null;
		public double SchuldenDerDDR { get; set; } = 86300000000;
		public string Version = "1.1.1a";
	}

	public class RegisteredServer
	{
		public ulong Id { get; set; }
        public Dictionary<ulong, int> xplist = null;
        public Dictionary<ulong, DateTime> timedoutedusers = null;
        public List<LevelRole> lvlroles = null;
        public List<ulong> channelxpexclude = null;
		public int MinXp { get; set; } = 10;
		public int MaxXp { get; set; } = 20;
		public TimeSpan CoolDown { get; set; } = TimeSpan.FromMinutes(2);
		public Modules EnabledModules { get; set; } = Modules.Rechenknecht;
	}

	public class LevelRole
	{
		public string Name { get; set; }
		public ulong RoleId { get; set; }
		public int XpReq { get; set; }
	}

	[Flags]
	public enum Modules
	{
		Levelling = 0b01,
		Rechenknecht = 0b10,
		All = 0b11
	}
}