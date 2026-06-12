namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DbCommandTest : DBTestHarness
{
	[Test]
	public void QueryCommand()
	{
		Database.CreateTable<Product>();
		var b = new Product();
		Database.Insert(b);

		var test = Database.CreateCommand("select * from Product")
			.ExecuteDeferredQuery<Product>(new TableMapping(typeof(Product))).ToList();


		Assert.AreEqual (test.Count, 1);
	}

	/// <summary>
	/// For issue #1048
	/// </summary>
	[Test]
	public void QueryCommandCastToObject()
	{
		Database.CreateTable<Product>();
		var b = new Product();
		Database.Insert(b);

		var test = Database.CreateCommand("select * from Product")
			.ExecuteDeferredQuery<object>(new TableMapping(typeof(Product))).ToList();

		Assert.AreEqual (test.Count, 1);
	}
}