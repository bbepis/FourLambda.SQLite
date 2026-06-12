namespace FourLambda.SQLite.Tests;

[TestFixture]
public class TableChangedTest : DBTestHarness
{
	int changeCount = 0;

	[SetUp]
	public void SetUp ()
	{
		Database.CreateTable<Product>();
		Database.CreateTable<Order>();
		Database.InsertAll (from i in Enumerable.Range (0, 22)
			select new Product { Name = "Thing" + i, Price = (decimal)Math.Pow (2, i) });

		changeCount = 0;

		Database.TableChanged += (sender, e) => {

			if (e.Table.TableName == "Product") {
				changeCount++;
			}
		};
	}

	[Test]
	public void Insert ()
	{
		var query = Database.Table<Product>().Select(p => p);

		Assert.AreEqual (0, changeCount);
		Assert.AreEqual (22, query.Count ());

		Database.Insert (new Product { Name = "Hello", Price = 1001 });

		Assert.AreEqual (1, changeCount);
		Assert.AreEqual (23, query.Count ());
	}

	[Test]
	public void InsertAll ()
	{
		var query = Database.Table<Product>().Select(p => p);

		Assert.AreEqual (0, changeCount);
		Assert.AreEqual (22, query.Count ());

		Database.InsertAll (from i in Enumerable.Range (0, 22)
			select new Product { Name = "Test" + i, Price = (decimal)Math.Pow (3, i) });

		Assert.AreEqual (22, changeCount);
		Assert.AreEqual (44, query.Count ());
	}

	[Test]
	public void Update ()
	{
		var query = Database.Table<Product>().Select(p => p);

		Assert.AreEqual (0, changeCount);
		Assert.AreEqual (22, query.Count ());

		var pr = query.First ();
		pr.Price = 10000000;
		Database.Update (pr);

		Assert.AreEqual (1, changeCount);
		Assert.AreEqual (22, query.Count ());
	}

	[Test]
	public void Delete ()
	{
		var query = Database.Table<Product>().Select(p => p);

		Assert.AreEqual (0, changeCount);
		Assert.AreEqual (22, query.Count ());

		var pr = query.First ();
		pr.Price = 10000000;
		Database.Delete (pr);

		Assert.AreEqual (1, changeCount);
		Assert.AreEqual (21, query.Count ());

		Database.DeleteAll<Product>();

		Assert.AreEqual (2, changeCount);
		Assert.AreEqual (0, query.Count ());
	}
}