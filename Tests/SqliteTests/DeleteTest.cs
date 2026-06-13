namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DeleteTest : DBTestHarness
{
	private class TestTable
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public int Datum { get; set; }
		public string Test { get; set; }
	}

	private const int Count = 100;

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestTable>();

		var items = Enumerable.Range(0, Count)
			.Select(i => new TestTable { Datum = 1000 + i, Test = "Hello World" });

		Database.InsertAll(items);

		Assert.AreEqual(Count, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteEntityOne()
	{
		var r = Database.Delete(Database.Find<TestTable>(1));

		Assert.AreEqual(1, r);
		Assert.AreEqual(Count - 1, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeletePKOne()
	{
		var r = Database.Delete<TestTable>(1);

		Assert.AreEqual(1, r);
		Assert.AreEqual(Count - 1, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeletePKNone()
	{
		var r = Database.Delete<TestTable>(348597);

		Assert.AreEqual(0, r);
		Assert.AreEqual(Count, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteAll()
	{
		var r = Database.DeleteAll<TestTable>();

		Assert.AreEqual(Count, r);
		Assert.AreEqual(0, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteWithPredicate()
	{
		var r = Database.Table<TestTable>().Delete(p => p.Test == "Hello World");

		Assert.AreEqual(Count, r);
		Assert.AreEqual(0, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteWithPredicateHalf()
	{
		Database.Insert(new TestTable { Datum = 1, Test = "Hello World 2" });

		var r = Database.Table<TestTable>().Delete(p => p.Test == "Hello World");

		Assert.AreEqual(Count, r);
		Assert.AreEqual(1, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteWithWherePredicate()
	{
		var r = Database.Table<TestTable>().Where(p => p.Test == "Hello World").Delete();

		Assert.AreEqual(Count, r);
		Assert.AreEqual(0, Database.Table<TestTable>().Count());
	}

	[Test]
	public void DeleteWithoutPredicate()
	{
		Assert.Throws<InvalidOperationException>(() =>
			Database.Table<TestTable>().Delete());
	}

	[Test]
	public void DeleteWithTake()
	{
		Assert.Throws<InvalidOperationException>(() =>
			Database.Table<TestTable>().Where(p => p.Test == "Hello World").Take(2).Delete());
	}

	[Test]
	public void DeleteWithSkip()
	{
		Assert.Throws<InvalidOperationException>(() =>
			Database.Table<TestTable>().Where(p => p.Test == "Hello World").Skip(2).Delete());
	}
}