using System.Text.Json;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zs.Bot.Services.Messaging;
using Zs.Bot.Telegram.Extensions;
using TelegramMessage = Telegram.Bot.Types.Message;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Zs.Bot.Telegram.IntegrationTests;

public sealed class BotClientTests : TestBase
{
    [Fact]
    public async Task Should_SendTextMessageAndThenDeleteIt()
    {
        var text = Fixture.Create<string>();
        var chat = Settings.TelegramTestGroupChatId.ToChat();
        var botClient = ServiceProvider.GetRequiredService<IBotClient>();

        var message = await botClient.SendMessageAsync(text, chat);

        message.Should().NotBeNull();
        var telegramMessage = JsonSerializer.Deserialize<TelegramMessage>(message.RawData);
        telegramMessage.Should().NotBeNull();

        var deleteAction = () => botClient.DeleteMessageAsync(message);

        await deleteAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetBotInfoAsync_Should()
    {
        var botClient = ServiceProvider.GetRequiredService<IBotClient>();
        var botInfo = await botClient.GetBotInfoAsync();

        botInfo.Should().NotBeNullOrEmpty();
    }
}