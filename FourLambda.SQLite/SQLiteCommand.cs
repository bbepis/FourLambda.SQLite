using System.Runtime.CompilerServices;

namespace FourLambda.SQLite;

public class SQLiteCommand(SQLiteConnection conn)
{
	private readonly List<Binding> _bindings = new();

	public string CommandText { get; set; } = "";

	public int ExecuteNonQuery()
	{
		if (conn.Trace)
		{
			conn.Tracer?.Invoke("Executing: " + this);
		}

		var r = SQLite3Native.Result.OK;
		var stmt = Prepare();
		r = SQLite3Native.Step(stmt);
		Finalize(stmt);
		if (r == SQLite3Native.Result.Done)
		{
			int rowsAffected = SQLite3Native.Changes(conn.Handle);
			return rowsAffected;
		}
		else if (r == SQLite3Native.Result.Error)
		{
			string msg = SQLite3Native.GetErrmsg(conn.Handle);
			throw SQLiteException.New(r, msg);
		}
		else if (r == SQLite3Native.Result.Constraint)
		{
			if (SQLite3Native.ExtendedErrCode(conn.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
			{
				throw NotNullConstraintViolationException.New(r, SQLite3Native.GetErrmsg(conn.Handle));
			}
		}

		throw SQLiteException.New(r, SQLite3Native.GetErrmsg(conn.Handle));
	}

	public IEnumerable<T> ExecuteDeferredQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		if (typeof(T).Name.StartsWith("ValueTuple`"))
			return ExecuteDeferredQueryAsValueTuple<T>();

		return ExecuteDeferredQuery<T>(conn.GetMapping(typeof(T)));
	}

	public List<T> ExecuteQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		if (typeof(T).Name.StartsWith("ValueTuple`"))
			return ExecuteDeferredQueryAsValueTuple<T>().ToList();

		return ExecuteDeferredQuery<T>(conn.GetMapping(typeof(T))).ToList();
	}

	public List<T> ExecuteQuery<T>(TableMapping map)
	{
		return ExecuteDeferredQuery<T>(map).ToList();
	}


	public IEnumerable<T> ExecuteDeferredQuery<T>(TableMapping map)
	{
		if (conn.Trace)
		{
			conn.Tracer?.Invoke("Executing Query: " + this);
		}

		var stmt = Prepare();
		try
		{
			var cols = new TableColumn[SQLite3Native.ColumnCount(stmt)];
			var fastColumnSetters = new Action<object, Sqlite3Statement, int>[SQLite3Native.ColumnCount(stmt)];

			MethodInfo getSetter = null;
			if (typeof(T) != map.MappedType)
			{
				// The runtime feature switch must be on a separate 'if' branch on its own,
				// or the analyzer might not be able to correctly follow the program flow.
				if (!RuntimeFeature.IsDynamicCodeSupported)
				{
					if (map.MappedType.IsValueType)
					{
						getSetter = null;
					}
					else
					{
						getSetter = FastColumnSetter.GetFastSetterMethodInfoUnsafe(map.MappedType);
					}
				}
				else
				{
					getSetter = FastColumnSetter.GetFastSetterMethodInfoUnsafe(map.MappedType);
				}
				getSetter = FastColumnSetter.GetFastSetterMethodInfoUnsafe(map.MappedType);
			}

			for (int i = 0; i < cols.Length; i++)
			{
				var name = SQLite3Native.ColumnName16(stmt, i);
				cols[i] = map.FindColumn(name);
				if (cols[i] != null)
					if (getSetter != null)
					{
						fastColumnSetters[i] = (Action<object, Sqlite3Statement, int>)getSetter.Invoke(null, new object[] { conn, cols[i] });
					}
					else
					{
						fastColumnSetters[i] = FastColumnSetter.GetFastSetter<T>(conn, cols[i]);
					}
			}

			while (SQLite3Native.Step(stmt) == SQLite3Native.Result.Row)
			{
				var obj = Activator.CreateInstance(map.MappedType);
				for (int i = 0; i < cols.Length; i++)
				{
					if (cols[i] == null)
						continue;

					if (fastColumnSetters[i] != null)
					{
						fastColumnSetters[i].Invoke(obj, stmt, i);
					}
					else
					{
						var colType = SQLite3Native.ColumnType(stmt, i);
						var val = ReadCol(stmt, i, colType, cols[i].ColumnType, cols[i]);
						cols[i].SetValue(obj, val);
					}
				}

				yield return (T)obj;
			}
		}
		finally
		{
			SQLite3Native.Finalize(stmt);
		}
	}

