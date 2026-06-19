using BenchmarkDotNet.Attributes;

namespace Benchmark;

[MemoryDiagnoser]
public class QueryAllBenchmarks
{
	public SQLite.SQLiteConnection SqliteNetConnection;
	public FourLambda.SQLite.SQLiteConnection LambdaSqliteConnection;
	public Microsoft.Data.Sqlite.SqliteConnection MicrosoftSqliteConnection;

	[GlobalSetup(Targets = [nameof(QueryAll_SqliteNet), nameof(QueryAllAsValueTuple_SqliteNet)])]
	public void GlobalSetup_SqliteNet()
	{
		Console.WriteLine("Inserting into SQLite.SQLiteConnection");
		SqliteNetConnection = InsertionHelper.Insert_SqliteNet();
	}

	[GlobalSetup(Targets = [nameof(QueryAll_FourLambda), nameof(QueryAllAsValueTuple_FourLambda), nameof(QueryAllReader_FourLambda), nameof(QueryAllReaderSkipping_FourLambda)])]
	public void GlobalSetup_FourLambda()
	{
		Console.WriteLine("Inserting into FourLambda.SQLite.SQLiteConnection");
		LambdaSqliteConnection = InsertionHelper.Insert_FourLambda();
	}

	[GlobalSetup(Targets = [nameof(QueryAllReader_Microsoft), nameof(QueryAllReaderSkipping_Microsoft)])]
	public void GlobalSetup_Microsoft()
	{
		Console.WriteLine("Inserting into Microsoft.Data.Sqlite.SqliteConnection");
		MicrosoftSqliteConnection = InsertionHelper.Insert_Microsoft();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		SqliteNetConnection?.Dispose();
		LambdaSqliteConnection?.Dispose();
		MicrosoftSqliteConnection?.Dispose();
	}

	[Benchmark(Baseline = true)]
	public void QueryAll_SqliteNet()
	{
		foreach (var _ in SqliteNetConnection.DeferredQuery<BenchmarkItem>("SELECT * FROM \"BenchmarkItem\"")) { }
	}

	[Benchmark]
	public void QueryAllAsValueTuple_SqliteNet()
	{
		foreach (var _ in SqliteNetConnection.DeferredQuery<(int id, int value, string stringValue, TestEnum enumValue)>("SELECT Id, Value, StringValue, EnumValue FROM \"BenchmarkItem\"")) { }
	}

	[Benchmark]
	public void QueryAll_FourLambda()
	{
		foreach (var _ in LambdaSqliteConnection.Query<BenchmarkItem>()) { }
	}

	[Benchmark]
	public void QueryAllAsValueTuple_FourLambda()
	{
		foreach (var _ in LambdaSqliteConnection.Query<(int id, int value, string stringValue, TestEnum enumValue)>("SELECT Id, Value, StringValue, EnumValue FROM \"BenchmarkItem\"")) { }
	}

	[Benchmark]
	public void QueryAllReader_FourLambda()
	{
		using var reader = LambdaSqliteConnection.ExecuteReader("SELECT * FROM \"BenchmarkItem\"");

		while (reader.Read())
		{
			_ = reader.GetInt32(0);
			_ = reader.GetInt32(1);
			_ = reader.GetString(2);
			_ = reader.GetValue<TestEnum>(3);
		}
	}

	[Benchmark]
	public void QueryAllReaderSkipping_FourLambda()
	{
		using var reader = LambdaSqliteConnection.ExecuteReader("SELECT * FROM \"BenchmarkItem\"");

		while (reader.Read()) { }
	}

	[Benchmark]
	public void QueryAllReader_Microsoft()
	{
		using var command = MicrosoftSqliteConnection.CreateCommand();
		command.CommandText = "SELECT * FROM \"BenchmarkItem\"";

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			_ = reader.GetInt32(0);
			_ = reader.GetInt32(1);
			_ = reader.GetString(2);
			_ = reader.GetInt32(3);
		}
	}

	[Benchmark]
	public void QueryAllReaderSkipping_Microsoft()
	{
		using var command = MicrosoftSqliteConnection.CreateCommand();
		command.CommandText = "SELECT * FROM \"BenchmarkItem\"";

		using var reader = command.ExecuteReader();

		while (reader.Read()) { }
	}
}