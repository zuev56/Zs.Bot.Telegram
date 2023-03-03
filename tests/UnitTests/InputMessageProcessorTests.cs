using System;
using FluentAssertions;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;

namespace UnitTests;

public sealed class InputMessageProcessorTests
{
    private const int DbEntitiesAmount = 100;
    private const int DbChatIdForMessages = 1;

    [Fact]
    public void EnqueueMessage_CorrectMessage_notThrowException()
    {
        var inputMessageProcessor = CreateInputMessageProcessor();
        var message = StubFactory.CreateTelegramMessage("Message text");
        var tgMessage = new TgMessage(message);

        var action = () => inputMessageProcessor.EnqueueMessage(tgMessage, out _);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnqueueMessage_CorrectMessage_returnsCorrectEventArgs()
    {
        var inputMessageProcessor = CreateInputMessageProcessor();
        var message = StubFactory.CreateTelegramMessage("Message text");
        var tgMessage = new TgMessage(message);

        inputMessageProcessor.EnqueueMessage(tgMessage, out var messageActionEventArgs);

        Assert.Equal(MessageAction.Received, messageActionEventArgs.Action);
        Assert.NotNull(messageActionEventArgs.Chat);
        Assert.NotNull(messageActionEventArgs.User);
        Assert.NotNull(messageActionEventArgs.Message);
        Assert.False(messageActionEventArgs.IsHandled);
    }

    [Fact]
    public void EnqueueMessage_NullableMessage_throwArgumentNullException()
    {
        var inputMessageProcessor = CreateInputMessageProcessor();

        var action = () => inputMessageProcessor.EnqueueMessage(tgMessage: null!, out _);

        action.Should().Throw<ArgumentNullException>();
    }

    private static IInputMessageProcessor CreateInputMessageProcessor()
    {
        var postgreSqlInMemory = new PostgreSqlInMemory();
        postgreSqlInMemory.FillWithFakeData(DbEntitiesAmount);

        return new InputMessageProcessor(
            postgreSqlInMemory.ChatsRepository,
            postgreSqlInMemory.UsersRepository,
            postgreSqlInMemory.MessagesRepository);
    }
}