	private IEnumerable<T> ExecuteDeferredQueryAsValueTuple<T>()
	{
		if (conn.Trace)
		{
			conn.Tracer?.Invoke("Executing Query: " + this);
		}

		var fields = typeof(T).GetFields();

		// https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest
		if (fields.Length > 7)
			throw new NotSupportedException("ValueTuple with more than 7 members not supported due to nesting; see https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest");

		var stmt = Prepare();
		try
		{
			while (SQLite3Native.Step(stmt) == SQLite3Native.Result.Row)
			{
				var obj = (object)Activator.CreateInstance<T>();

				for (int i = 0; i < fields.Length; i++)
				{
					var colType = SQLite3Native.ColumnType(stmt, i);
					var val = ReadCol(stmt, i, colType, fields[i].FieldType, null);
					fields[i].SetValue(obj, val);
				}

				yield return (T)obj;
			}
		}
		finally
		{
			SQLite3Native.Finalize(stmt);
		}
	}

	public T? ExecuteScalar<T>()
	{
		if (conn.Trace)
		{
			conn.Tracer?.Invoke("Executing Query: " + this);
		}

		T? val = default(T);

		var stmt = Prepare();

		try
		{
			var r = SQLite3Native.Step(stmt);
			if (r == SQLite3Native.Result.Row)
			{
				var colType = SQLite3Native.ColumnType(stmt, 0);
				var colval = ReadCol(stmt, 0, colType, typeof(T), null);
				if (colval != null)
				{
					val = (T)colval;
				}
			}
			else if (r == SQLite3Native.Result.Done)
			{
			}
			else
			{
				throw SQLiteException.New(r, SQLite3Native.GetErrmsg(conn.Handle));
			}
		}
		finally
		{
			Finalize(stmt);
		}

		return val;
	}

	public IEnumerable<T?> ExecuteQueryScalars<T>()
	{
		if (conn.Trace)
		{
			conn.Tracer?.Invoke("Executing Query: " + this);
		}
		var stmt = Prepare();
		try
		{
			if (SQLite3Native.ColumnCount(stmt) < 1)
			{
				throw new InvalidOperationException("QueryScalars should return at least one column");
			}
			while (SQLite3Native.Step(stmt) == SQLite3Native.Result.Row)
			{
				var colType = SQLite3Native.ColumnType(stmt, 0);
				var val = ReadCol(stmt, 0, colType, typeof(T), null);
				if (val == null)
				{
					yield return default(T);
				}
				else
				{
					yield return (T)val;
				}
			}
		}
		finally
		{
			Finalize(stmt);
		}
	}

	public void Bind(string name, object val)
	{
		_bindings.Add(new Binding
		{
			Name = name,
			Value = val
		});
	}

	public void Bind(object val)
	{
		Bind(null, val);
	}

	public override string ToString()
	{
		var parts = new string[1 + _bindings.Count];
		parts[0] = CommandText;
		var i = 1;
		foreach (var b in _bindings)
		{
			parts[i] = $"  {i - 1}: {b.Value}";
			i++;
		}
		return string.Join(Environment.NewLine, parts);
	}

	Sqlite3Statement Prepare()
	{
		var stmt = SQLite3Native.Prepare2(conn.Handle, CommandText);
		BindAll(stmt);
		return stmt;
	}

	void Finalize(Sqlite3Statement stmt)
	{
		SQLite3Native.Finalize(stmt);
	}

