using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using static FourLambda.SQLite.ValueConverter;

namespace FourLambda.SQLite;

public class SQLiteDataReader : DbDataReader, IDataRecord
{
	private Sqlite3Statement statement;

	public bool IsActive { get; private set; }

	private string[] columnNames;
	private Dictionary<string, int> columnMapping = new();

	private SQLite3Native.ColType[] columnTypes;

	private readonly int _fieldCount;
	/// <inheritdoc />
	public override int FieldCount => statement != 0 || IsActive ? _fieldCount : -1;

	/// <inheritdoc />
	public override object this[int ordinal] => GetValue(ordinal);

	/// <inheritdoc />
	public override object this[string name] => GetValue(GetOrdinal(name));

	/// <inheritdoc />
	public override int RecordsAffected => -1;

	private bool _hasRows;
	/// <inheritdoc />
	public override bool HasRows => _hasRows;

	/// <inheritdoc />
	public override bool IsClosed => statement == 0;

	/// <inheritdoc />
	public override int Depth => IsActive ? 1 : -1;

	internal SQLiteDataReader(Sqlite3Statement statement)
	{
		this.statement = statement;

		_fieldCount = SQLite3Native.ColumnCount(statement);

		columnNames = new string[_fieldCount];
		columnTypes = new SQLite3Native.ColType[_fieldCount];

		for (int i = 0; i < _fieldCount; i++)
		{
			var columnName = SQLite3Native.ColumnName16(statement, i);

			columnNames[i] = columnName;
			columnMapping[columnName] = i;
		}
	}

	private void OrdinalCheck(int ordinal)
	{
		if (!IsActive)
			throw new InvalidOperationException("Reader is not active");

		if (ordinal < 0 || ordinal >= FieldCount)
			throw new ArgumentOutOfRangeException(nameof(ordinal), "No field with that index");
	}

	/// <inheritdoc />
	public override bool GetBoolean(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<bool>.Getter);

	/// <inheritdoc />
	public override byte GetByte(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<byte>.Getter);

	/// <inheritdoc />
	public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		OrdinalCheck(ordinal);

		if (buffer == null)
			throw new ArgumentNullException(nameof(buffer));

		var copyLength = Math.Min(length, buffer.Length - bufferOffset);
		var memory = GetMemory(ordinal, (int)dataOffset, copyLength);

		copyLength = Math.Min(copyLength, memory.Length);

		memory.CopyTo(buffer.AsSpan(bufferOffset, copyLength));

