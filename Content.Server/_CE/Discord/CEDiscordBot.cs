using Content.Server.Discord.DiscordLink;
using NetCord.Gateway;
using NetCord.Rest;

namespace Content.Server._CE.Discord;

public sealed class CEDiscordBot : IPostInjectInit
{
    [Dependency] private readonly DiscordLink _discordLink = default!;

    public void Initialize()
    {
        _discordLink.OnMessageReceived += AutoReactChivapchichi;
    }

    public void Shutdown()
    {
        _discordLink.OnMessageReceived -= AutoReactChivapchichi;
    }

    private async void AutoReactChivapchichi(Message message) //  >:)
    {
        if (message.Author.Id != 1049902621630136351)
            return;

        await _discordLink.AddReactionAsync(
            message.ChannelId,
            message.Id,
            new ReactionEmojiProperties("chivapchichi", 1483077159995314226));
    }

    void IPostInjectInit.PostInject()
    {
    }
}
