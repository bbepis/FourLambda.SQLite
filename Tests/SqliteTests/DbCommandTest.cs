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
			.ExecuteDeferredQuery<Product>(TableMappingBuilder.FromType<Product>().Build()).ToList();


		Assert.AreEqual(test.Count, 1);
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
			.ExecuteDeferredQuery<object>(TableMappingBuilder.FromType<Product>().Build()).ToList();

		Assert.AreEqual(test.Count, 1);
	}
}