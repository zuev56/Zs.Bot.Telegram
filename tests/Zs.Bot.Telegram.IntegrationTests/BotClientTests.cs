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
    public async Task Should_SendTextMessageThenReplyToItAndThenDeleteBoth()
    {
        var text = Fixture.Create<string>();
        var chat = Settings.TelegramTestGroupChatId.ToChat();
        var botClient = ServiceProvider.GetRequiredService<IBotClient>();

        // Send text message
        var message = await botClient.SendMessageAsync(text, chat);

        message.Should().NotBeNull();
        var telegramMessage = JsonSerializer.Deserialize<TelegramMessage>(message.RawData);
        telegramMessage.Should().NotBeNull();
        message.Id.Should().Be(telegramMessage!.MessageId);

        // Reply to the message
        var replyText = Fixture.Create<string>();
        var replyMessage = await botClient.SendMessageAsync(replyText, chat, messageToReply: message);

        replyMessage.Should().NotBeNull();
        replyMessage.ReplyToMessageId.Should().Be(message.Id);
        var telegramReplyMessage = JsonSerializer.Deserialize<TelegramMessage>(replyMessage.RawData);
        telegramReplyMessage.Should().NotBeNull();
        replyMessage.Id.Should().Be(telegramReplyMessage!.MessageId);

        // Delete both messages
        var deleteFirstMessageAction = () => botClient.DeleteMessageAsync(message);
        var deleteReplyMessageAction = () => botClient.DeleteMessageAsync(replyMessage);

        await deleteFirstMessageAction.Should().NotThrowAsync();
        await deleteReplyMessageAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetBotInfoAsync_Should()
    {
        var botClient = ServiceProvider.GetRequiredService<IBotClient>();
        var botInfo = await botClient.GetBotInfoAsync();

        botInfo.Should().NotBeNullOrEmpty();
    }
}