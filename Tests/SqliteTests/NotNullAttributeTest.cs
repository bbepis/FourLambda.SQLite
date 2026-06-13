using System.Reflection;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class NotNullAttributeTest : DBTestHarness
{
	private class ExpectedNullAttribute(bool shouldBeNull) : Attribute
	{
		public bool ShouldBeNull => shouldBeNull;
	}

	private class NotNullNoPK
	{
		[PrimaryKey, AutoIncrement, ExpectedNull(false)]
		public int objectId { get; set; }

		[ExpectedNull(false)]
		public int RequiredIntProp { get; set; }

		[ExpectedNull(true)]
		public int? OptionalIntProp { get; set; }

		[NotNull, ExpectedNull(false)]
		public string RequiredStringProp { get; set; }

		[ExpectedNull(true)]
		public string OptionalStringProp { get; set; }

		[NotNull, ExpectedNull(false)]
		public string AnotherRequiredStringProp { get; set; }
	}

	private class ClassWithPK
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<NotNullNoPK>();
	}

	private (string name, bool nullable)[] GetExpectedColumnInfos(Type type)
	{
		var expectedValues = type.GetRuntimeProperties()
			.Select(prop => (prop.Name, prop.GetCustomAttribute<ExpectedNullAttribute>()!.ShouldBeNull))
			.ToArray();

		return expectedValues;
	}

	[Test]
	public void PrimaryKeyHasNotNullConstraint()
	{
		Database.CreateTable<ClassWithPK>();
		var cols = Database.GetTableInfo("ClassWithPK");

		Assert.AreEqual(1, cols.Count, "Failed to get table info");
		Assert.IsFalse(cols[0].IsNullable, $"not null constraint was not created for the primary key");
	}

	[Test]
	public void CreateTableWithNotNullConstraints()
	{
		Database.CreateTable<NotNullNoPK>();
		var cols = Database.GetTableInfo(nameof(NotNullNoPK));
		var expected = GetExpectedColumnInfos(typeof(NotNullNoPK));


		var joined = expected.Join(cols, expected => expected.name, actual => actual.Name,
				(expected, actual) => new { expected, actual })
			.Where(@t => @t.actual.IsNullable != @t.expected.nullable)
			.Select(@t => @t.actual.Name)
			.ToArray();

		Assert.AreNotEqual(0, cols.Count(), "Failed to get table info");
		Assert.IsTrue(joined.Length == 0,
			$"not null constraint was not created for the following properties: {string.Join(", ", joined)}");
	}

	private NotNullConstraintViolationException AssertNotNullConstraint(Action action)
	{
		return Assert.Throws<NotNullConstraintViolationException>(() =>
		{
			try
			{
				action();
			}
			catch (SQLiteException ex)
			{
				if (SQLite3Native.LibVersionNumber() < 3007017 && ex.Result == SQLite3Native.Result.Constraint)
				{
					Console.WriteLine(
						"Detailed constraint information is only available in SQLite3 version 3.7.17 and above.");
					Assert.Inconclusive();
					return;
				}

				throw;
			}
		});
	}

	[Test]
	public void InsertWithNullsThrowsException()
	{
		AssertNotNullConstraint(() =>
		{
			var obj = new NotNullNoPK();
			Database.Insert(obj);
		});
	}


	[Test]
	public void UpdateWithNullThrowsException()
	{
		var obj = new NotNullNoPK
		{
			AnotherRequiredStringProp = "Another required string",
			RequiredIntProp = 123,
			RequiredStringProp = "Required string"
		};
		Database.Insert(obj);

		AssertNotNullConstraint(() =>
		{
			obj.RequiredStringProp = null;
			Database.Update(obj);
		});
	}

	[Test]
	public void NotNullConstraintExceptionListsOffendingColumnsOnInsert()
	{
		var ex = AssertNotNullConstraint(() =>
		{
			var obj = new NotNullNoPK { RequiredStringProp = "Some value" };
			Database.Insert(obj);
		});

		var expected = "AnotherRequiredStringProp";
		var actual = string.Join(", ",
			ex.Columns.Where(c => !c.IsPK).OrderBy(p => p.PropertyName).Select(c => c.PropertyName));

		Assert.AreEqual(expected, actual,
			"NotNullConstraintViolationException did not correctly list the columns that violated the constraint");
	}

	[Test]
	public void NotNullConstraintExceptionListsOffendingColumnsOnUpdate()
	{
		var obj = new NotNullNoPK
		{
			AnotherRequiredStringProp = "Another required string",
			RequiredIntProp = 123,
			RequiredStringProp = "Required string"
		};
		Database.Insert(obj);

		var ex = AssertNotNullConstraint(() =>
		{
			obj.RequiredStringProp = null;
			Database.Update(obj);
		});

		var expected = "RequiredStringProp";
		var actual = string.Join(", ",
			ex.Columns.Where(c => !c.IsPK).OrderBy(p => p.PropertyName).Select(c => c.PropertyName));

		Assert.AreEqual(expected, actual,
			"NotNullConstraintViolationException did not correctly list the columns that violated the constraint");
	}

	[Test]
	public void InsertQueryWithNullThrowsException()
	{
		AssertNotNullConstraint(() =>
		{
			Database.Execute(
				"insert into \"NotNullNoPK\" (AnotherRequiredStringProp, RequiredIntProp, RequiredStringProp) values(?, ?, ?)",
				"Another required string", 123, null);
		});
	}

	[Test]
	public void UpdateQueryWithNullThrowsException()
	{
			Database.Execute(
				"insert into \"NotNullNoPK\" (AnotherRequiredStringProp, RequiredIntProp, RequiredStringProp) values(?, ?, ?)",
				"Another required string", 123, "Required string");

		AssertNotNullConstraint(() =>
		{
			Database.Execute(
				"update \"NotNullNoPK\" set AnotherRequiredStringProp=?, RequiredIntProp=?, RequiredStringProp=? where ObjectId=?",
				"Another required string", 123, null, 1);
		});
	}

	[Test]
	public void ExecuteNonQueryWithNullThrowsException()
	{
		var obj = new NotNullNoPK
		{
			AnotherRequiredStringProp = "Another required prop",
			RequiredIntProp = 123,
			RequiredStringProp = "Required string prop"
		};
		Database.Insert(obj);

		AssertNotNullConstraint(() =>
		{
			var obj2 = new NotNullNoPK
			{
				objectId = 1,
				OptionalIntProp = 123
			};
			Database.Insert(obj2, InsertConflictAction.Replace);
		});
	}
}