	void BindAll(Sqlite3Statement stmt)
	{
		int nextIdx = 1;
		foreach (var b in _bindings)
		{
			if (b.Name != null)
			{
				b.Index = SQLite3Native.BindParameterIndex(stmt, b.Name);
			}
			else
			{
				b.Index = nextIdx++;
			}

			BindParameter(stmt, b.Index, b.Value);
		}
	}

	static IntPtr NegativePointer = new IntPtr(-1);

	internal static void BindParameter(Sqlite3Statement stmt, int index, object? value)
	{
		QueryArgument? queryArgument = value as QueryArgument;

		if (queryArgument != null)
		{ 
			value = queryArgument!.Value;
		}

		if (value == null)
		{
			SQLite3Native.BindNull(stmt, index);
		}
		else
		{
			if (value is Int32)
			{
				SQLite3Native.BindInt(stmt, index, (int)value);
			}
			else if (value is string)
			{
				SQLite3Native.BindText(stmt, index, (string)value, -1, NegativePointer);
			}
			else if (value is Byte || value is UInt16 || value is SByte || value is Int16)
			{
				SQLite3Native.BindInt(stmt, index, Convert.ToInt32(value));
			}
			else if (value is bool)
			{
				SQLite3Native.BindInt(stmt, index, (bool)value ? 1 : 0);
			}
			else if (value is UInt32 || value is Int64 || value is UInt64)
			{
				SQLite3Native.BindInt64(stmt, index, Convert.ToInt64(value));
			}
			else if (value is Single || value is Double || value is Decimal)
			{
				SQLite3Native.BindDouble(stmt, index, Convert.ToDouble(value));
			}
			else if (value is TimeSpan timespanValue)
			{
				string? stringFormat = null;

				if (queryArgument?.AgainstColumn?.StoreAsText == true)
					stringFormat = queryArgument.AgainstColumn.StoreAsTextFormat ?? "c";

				if (stringFormat != null)
					SQLite3Native.BindText(stmt, index, timespanValue.ToString(stringFormat), -1, NegativePointer);
				else
					SQLite3Native.BindInt64(stmt, index, timespanValue.Ticks);
			}
			else if (value is DateTime datetimeValue)
			{
				string? stringFormat = null;

				if (queryArgument?.AgainstColumn?.StoreAsText == true)
					stringFormat = queryArgument.AgainstColumn.StoreAsTextFormat ?? "o";

				if (stringFormat != null)
					SQLite3Native.BindText(stmt, index, datetimeValue.ToString(stringFormat), -1, NegativePointer);
				else
					SQLite3Native.BindInt64(stmt, index, datetimeValue.Ticks);
			}
			else if (value is DateTimeOffset datetimeOffsetValue)
			{
				string? stringFormat = null;

				if (queryArgument?.AgainstColumn?.StoreAsText == true)
					stringFormat = queryArgument.AgainstColumn.StoreAsTextFormat ?? "o";

				if (stringFormat != null)
					SQLite3Native.BindText(stmt, index, datetimeOffsetValue.ToString(stringFormat), -1, NegativePointer);
				else
					SQLite3Native.BindInt64(stmt, index, datetimeOffsetValue.UtcTicks);
			}
			else if (value is DateOnly dateOnlyValue)
			{
				string? stringFormat = null;

				if (queryArgument?.AgainstColumn?.StoreAsText == true)
					stringFormat = queryArgument.AgainstColumn.StoreAsTextFormat ?? "o";

				if (stringFormat != null)
					SQLite3Native.BindText(stmt, index, dateOnlyValue.ToString(stringFormat), -1, NegativePointer);
				else
					SQLite3Native.BindInt64(stmt, index, dateOnlyValue.ToDateTime(TimeOnly.MinValue).Ticks);
			}
			else if (value is TimeOnly timeOnlyValue)
			{
				string? stringFormat = null;

				if (queryArgument?.AgainstColumn?.StoreAsText == true)
					stringFormat = queryArgument.AgainstColumn.StoreAsTextFormat ?? "o";

				if (stringFormat != null)
					SQLite3Native.BindText(stmt, index, timeOnlyValue.ToString(stringFormat), -1, NegativePointer);
				else
					SQLite3Native.BindInt64(stmt, index, timeOnlyValue.Ticks);
			}
			else if (value is byte[])
			{
				SQLite3Native.BindBlob(stmt, index, (byte[])value, ((byte[])value).Length, NegativePointer);
			}
			else if (value is Guid)
			{
				SQLite3Native.BindText(stmt, index, ((Guid)value).ToString(), 72, NegativePointer);
			}
			else
			{
				// Now we could possibly get an enum, retrieve cached info
				var valueType = value.GetType();

				if (valueType.IsEnum)
				{
					var enumIntValue = Convert.ToInt64(value);
					SQLite3Native.BindInt64(stmt, index, enumIntValue);
				}
				else
				{
					throw new NotSupportedException("Cannot store type: " + Orm.GetType(value));
				}
			}
		}
	}

