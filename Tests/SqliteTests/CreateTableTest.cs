namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CreateTableTest : DBTestHarness
{
	private class NoPropObject { }

	[Test]
	public void CreateTypeWithNoProps()
	{
		Assert.Throws<Exception>(() => Database.CreateTable<NoPropObject>());
	}

	[Test]
	public void CreateThem()
	{
		Database.CreateTable<Product>();
		Database.CreateTable<Order>();
		Database.CreateTable<OrderLine>();
		Database.CreateTable<OrderHistory>();

		VerifyCreations();
	}

	[Test]
	public void CreateAsPassedInTypes()
	{
		Database.CreateTable(typeof(Product));
		Database.CreateTable(typeof(Order));
		Database.CreateTable(typeof(OrderLine));
		Database.CreateTable(typeof(OrderHistory));

		VerifyCreations();
	}

	[Test]
	public void CreateTwice()
	{
		Database.CreateTable<Product>();
		Database.CreateTable<OrderLine>();
		Database.CreateTable<Order>();
		Database.CreateTable<OrderLine>();
		Database.CreateTable<OrderHistory>();

		VerifyCreations();
	}

	private void VerifyCreations()
	{
		var orderLine = Database.GetMapping(typeof(OrderLine));
		Assert.AreEqual(6, orderLine.Columns.Length);

		var l = new OrderLine
		{
			Status = OrderLineStatus.Shipped
		};
		Database.Insert(l);
		var lo = Database.Table<OrderLine>().First(x => x.Status == OrderLineStatus.Shipped);
		Assert.AreEqual(lo.Id, l.Id);
	}

	private class Issue115_MyObject
	{
		[PrimaryKey]
		public string UniqueId { get; set; }

		public byte OtherValue { get; set; }
	}

	[Test]
	public void Issue115_MissingPrimaryKey()
	{
		Database.CreateTable<Issue115_MyObject>();
		Database.InsertAll(Enumerable.Range(0, 10)
			.Select(i => new Issue115_MyObject
			{
				UniqueId = i.ToString(),
				OtherValue = (byte)(i * 10)
			}));

		var query = Database.Table<Issue115_MyObject>();
		foreach (var itm in query)
		{
			itm.OtherValue++;
			Assert.AreEqual(1, Database.Update(itm, typeof(Issue115_MyObject)));
		}
	}

	[Table("WantsNoRowId", WithoutRowId = true)]
	private class WantsNoRowId
	{
		[PrimaryKey]
		public int Id { get; set; }

		public string Name { get; set; }
	}

	[Table("sqlite_master")]
	private class SqliteMaster
	{
		[Column("type")]
		public string Type { get; set; }

		[Column("name")]
		public string Name { get; set; }

		[Column("tbl_name")]
		public string TableName { get; set; }

		[Column("rootpage")]
		public int RootPage { get; set; }

		[Column("sql")]
		public string Sql { get; set; }
	}

	[Test]
	public void WithoutRowId()
	{
		Database.CreateTable<OrderLine>();
		var info = Database.Table<SqliteMaster>().First(m => m.TableName == "OrderLine");
		Assert.That(!info.Sql.Contains("without rowid"));

		Database.CreateTable<WantsNoRowId>();
		info = Database.Table<SqliteMaster>().First(m => m.TableName == "WantsNoRowId");
		Assert.That(info.Sql.Contains("without rowid"));
	}
}