namespace FourLambda.SQLite.Tests;

[TestFixture]
public class MappingTest : DBTestHarness
{
	[Table("AGoodTableName")]
	private class AFunnyTableName
	{
		[PrimaryKey]
		public int Id { get; set; }

		[Column("AGoodColumnName")]
		public string AFunnyColumnName { get; set; }
	}


	[Test]
	public void HasGoodNames()
	{
		Database.CreateTable<AFunnyTableName>();

		var mapping = Database.GetMapping<AFunnyTableName>();

		Assert.AreEqual("AGoodTableName", mapping.TableName);

		Assert.AreEqual("Id", mapping.Columns[0].Name);
		Assert.AreEqual("AGoodColumnName", mapping.Columns[1].Name);
	}

	private class OverrideNamesBase
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public virtual string Name { get; set; }
		public virtual string Value { get; set; }
	}

	private class OverrideNamesClass : OverrideNamesBase
	{
		[Column("n")]
		public override string Name { get; set; }

		[Column("v")]
		public override string Value { get; set; }
	}

	[Test]
	public void OverrideNames()
	{
		Database.CreateTable<OverrideNamesClass>();

		var cols = Database.GetTableInfo("OverrideNamesClass");
		Assert.AreEqual(3, cols.Count);
		Assert.IsTrue(cols.Exists(x => x.Name == "n"));
		Assert.IsTrue(cols.Exists(x => x.Name == "v"));

		var o = new OverrideNamesClass
		{
			Name = "Foo",
			Value = "Bar"
		};

		Database.Insert(o);

		var oo = Database.Table<OverrideNamesClass>().First();

		Assert.AreEqual("Foo", oo.Name);
		Assert.AreEqual("Bar", oo.Value);
	}

	#region Issue #86

	[Table("foo")]
	public class Foo
	{
		[Column("baz")]
		public int Bar { get; set; }
	}

	[Test]
	public void Issue86()
	{
		Database.CreateTable<Foo>();

		Database.Insert(new Foo { Bar = 42 });
		Database.Insert(new Foo { Bar = 69 });

		var found42 = Database.Table<Foo>().FirstOrDefault(f => f.Bar == 42);
		Assert.IsNotNull(found42);

		var ordered = new List<Foo>(Database.Table<Foo>().OrderByDescending(f => f.Bar));
		Assert.AreEqual(2, ordered.Count);
		Assert.AreEqual(69, ordered[0].Bar);
		Assert.AreEqual(42, ordered[1].Bar);
	}

	#endregion

	#region Issue #572

	public class OnlyKeyModel
	{
		[PrimaryKey]
		public string MyModelId { get; set; }
	}

	[Test]
	public void OnlyKey()
	{
		Database.CreateTable<OnlyKeyModel>();

		Database.InsertOrReplace(new OnlyKeyModel { MyModelId = "Foo" });
		var foo = Database.Get<OnlyKeyModel>("Foo");
		Assert.AreEqual(foo.MyModelId, "Foo");

		Database.Insert(new OnlyKeyModel { MyModelId = "Bar" });
		var bar = Database.Get<OnlyKeyModel>("Bar");
		Assert.AreEqual(bar.MyModelId, "Bar");

		Database.Update(new OnlyKeyModel { MyModelId = "Foo" });
		var foo2 = Database.Get<OnlyKeyModel>("Foo");
		Assert.AreEqual(foo2.MyModelId, "Foo");
	}

	#endregion
}