	class Binding
	{
		public string Name { get; set; }

		public object Value { get; set; }

		public int Index { get; set; }
	}

	object? ReadCol(Sqlite3Statement stmt, int index, SQLite3Native.ColType type, Type clrType, TableColumn? column)
	{
		if (type == SQLite3Native.ColType.Null)
		{
			return null;
		}
		else
		{
			var clrTypeInfo = clrType.GetTypeInfo();
			if (clrTypeInfo.IsGenericType && clrTypeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				clrType = clrTypeInfo.GenericTypeArguments[0];
				clrTypeInfo = clrType.GetTypeInfo();
			}

			if (clrType == typeof(string))
			{
				return SQLite3Native.ColumnString(stmt, index);
			}
			else if (clrType == typeof(Int32))
			{
				return (int)SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(bool))
			{
				return SQLite3Native.ColumnInt(stmt, index) == 1;
			}
			else if (clrType == typeof(double))
			{
				return SQLite3Native.ColumnDouble(stmt, index);
			}
			else if (clrType == typeof(float))
			{
				return (float)SQLite3Native.ColumnDouble(stmt, index);
			}
			else if (clrType == typeof(TimeSpan))
			{
				if (type == SQLite3Native.ColType.Integer)
				{
					return new TimeSpan(SQLite3Native.ColumnInt64(stmt, index));
				}
				else
				{
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column?.StoreAsTextFormat != null && TimeSpan.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, TimeSpanStyles.None, out var resultTime))
						return resultTime;

					if (TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out resultTime))
						return resultTime;

