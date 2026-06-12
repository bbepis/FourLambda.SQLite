namespace FourLambda.SQLite.Tests;

[TestFixture]
public class ScalarTest : DBTestHarness
{
	class TestTable
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public int Two { get; set; }
	}

	const int Count = 100;

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestTable>();
		var items = from i in Enumerable.Range(0, Count)
			select new TestTable { Two = 2 };
		Database.InsertAll(items);
		Assert.AreEqual(Count, Database.Table<TestTable>().Count());
	}


	[Test]
	public void Int32 ()
	{
		var r = Database.ExecuteScalar<int>("SELECT SUM(Two) FROM TestTable");

		Assert.AreEqual (Count * 2, r);

		Database.DeleteAll<TestTable>();

		var r1 = Database.ExecuteScalar<int>("SELECT SUM(Two) FROM TestTable");

		Assert.AreEqual (0, r1);
	}

	[Test]
	public void SelectSingleRowValue ()
	{
		var r = Database.ExecuteScalar<int>("SELECT Two FROM TestTable WHERE Id = 1 LIMIT 1");

		Assert.AreEqual (2, r);
	}

	[Test]
	public void SelectNullableSingleRowValue ()
	{
		var r = Database.ExecuteScalar<int?>("SELECT Two FROM TestTable WHERE Id = 1 LIMIT 1");

		Assert.AreEqual (true, r.HasValue);
		Assert.AreEqual (2, r);
	}

	[Test]
	public void SelectNoRowValue ()
	{
		var r = Database.ExecuteScalar<int?>("SELECT Two FROM TestTable WHERE Id = 999");

		Assert.AreEqual (false, r.HasValue);
	}

	[Test]
	public void SelectNullRowValue ()
	{
		var r = Database.ExecuteScalar<int?>("SELECT null AS Unknown FROM TestTable WHERE Id = 1 LIMIT 1");

		Assert.AreEqual (false, r.HasValue);
	}
}