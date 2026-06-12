namespace FourLambda.SQLite.Tests;

[TestFixture]
public class SkipTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public int Order { get; set; }

		public override string ToString () => $"[TestObj: Id={Id}, Order={Order}]";
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
	}
		
	[Test]
	public void Skip()
	{
		var n = 100;
			
		var cq = Enumerable.Range(1, n)
			.Select(i => new TestObj { Order = i });

		var objs = cq.ToArray();
						
		var numIn = Database.InsertAll(objs);			
		Assert.AreEqual(numIn, n, "Num inserted must = num objects");
			
		var q = Database.Table<TestObj>()
			.OrderBy(o => o.Order);
			
		var qs1 = q.Skip(1);			
		var s1 = qs1.ToList();
		Assert.AreEqual(n - 1, s1.Count);
		Assert.AreEqual(2, s1[0].Order);
			
		var qs5 = q.Skip(5);			
		var s5 = qs5.ToList();
		Assert.AreEqual(n - 5, s5.Count);
		Assert.AreEqual(6, s5[0].Order);
	}
}