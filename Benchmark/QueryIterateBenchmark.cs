using BenchmarkDotNet.Attributes;

namespace Benchmark;

[MemoryDiagnoser]
public class QueryIterateBenchmark : IDisposable
{
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

	public SQLite.SQLiteConnection SqlitePclConnection;
	public FourLambda.SQLite.SQLiteConnection LambdaSqliteConnection;

	public QueryIterateBenchmark()
	{
		const int itemCount = 1_000_000;

		var items = new BenchmarkItem[itemCount];

		for (int i = 0; i < itemCount; i++)
		{
			items[i] = new()
			{
				ID = i + 1,
				Value = Random.Shared.Next(),
				EnumValue = (TestEnum)Random.Shared.Next(3),
				StringValue = Random.Shared.Next().ToString()
			};
		}

		SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());


		SqlitePclConnection = new SQLite.SQLiteConnection(":memory:");
		SqlitePclConnection.CreateTable<BenchmarkItem>();
		SqlitePclConnection.InsertAll(items);

		LambdaSqliteConnection = new FourLambda.SQLite.SQLiteConnection(":memory:");
		LambdaSqliteConnection.CreateTable<BenchmarkItem>();
		LambdaSqliteConnection.InsertAll(items);
	}

	[Benchmark(Baseline = true)]
	public void QueryAll_SQLitePCL()
	{
		foreach (var item in SqlitePclConnection.Query<BenchmarkItem>("SELECT * FROM \"BenchmarkItem\"")) { }
	}

	[Benchmark]
	public void QueryAll_FourLambda()
	{
		foreach (var item in LambdaSqliteConnection.Query<BenchmarkItem>()) { }
	}

	public void Dispose()
	{
		SqlitePclConnection.Dispose();
		LambdaSqliteConnection.Dispose();
	}
}