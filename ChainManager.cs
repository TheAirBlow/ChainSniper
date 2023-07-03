using DSharpPlus;
using Serilog;

namespace DisControl; 

public static class ChainManager {
    public static Dictionary<ulong, ulong> ChainNumber = new();

    public static async Task Initialize(DiscordClient client) {
        foreach (var i in Configuration.Instance.Entries) {
            var guild = await client.GetGuildAsync(i.Guild);
            var channel = guild.GetChannel(i.ChainChannel);
            var message = (await channel.GetMessagesAsync(1))[0];
            if (!ulong.TryParse(message.Content, out var num)) {
                Log.Fatal("[ChainManager] {0} {1}'s last message is not a number!",
                    guild.Name, channel.Name);
                Environment.Exit(0);
            }
            
            Log.Information("[ChainManager] {0} {1}'s number is {2}",
                guild.Name, channel.Name, num);
            ChainNumber.Add(i.ChainChannel, num);
        }
    } 
}