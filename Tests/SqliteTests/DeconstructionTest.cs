namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DeconstructionTest : DBTestHarness
{
	private readonly (int Value, double DoubleValue)[] _records =
	[
		(42, 0.5)
	];

	protected override void InitializeDatabase()
	{
		Database.Execute("create table G(Value integer not null, DoubleValue real not null)");

		for (var i = 0; i < _records.Length; i++)
			Database.Execute("insert into G(Value, DoubleValue) values (?, ?)",
				_records[i].Value, _records[i].DoubleValue);
	}

	private class GenericObject
	{
		public int Value { get; set; }
		public double DoubleValue { get; set; }
	}

	[Test]
	public void DeconstructToClass()
	{
		var r = Database.Query<GenericObject>("select * from G");

		Assert.AreEqual(_records.Length, r.Count);
		Assert.AreEqual(_records[0].Value, r[0].Value);
		Assert.AreEqual(_records[0].DoubleValue, r[0].DoubleValue);
	}

	#region Issue #1007

	[Test]
	public void DeconstructToValueTuple()
	{
		var r = Database.Query<(int Value, double Walue)>("select * from G");

		Assert.AreEqual(_records.Length, r.Count);
		Assert.AreEqual(_records[0].Value, r[0].Value);
		Assert.AreEqual(_records[0].DoubleValue, r[0].Walue);
	}

	#endregion
}