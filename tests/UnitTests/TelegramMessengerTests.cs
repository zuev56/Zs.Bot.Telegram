using System.Threading.Tasks;
using Moq;
using Telegram.Bot;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;
using Zs.Bot.Services.Messaging;

namespace UnitTests;

public sealed class TelegramMessengerTests
{
    private const int DbEntitiesAmount = 100;
    private const int DbChatIdForMessages = 1;

    [Fact]
    public void AddMessageToOutbox_CorrectParameters_ReturnsTrue()
    {
        var telegramMessenger = GetTelegramMessenger();
        var chat = StubFactory.CreateChat();

        var isSuccess = telegramMessenger.AddMessageToOutbox(chat, "Message text");

        Assert.True(isSuccess);
    }

    private static IMessenger GetTelegramMessenger()
    {
        var postgreSqlInMemory = new PostgreSqlInMemory();
        postgreSqlInMemory.FillWithFakeData(DbEntitiesAmount);

        return new TelegramMessenger(
            Mock.Of<ITelegramBotClient>(),
            postgreSqlInMemory.ChatsRepository,
            postgreSqlInMemory.UsersRepository,
            postgreSqlInMemory.MessagesRepository);
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(false, null)]
    [InlineData(false, "MessageText")]
    public void AddMessageToOutbox_WrongParameters_ReturnsFalse(bool hasChat, string messageText)
    {
        var telegramMessenger = GetTelegramMessenger();
        var chat = hasChat ? StubFactory.CreateChat() : null;

        var isSuccess = telegramMessenger.AddMessageToOutbox(chat, messageText);

        Assert.False(isSuccess);
    }

    [Theory]
    [InlineData(new[] { Role.Admin })]
    [InlineData(new[] { Role.Admin, Role.Owner })]
    public async Task AddMessageToOutboxAsync_CorrectParameters_ReturnsTrue(Role[] roles)
    {
        var telegramMessenger = GetTelegramMessenger();

        var isSuccess = await telegramMessenger.AddMessageToOutboxAsync("Message text", roles);

        Assert.True(isSuccess);
    }


    [Theory]
    [InlineData("", null)]
    [InlineData("", new Role[] { })]
    [InlineData("", new[] { Role.Admin })]
    [InlineData("", new[] { Role.Admin, Role.Owner })]
    [InlineData("MessageText", new Role[] { })]
    [InlineData("MessageText", null)]
    public async Task AddMessageToOutboxAsync_WrongParameters_ReturnsFalse(string messageText, Role[] roles)
    {
        var telegramMessenger = GetTelegramMessenger();

        var isSuccess = await telegramMessenger.AddMessageToOutboxAsync(messageText, roles);

        Assert.False(isSuccess);
    }

    [Fact]
    public async Task DeleteMessageAsync_CorrectMessage_ReturnsTrue()
    {
        var telegramMessenger = GetTelegramMessenger();
        var message = StubFactory.CreateMessage(1, 1);

        var isSuccess = await telegramMessenger.DeleteMessageAsync(message);

        Assert.True(isSuccess);
    }

    [Fact]
    public async Task DeleteMessageAsync_NullableMessage_ReturnsFalse()
    {
        var telegramMessenger = GetTelegramMessenger();

        var isSuccess = await telegramMessenger.DeleteMessageAsync(null!);

        Assert.False(isSuccess);
    }
}