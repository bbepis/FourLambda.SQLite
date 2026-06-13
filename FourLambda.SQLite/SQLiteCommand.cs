using System.Data.Common;
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
			var cols = new TableColumn?[SQLite3Native.ColumnCount(stmt)];
			var fastColumnSetters = new Action<T, Sqlite3Statement, int>[SQLite3Native.ColumnCount(stmt)];

			for (int i = 0; i < cols.Length; i++)
			{
				var name = SQLite3Native.ColumnName16(stmt, i);
				
				var column = cols[i] = map.FindColumn(name);

				if (column == null)
					continue;

				if (!ValueConverter.TryGetConverterDefinition(column.ColumnType, out var definition))
					throw new Exception("Unable to convert column type " + typeof(T));

				fastColumnSetters[i] = definition.StatementSetterGeneric<T>(column);
			}

			while (SQLite3Native.Step(stmt) == SQLite3Native.Result.Row)
			{
				var obj = Activator.CreateInstance<T>();

				for (int i = 0; i < cols.Length; i++)
				{
					if (cols[i] == null)
						continue;

					fastColumnSetters[i].Invoke(obj, stmt, i);
				}

				yield return obj;
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
			var clrType = value.GetType();

			if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				clrType = clrType.GenericTypeArguments[0];
			}

			if (!ValueConverter.TryGetConverterDefinition(clrType, out var definition))
				throw new NotSupportedException("Don't know how to read " + clrType);

			definition!.StatementSetterBoxed(stmt, index, queryArgument?.AgainstColumn, value);
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

		if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			clrType = clrType.GenericTypeArguments[0];
		}

		if (!ValueConverter.TryGetConverterDefinition(clrType, out var definition))
			throw new NotSupportedException("Don't know how to read " + clrType);

		return definition!.StatementGetterBoxed(stmt, index, column, type);
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