					return TimeSpan.Parse(text);
				}
			}
			else if (clrType == typeof(DateTime))
			{
				if (type == SQLite3Native.ColType.Integer)
				{
					return new DateTime(SQLite3Native.ColumnInt64(stmt, index));
				}
				else
				{
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column?.StoreAsTextFormat != null && DateTime.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return DateTime.Parse(text);
				}
			}
			else if (clrType == typeof(DateTimeOffset))
			{
				if (type == SQLite3Native.ColType.Integer)
				{
					return new DateTimeOffset(SQLite3Native.ColumnInt64(stmt, index), TimeSpan.Zero);
				}
				else
				{
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column?.StoreAsTextFormat != null && DateTimeOffset.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return DateTimeOffset.Parse(text);
				}
			}
			else if (clrType == typeof(DateOnly))
			{
				if (type == SQLite3Native.ColType.Integer)
				{
					return DateOnly.FromDateTime(new DateTime(SQLite3Native.ColumnInt64(stmt, index)));
				}
				else
				{
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column?.StoreAsTextFormat != null && DateOnly.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return DateOnly.Parse(text);
				}
			}
			else if (clrType == typeof(TimeOnly))
			{
				if (type == SQLite3Native.ColType.Integer)
				{
					return new TimeOnly(SQLite3Native.ColumnInt64(stmt, index));
				}
				else
				{
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column?.StoreAsTextFormat != null && TimeOnly.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return TimeOnly.Parse(text);
				}
			}
			else if (clrTypeInfo.IsEnum)
			{
				if (type == SQLite3Native.ColType.Text)
				{
					var value = SQLite3Native.ColumnString(stmt, index);
					return Enum.Parse(clrType, value.ToString(), true);
				}
				else
					return SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(Int64))
			{
				return SQLite3Native.ColumnInt64(stmt, index);
			}
			else if (clrType == typeof(UInt64))
			{
				return (ulong)SQLite3Native.ColumnInt64(stmt, index);
			}
			else if (clrType == typeof(UInt32))
			{
				return (uint)SQLite3Native.ColumnInt64(stmt, index);
			}
			else if (clrType == typeof(decimal))
			{
				return (decimal)SQLite3Native.ColumnDouble(stmt, index);
			}
			else if (clrType == typeof(Byte))
			{
				return (byte)SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(UInt16))
			{
				return (ushort)SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(Int16))
			{
				return (short)SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(sbyte))
			{
				return (sbyte)SQLite3Native.ColumnInt(stmt, index);
			}
			else if (clrType == typeof(byte[]))
			{
				return SQLite3Native.ColumnByteArray(stmt, index);
			}
			else if (clrType == typeof(Guid))
			{
				var text = SQLite3Native.ColumnString(stmt, index);
				return new Guid(text);
			}
			else
			{
				throw new NotSupportedException("Don't know how to read " + clrType);
			}
		}
	}
}



/// <summary>
/// Since the insert never changed, we only need to prepare once.
/// </summary>
class PreparedSqlLiteInsertCommand : IDisposable
{
	bool Initialized;

	SQLiteConnection Connection;

	string CommandText;

	Sqlite3Statement Statement;
	static readonly Sqlite3Statement NullStatement = default(Sqlite3Statement);

	public PreparedSqlLiteInsertCommand(SQLiteConnection conn, string commandText)
	{
		Connection = conn;
		CommandText = commandText;
	}

	public int ExecuteNonQuery(object[] source)
	{
		if (Initialized && Statement == NullStatement)
		{
			throw new ObjectDisposedException(nameof(PreparedSqlLiteInsertCommand));
		}

		if (Connection.Trace)
		{
			Connection.Tracer?.Invoke("Executing: " + CommandText);
		}

		var r = SQLite3Native.Result.OK;

		if (!Initialized)
		{
			Statement = SQLite3Native.Prepare2(Connection.Handle, CommandText);
			Initialized = true;
		}

		//bind the values.
		if (source != null)
		{
			for (int i = 0; i < source.Length; i++)
			{
				SQLiteCommand.BindParameter(Statement, i + 1, source[i]);
			}
		}
		r = SQLite3Native.Step(Statement);

		if (r == SQLite3Native.Result.Done)
		{
			int rowsAffected = SQLite3Native.Changes(Connection.Handle);
			SQLite3Native.Reset(Statement);
			return rowsAffected;
		}
		else if (r == SQLite3Native.Result.Error)
		{
			string msg = SQLite3Native.GetErrmsg(Connection.Handle);
			SQLite3Native.Reset(Statement);
			throw SQLiteException.New(r, msg);
		}
		else if (r == SQLite3Native.Result.Constraint && SQLite3Native.ExtendedErrCode(Connection.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
		{
			SQLite3Native.Reset(Statement);
			throw NotNullConstraintViolationException.New(r, SQLite3Native.GetErrmsg(Connection.Handle));
		}
		else
		{
			SQLite3Native.Reset(Statement);
			throw SQLiteException.New(r, SQLite3Native.GetErrmsg(Connection.Handle));
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	void Dispose(bool disposing)
	{
		var s = Statement;
		Statement = NullStatement;
		Connection = null;
		if (s != NullStatement)
		{
			SQLite3Native.Finalize(s);
		}
	}

	~PreparedSqlLiteInsertCommand()
	{
		Dispose(false);
	}
}