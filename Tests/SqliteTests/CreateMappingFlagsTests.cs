namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CreateMappingFlagsTests : DBTestHarness
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

	private void CheckPK(TableMapping map)
	{
		for (var i = 1; i <= 10; i++)
		{
			var na = new NoAttributes { Id = i, AColumn = i.ToString(), IndexedId = 0 };
			Database.Insert(na);
		}

		var item = Database.Find<NoAttributes>(map, 2);
		Assert.IsNotNull(item);
		Assert.AreEqual(2, item.Id);
	}

	[Test]
	public void WithoutImplicitMapping()
	{
		var mapping = TableMappingBuilder.FromType<NoAttributesNoOptions>().Build();

		Assert.AreEqual(0, mapping.PrimaryKeyColumns.Length, "Should not be a key");

		var column = mapping.Columns[2];
		Assert.AreEqual("IndexedId", column.Name);
		Assert.IsFalse(column.Indices.Any());
	}

	[Test]
	public void ImplicitPK()
	{
		var mapping = TableMappingBuilder.FromType<NoAttributes>(TableCreateFlags.ImplicitPK).Build();

		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsFalse(pk.IsAutoInc);

		Database.CreateTable(mapping);
		CheckPK(mapping);
	}


	[Test]
	public void ImplicitAutoInc()
	{
		var mapping = TableMappingBuilder.FromType<PkAttribute>(TableCreateFlags.AutoIncPK).Build();

		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}

	[Test]
	public void ImplicitPKAutoInc()
	{
		var mapping = TableMappingBuilder.FromType<NoAttributes>(TableCreateFlags.ImplicitPK | TableCreateFlags.AutoIncPK).Build();

		Assert.AreEqual(1, mapping.PrimaryKeyColumns.Length);
		var pk = mapping.PrimaryKeyColumns[0];

		Assert.AreEqual("Id", pk.Name);
		Assert.IsTrue(pk.IsPK);
		Assert.IsTrue(pk.IsAutoInc);
	}
}