		return memory.Length;
	}

	public byte[] GetBytes(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<byte[]>.Getter);

	public ReadOnlySpan<byte> GetMemory(int ordinal, int offset, int length)
	{
		if (columnTypes[ordinal] != SQLite3Native.ColType.Blob)
			throw new InvalidCastException("Column is not a blob");

		if (length < 0)
			throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");

		if (length == 0)
			return default;

		int columnLength = SQLite3Native.ColumnBytes(statement, ordinal);

		if (columnLength < 0)
			throw new InvalidCastException("Column is not a blob");

		if (offset < 0 || offset >= columnLength)
			throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside of range of blob length");

		if (length + offset > columnLength)
			throw new ArgumentOutOfRangeException(nameof(offset), "Offset + length is outside of range of blob length");

		if (columnLength == 0)
			return default;


		var pointer = SQLite3Native.ColumnBlob(statement, ordinal);

		return MemoryMarshal.CreateReadOnlySpan(
			Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), pointer + offset),
			Math.Min(length, columnLength - offset));
	}

	/// <inheritdoc />
	public override char GetChar(int ordinal) => GetString(ordinal)[0];

	/// <inheritdoc />
	public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc />
	public override DateTime GetDateTime(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<DateTime>.Getter);

	/// <inheritdoc />
	public override decimal GetDecimal(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<decimal>.Getter);

	/// <inheritdoc />
	public override double GetDouble(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<double>.Getter);

	/// <inheritdoc />
	public override float GetFloat(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<float>.Getter);

	/// <inheritdoc />
	public override Guid GetGuid(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<Guid>.Getter);

	/// <inheritdoc />
	public override short GetInt16(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<short>.Getter);

	/// <inheritdoc />
	public override int GetInt32(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<int>.Getter);

	/// <inheritdoc />
	public override long GetInt64(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<long>.Getter);

	/// <inheritdoc />
	public override string GetString(int ordinal) => SafeReadFromStatement(ordinal, GenericConverterCache<string>.Getter);


	/// <inheritdoc />
	public override string GetName(int ordinal)
	{
		OrdinalCheck(ordinal);
		return columnNames[ordinal];
	}

	/// <inheritdoc />
	public override int GetOrdinal(string name)
	{
		if (columnMapping.TryGetValue(name, out var ordinal))
			return ordinal;

		throw new ArgumentOutOfRangeException(nameof(name), "No column with that name exists");
	}

	/// <inheritdoc />
	public override Type GetFieldType(int ordinal)
	{
		OrdinalCheck(ordinal);

		return columnTypes[ordinal] switch
		{
			SQLite3Native.ColType.Integer => typeof(long),
			SQLite3Native.ColType.Real => typeof(double),
			SQLite3Native.ColType.Text => typeof(string),
			SQLite3Native.ColType.Blob => typeof(byte[]),
			SQLite3Native.ColType.Null => typeof(DBNull),
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	/// <inheritdoc />
	public override string GetDataTypeName(int ordinal)
	{
		OrdinalCheck(ordinal);
		return columnTypes[ordinal].ToString();
	}

	/// <inheritdoc />
	public override object GetValue(int ordinal)
	{
		OrdinalCheck(ordinal);

		return columnTypes[ordinal] switch
		{
			SQLite3Native.ColType.Integer => GetInt64(ordinal),
			SQLite3Native.ColType.Real => GetDouble(ordinal),
			SQLite3Native.ColType.Text => GetString(ordinal),
			SQLite3Native.ColType.Blob => GetBytes(ordinal)!,
			SQLite3Native.ColType.Null => DBNull.Value,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	private T SafeReadFromStatement<T>(int ordinal, StatementGetterFunc<T> statementGetter)
	{
		OrdinalCheck(ordinal);

		var columnType = columnTypes[ordinal];

		if (columnType == SQLite3Native.ColType.Null)
			throw new InvalidCastException("Field is null");

		return statementGetter.Invoke(statement, ordinal, null, columnType)!;
	}

	private Dictionary<Type, IGenericConverterDefinition>? localConverters = null;
	public T GetValue<T>(int ordinal)
	{
		OrdinalCheck(ordinal);

		localConverters ??= new();

		IConverterDefinition<T> converter;

		if (localConverters.TryGetValue(typeof(T), out var boxedConverter))
		{
			converter = (IConverterDefinition<T>)boxedConverter;
		}
		else if (TryGetConverterDefinition(out converter))
		{
			localConverters.Add(typeof(T), converter);
		}
		else
		{
			throw new ArgumentException("Reader does not support type: " + typeof(T));
		}

		var colType = SQLite3Native.ColumnType(statement, ordinal);

		if (colType == SQLite3Native.ColType.Null)
			throw new InvalidCastException("Field is null");
		
		return converter.StatementGetter(statement, ordinal, null, colType)!;
	}

	/// <inheritdoc />
	public override int GetValues(object[] values)
	{
		if (!IsActive)
			throw new InvalidOperationException("Reader is not active");

		var copyCount = Math.Min(values.Length, FieldCount);

		for (int i = 0; i < copyCount; i++)
		{
			values[i] = GetValue(i);
		}

		return copyCount;
	}

	/// <inheritdoc />
	public override bool IsDBNull(int ordinal)
	{
		OrdinalCheck(ordinal);
		return columnTypes[ordinal] == SQLite3Native.ColType.Null;
	}

	/// <inheritdoc />
	public override bool NextResult() => Read();

	/// <inheritdoc />
	public override bool Read()
	{
		if (statement == 0)
			return false;

		if (SQLite3Native.Step(statement) == SQLite3Native.Result.Row)
		{
			IsActive = true;
			_hasRows = true;

			for (int i = 0; i < _fieldCount; i++)
				columnTypes[i] = SQLite3Native.ColumnType(statement, i);

			return true;
		}

		SQLite3Native.Finalize(statement);
		statement = 0;

		IsActive = false;

		return false;
	}

	/// <inheritdoc />
	public override Task<bool> ReadAsync(CancellationToken cancellationToken)
	{
		return Task.Run(Read, cancellationToken);
	}

	/// <inheritdoc />
	public override IEnumerator GetEnumerator()
	{
		while (Read())
			yield return this;
	}

	/// <inheritdoc />
	public override void Close() => Dispose(true);

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (statement != 0)
		{
			SQLite3Native.Finalize(statement);
			statement = 0;
		}

		GC.SuppressFinalize(this);
	}

	~SQLiteDataReader()
	{
		Dispose();
	}

	private static class GenericConverterCache<T>
	{
		public static readonly StatementGetterFunc<T> Getter = GetConverterDefinition<T>().StatementGetter;
	}
}