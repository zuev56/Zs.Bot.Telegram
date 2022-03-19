using UnitTests.Data;
using Xunit;
using Zs.Bot.Data.Enums;
using Zs.Bot.Messenger.Telegram;

namespace UnitTests
{
    public class InputMessageProcessorTests
    {
        private const int _dbEntitiesAmount = 100;
        private const int _dbChatIdForMessages = 1;

        [Fact]
        public void EnqueueMessage_CorrectMessage_returnsTrue()
        {
            // Arrange
            var inputMessageProcessor = CreateIntputMessageProcessor();
            var message = StubFactory.CreateTelegramMessage("Message text");
            var tgMessage = new TgMessage(message);

            // Act
            var isSuccess = inputMessageProcessor.EnqueueMessage(tgMessage, out var messageActionEventArgs);

            // Assert
            Assert.True(isSuccess);
        }

        [Fact]
        public void EnqueueMessage_CorrectMessage_returnsCorrectEventArgs()
        {
            // Arrange
            var inputMessageProcessor = CreateIntputMessageProcessor();
            var message = StubFactory.CreateTelegramMessage("Message text");
            var tgMessage = new TgMessage(message);

            // Act
            inputMessageProcessor.EnqueueMessage(tgMessage, out var messageActionEventArgs);

            // Assert
            Assert.Equal(MessageAction.Received, messageActionEventArgs.Action);
            Assert.NotNull(messageActionEventArgs.Chat);
            Assert.NotNull(messageActionEventArgs.User);
            Assert.NotNull(messageActionEventArgs.Message);
            Assert.False(messageActionEventArgs.IsHandled);
        }

        [Fact]
        public void EnqueueMessage_NullableMessage_returnsFalse()
        {
            // Arrange
            var inputMessageProcessor = CreateIntputMessageProcessor();

            // Act
            var isSuccess = inputMessageProcessor.EnqueueMessage(tgMessage: null, out var _);

            // Assert
            Assert.False(isSuccess);
        }

        [Fact]
        public void EnqueueMessage_NullableMessage_returnsNullableEventArgs()
        {
            // Arrange
            var inputMessageProcessor = CreateIntputMessageProcessor();

            // Act
            inputMessageProcessor.EnqueueMessage(tgMessage: null, out var messageActionEventArgs);

            // Assert
            Assert.Null(messageActionEventArgs);
        }



        private IInputMessageProcessor CreateIntputMessageProcessor()
        {
            var postgreSqlInMemory = new PostgreSqlInMemory();
            postgreSqlInMemory.FillWithFakeData(_dbEntitiesAmount, _dbChatIdForMessages);

            return new InputMessageProcessor(
                postgreSqlInMemory.ChatsRepository,
                postgreSqlInMemory.UsersRepository,
                postgreSqlInMemory.MessagesRepository);
        }
    }
}
