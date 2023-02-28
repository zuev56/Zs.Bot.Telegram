using System.Threading.Tasks;
using Moq;
using Telegram.Bot;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;

namespace UnitTests;

public class OutputMessageProcessorTests
{
    private const int _dbEntitiesAmount = 100;
    private const int _chatIdForMessages = 1;
    private const int _fristItemId = 1;


    // TODO: Проверить выполнение внутренних методов!


    [Theory]
    [InlineData(_fristItemId, "Message text")]
    [InlineData(_dbEntitiesAmount, "Message text")]
    public void EnqueueMessage_WithChatId_ReturnsTrue(int chatId, string messageText)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chatId, messageText);

        // Assert
        Assert.True(isSuccess);
    }

    private IOutputMessageProcessor CreateOutputMessageProcessor()
    {
        var postgreSqlInMemory = new PostgreSqlInMemory();
        postgreSqlInMemory.FillWithFakeData(_dbEntitiesAmount, _chatIdForMessages);

        return new OutputMessageProcessor(
            Mock.Of<ITelegramBotClient>(),
            postgreSqlInMemory.ChatsRepository,
            postgreSqlInMemory.UsersRepository,
            postgreSqlInMemory.MessagesRepository);
    }

    [Theory]
    [InlineData(_fristItemId - 1, "Message text")]
    [InlineData(_dbEntitiesAmount + 1, "Message text")]
    [InlineData(_fristItemId, null)]
    [InlineData(_fristItemId, "")]
    [InlineData(_fristItemId, "  ")]
    public void EnqueueMessage_WithChatId_ReturnsFalse(int chatId, string messageText)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chatId, messageText);

        // Assert
        Assert.False(isSuccess);
    }

    [Theory]
    [InlineData(_fristItemId, "Message text")]
    [InlineData(_dbEntitiesAmount, "Message text")]
    public void EnqueueMessage_WithChat_ReturnsTrue(int chatId, string messageText)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chat, messageText);

        // Assert
        Assert.True(isSuccess);
    }

    [Theory]
    [InlineData(_fristItemId, null)]
    [InlineData(_fristItemId, "")]
    [InlineData(_fristItemId, "  ")]
    public void EnqueueMessage_WithChat_ReturnsFalse(int chatId, string messageText)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chat, messageText);

        // Assert
        Assert.False(isSuccess);
    }

    [Theory]
    [InlineData(_fristItemId, "Message text", _fristItemId - 1)]
    [InlineData(_fristItemId, "Message text", _fristItemId)]
    [InlineData(_fristItemId, "Message text", _dbEntitiesAmount)]
    [InlineData(_fristItemId, "Message text", _dbEntitiesAmount + 1)]
    [InlineData(_dbEntitiesAmount, "Message text", 1)]
    public void EnqueueMessage_WithChatAndMessageToReply_ReturnsTrue(int chatId, string messageText, int messageToReplyId)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);
        var messageToReply = StubFactory.CreateMessage(chatId, messageToReplyId);

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);

        // Assert
        Assert.True(isSuccess);
    }

    [Theory]
    [InlineData(_fristItemId, null, _fristItemId)]
    [InlineData(_fristItemId, "", _fristItemId)]
    [InlineData(_fristItemId, "  ", _fristItemId)]
    public void EnqueueMessage_WithChatAndMessageToReply_ReturnsFalse(int chatId, string messageText, int messageToReplyId)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var chat = StubFactory.CreateChat(chatId);
        var messageToReply = StubFactory.CreateMessage(chatId, messageToReplyId);

        // Act
        bool isSuccess = outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);

        // Assert
        Assert.False(isSuccess);
    }

    [Theory]
    [InlineData("Message text", new[] { Role.Admin })]
    [InlineData("Message text", new[] { Role.Owner, Role.Admin })]
    public async Task EnqueueMessage_WithRoles_ReturnsTrue(string messageText, Role[] roles)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();

        // Act
        bool isSuccess = await outputMessageProcessor.EnqueueMessageAsync(messageText, roles);

        // Assert
        Assert.True(isSuccess);
    }

    [Theory]
    [InlineData(null, new[] { Role.Admin })]
    [InlineData("", new[] { Role.Admin })]
    [InlineData("  ", new[] { Role.Admin })]
    [InlineData("Message text", new Role[] { })]
    [InlineData("Message text", null)]
    public async Task EnqueueMessage_WithRoles_ReturnsFalse(string messageText, Role[] roles)
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();

        // Act
        bool isSuccess = await outputMessageProcessor.EnqueueMessageAsync(messageText, roles);

        // Assert
        Assert.False(isSuccess);
    }

    [Fact]
    public async Task DeleteMessageAsync_ExistingMessage_ReturnsTrue()
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var message = StubFactory.CreateMessage(1, 1);

        // Act
        bool isSuccess = await outputMessageProcessor.DeleteMessageAsync(message);

        // Assert
        Assert.True(isSuccess);
    }

    [Fact]
    public async Task DeleteMessageAsync_NonexistentChat_ReturnsFalse()
    {
        // Arrange
        var outputMessageProcessor = CreateOutputMessageProcessor();
        var message = StubFactory.CreateMessage(9999, 1);

        // Act
        bool isSuccess = await outputMessageProcessor.DeleteMessageAsync(message);

        // Assert
        Assert.False(isSuccess);
    }
}