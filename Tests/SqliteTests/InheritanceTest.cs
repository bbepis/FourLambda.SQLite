namespace FourLambda.SQLite.Tests;

[TestFixture]
public class InheritanceTest : DBTestHarness
{
	private class Base
	{
		[PrimaryKey]
		public int Id { get; set; }

		public string BaseProp { get; set; }
	}

	private class Derived : Base
	{
		public string DerivedProp { get; set; }
	}


	[Test]
	public void InheritanceWorks()
	{
		var mapping = Database.GetMapping<Derived>();

		Assert.AreEqual(3, mapping.Columns.Length);
		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		Assert.AreEqual("Id", mapping.PrimaryKeyColumns[0].Name);
	}
}