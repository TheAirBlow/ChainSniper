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

var ignore = new List<ulong>();
var dict = new Dictionary<ulong, IEnumerable<ulong>>();
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
        entry.Infractions++; ignore.Add(args.Message.Id);
        await args.Message.DeleteAsync();
        var failed = false;
        try {
            await args.Guild.BanMemberAsync(args.Author.Id,
                0, $"You broke the chain: {reason}");
            var member = await args.Guild.GetMemberAsync(args.Author.Id);
            dict.Add(args.Author.Id, member.Roles.Select(x => x.Id));
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
            .AddField("Bruh moment", "That's really stupid", true)
            .AddField("Infractions", $"{entry.Infractions} total", true);
        if (!failed)
            embed.WithFooter("If a staff member thinks it's unfair, click the button below, roles will be given back");
        var builder = new DiscordMessageBuilder()
            .WithEmbed(embed.Build());
        if (!failed)
            builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary,
                args.Author.Id.ToString(), "Unban member"));
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
        await args.Guild.BanMemberAsync(args.Author.Id,
            0, "You broke the chain: Sabotage (editing)");
    } catch {
        failed = true;
    }

    entry.Infractions++;
    await args.Message.DeleteAsync();
    var embed = new DiscordEmbedBuilder()
        .WithColor(failed ? DiscordColor.Red : DiscordColor.Azure)
        .WithTitle(failed
            ? "Failed to ban a member (no permissions)"
            : "Someone broke the chain and got sniped!")
        .AddField("Perpetrator", args.Author.Username, true)
        .AddField("Reason", "Sabotage (editing)", true)
        .AddField("Message", args.Message.Content, true)
        .AddField("Expected", "Come on man!", true)
        .AddField("Bruh moment", "That's really stupid", true)
        .AddField("Infractions", $"{entry.Infractions} total", true);
    if (!failed)
        embed.WithFooter("If a staff member thinks it's unfair, click the button below, roles will be given back");
    var builder = new DiscordMessageBuilder()
        .WithEmbed(embed.Build());
    if (!failed)
        builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary,
            args.Author.Id.ToString(), "Unban member"));
    var channel = args.Guild.GetChannel(entry.LogChannel);
    await builder.SendAsync(channel);
};

client.MessageDeleted += async (_, args) => {
    if (ignore.Contains(args.Message.Id)) {
        ignore.Remove(args.Message.Id);
        return; // Ignored
    }
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

    entry.Infractions++;
    var embed = new DiscordEmbedBuilder()
        .WithColor(failed ? DiscordColor.Red : DiscordColor.Azure)
        .WithTitle(failed
            ? "Failed to ban a member (no permissions)"
            : "Someone broke the chain and got sniped!")
        .AddField("Perpetrator", args.Message.Author.Username, true)
        .AddField("Reason", "Sabotage (deleting)", true)
        .AddField("Message", args.Message.Content, true)
        .AddField("Expected", "Come on man!", true)
        .AddField("Bruh moment", "That's really stupid", true)
        .AddField("Infractions", $"{entry.Infractions} total", true);
    if (!failed)
        embed.WithFooter("If a staff member thinks it's unfair, click the button below, roles will be given back");
    var builder = new DiscordMessageBuilder()
        .WithEmbed(embed.Build());
    if (!failed)
        builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary,
            args.Message.Author.Id.ToString(), "Unban member"));
    var channel = args.Guild.GetChannel(entry.LogChannel);
    await builder.SendAsync(channel);
};

client.ComponentInteractionCreated += async (_, args) => {
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
    
    Configuration.Instance.Entries.First(
        x => x.LogChannel == args.Channel.Id 
             && x.Guild == args.Guild.Id).Infractions--;
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

Log.Information("[DisControl] Initialization finished successfully");
await Task.Delay(-1);