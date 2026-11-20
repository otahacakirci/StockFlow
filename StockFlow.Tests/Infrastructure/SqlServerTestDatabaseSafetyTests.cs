namespace StockFlow.Tests.Infrastructure;

public sealed class SqlServerTestDatabaseSafetyTests
{
    private const string ValidTestDatabaseName = "StockFlow_Tests_0123456789abcdef0123456789abcdef";
    private const string TestContainerDataSource = "127.0.0.1,14330";

    [Fact]
    public void GenerateDatabaseName_WhenCalledTwice_ReturnsUniqueSafeNames()
    {
        var firstName = SqlServerTestDatabase.GenerateDatabaseName();
        var secondName = SqlServerTestDatabase.GenerateDatabaseName();

        Assert.NotEqual(firstName, secondName);
        AssertValidDatabaseName(firstName);
        AssertValidDatabaseName(secondName);
    }

    [Fact]
    public void ValidateTarget_WhenDevelopmentDatabaseIsProvided_RejectsTarget()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestDatabase.ValidateTarget(
                TestContainerDataSource,
                "StockFlow",
                TestContainerDataSource));

        Assert.Contains("benzersiz StockFlow test veritabanlarında", exception.Message);
    }

    [Theory]
    [InlineData(@"(localdb)\MSSQLLocalDB")]
    [InlineData("sql.example.invalid")]
    [InlineData("127.0.0.1,14331")]
    public void ValidateTarget_WhenSourceIsNotTestContainer_RejectsTarget(string dataSource)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestDatabase.ValidateTarget(
                dataSource,
                ValidTestDatabaseName,
                TestContainerDataSource));

        Assert.Contains(
            "yalnızca bu test koşusunun SQL Server test konteynerinde",
            exception.Message);
    }

    [Fact]
    public void ValidateTarget_WhenSourceIsTestContainer_AcceptsTarget()
    {
        var exception = Record.Exception(() =>
            SqlServerTestDatabase.ValidateTarget(
                TestContainerDataSource,
                ValidTestDatabaseName,
                TestContainerDataSource));

        Assert.Null(exception);
    }

    [Fact]
    public void CreateStartupFailure_WrapsInnerExceptionWithEnvironmentErrorMessage()
    {
        var innerException = new TimeoutException("Docker daemon yanıt vermedi.");

        var exception = MsSqlTestServer.CreateStartupFailure(innerException);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Same(innerException, exception.InnerException);
        Assert.Contains("Docker", exception.Message);
        Assert.Contains("ortam hatası", exception.Message);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(innerException.Message, exception.Message);
    }

    [Fact]
    public async Task ConnectionString_WhenNotInitialized_Throws()
    {
        await using var testDatabase = SqlServerTestDatabase.Create();

        var exception = Assert.Throws<InvalidOperationException>(() => testDatabase.ConnectionString);

        Assert.Contains("Test veritabanı başlatılmadı.", exception.Message);
    }

    private static void AssertValidDatabaseName(string databaseName)
    {
        Assert.StartsWith(SqlServerTestDatabase.DatabaseNamePrefix, databaseName);
        Assert.True(Guid.TryParseExact(
            databaseName[SqlServerTestDatabase.DatabaseNamePrefix.Length..],
            "N",
            out _));
    }
}
