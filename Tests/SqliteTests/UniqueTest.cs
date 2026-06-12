namespace FourLambda.SQLite.Tests;

[TestFixture]
public class UniqueIndexTest : DBTestHarness
{
	private const string IndexOneName = "IDX_One";
	private const string IndexTwoName = "IDX_Two";
	private const string IndexThreeName = "IDX_Three";
	private const string IndexFourName = "IDX_Four";

	public class TestClass {
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		[Unique (Name = IndexOneName)]
		public int IndexOneField { get; set;}

		[Unique (Name = IndexTwoName)]
		public int IndexTwoFieldFirstHalf { get; set;}
		[Unique (Name = IndexTwoName)]
		public int IndexTwoFieldSecondHallf { get; set;}

		[Indexed (Name = IndexThreeName, Unique = true)]
		public int IndexThreeField { get; set;}

		[Indexed (Name = IndexFourName, Unique = true)]
		public int IndexFourFieldFirstHalf { get; set;}
		[Indexed (Name = IndexFourName, Unique = true)]
		public int IndexFourFieldSecondHalf { get; set;}
	}

	public class IndexColumns {
		public int seqno { get; set;} 
		public int cid { get; set;} 
		public string name { get; set; } 
	}

	public class IndexInfo {
		public int seq { get; set;} 
		public string name { get; set;} 
		public bool unique { get; set;}
	}

	[Test]
	public void CreateUniqueIndexes ()
	{
		Database.CreateTable<TestClass>();
		var indexes = Database.Query<IndexInfo>($"PRAGMA INDEX_LIST (\"{nameof(TestClass)}\")");
		Assert.AreEqual (4, indexes.Count, "# of indexes");

		CheckIndex (indexes, IndexOneName, nameof(TestClass.IndexOneField));
		CheckIndex (indexes, IndexTwoName, nameof(TestClass.IndexTwoFieldFirstHalf), nameof(TestClass.IndexTwoFieldSecondHallf));
		CheckIndex (indexes, IndexThreeName, nameof(TestClass.IndexThreeField));
		CheckIndex (indexes, IndexFourName, nameof(TestClass.IndexFourFieldFirstHalf), nameof(TestClass.IndexFourFieldSecondHalf));
	}

	void CheckIndex (List<IndexInfo> indexes, string iname, params string [] columns)
	{
		if (columns == null || columns.Length == 0)
			throw new ArgumentException("Columns must be provided");

		var idx = indexes.SingleOrDefault (i => i.name == iname);
		Assert.IsNotNull (idx, $"Index {iname} not found");
		Assert.IsTrue(idx.unique, $"Index {iname} was not unique");

		var idx_columns = Database.Query<IndexColumns>($"PRAGMA INDEX_INFO (\"{iname}\")");
		Assert.AreEqual (columns.Length, idx_columns.Count, $"# of columns: expected {columns.Length}, got {idx_columns.Count}");

		foreach (var col in columns) {
			Assert.IsNotNull (idx_columns.SingleOrDefault (c => c.name == col), $"Column {col} not in index {idx.name}");
		}
	}
}