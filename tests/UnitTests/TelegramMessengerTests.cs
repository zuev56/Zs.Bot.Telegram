using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;
using Zs.Bot.Services.Messaging;

namespace UnitTests
{
    public class TelegramMessengerTests
    {
        private const int _dbEntitiesAmount = 100;
        private const int _dbChatIdForMessages = 1;

        [Fact]
        public void AddMessageToOutbox_CorrectParameters_ReturnsTrue()
        {
            // Arrange
            var telegramMessenger = GetTelegramMessenger();
            var chat = StubFactory.CreateChat();

            // Act
            var isSuccess = telegramMessenger.AddMessageToOutbox(chat, "Message text");

            // Assert
            Assert.True(isSuccess);
        }

        private IMessenger GetTelegramMessenger()
        {
            var postgreSqlInMemory = new PostgreSqlInMemory();
            postgreSqlInMemory.FillWithFakeData(_dbEntitiesAmount, _dbChatIdForMessages);

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
            // Arrange
            var telegramMessenger = GetTelegramMessenger();
            var chat = hasChat ? StubFactory.CreateChat() : null;

            // Act
            var isSuccess = telegramMessenger.AddMessageToOutbox(chat, messageText);

            // Assert
            Assert.False(isSuccess);
        }

        [Theory]
        [InlineData(Role.Admin)]
        [InlineData(new[] { Role.Admin, Role.Owner })]
        public async Task AddMessageToOutboxAsync_CorrectParameters_ReturnsTrue(params Role[] roles)
        {
            // Arrange
            var telegramMessenger = GetTelegramMessenger();

            // Act
            var isSuccess = await telegramMessenger.AddMessageToOutboxAsync("Message text", roles);

            // Assert
            Assert.True(isSuccess);
        }


        [Theory]
        [InlineData("", null)]
        [InlineData("", new Role[] { })]
        [InlineData("", Role.Admin)]
        [InlineData("", new[] { Role.Admin, Role.Owner })]
        [InlineData("MessageText", new Role[] { })]
        [InlineData("MessageText", null)]
        public async Task AddMessageToOutboxAsync_WrongParameters_ReturnsFalse(string messageText, params Role[] roles)
        {
            // Arrange
            var telegramMessenger = GetTelegramMessenger();

            // Act
            var isSuccess = await telegramMessenger.AddMessageToOutboxAsync(messageText, roles);

            // Assert
            Assert.False(isSuccess);
        }

        [Fact]
        public async Task DeleteMessageAsync_CorrectMessage_ReturnsTrue()
        {
            // Arrange
            var telegramMessenger = GetTelegramMessenger();
            var message = StubFactory.CreateMessage(1, 1);

            // Act
            var isSuccess = await telegramMessenger.DeleteMessageAsync(message);

            // Assert
            Assert.True(isSuccess);
        }

        [Fact]
        public async Task DeleteMessageAsync_NullableMessage_ReturnsFalse()
        {
            // Arrange
            var telegramMessenger = GetTelegramMessenger();

            // Act
            var isSuccess = await telegramMessenger.DeleteMessageAsync(null);

            // Assert
            Assert.False(isSuccess);
        }
    }
}
