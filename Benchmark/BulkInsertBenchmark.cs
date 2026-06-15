using BenchmarkDotNet.Attributes;

namespace Benchmark;

[MemoryDiagnoser]
public class BulkInsertBenchmark
{
	private static readonly BenchmarkItem[] array = InsertionHelper.GenerateItems().ToArray();

	[Benchmark(Baseline = true)]
	public void BulkInsert_SqliteNet()
	{
		using var connection = InsertionHelper.Insert_SqliteNet(array);
	}

	[Benchmark]
	public void BulkInsert_FourLambda()
	{
		using var connection = InsertionHelper.Insert_FourLambda(array);
	}

	[Benchmark]
	public void BulkInsert_Microsoft()
	{
		using var connection = InsertionHelper.Insert_Microsoft(array);
	}
}


public class BenchmarkItem
{
	[SQLite.PrimaryKey, FourLambda.SQLite.PrimaryKey]
	public int ID { get; set; }

	public int Value { get; set; }
	public string StringValue { get; set; }
	public TestEnum EnumValue { get; set; }
}

public enum TestEnum
{
	Item1,
	Item2,
	Item3
}

internal static class InsertionHelper
{
	public static IEnumerable<BenchmarkItem> GenerateItems()
	{
		const int itemCount = 1_000_000;

		var random = new Random(12345);

		return Enumerable.Range(0, itemCount)
			.Select(i => new BenchmarkItem
			{
				ID = i + 1,
				Value = random.Next(),
				EnumValue = (TestEnum)random.Next(3),
				StringValue = random.Next().ToString()
			});
	}

	public static SQLite.SQLiteConnection Insert_SqliteNet(IEnumerable<BenchmarkItem>? items = null)
	{
		SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

		var connection = new SQLite.SQLiteConnection(":memory:");
		connection.CreateTable<BenchmarkItem>();
		connection.InsertAll(GenerateItems());

		return connection;
	}

	public static FourLambda.SQLite.SQLiteConnection Insert_FourLambda(IEnumerable<BenchmarkItem>? items = null)
	{
		var connection = new FourLambda.SQLite.SQLiteConnection(":memory:");
		connection.CreateTable<BenchmarkItem>();
		connection.InsertAll(items ?? GenerateItems());

		return connection;
	}

	public static Microsoft.Data.Sqlite.SqliteConnection Insert_Microsoft(IEnumerable<BenchmarkItem>? items = null)
	{
		SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

		var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
		connection.Open();

		using var command = connection.CreateCommand();
		command.CommandText = @"CREATE TABLE IF NOT EXISTS ""BenchmarkItem"" (
""ID"" INTEGER not null,
""Value"" INTEGER not null default 0,
""StringValue"" TEXT not null,
""EnumValue"" INTEGER not null default 0,
PRIMARY KEY (ID)
)";
		command.ExecuteNonQuery();

		using (var bulkCopy = connection.CreateCommand())
		{
			bulkCopy.CommandText = @"INSERT INTO ""BenchmarkItem"" (
ID,
Value,
StringValue,
EnumValue) VALUES ($aaaa, $bbbb, $cccc, $dddd)";

			var idParam = bulkCopy.CreateParameter();
			idParam.ParameterName = "$aaaa";
			bulkCopy.Parameters.Add(idParam);

			var valueParam = bulkCopy.CreateParameter();
			valueParam.ParameterName = "$bbbb";
			bulkCopy.Parameters.Add(valueParam);

			var stringParam = bulkCopy.CreateParameter();
			stringParam.ParameterName = "$cccc";
			bulkCopy.Parameters.Add(stringParam);

			var enumParam = bulkCopy.CreateParameter();
			enumParam.ParameterName = "$dddd";
			bulkCopy.Parameters.Add(enumParam);

			bulkCopy.Prepare();

			foreach (var item in GenerateItems())
			{
				idParam.Value = item.ID;
				valueParam.Value = item.Value;
				stringParam.Value = item.StringValue;
				enumParam.Value = (int)item.EnumValue;

				bulkCopy.ExecuteNonQuery();
			}
		}

		return connection;
	}
}