using System.Linq.Expressions;
using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Integration.Containers;

[TestFixture]
[Category("Docker")]
[NonParallelizable]
public sealed class DockerLambdaTranslatorMatrixTests
{
    [SetUp]
    public void RequireDocker()
    {
        DatabaseContainers.RequireDocker();
    }

    [Test]
    public Task MySql_executes_the_lambda_matrix() =>
        VerifyLambdaMatrix<MySqlDevice>(CreateDevice<MySqlDevice>);

    [Test]
    public Task PostgreSql_executes_the_lambda_matrix() =>
        VerifyLambdaMatrix<PostgreSqlDevice>(CreateDevice<PostgreSqlDevice>);

    [Test]
    public Task SqlServer_executes_the_lambda_matrix() =>
        VerifyLambdaMatrix<MsSqlDevice>(CreateDevice<MsSqlDevice>);

    [Test]
    public Task MySql_translates_date_components() =>
        VerifyDateComponents<MySqlDevice>(CreateDevice<MySqlDevice>);

    [Test]
    public Task PostgreSql_translates_date_components() =>
        VerifyDateComponents<PostgreSqlDevice>(CreateDevice<PostgreSqlDevice>);

    [Test]
    public Task SqlServer_translates_date_components() =>
        VerifyDateComponents<MsSqlDevice>(CreateDevice<MsSqlDevice>);

    [Test]
    public Task MySql_escapes_quotes_in_string_constants() =>
        VerifyQuotedStrings<MySqlDevice>(CreateDevice<MySqlDevice>);

    [Test]
    public Task PostgreSql_escapes_quotes_in_string_constants() =>
        VerifyQuotedStrings<PostgreSqlDevice>(CreateDevice<PostgreSqlDevice>);

    [Test]
    public Task SqlServer_escapes_quotes_in_string_constants() =>
        VerifyQuotedStrings<MsSqlDevice>(CreateDevice<MsSqlDevice>);

