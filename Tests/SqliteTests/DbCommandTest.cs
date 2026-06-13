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
			.ExecuteQuery<Product>(TableMappingBuilder.FromType<Product>().Build()).ToList();


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
			.ExecuteQuery<object>(TableMappingBuilder.FromType<Product>().Build()).ToList();

		Assert.AreEqual(test.Count, 1);
	}

	public class ProductAdjacent
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string Name { get; set; }
		public decimal Price { get; set; }

		public uint TotalSales { get; set; }
	}

	[Test]
	public void QueryCommandCastToAdjacentObject()
	{
		Database.CreateTable<Product>();

		var b = new Product
		{
			Id = 1,
			Name = "test name",
			Price = 123.2345m,
			TotalSales = 134543
		};

		Database.Insert(b);

		var test = Database.CreateCommand("select * from Product")
			.ExecuteQuery<ProductAdjacent>(Database.GetMapping<Product>()).ToList();

		Assert.AreEqual(test.Count, 1);
		Assert.AreEqual(b.Id, test[0].Id);
		Assert.AreEqual(b.Name, test[0].Name);
		Assert.AreEqual(b.Price, test[0].Price);
		Assert.AreEqual(b.TotalSales, test[0].TotalSales);
	}

	public struct ProductAdjacentStruct
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string Name { get; set; }
		public decimal Price { get; set; }

		public uint TotalSales { get; set; }
	}

	[Test]
	public void QueryCommandCastToAdjacentStruct()
	{
		Database.CreateTable<Product>();

		var b = new Product
		{
			Id = 1,
			Name = "test name",
			Price = 123.2345m,
			TotalSales = 134543
		};

		Database.Insert(b);

		var test = Database.CreateCommand("select * from Product")
			.ExecuteQuery<ProductAdjacentStruct>(Database.GetMapping<Product>()).ToList();

		Assert.AreEqual(test.Count, 1);
		Assert.AreEqual(b.Id, test[0].Id);
		Assert.AreEqual(b.Name, test[0].Name);
		Assert.AreEqual(b.Price, test[0].Price);
		Assert.AreEqual(b.TotalSales, test[0].TotalSales);
	}
}