namespace FourLambda.SQLite.Tests;

[TestFixture]
public class LinqTest : DBTestHarness
{
	protected override void InitializeDatabase()
	{
		Database.CreateTable<Product>();
		Database.CreateTable<Order>();
		Database.CreateTable<OrderLine>();
		Database.CreateTable<OrderHistory>();
	}

	[Test]
	public void FunctionParameter()
	{
		Database.Insert(new Product
		{
			Name = "A",
			Price = 20
		});

		Database.Insert(new Product
		{
			Name = "B",
			Price = 10
		});

		var r = Database.Table<Product>()
			.Where(p => p.Price > 15)
			.ToList();

		Assert.AreEqual(1, r.Count);
		Assert.AreEqual("A", r[0].Name);
	}

	[Test]
	public void WhereGreaterThan()
	{
		Database.Insert(new Product
		{
			Name = "A",
			Price = 20
		});

		Database.Insert(new Product
		{
			Name = "B",
			Price = 10
		});

		Assert.AreEqual(2, Database.Table<Product>().Count());

		var r = Database.Table<Product>()
			.Where(p => p.Price > 15)
			.ToList();

		Assert.AreEqual(1, r.Count);
		Assert.AreEqual("A", r[0].Name);
	}

	[Test]
	public void GetWithExpression()
	{
		Database.Insert(new Product
		{
			Name = "A",
			Price = 20
		});

		Database.Insert(new Product
		{
			Name = "B",
			Price = 10
		});

		Database.Insert(new Product
		{
			Name = "C",
			Price = 5
		});

		Assert.AreEqual(3, Database.Table<Product>().Count());

		var r = Database.Get<Product>(x => x.Price == 10);
		Assert.IsNotNull(r);
		Assert.AreEqual("B", r.Name);
	}

	[Test]
	public void FindWithExpression()
	{
		var r = Database.Find<Product>(x => x.Price == 10);
		Assert.IsNull(r);
	}

	[Test]
	public void OrderByCast()
	{
		Database.Insert(new Product
		{
			Name = "A",
			TotalSales = 1
		});
		Database.Insert(new Product
		{
			Name = "B",
			TotalSales = 100
		});

		var uncasted = Database.Table<Product>()
			.OrderByDescending(p => p.TotalSales)
			.ToList();

		Assert.AreEqual(2, uncasted.Count);
		Assert.AreEqual("B", uncasted[0].Name);

		var casted = Database.Table<Product>()
			.OrderByDescending(p => (int)p.TotalSales)
			.ToList();

		Assert.AreEqual(2, casted.Count);
		Assert.AreEqual("B", casted[0].Name);
	}

	public class Issue96_A
	{
		[AutoIncrement, PrimaryKey]
		public int ID { get; set; }

		public string AddressLine { get; set; }

		[Indexed]
		public int? ClassB { get; set; }

		[Indexed]
		public int? ClassC { get; set; }
	}

	public class Issue96_B
	{
		[AutoIncrement, PrimaryKey]
		public int ID { get; set; }

		public string CustomerName { get; set; }
	}

	public class Issue96_C
	{
		[AutoIncrement, PrimaryKey]
		public int ID { get; set; }

		public string SupplierName { get; set; }
	}

	[Test]
	public void Issue96_NullableIntsInQueries()
	{
		Database.CreateTable<Issue96_A>();

		var id = 42;

		Database.Insert(new Issue96_A
		{
			ClassB = id
		});
		Database.Insert(new Issue96_A
		{
			ClassB = null
		});
		Database.Insert(new Issue96_A
		{
			ClassB = null
		});
		Database.Insert(new Issue96_A
		{
			ClassB = null
		});


		Assert.AreEqual(1, Database.Table<Issue96_A>().Count(p => p.ClassB == id));
		Assert.AreEqual(3, Database.Table<Issue96_A>().Count(p => p.ClassB == null));
	}

	public class Issue303_A
	{
		[PrimaryKey, NotNull]
		public int Id { get; set; }

		public string Name { get; set; }
	}

	public class Issue303_B
	{
		[PrimaryKey, NotNull]
		public int Id { get; set; }

		public bool Flag { get; set; }
	}

	[Test]
	public void Issue303_WhereNot_A()
	{
		Database.CreateTable<Issue303_A>();
		Database.Insert(new Issue303_A { Id = 1, Name = "aa" });
		Database.Insert(new Issue303_A { Id = 2, Name = null });
		Database.Insert(new Issue303_A { Id = 3, Name = "test" });
		Database.Insert(new Issue303_A { Id = 4, Name = null });

		var r = Database.Table<Issue303_A>()
			.Where(p => p.Name != null)
			.ToList();

		Assert.AreEqual(2, r.Count);
		Assert.AreEqual(1, r[0].Id);
		Assert.AreEqual(3, r[1].Id);
	}

	[Test]
	public void Issue303_WhereNot_B()
	{
		Database.CreateTable<Issue303_B>();
		Database.Insert(new Issue303_B { Id = 1, Flag = true });
		Database.Insert(new Issue303_B { Id = 2, Flag = false });
		Database.Insert(new Issue303_B { Id = 3, Flag = true });
		Database.Insert(new Issue303_B { Id = 4, Flag = false });

		var r = Database.Table<Issue303_B>()
			.Where(p => !p.Flag)
			.ToList();

		Assert.AreEqual(2, r.Count);
		Assert.AreEqual(2, r[0].Id);
		Assert.AreEqual(4, r[1].Id);
	}

	[Test]
	public void QuerySelectAverage()
	{
		Database.Insert(new Product
		{
			Name = "A",
			Price = 20,
			TotalSales = 100
		});

		Database.Insert(new Product
		{
			Name = "B",
			Price = 10,
			TotalSales = 100
		});

		Database.Insert(new Product
		{
			Name = "C",
			Price = 1000,
			TotalSales = 1
		});

		var r = Database.Table<Product>()
			.Where(x => x.TotalSales > 50)
			.Select(s => s.Price)
			.Average();

		Assert.AreEqual(15m, r);
	}

	private interface IEntity
	{
		int Id { get; set; }
		string Value { get; set; }
	}

	private class Entity : IEntity
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }

		public string Value { get; set; }
	}

	[Test]
	public void CastedParameters()
	{
		Database.CreateTable<Entity>();

		Database.Insert(new Entity
		{
			Value = "Foo"
		});

		var r = Database.Table<Entity>().FirstOrDefault(x => ((IEntity)x).Id == 1);

		Assert.AreEqual("Foo", r.Value);
	}

	[Test]
	public void Issue460_ReplaceWith2Args()
	{
		//Database.Tracer = Console.WriteLine;

		Database.Insert(new Product
		{
			Name = "I am not B X B"
		});
		Database.Insert(new Product
		{
			Name = "I am B O B"
		});

		var cl = (from c in Database.Table<Product>()
			where c.Name.Replace(" ", "").Contains("BOB")
			select c).FirstOrDefault();

		Assert.AreEqual(2, cl.Id);
		Assert.AreEqual("I am B O B", cl.Name);
	}
}