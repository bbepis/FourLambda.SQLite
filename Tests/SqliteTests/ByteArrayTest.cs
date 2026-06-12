namespace FourLambda.SQLite.Tests;

[TestFixture]
public class ByteArrayTest : DBTestHarness
{
	public class ByteArrayClass
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public byte[] bytes { get; set; }

		public void AssertEquals(ByteArrayClass other)
		{
			Assert.AreEqual(other.ID, ID);
			if (other.bytes == null || bytes == null) {
				Assert.IsNull (other.bytes);
				Assert.IsNull (bytes);
			}
			else {
				Assert.AreEqual(other.bytes.Length, bytes.Length);
				for (var i = 0; i < bytes.Length; i++) {
					Assert.AreEqual(other.bytes[i], bytes[i]);
				}
			}
		}
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<ByteArrayClass>();
	}

	[Test]
	[Description("Create objects with various byte arrays and check they can be stored and retrieved correctly")]
	public void ByteArrays()
	{
		//Byte Arrays for comparisson
		ByteArrayClass[] byteArrays =
		[
			new() { bytes = [1, 2, 3, 4, 250, 252, 253, 254, 255] }, //Range check
			new() { bytes = [0] }, //null bytes need to be handled correctly
			new() { bytes = [0, 0] },
			new() { bytes = [0, 1, 0] },
			new() { bytes = [1, 0, 1] },
			new() { bytes = [] }, //Empty byte array should stay empty (and not become null)
			new() { bytes = null } //Null should be supported
		];

		//Insert all of the ByteArrayClass
		foreach (ByteArrayClass b in byteArrays)
			Database.Insert(b);

		//Get them back out
		ByteArrayClass[] fetchedByteArrays = Database.Table<ByteArrayClass>().OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(fetchedByteArrays.Length, byteArrays.Length);
		//Check they are the same
		for (int i = 0; i < byteArrays.Length; i++)
		{
			byteArrays[i].AssertEquals(fetchedByteArrays[i]);
		}
	}

	[Test]
	[Description("Uses a byte array to find a record")]
	public void ByteArrayWhere()
	{
		//Byte Arrays for comparison
		ByteArrayClass[] byteArrays =
		[
			new() { bytes = [1, 2, 3, 4, 250, 252, 253, 254, 255] }, //Range check
			new() { bytes = [0] }, //null bytes need to be handled correctly
			new() { bytes = [0, 0] },
			new() { bytes = [0, 1, 0] },
			new() { bytes = [1, 0, 1] },
			new() { bytes = [] }, //Empty byte array should stay empty (and not become null)
			new() { bytes = null } //Null should be supported
		];

		byte[] criterion = [1, 0, 1];

		//Insert all the ByteArrayClasses
		int id = 0;
		foreach (ByteArrayClass b in byteArrays)
		{
			Database.Insert(b);
			if (b.bytes != null && criterion.SequenceEqual<byte>(b.bytes))
				id = b.ID;
		}
		Assert.AreNotEqual(0, id, "An ID wasn't set");

		//Get it back out
		ByteArrayClass fetchedByteArray = Database.Table<ByteArrayClass>().First(x => x.bytes == criterion);
		Assert.IsNotNull(fetchedByteArray);
		//Check they are the same
		Assert.AreEqual(id, fetchedByteArray.ID);
	}

	[Test]
	[Description("Uses a null byte array to find a record")]
	public void ByteArrayWhereNull()
	{
		//Byte Arrays for comparison
		ByteArrayClass[] byteArrays =
		[
			new() { bytes = [1, 2, 3, 4, 250, 252, 253, 254, 255] }, //Range check
			new() { bytes = [0] }, //null bytes need to be handled correctly
			new() { bytes = [0, 0] },
			new() { bytes = [0, 1, 0] },
			new() { bytes = [1, 0, 1] },
			new() { bytes = [] }, //Empty byte array should stay empty (and not become null)
			new() { bytes = null } //Null should be supported
		];

		byte[] criterion = null;

		//Insert all the ByteArrayClasses
		int id = 0;
		foreach (ByteArrayClass b in byteArrays)
		{
			Database.Insert(b);
			if (b.bytes == null)
				id = b.ID;
		}
		Assert.AreNotEqual(0, id, "An ID wasn't set");

		//Get it back out
		ByteArrayClass fetchedByteArray = Database.Table<ByteArrayClass>().First(x => x.bytes == criterion);

		Assert.IsNotNull(fetchedByteArray);
		//Check they are the same
		Assert.AreEqual(id, fetchedByteArray.ID);
	}

	[Test]
	[Description("Create a large byte array and check it can be stored and retrieved correctly")]
	public void LargeByteArray()
	{
		const int byteArraySize = 1024 * 1024;
		byte[] bytes = new byte[byteArraySize];
		for (int i = 0; i < byteArraySize; i++)
			bytes[i] = (byte)(i % 256);

		ByteArrayClass byteArray = new ByteArrayClass() { bytes = bytes };

		//Insert the ByteArrayClass
		Database.Insert(byteArray);

		//Get it back out
		ByteArrayClass[] fetchedByteArrays = Database.Table<ByteArrayClass>().ToArray();

		Assert.AreEqual(fetchedByteArrays.Length, 1);

		//Check they are the same
		byteArray.AssertEquals(fetchedByteArrays[0]);
	}
}