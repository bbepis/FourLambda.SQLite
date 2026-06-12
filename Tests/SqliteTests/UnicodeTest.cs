namespace FourLambda.SQLite.Tests;

[TestFixture]
public class UnicodeTest : DBTestHarness
{
	protected override void InitializeDatabase()
	{
		Database.CreateTable<Product>();
	}

	private const string UnicodeTestString = "\u2329\u221E\u232A";

	[Test]
	public void Insert ()
	{
		Database.Insert (new Product {
			Name = UnicodeTestString,
		});
		
		var p = Database.Get<Product>(1);
		
		Assert.AreEqual (UnicodeTestString, p.Name);
	}
		
	[Test]
	public void Query ()
	{
		Database.Insert (new Product {
			Name = UnicodeTestString,
		});
			
		var ps = Database.Table<Product>().Where(p => p.Name == UnicodeTestString).ToList ();
			
		Assert.AreEqual (1, ps.Count);
		Assert.AreEqual (UnicodeTestString, ps [0].Name);
	}
}