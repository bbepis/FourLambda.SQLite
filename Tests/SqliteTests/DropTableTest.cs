namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DropTableTest : DBTestHarness
{
	public class Product
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string Name { get; set; }
		public decimal Price { get; set; }
	}
		
	[Test]
	public void CreateInsertDrop ()
	{
		Database.CreateTable<Product>();
			
		Database.Insert (new Product {
			Name = "Hello",
			Price = 16,
		});
			
		var n = Database.Table<Product>().Count();
			
		Assert.AreEqual (1, n);
			
		Database.DropTable<Product>();
		
		Assert.Throws<SQLiteException>(() => Database.Table<Product>().Count());
	}
}