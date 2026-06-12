namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CreateTableImplicitTest : DBTestHarness
{
	private class NoAttributes
	{
		public int Id { get; set; }
		public string AColumn { get; set; }
		public int IndexedId { get; set; }
	}

	private class NoAttributesNoOptions
	{
		public int Id { get; set; }
		public string AColumn { get; set; }
		public int IndexedId { get; set; }
	}

	private class PkAttribute
	{
		[PrimaryKey]
		public int Id { get; set; }

		public string AColumn { get; set; }
		public int IndexedId { get; set; }
	}

	private void CheckPK()
	{
		for (var i = 1; i <= 10; i++)
		{
			var na = new NoAttributes { Id = i, AColumn = i.ToString(), IndexedId = 0 };
			Database.Insert(na);
		}

		var item = Database.Get<NoAttributes>(2);
		Assert.IsNotNull(item);
		Assert.AreEqual(2, item.Id);
	}

	[Test]
	public void WithoutImplicitMapping()
	{
		Database.CreateTable<NoAttributesNoOptions>();

		var mapping = Database.GetMapping<NoAttributesNoOptions>();

		Assert.AreEqual(0, mapping.PrimaryKeyColumns.Length, "Should not be a key");
		var pk = mapping.PrimaryKeyColumns[0];

		var column = mapping.Columns[2];
		Assert.AreEqual("IndexedId", column.Name);
		Assert.IsFalse(column.Indices.Any());
	}

	[Test]
	public void ImplicitPK()
	{
		Database.CreateTable<NoAttributes>(CreateFlags.ImplicitPK);

		var mapping = Database.GetMapping<NoAttributes>();

		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsFalse(pk.IsAutoInc);

		CheckPK();
	}


	[Test]
	public void ImplicitAutoInc()
	{
		Database.CreateTable<PkAttribute>(CreateFlags.AutoIncPK);

		var mapping = Database.GetMapping<PkAttribute>();

		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}

	[Test]
	public void ImplicitIndex()
	{
		Database.CreateTable<NoAttributes>(CreateFlags.ImplicitIndex);

		var mapping = Database.GetMapping<NoAttributes>();
		var column = mapping.Columns[2];
		Assert.AreEqual("IndexedId", column.Name);
		Assert.IsTrue(column.Indices.Any());
	}

	[Test]
	public void ImplicitPKAutoInc()
	{
		Database.CreateTable(typeof(NoAttributes), CreateFlags.ImplicitPK | CreateFlags.AutoIncPK);

		var mapping = Database.GetMapping<NoAttributes>();
		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}

	[Test]
	public void ImplicitAutoIncAsPassedInTypes()
	{
		Database.CreateTable(typeof(PkAttribute), CreateFlags.AutoIncPK);

		var mapping = Database.GetMapping<PkAttribute>();
		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}

	[Test]
	public void ImplicitPkAsPassedInTypes()
	{
		Database.CreateTable(typeof(NoAttributes), CreateFlags.ImplicitPK);

		var mapping = Database.GetMapping<NoAttributes>();
		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsFalse(pk.IsAutoInc);
	}

	[Test]
	public void ImplicitPKAutoIncAsPassedInTypes()
	{
		Database.CreateTable(typeof(NoAttributes), CreateFlags.ImplicitPK | CreateFlags.AutoIncPK);

		var mapping = Database.GetMapping<NoAttributes>();
		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}
}