using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Telegram.Bot;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;

namespace UnitTests;

public sealed class OutputMessageProcessorTests
{
    private const int DbEntitiesAmount = 100;
    private const int ChatIdForMessages = 1;
    private const int FirstItemId = 1;


    // TODO: Проверить выполнение внутренних методов!


    [Theory]
    [InlineData(FirstItemId, "Message text")]
    [InlineData(DbEntitiesAmount, "Message text")]
    public void EnqueueMessage_WithChatId_NotThrowException(int chatId, string messageText)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();

        var action = () => outputMessageProcessor.EnqueueMessage(chatId, messageText);

        action.Should().NotThrow();
    }

    private static IOutputMessageProcessor CreateOutputMessageProcessor()
    {
        var postgreSqlInMemory = new PostgreSqlInMemory();
        postgreSqlInMemory.FillWithFakeData(DbEntitiesAmount);

        return new OutputMessageProcessor(
            Mock.Of<ITelegramBotClient>(),
            postgreSqlInMemory.ChatsRepository,
            postgreSqlInMemory.UsersRepository);
    }

    [Theory]
    [InlineData(FirstItemId - 1, "Message text")]
    [InlineData(DbEntitiesAmount + 1, "Message text")]
    [InlineData(FirstItemId, null)]
    [InlineData(FirstItemId, "")]
    [InlineData(FirstItemId, "  ")]
    public void EnqueueMessage_WithChatId_ThrowArgumentNullException(int chatId, string messageText)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();

        var action = () => outputMessageProcessor.EnqueueMessage(chatId, messageText);

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(FirstItemId, "Message text")]
    [InlineData(DbEntitiesAmount, "Message text")]
    public void EnqueueMessage_WithChat_NotThrowException(int chatId, string messageText)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);

        var action = () => outputMessageProcessor.EnqueueMessage(chat, messageText);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(FirstItemId, null)]
    [InlineData(FirstItemId, "")]
    [InlineData(FirstItemId, "  ")]
    public void EnqueueMessage_WithChat_ThrowArgumentNullException(int chatId, string messageText)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);

        var action = () => outputMessageProcessor.EnqueueMessage(chat, messageText);

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(FirstItemId, "Message text", FirstItemId - 1)]
    [InlineData(FirstItemId, "Message text", FirstItemId)]
    [InlineData(FirstItemId, "Message text", DbEntitiesAmount)]
    [InlineData(FirstItemId, "Message text", DbEntitiesAmount + 1)]
    [InlineData(DbEntitiesAmount, "Message text", 1)]
    public void EnqueueMessage_WithChatAndMessageToReply_NotThrowException(int chatId, string messageText, int messageToReplyId)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);
        var messageToReply = StubFactory.CreateMessage(chatId, messageToReplyId);

        var action = () => outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(FirstItemId, null, FirstItemId)]
    [InlineData(FirstItemId, "", FirstItemId)]
    [InlineData(FirstItemId, "  ", FirstItemId)]
    public void EnqueueMessage_WithChatAndMessageToReply_ThrowArgumentNullException(int chatId, string messageText, int messageToReplyId)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);
        var messageToReply = StubFactory.CreateMessage(chatId, messageToReplyId);

        var action = () => outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Message text", new[] { Role.Admin })]
    [InlineData("Message text", new[] { Role.Owner, Role.Admin })]
    public async Task EnqueueMessage_WithRoles_NotThrowException(string messageText, Role[] roles)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();

        var action = () => outputMessageProcessor.EnqueueMessageAsync(messageText, roles);

        await action.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null, new[] { Role.Admin })]
    [InlineData("", new[] { Role.Admin })]
    [InlineData("  ", new[] { Role.Admin })]
    [InlineData("Message text", null)]
    public async Task EnqueueMessage_WithRoles_ThrowArgumentNullException(string messageText, Role[] roles)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();

        var action = () => outputMessageProcessor.EnqueueMessageAsync(messageText, roles);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Message text", new Role[] { })]
    public async Task EnqueueMessage_WithRoles_ThrowArgumentException(string messageText, Role[] roles)
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();

        var action = () => outputMessageProcessor.EnqueueMessageAsync(messageText, roles);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteMessageAsync_ExistingMessage_ReturnsTrue()
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var message = StubFactory.CreateMessage(1, 1);

        var isSuccess = await outputMessageProcessor.DeleteMessageAsync(message);

        Assert.True(isSuccess);
    }

    [Fact]
    public async Task DeleteMessageAsync_NonexistentChat_ReturnsFalse()
    {
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var message = StubFactory.CreateMessage(9999, 1);

        var isSuccess = await outputMessageProcessor.DeleteMessageAsync(message);

        Assert.False(isSuccess);
    }
}