namespace StockFlow.Tests.Infrastructure;

public sealed class SqlServerTestDatabaseSafetyTests
{
    private const string ValidTestDatabaseName = "StockFlow_Tests_0123456789abcdef0123456789abcdef";

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
                SqlServerTestDatabase.LocalDbDataSource,
                "StockFlow"));

        Assert.Contains("benzersiz StockFlow test veritabanlarında", exception.Message);
    }

    [Theory]
    [InlineData(@"(localdb)\StockFlowTests")]
    [InlineData("sql.example.invalid")]
    public void ValidateTarget_WhenNonDefaultLocalDbSourceIsProvided_RejectsTarget(string dataSource)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestDatabase.ValidateTarget(dataSource, ValidTestDatabaseName));

        Assert.Contains("sabit SQL Server LocalDB", exception.Message);
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
