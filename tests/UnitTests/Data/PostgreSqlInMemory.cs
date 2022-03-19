using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zs.Bot.Data.PostgreSQL;
using Zs.Bot.Data.PostgreSQL.Repositories;

namespace UnitTests.Data;

public class PostgreSqlInMemory
{
    public ChatsRepository<PostgreSqlBotContext> ChatsRepository { get; }
    public UsersRepository<PostgreSqlBotContext> UsersRepository { get; }
    public MessagesRepository<PostgreSqlBotContext> MessagesRepository { get; }

    public PostgreSqlInMemory()
    {
        var dbContextFactory = GetPostgreSqlBotContextFactory();

        ChatsRepository = new ChatsRepository<PostgreSqlBotContext>(dbContextFactory);
        UsersRepository = new UsersRepository<PostgreSqlBotContext>(dbContextFactory);
        MessagesRepository = new MessagesRepository<PostgreSqlBotContext>(dbContextFactory);
    }

    private PostgreSqlBotContextFactory GetPostgreSqlBotContextFactory()
    {
        var dbName = $"PostgreSQLInMemoryDB_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<PostgreSqlBotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new PostgreSqlBotContextFactory(options);
    }

    public void FillWithFakeData(int entitiesCount, int chatIdForMessages = 1)
    {
        var chat = StubFactory.CreateChats(entitiesCount);
        var messages = StubFactory.CreateMessages(chatIdForMessages, entitiesCount);
        var users = StubFactory.CreateUsers(entitiesCount);


        Task.WaitAll(new Task[]
        {
            ChatsRepository.SaveRangeAsync(chat),
            UsersRepository.SaveRangeAsync(users),
            MessagesRepository.SaveRangeAsync(messages)
        });
    }
}