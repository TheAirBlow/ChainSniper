using Configuration = DisControl.Configuration;
using Microsoft.Extensions.Logging;
using DSharpPlus.Entities;
using DisControl;
using DSharpPlus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("[DisControl] Initializing Discord");
var factory = new LoggerFactory().AddSerilog();
var client = new DiscordClient(new DiscordConfiguration {
    Token = Configuration.Instance.BotToken,
    Intents = DiscordIntents.AllUnprivileged
              | DiscordIntents.MessageContents 
              | DiscordIntents.GuildMessages,
    TokenType = TokenType.Bot,
    LoggerFactory = factory
});

Log.Information("[DisControl] Connecting to Discord");
await client.ConnectAsync();
client.Ready += async (_, _) => {
    await client.UpdateStatusAsync(new DiscordActivity(
        "the number chain", ActivityType.Watching));
};

var dict = new Dictionary<ulong, List<ulong>>();
client.MessageCreated += async (_, args) => {
    var entry = Configuration.Instance.Entries.FirstOrDefault(
        x => x.ChainChannel == args.Channel.Id
             && x.Guild == args.Guild.Id);
    if (entry == null) return;
    var ban = false; var reason = ""; ulong expected;
    if (!ulong.TryParse(args.Message.Content, out var num)) {
        reason = "Not a number"; ban = true;
    }
    
    lock (ChainManager.ChainNumber) {
        expected = ChainManager.ChainNumber[args.Channel.Id] + 1;
        if (!ban && num != expected) {
            reason = "Wrong number"; ban = true;
        }

        // Increment chain number, almost forgot about that
        if (!ban) ChainManager.ChainNumber[args.Channel.Id]++;
    }

    if (ban) {
        await args.Message.DeleteAsync();
        var failed = false;
        try {
            await args.Guild.BanMemberAsync(args.Author.Id,
                0, $"You broke the chain: {reason}");
        } catch {
            failed = true;
        }

        var embed = new DiscordEmbedBuilder()
            .WithColor(failed ? DiscordColor.Red : DiscordColor.Azure)
            .WithTitle(failed
                ? "Failed to ban a member (no permissions)"
                : "Someone broke the chain and got sniped!")
            .AddField("Perpetrator", args.Author.Username, true)
            .AddField("Reason", reason, true)
            .AddField("Message", args.Message.Content, true)
            .AddField("Expected", expected.ToString(), true)
            .AddField("Bruh", "Git Gud", true)
            .AddField("Why", "Who knows", true);
        if (!failed)
            embed.WithFooter("If a staff member thinks it's unfair, click the button below, roles will be given back");
        var builder = new DiscordMessageBuilder()
            .WithEmbed(embed.Build());
        if (!failed)
            builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Secondary,
                args.Author.Id.ToString(), "Unban member"));
        var channel = args.Guild.GetChannel(entry.LogChannel);
        await builder.SendAsync(channel);
    }
};

client.InteractionCreated += async (_, args) => {
    var member = await args.Interaction.Guild.GetMemberAsync(
        args.Interaction.User.Id);
    if (!member.Permissions.HasPermission(Permissions.BanMembers)) {
        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(
                new DiscordEmbedBuilder()
                    .WithTitle("You don't have Ban Members permission")
                    .WithDescription("You're not a staff member, git gud")
                    .Build()).AsEphemeral());
        return;
    }
    
    var id = ulong.Parse(args.Interaction.Data.CustomId);
    var perpetrator = await args.Interaction.Guild.GetMemberAsync(id);
    await perpetrator.UnbanAsync($"Requested by staff member {member.Username}");
    if (dict.ContainsKey(id)) {
        foreach (var i in dict[id]) {
            var role = args.Interaction.Guild.GetRole(i);
            await perpetrator.GrantRoleAsync(role, "Restore roles after unban");
        }
        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(
                new DiscordEmbedBuilder()
                    .WithTitle("Member was successfully unbanned, roles were given back")
                    .WithDescription($"Requested by staff member {member.Username}")
                    .Build()));
        return;
    }
    
    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().AddEmbed(
            new DiscordEmbedBuilder()
                .WithTitle("Member was unbanned, but was unable to recover roles")
                .WithDescription("Bot was restarted sometime, so roles were lost\n" +
                                 $"Requested by staff member {member.Username}")
                .Build()));
};

Log.Information("[DisControl] Initializing chain manager");
await ChainManager.Initialize(client);

Log.Information("[DisControl] Initialization finished successfully");
await Task.Delay(-1);