using Configuration = DisControl.Configuration;
using Microsoft.Extensions.Logging;
using DSharpPlus.Entities;
using DSharpPlus;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("[ChainSniper] Initializing Discord");
var factory = new LoggerFactory().AddSerilog();
var client = new DiscordClient(new DiscordConfiguration {
    Token = Configuration.Instance.BotToken,
    Intents = DiscordIntents.AllUnprivileged
              | DiscordIntents.MessageContents,
    TokenType = TokenType.Bot,
    LoggerFactory = factory
});

Log.Information("[ChainSniper] Connecting to Discord");
await client.ConnectAsync();
client.Ready += async (_, _) => {
    await client.UpdateStatusAsync(new DiscordActivity(
        "the number chain", ActivityType.Watching));
};

client.MessageCreated += async (_, args) => {
    var entry = Configuration.Instance.Entries.FirstOrDefault(
        x => x.ChainChannel == args.Channel.Id
             && x.Guild == args.Guild.Id);
    if (entry == null) return;
    var ban = false; var reason = "";
    if (!ulong.TryParse(args.Message.Content, out var num)) {
        reason = "Not a number"; ban = true;
    }
    
    var message = (await args.Channel.GetMessagesAsync(2))[1];
    if (message.Author.Id == args.Author.Id) {
        reason = "Same person"; ban = true;
    }
    
    var expected = ulong.Parse(message.Content) + 1;
    if (!ban && num != expected) {
        reason = "Wrong number"; ban = true;
    }

    if (ban) {
        entry.Infractions++;
        await args.Message.DeleteAsync();
        var failed = false;
        try {
            await args.Guild.BanMemberAsync(args.Author.Id,
                0, $"You broke the chain: {reason}");
        } catch {
            failed = true;
        }

        if (failed) return;
        var embed = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Azure)
            .WithTitle("Someone broke the chain and got sniped!")
            .AddField("Perpetrator", args.Author.Username, true)
            .AddField("Reason", reason, true)
            .AddField("Message", args.Message.Content, true)
            .AddField("Expected", expected.ToString(), true)
            .AddField("Bruh moment", "That's really stupid", true)
            .AddField("Infractions", $"{entry.Infractions} total", true);
        var builder = new DiscordMessageBuilder()
            .WithEmbed(embed.Build());
        var channel = args.Guild.GetChannel(entry.LogChannel);
        await builder.SendAsync(channel);
    }
};

client.MessageUpdated += async (_, args) => {
    var entry = Configuration.Instance.Entries.FirstOrDefault(
        x => x.ChainChannel == args.Channel.Id
             && x.Guild == args.Guild.Id);
    if (entry == null) return;
    var failed = false;
    try {
        await args.Guild.BanMemberAsync(args.Message.Author.Id,
            0, "You broke the chain: Sabotage (editing)");
    } catch {
        failed = true;
    }

    if (failed) return;
    entry.Infractions++;
    await args.Message.DeleteAsync();
    var embed = new DiscordEmbedBuilder()
        .WithColor(DiscordColor.Azure)
        .WithTitle("Someone broke the chain and got sniped!")
        .AddField("Perpetrator", args.Author.Username, true)
        .AddField("Reason", "Sabotage (editing)", true)
        .AddField("Message", args.Message.Content, true)
        .AddField("Expected", "Come on man!", true)
        .AddField("Bruh moment", "That's really stupid", true)
        .AddField("Infractions", $"{entry.Infractions} total", true);
    var builder = new DiscordMessageBuilder()
        .WithEmbed(embed.Build());
    var channel = args.Guild.GetChannel(entry.LogChannel);
    await builder.SendAsync(channel);
};

client.MessageDeleted += async (_, args) => {
    var entry = Configuration.Instance.Entries.FirstOrDefault(
        x => x.ChainChannel == args.Channel.Id
             && x.Guild == args.Guild.Id);
    if (entry == null) return;
    var failed = false;
    try {
        await args.Guild.BanMemberAsync(args.Message.Author.Id,
            0, "You broke the chain: Sabotage (deleting)");
    } catch {
        failed = true;
    }

    if (failed) return;
    entry.Infractions++;
    var embed = new DiscordEmbedBuilder()
        .WithColor(DiscordColor.Azure)
        .WithTitle("Someone broke the chain and got sniped!")
        .AddField("Perpetrator", args.Message.Author.Username, true)
        .AddField("Reason", "Sabotage (deleting)", true)
        .AddField("Message", args.Message.Content, true)
        .AddField("Expected", "Come on man!", true)
        .AddField("Bruh moment", "That's really stupid", true)
        .AddField("Infractions", $"{entry.Infractions} total", true);
    var builder = new DiscordMessageBuilder()
        .WithEmbed(embed.Build());
    var channel = args.Guild.GetChannel(entry.LogChannel);
    await builder.SendAsync(channel);
};

Log.Information("[DisControl] Initialization finished successfully");
await Task.Delay(-1);
