using BenchmarkDotNet.Attributes;

namespace Benchmark;

[MemoryDiagnoser]
public class BulkInsertBenchmark
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

	private BenchmarkItem[] BenchmarkItems;

	public BulkInsertBenchmark()
	{
		const int itemCount = 1_000_000;

		BenchmarkItems = new BenchmarkItem[itemCount];

		for (int i = 0; i < itemCount; i++)
		{
			BenchmarkItems[i] = new()
			{
				ID = i + 1,
				Value = Random.Shared.Next(),
				EnumValue = (TestEnum)Random.Shared.Next(3),
				StringValue = Random.Shared.Next().ToString()
			};
		}

		SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
	}

	[Benchmark(Baseline = true)]
	public void BulkInsert_SQLitePCL()
	{
		using var connection = new SQLite.SQLiteConnection(":memory:");

		connection.CreateTable<BenchmarkItem>();

		connection.InsertAll(BenchmarkItems);
	}

	[Benchmark]
	public void BulkInsert_FourLambda()
	{
		using var connection = new FourLambda.SQLite.SQLiteConnection(":memory:");

		connection.CreateTable<BenchmarkItem>();

		connection.InsertAll(BenchmarkItems);
	}
}