    private static async Task VerifyLambdaMatrix<T>(Func<string, string, int, bool, string?, DateTime, T> create)
        where T : class, IStorable, IContainerDevice
    {
        var manager = GenericDM.Get<T>();
        var cleanup = await manager.CreateDelete<T>()
            .Where(item => item.Id > 0)
            .RunWithError();
        AssertSuccess(cleanup, "cleanup");

        var oldDate = new DateTime(2020, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var recentDate = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var createResult = await manager.CreateWithError(new List<T>
        {
            create("Desk Lamp", "Office", 10, true, null, oldDate),
            create("Ceiling Lamp", "Office", 30, true, "main", recentDate),
            create("Desk Sensor", "Kitchen", 20, false, "sensor", recentDate),
            create("Outdoor Light", "Garden", 40, true, null, recentDate)
        });
        AssertSuccess(createResult, "seed");

        var minimum = 20;
        var room = "Office";
        var captured = await Query<T>(manager,
            item => item.Room == room && item.Value >= minimum && item.Enabled);
        AssertNames(captured, "captured variables", "Ceiling Lamp");

        var startsWith = await Query<T>(manager, item => item.Name.StartsWith("Desk"));
        AssertNames(startsWith, "StartsWith", "Desk Lamp", "Desk Sensor");

        var endsWith = await Query<T>(manager, item => item.Name.EndsWith("Lamp"));
        AssertNames(endsWith, "EndsWith", "Desk Lamp", "Ceiling Lamp");

        var lower = await Query<T>(manager, item => item.Name.ToLower() == "desk lamp");
        AssertNames(lower, "ToLower", "Desk Lamp");

        var rooms = new List<string> { "Office", "Garden" };
        var collection = await Query<T>(manager, item => rooms.Contains(item.Room));
        AssertNames(collection, "collection Contains", "Desk Lamp", "Ceiling Lamp", "Outdoor Light");

        var nulls = await Query<T>(manager, item => item.Description == null);
        AssertNames(nulls, "null", "Desk Lamp", "Outdoor Light");

        var nonNulls = await Query<T>(manager, item => item.Description != null);
        AssertNames(nonNulls, "not null", "Ceiling Lamp", "Desk Sensor");

        var threshold = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dates = await Query<T>(manager, item => item.InstalledAt >= threshold);
        AssertNames(dates, "DateTime comparison", "Ceiling Lamp", "Desk Sensor", "Outdoor Light");

        var booleanLogic = await Query<T>(manager,
            item => (item.Room == "Office" || item.Value >= 40) && item.Enabled);
        AssertNames(booleanLogic, "AND/OR/NOT", "Desk Lamp", "Ceiling Lamp", "Outdoor Light");

        var negation = await Query<T>(manager, item => !item.Enabled);
        AssertNames(negation, "boolean negation", "Desk Sensor");

        var page = await manager.CreateQuery<T>()
            .Where(item => item.Value >= 10)
            .Sort(item => item.Value, Sort.DESC)
            .Limit(2)
            .Offset(1)
            .RunWithError();
        AssertSuccess(page, "sort/limit/offset");
        Assert.That(page.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Ceiling Lamp", "Desk Sensor" }));

        var groupedRooms = await manager.CreateQuery<T>()
            .Field(item => item.Room)
            .Group(item => item.Room)
            .Sort(item => item.Room, Sort.ASC)
            .RunWithError();
        AssertSuccess(groupedRooms, "group projection");
        Assert.That(groupedRooms.Result!.Select(item => item.Room),
            Is.EqualTo(new[] { "Garden", "Kitchen", "Office" }));

        var preparedRooms = new List<string>();
        var preparedCollection = manager.CreateQuery<T>()
            .WhereWithParameters(item => preparedRooms.Contains(item.Room));
        var preparedNonEmpty = await preparedCollection.New()
            .Prepare(new List<string> { "Office" })
            .RunWithError();
        AssertSuccess(preparedNonEmpty, "prepared collection");
        AssertNames(preparedNonEmpty, "prepared collection", "Desk Lamp", "Ceiling Lamp");

        var preparedEmpty = await preparedCollection.New()
            .Prepare(new List<string>())
            .RunWithError();
        AssertSuccess(preparedEmpty, "empty prepared collection");
        Assert.That(preparedEmpty.Result, Is.Empty);
    }

    private static T CreateDevice<T>(
        string name,
        string room,
        int value,
        bool enabled,
        string? description,
        DateTime installedAt)
        where T : class, IContainerDevice, new()
    {
        return new T
        {
            Name = name,
            Room = room,
            Value = value,
            Enabled = enabled,
            Description = description,
            InstalledAt = installedAt
        };
    }

    private static async Task VerifyDateComponents<T>(
        Func<string, string, int, bool, string?, DateTime, T> create)
        where T : class, IStorable, IContainerDevice
    {
        var manager = GenericDM.Get<T>();
        var cleanup = await manager.CreateDelete<T>()
            .Where(item => item.Id > 0)
            .RunWithError();
        AssertSuccess(cleanup, "cleanup");

        var installedAt = new DateTime(
            2026, 7, 25, 14, 35, 42, DateTimeKind.Utc);
        var creation = await manager.CreateWithError(create(
            "Temporal",
            "Lab",
            1,
            true,
            null,
            installedAt));
        AssertSuccess(creation, "seed");

        DateTime expected = DataMainManager.Config.DateTimeStorageMode ==
            DateTimeStorageMode.Utc
                ? installedAt
                : installedAt.ToLocalTime();
        int expectedYear = expected.Year;
        int expectedMonth = expected.Month;
        int expectedDay = expected.Day;
        int expectedHour = expected.Hour;
        int expectedMinute = expected.Minute;
        int expectedSecond = expected.Second;

        var result = await Query<T>(manager,
            item => item.InstalledAt.Year == expectedYear
                && item.InstalledAt.Month == expectedMonth
                && item.InstalledAt.Day == expectedDay
                && item.InstalledAt.Hour == expectedHour
                && item.InstalledAt.Minute == expectedMinute
                && item.InstalledAt.Second == expectedSecond);

        AssertNames(result, "date components", "Temporal");
    }

    private static async Task VerifyQuotedStrings<T>(
        Func<string, string, int, bool, string?, DateTime, T> create)
        where T : class, IStorable, IContainerDevice
    {
        var manager = GenericDM.Get<T>();
        var cleanup = await manager.CreateDelete<T>()
            .Where(item => item.Id > 0)
            .RunWithError();
        AssertSuccess(cleanup, "cleanup");

        var creation = await manager.CreateWithError(create(
            "O'Reilly lamp",
            "Owner's office",
            1,
            true,
            null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        AssertSuccess(creation, "seed");

        var equality = await Query<T>(manager, item => item.Name == "O'Reilly lamp");
        var startsWith = await Query<T>(manager, item => item.Room.StartsWith("Owner's"));

        AssertNames(equality, "quoted equality", "O'Reilly lamp");
        AssertNames(startsWith, "quoted StartsWith", "O'Reilly lamp");
    }

    private static Task<ResultWithError<List<T>>> Query<T>(
        IGenericDM manager,
        Expression<Func<T, bool>> expression)
        where T : class, IStorable =>
        manager.CreateQuery<T>().Where(expression).RunWithError();

    private static void AssertNames<T>(
        ResultWithError<List<T>> result,
        string operation,
        params string[] expected)
        where T : IContainerDevice
    {
        AssertSuccess(result, operation);
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EquivalentTo(expected), operation);
    }

    private static void AssertSuccess<TError>(IWithError<TError> result, string operation)
        where TError : GenericError
    {
        Assert.That(result.Success, Is.True,
            $"{operation}:{Environment.NewLine}"
            + string.Join(Environment.NewLine,
                result.Errors.Select(error => error.GetMessageException(true))));
    }
}
