using System.Diagnostics.CodeAnalysis;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class ContainsTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }

		public string Name { get; set; }


		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: Id={Id}, Name={Name}]";
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
	}

	[Test]
	public void ContainsConstantData()
	{
		var n = 20;
		var cq = Enumerable.Range(1, n)
			.Select(i => new TestObj
			{
				Name = i.ToString()
			});

		Database.InsertAll(cq);

		var tensq = new string[] { "0", "10", "20" };

		var tens = Database.Table<TestObj>()
			.Where(o => tensq.Contains(o.Name))
			.ToList();

		Assert.AreEqual(2, tens.Count);


		var moreq = new string[] { "0", "x", "99", "10", "20", "234324" };

		var more = Database.Table<TestObj>()
			.Where(o => moreq.Contains(o.Name))
			.ToList();

		Assert.AreEqual(2, more.Count);
	}

	[Test]
	public void ContainsQueriedData()
	{
		var n = 20;
		var cq = Enumerable.Range(1, n)
			.Select(i => new TestObj
			{
				Name = i.ToString()
			});

		Database.InsertAll(cq);

		var tensq = new string[] { "0", "10", "20" };

		var tens = Database.Table<TestObj>()
			.Where(o => tensq.Contains(o.Name))
			.ToList();

		Assert.AreEqual(2, tens.Count);

		var moreq = new string[] { "0", "x", "99", "10", "20", "234324" };

		var more = Database.Table<TestObj>()
			.Where(o => moreq.Contains(o.Name))
			.ToList();

		Assert.AreEqual(2, more.Count);

		// https://github.com/praeclarum/sqlite-net/issues/28
		var moreq2 = moreq.ToList();

		var more2 = Database.Table<TestObj>()
			.Where(o => moreq2.Contains(o.Name))
			.ToList();

		Assert.AreEqual(2, more2.Count);
	}
}