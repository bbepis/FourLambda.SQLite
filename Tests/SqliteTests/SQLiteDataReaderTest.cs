namespace FourLambda.SQLite.Tests;

[TestFixture]
public class SQLiteDataReaderTest : DBTestHarness
{
	public class DataRecord
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string? Name { get; set; }
		public int? Age { get; set; }
		public double? Score { get; set; }
		public bool? IsActive { get; set; }
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<DataRecord>();
		Database.Insert(new DataRecord { Name = "Alice", Age = 30, Score = 95.5, IsActive = true });
		Database.Insert(new DataRecord { Name = "Bob", Age = 25, Score = 87.3, IsActive = false });
		Database.Insert(new DataRecord { Name = "Charlie", Age = 35, Score = 92.1, IsActive = true });
	}

	[Test]
	public void Reader_FieldCount_ReturnsCorrectCount()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");

		Assert.That(reader.FieldCount, Is.GreaterThan(0));
		Assert.That(reader.FieldCount, Is.EqualTo(5));
	}

	[Test]
	public void Reader_HasRows_ReturnsTrueWhenDataExists()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Read();

		Assert.That(reader.HasRows, Is.True);
	}

	[Test]
	public void Reader_IsClosed_FalseWhenActive()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");

		Assert.That(reader.IsClosed, Is.False);
	}

	[Test]
	public void Reader_Read_AdvancesThroughRows()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id");

		int count = 0;
		while (reader.Read())
		{
			count++;
		}

		Assert.That(count, Is.EqualTo(3));
	}

	[Test]
	public void Reader_Read_ReturnsFalseAtEnd()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord LIMIT 1");

		Assert.That(reader.Read(), Is.True);
		Assert.That(reader.Read(), Is.False);
	}

	[Test]
	public void Reader_GetName_ReturnsCorrectColumnNames()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name, Age FROM DataRecord");
		reader.Read();

		Assert.That(reader.GetName(0), Is.EqualTo("Id"));
		Assert.That(reader.GetName(1), Is.EqualTo("Name"));
		Assert.That(reader.GetName(2), Is.EqualTo("Age"));
	}

	[Test]
	public void Reader_GetOrdinal_ReturnsCorrectIndex()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name, Age FROM DataRecord");
		reader.Read();

		Assert.That(reader.GetOrdinal("Id"), Is.EqualTo(0));
		Assert.That(reader.GetOrdinal("Name"), Is.EqualTo(1));
		Assert.That(reader.GetOrdinal("Age"), Is.EqualTo(2));
	}

	[Test]
	public void Reader_GetValue_ByOrdinal_ReturnsCorrectValues()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		Assert.That(reader.GetValue(0), Is.EqualTo(1L));
		Assert.That(reader.GetValue(1), Is.EqualTo("Alice"));
	}

	[Test]
	public void Reader_GetValue_ByName_ReturnsCorrectValues()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		Assert.That(reader["Id"], Is.EqualTo(1L));
		Assert.That(reader["Name"], Is.EqualTo("Alice"));
	}

	[Test]
	public void Reader_GetInt32_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		Assert.That(reader.GetInt32(0), Is.EqualTo(30));
	}

	[Test]
	public void Reader_GetInt64_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		Assert.That(reader.GetInt64(0), Is.EqualTo(30L));
	}

	[Test]
	public void Reader_GetString_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 25");
		reader.Read();

		Assert.That(reader.GetString(0), Is.EqualTo("Bob"));
	}

	[Test]
	public void Reader_GetDouble_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		Assert.That(reader.GetDouble(0), Is.EqualTo(95.5));
	}

	[Test]
	public void Reader_GetBoolean_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT IsActive FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		Assert.That(reader.GetBoolean(0), Is.True);
	}

	[Test]
	public void Reader_GetValues_PopulatesArray()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name, Age FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		var values = new object[3];
		int count = reader.GetValues(values);

		Assert.That(count, Is.EqualTo(3));
		Assert.That(values[0], Is.EqualTo(1L));
		Assert.That(values[1], Is.EqualTo("Alice"));
		Assert.That(values[2], Is.EqualTo(30L));
	}

	[Test]
	public void Reader_GetValues_PartialArray()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name, Age FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		var values = new object[2];
		int count = reader.GetValues(values);

		Assert.That(count, Is.EqualTo(2));
		Assert.That(values[0], Is.EqualTo(1L));
		Assert.That(values[1], Is.EqualTo("Alice"));
	}

	[Test]
	public void Reader_IsDBNull_ReturnsFalseForNonNull()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		Assert.That(reader.IsDBNull(0), Is.False);
	}

	[Test]
	public void Reader_IsDBNull_ReturnsTrueForNull()
	{
		Database.Execute("INSERT INTO DataRecord (Name, Age) VALUES (NULL, 99)");

		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 99");
		reader.Read();

		Assert.That(reader.IsDBNull(0), Is.True);
	}

	[Test]
	public void Reader_GetFieldType_ReturnsCorrectTypes()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name, Score FROM DataRecord");
		reader.Read();

		Assert.That(reader.GetFieldType(0), Is.EqualTo(typeof(long)));
		Assert.That(reader.GetFieldType(1), Is.EqualTo(typeof(string)));
		Assert.That(reader.GetFieldType(2), Is.EqualTo(typeof(double)));
	}

	[Test]
	public void Reader_GetDataTypeName_ReturnsCorrectTypeName()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord");
		reader.Read();

		Assert.That(reader.GetDataTypeName(0), Is.EqualTo("Integer"));
		Assert.That(reader.GetDataTypeName(1), Is.EqualTo("Text"));
	}

	[Test]
	public void Reader_RecordsAffected_ReturnsNegativeOne()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");

		Assert.That(reader.RecordsAffected, Is.EqualTo(-1));
	}

	[Test]
	public void Reader_Depth_ReturnsOneWhenActive()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Read();

		Assert.That(reader.Depth, Is.EqualTo(1));
	}

	[Test]
	public void Reader_IndexOf_GetValue_ByColumnIndex()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		var val = reader[0];
		Assert.That(val, Is.EqualTo(1L));
	}

	[Test]
	public void Reader_IndexOf_GetValue_ByColumnName()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		var val = reader["Name"];
		Assert.That(val, Is.EqualTo("Alice"));
	}

	[Test]
	public void Reader_GetOrdinal_ThrowsForNonExistentColumn()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetOrdinal("NonExistent"));
	}

	[Test]
	public void Reader_OrdinalCheck_ThrowsForNegativeIndex()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetName(-1));
	}

	[Test]
	public void Reader_OrdinalCheck_ThrowsForOutOfRangeIndex()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetName(reader.FieldCount));
	}

	[Test]
	public void Reader_Close_DisposesStatement()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Close();

		Assert.That(reader.IsClosed, Is.True);
	}

	[Test]
	public void Reader_ReadAfterClose_ReturnsFalse()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Close();

		Assert.That(reader.Read(), Is.False);
	}

	[Test]
	public void Reader_GetBoolean_ThrowsWhenNotActive()
	{
		var reader = Database.ExecuteReader("SELECT IsActive FROM DataRecord LIMIT 1");
		reader.Read();
		reader.Read();
		reader.Dispose();

		Assert.Throws<InvalidOperationException>(() => reader.GetBoolean(0));
	}

	[Test]
	public void Reader_GetGenericValue_Int32()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<int>(0);
		Assert.That(val, Is.EqualTo(30));
	}

	[Test]
	public void Reader_GetGenericValue_String()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 25");
		reader.Read();

		var val = reader.GetValue<string>(0);
		Assert.That(val, Is.EqualTo("Bob"));
	}

	[Test]
	public void Reader_GetGenericValue_Double()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<double>(0);
		Assert.That(val, Is.EqualTo(95.5));
	}

	[Test]
	public void Reader_GetGenericValue_Bool()
	{
		using var reader = Database.ExecuteReader("SELECT IsActive FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<bool>(0);
		Assert.That(val, Is.True);
	}

	[Test]
	public void Reader_GetGenericValue_ThrowsForNull()
	{
		Database.Execute("INSERT INTO DataRecord (Name, Age) VALUES (NULL, 88)");

		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 88");
		reader.Read();

		Assert.Throws<InvalidCastException>(() => reader.GetValue<string>(0));
	}

	[Test]
	public void Reader_GetGenericValue_ThrowsForUnsupportedType()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord LIMIT 1");
		reader.Read();

		Assert.Throws<ArgumentException>(() => reader.GetValue<char[]>(0));
	}

	[Test]
	public void Reader_Enumerator_IteratesRows()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id");

		var names = new List<string>();
		foreach (var _ in reader)
		{
			names.Add(reader.GetString(1));
		}

		Assert.That(names, Is.EquivalentTo(new[] { "Alice", "Bob", "Charlie" }));
	}

	[Test]
	public void Reader_EmptyResult_ReadReturnsFalse()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord WHERE Name = 'NonExistent'");

		Assert.That(reader.Read(), Is.False);
	}

	[Test]
	public void Reader_ActiveProperty_BecomesFalseAfterReadExhaustion()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord LIMIT 1");
		reader.Read();

		Assert.That(reader.IsActive, Is.True);

		reader.Read();
		Assert.That(reader.IsActive, Is.False);
	}

	[Test]
	public void Reader_GetByte_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT CAST(Age AS INTEGER) FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetByte(0);
		Assert.That(val, Is.EqualTo((byte)30));
	}

	[Test]
	public void Reader_GetInt16_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetInt16(0);
		Assert.That(val, Is.EqualTo((short)30));
	}

	[Test]
	public void Reader_GetDecimal_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetDecimal(0);
		Assert.That(val, Is.EqualTo((decimal)95.5));
	}

	[Test]
	public void Reader_GetFloat_ReturnsCorrectValue()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetFloat(0);
		Assert.That(val, Is.EqualTo((float)95.5).Within(0.001));
	}

	public class BlobRecord
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public byte[] Data { get; set; }
	}

	[Test]
	public void Reader_GetBytes_ReturnsCorrectData()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3, 4, 5 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var buffer = new byte[10];
		long bytesRead = reader.GetBytes(0, 0, buffer, 0, 5);

		Assert.That(bytesRead, Is.EqualTo(5));
		Assert.That(buffer.Take(5).ToArray(), Is.EqualTo(data));
	}

	[Test]
	public void Reader_GetMemory_ReturnsReadOnlySpan()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 10, 20, 30, 40, 50 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var memory = reader.GetMemory(0, 0, 3);

		Assert.That(memory.Length, Is.EqualTo(3));
		Assert.That(memory[0], Is.EqualTo(10));
		Assert.That(memory[1], Is.EqualTo(20));
		Assert.That(memory[2], Is.EqualTo(30));
	}

	[Test]
	public void Reader_GetMemory_ThrowsForNonBlob()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord LIMIT 1");
		reader.Read();

		Assert.Throws<InvalidCastException>(() => reader.GetMemory(0, 0, 1));
	}

	[Test]
	public void Reader_GetMemory_ThrowsForNegativeLength()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetMemory(0, 0, -1));
	}

	[Test]
	public void Reader_GetMemory_ThrowsForOutOfRangeOffset()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetMemory(0, 10, 1));
	}

	[Test]
	public void Reader_GetMemory_ThrowsWhenOffsetPlusLengthExceeds()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetMemory(0, 2, 5));
	}

	[Test]
	public void Reader_GetMemory_ReturnsDefaultForZeroLength()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var memory = reader.GetMemory(0, 0, 0);

		Assert.That(memory.Length, Is.EqualTo(0));
	}

	public class DateTimeRecord
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	[Test]
	public void Reader_GetDateTime_ReturnsCorrectValue()
	{
		Database.CreateTable<DateTimeRecord>();
		var dt = new DateTime(2024, 6, 15, 10, 30, 0);
		Database.Insert(new DateTimeRecord { CreatedAt = dt });

		using var reader = Database.ExecuteReader("SELECT CreatedAt FROM DateTimeRecord WHERE Id = 1");
		reader.Read();

		var val = reader.GetDateTime(0);
		Assert.That(val.Year, Is.EqualTo(2024));
		Assert.That(val.Month, Is.EqualTo(6));
		Assert.That(val.Day, Is.EqualTo(15));
	}

	public class GuidTestRecord
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public Guid UniqueId { get; set; }
	}

	[Test]
	public void Reader_GetGuid_ReturnsCorrectValue()
	{
		Database.CreateTable<GuidTestRecord>();
		var guid = Guid.NewGuid();
		Database.Insert(new GuidTestRecord { UniqueId = guid });

		using var reader = Database.ExecuteReader("SELECT UniqueId FROM GuidTestRecord WHERE Id = 1");
		reader.Read();

		var val = reader.GetGuid(0);
		Assert.That(val, Is.EqualTo(guid));
	}

	[Test]
	public void Reader_GetChar_ReturnsFirstChar()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 30");
		reader.Read();

		var val = reader.GetChar(0);
		Assert.That(val, Is.EqualTo('A'));
	}

	[Test]
	public void Reader_NextResult_DelegatesToRead()
	{
		using var reader = Database.ExecuteReader("SELECT * FROM DataRecord LIMIT 1");

		Assert.That(reader.NextResult(), Is.True);
	}

	[Test]
	public void Reader_Dispose_FinalizesStatement()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord");
		reader.Dispose();

		Assert.That(reader.IsClosed, Is.True);
	}

	[Test]
	public void Reader_FieldCount_NegativeWhenNotActive()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord LIMIT 1");
		reader.Read();
		reader.Read();
		reader.Dispose();

		Assert.That(reader.FieldCount, Is.EqualTo(-1));
	}

	[Test]
	public void Reader_Depth_NegativeWhenNotActive()
	{
		var reader = Database.ExecuteReader("SELECT * FROM DataRecord LIMIT 1");
		reader.Read();
		reader.Read();
		reader.Dispose();

		Assert.That(reader.Depth, Is.EqualTo(-1));
	}

	[Test]
	public void Reader_GetBytes_ThrowsForNullBuffer()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		Assert.Throws<ArgumentNullException>(() => reader.GetBytes(0, 0, null, 0, 3));
	}

	[Test]
	public void Reader_GetBytes_CopiesToOffset()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var buffer = new byte[5];
		long bytesRead = reader.GetBytes(0, 0, buffer, 2, 3);

		Assert.That(bytesRead, Is.EqualTo(3));
		Assert.That(buffer[2], Is.EqualTo(1));
		Assert.That(buffer[3], Is.EqualTo(2));
		Assert.That(buffer[4], Is.EqualTo(3));
	}

	[Test]
	public void Reader_GetBytes_WithOffset()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 10, 20, 30, 40, 50 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var buffer = new byte[5];
		long bytesRead = reader.GetBytes(0, 2, buffer, 0, 3);

		Assert.That(bytesRead, Is.EqualTo(3));
		Assert.That(buffer[0], Is.EqualTo(30));
		Assert.That(buffer[1], Is.EqualTo(40));
		Assert.That(buffer[2], Is.EqualTo(50));
	}

	[Test]
	public void Reader_GetFieldType_Blob()
	{
		Database.CreateTable<BlobRecord>();
		var data = new byte[] { 1, 2, 3 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", data);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		Assert.That(reader.GetFieldType(0), Is.EqualTo(typeof(byte[])));
	}

	[Test]
	public void Reader_GetValue_Blob_ReturnsByteArray()
	{
		Database.CreateTable<BlobRecord>();
		var expected = new byte[] { 7, 8, 9 };
		Database.Execute("INSERT INTO BlobRecord (Data) VALUES (?)", expected);

		using var reader = Database.ExecuteReader("SELECT Data FROM BlobRecord WHERE Id = 1");
		reader.Read();

		var val = (byte[])reader.GetValue(0);
		Assert.That(val, Is.EqualTo(expected));
	}

	[Test]
	public void Reader_GetFieldType_Null_ReturnsDBNull()
	{
		Database.Execute("INSERT INTO DataRecord (Name, Age) VALUES (NULL, 77)");

		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 77");
		reader.Read();

		Assert.That(reader.GetFieldType(0), Is.EqualTo(typeof(DBNull)));
	}

	[Test]
	public void Reader_GetValue_Null_ReturnsDBNull()
	{
		Database.Execute("INSERT INTO DataRecord (Name, Age) VALUES (NULL, 66)");

		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age = 66");
		reader.Read();

		Assert.That(reader.GetValue(0), Is.EqualTo(DBNull.Value));
	}

	[Test]
	public void Reader_GetValues_EmptyArray()
	{
		using var reader = Database.ExecuteReader("SELECT Id, Name FROM DataRecord ORDER BY Id LIMIT 1");
		reader.Read();

		var values = new object[0];
		int count = reader.GetValues(values);

		Assert.That(count, Is.EqualTo(0));
	}

	[Test]
	public void Reader_GetGenericValue_Int64()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<long>(0);
		Assert.That(val, Is.EqualTo(30L));
	}

	[Test]
	public void Reader_GetGenericValue_Byte()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<byte>(0);
		Assert.That(val, Is.EqualTo((byte)30));
	}

	[Test]
	public void Reader_GetGenericValue_Int16()
	{
		using var reader = Database.ExecuteReader("SELECT Age FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<short>(0);
		Assert.That(val, Is.EqualTo((short)30));
	}

	[Test]
	public void Reader_GetGenericValue_Float()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<float>(0);
		Assert.That(val, Is.EqualTo((float)95.5).Within(0.001f));
	}

	[Test]
	public void Reader_GetGenericValue_Decimal()
	{
		using var reader = Database.ExecuteReader("SELECT Score FROM DataRecord WHERE Name = 'Alice'");
		reader.Read();

		var val = reader.GetValue<decimal>(0);
		Assert.That(val, Is.EqualTo((decimal)95.5));
	}

	[Test]
	public void Reader_Parameters_Query()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age > ?", 28);

		var names = new List<string>();
		while (reader.Read())
		{
			names.Add(reader.GetString(0));
		}

		Assert.That(names, Is.EquivalentTo(new[] { "Alice", "Charlie" }));
	}

	[Test]
	public async Task Reader_Async_Query()
	{
		using var reader = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Age > ?", 28);

		var names = new List<string>();
		while (await reader.ReadAsync())
		{
			names.Add(reader.GetString(0));
		}

		Assert.That(names, Is.EquivalentTo(new[] { "Alice", "Charlie" }));
	}

	[Test]
	public void Reader_MultipleReaders_SameConnection()
	{
		using var reader1 = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Id = 1");
		using var reader2 = Database.ExecuteReader("SELECT Name FROM DataRecord WHERE Id = 2");

		reader1.Read();
		reader2.Read();

		Assert.That(reader1.GetString(0), Is.EqualTo("Alice"));
		Assert.That(reader2.GetString(0), Is.EqualTo("Bob"));
	}
}
