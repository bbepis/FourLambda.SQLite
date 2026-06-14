namespace FourLambda.SQLite;

public class SQLiteCommand(SQLiteConnection conn)
{
	private readonly List<Binding> _bindings = new();

	public string CommandText { get; set; } = "";

	public int ExecuteNonQuery()
	{
		conn.Tracer?.Invoke("Executing: " + this);

		var statement = Prepare();
		var result = SQLite3Native.Step(statement);

		Finalize(statement);

		switch (result)
		{
			case SQLite3Native.Result.Done:
			{
				int rowsAffected = SQLite3Native.Changes(conn.Handle);
				return rowsAffected;
			}

			case SQLite3Native.Result.Error:
			{
				string msg = SQLite3Native.GetErrmsg(conn.Handle);
				throw new SQLiteException(result, msg);
			}

			case SQLite3Native.Result.Constraint when SQLite3Native.ExtendedErrCode(conn.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull:
				throw new NotNullConstraintViolationException(result, SQLite3Native.GetErrmsg(conn.Handle), null, null);

			default:
				throw new SQLiteException(result, SQLite3Native.GetErrmsg(conn.Handle));
		}
	}

	public T? ExecuteScalar<T>()
	{
		conn.Tracer?.Invoke("Executing Query: " + this);

		if (!ValueConverter.TryGetConverterDefinition(typeof(T), out var definition))
			throw new NotSupportedException("Don't know how to read " + typeof(T));

		var statement = Prepare();

		try
		{
			var result = SQLite3Native.Step(statement);

			switch (result)
			{
				case SQLite3Native.Result.Row:
				{
					var colType = SQLite3Native.ColumnType(statement, 0);

					return colType == SQLite3Native.ColType.Null ? default : (T?)definition!.StatementGetterBoxed(statement, 0, null, colType);
				}

				case SQLite3Native.Result.Done:
					break;

				default:
					throw new SQLiteException(result, SQLite3Native.GetErrmsg(conn.Handle));
			}
		}
		finally
		{
			Finalize(statement);
		}

		return default;
	}

	/// <summary>
	/// Executes the statement held by this command, and returns the data fetched by the command.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This can be of three category of types:<br/>
	/// - A regular class/struct that contains column definitions as properties.<br/>
	/// - A <see cref="ValueTuple"/> with up to 7 arguments. Note that the values are loaded positionally and not by name; make sure the positions of the values match up with the statement.<br/>
	/// - A scalar type (e.g. string, int). The value in the first column is parsed as this scalar type and returned.
	/// </typeparam>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> ExecuteQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		if (typeof(T).Name.StartsWith("ValueTuple`"))
			return ExecuteDeferredQueryIntoValueTuple<T>();

		if (ValueConverter.TryGetConverterDefinition<T>(out var converter))
		{
			// only scalar types have a converter definition
			return ExecuteQueryIntoScalar(converter);
		}

		return ExecuteQuery<T>(conn.GetMapping<T>());
	}

	/// <summary>
	/// Executes the statement held by this command, and returns the data fetched by the command.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This type must correspond with the data type the <see cref="TableMapping"/> was constructed with, or a base type (including <see cref="object"/>).
	/// </typeparam>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> ExecuteQuery<T>(TableMapping map)
	{
		if (typeof(T) == map.MappedType)
			return ExecuteQueryIntoObject<T>(map);

		return ExecuteQueryIntoObjectSafe<T>(map);
	}

	private IEnumerable<T> ExecuteQueryIntoObject<T>(TableMapping map)
	{
		conn.Tracer?.Invoke("Executing Query: " + this);

		if (typeof(T) != map.MappedType)
			throw new ArgumentException("Cannot use this mapping to create objects of type " + typeof(T));

		// TODO: this needs to have a proper wrapper to prevent statement leaks when enumerable doesn't complete
		var statement = Prepare();
		try
		{
			var cols = new TableColumn?[SQLite3Native.ColumnCount(statement)];
			var fastColumnSetters = new Action<T, Sqlite3Statement, int>[SQLite3Native.ColumnCount(statement)];

			for (int i = 0; i < cols.Length; i++)
			{
				var name = SQLite3Native.ColumnName16(statement, i);
				
				var column = cols[i] = map.FindColumn(name);

				if (column == null)
					continue;

				if (!ValueConverter.TryGetConverterDefinition(column.ColumnType, out var definition))
					throw new Exception("Unable to convert column type " + typeof(T));

				fastColumnSetters[i] = definition.StatementGetterGeneric<T>(column);
			}

			while (SQLite3Native.Step(statement) == SQLite3Native.Result.Row)
			{
				var obj = Activator.CreateInstance<T>();

				for (int i = 0; i < cols.Length; i++)
				{
					if (cols[i] == null)
						continue;

					fastColumnSetters[i].Invoke(obj, statement, i);
				}

				yield return obj;
			}
		}
		finally
		{
			SQLite3Native.Finalize(statement);
		}
	}

	private IEnumerable<T> ExecuteQueryIntoObjectSafe<T>(TableMapping map)
	{
		conn.Tracer?.Invoke("Executing Query: " + this);

		var targetType = map.MappedType != null && typeof(T).IsAssignableFrom(map.MappedType)
			? map.MappedType
			: typeof(T);

		// TODO: this needs to have a proper wrapper to prevent statement leaks when enumerable doesn't complete
		var statement = Prepare();
		try
		{
			var columnCount = SQLite3Native.ColumnCount(statement);
			var cols = new (TableColumn?, PropertyInfo?)[columnCount];
			var converters = new ValueConverter.IGenericConverterDefinition[columnCount];
			PropertyInfo[]? properties = null;

			for (int i = 0; i < cols.Length; i++)
			{
				var name = SQLite3Native.ColumnName16(statement, i);

				var column = map.FindColumn(name);

				if (column == null)
					continue;

				if (column.PropertyInfo != null && column.PropertyInfo.DeclaringType.IsAssignableFrom(targetType))
				{
					cols[i] = (column, column.PropertyInfo);
				}
				else
				{
					properties ??= targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

					var matchedProperty = properties.FirstOrDefault(x =>
						x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
						&& x.PropertyType == column.ColumnType);

					if (matchedProperty != null)
						cols[i] = (column, matchedProperty);
				}

				if (cols[i].Item2 != null)
				{
					if (!ValueConverter.TryGetConverterDefinition(cols[i].Item2.PropertyType, out var definition))
						throw new Exception("Unable to convert column type " + typeof(T));

					converters[i] = definition;
				}
			}

			if (cols.All(x => x == default))
				throw new ArgumentException($"Unable to use type {typeof(T)} to map this statement; no columns match");

			while (SQLite3Native.Step(statement) == SQLite3Native.Result.Row)
			{
				var obj = Activator.CreateInstance(targetType);

				for (int i = 0; i < cols.Length; i++)
				{
					if (cols[i] == default)
						continue;

					var colType = SQLite3Native.ColumnType(statement, i);

					if (colType == SQLite3Native.ColType.Null)
						continue;

					var (column, propertyInfo) = cols[i];

					propertyInfo.SetValue(obj, converters[i].StatementGetterBoxed(statement, i, column, colType));
				}

				yield return (T)obj;
			}
		}
		finally
		{
			SQLite3Native.Finalize(statement);
		}
	}

	private IEnumerable<T> ExecuteDeferredQueryIntoValueTuple<T>()
	{
		conn.Tracer?.Invoke("Executing Query: " + this);

		var fields = typeof(T).GetFields();

		// https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest
		if (fields.Length > 7)
			throw new NotSupportedException("ValueTuple with more than 7 members not supported due to nesting; see https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest");

		var statement = Prepare();
		try
		{
			var converters = new ValueConverter.IGenericConverterDefinition[SQLite3Native.ColumnCount(statement)];

			// unfortunately, we can't try and match column names to value tuple fields
			// https://stackoverflow.com/a/46602134

			for (int i = 0; i < converters.Length; i++)
			{
				if (!ValueConverter.TryGetConverterDefinition(fields[i].FieldType, out var definition))
					throw new Exception("Unable to convert column type " + typeof(T));

				converters[i] = definition;
			}

			while (SQLite3Native.Step(statement) == SQLite3Native.Result.Row)
			{
				// needs to be boxed. only way we can fix this is by not using reflection here
				var obj = (object)Activator.CreateInstance<T>();

				for (int i = 0; i < fields.Length; i++)
				{
					var colType = SQLite3Native.ColumnType(statement, i);
					fields[i].SetValue(obj, converters[i].StatementGetterBoxed(statement, i, null, colType));
				}

				yield return (T)obj;
			}
		}
		finally
		{
			SQLite3Native.Finalize(statement);
		}
	}

	private IEnumerable<T> ExecuteQueryIntoScalar<T>(ValueConverter.IConverterDefinition<T> converter)
	{
		conn.Tracer?.Invoke("Executing Query: " + this);

		var statement = Prepare();
		try
		{
			if (SQLite3Native.ColumnCount(statement) < 1)
			{
				throw new InvalidOperationException("Command should return at least one column");
			}
			while (SQLite3Native.Step(statement) == SQLite3Native.Result.Row)
			{
				var colType = SQLite3Native.ColumnType(statement, 0);
				yield return converter.StatementGetter(statement, 0, null, colType)!;
			}
		}
		finally
		{
			Finalize(statement);
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

	Sqlite3Statement Prepare()
	{
		var statement = SQLite3Native.Prepare2(conn.Handle, CommandText);
		BindAll(statement);
		return statement;
	}

	void Finalize(Sqlite3Statement statement)
	{
		SQLite3Native.Finalize(statement);
	}

	void BindAll(Sqlite3Statement statement)
	{
		int nextIdx = 1;
		foreach (var b in _bindings)
		{
			if (b.Name != null)
			{
				b.Index = SQLite3Native.BindParameterIndex(statement, b.Name);
			}
			else
			{
				b.Index = nextIdx++;
			}

			BindParameter(statement, b.Index, b.Value);
		}
	}

	internal static void BindParameter(Sqlite3Statement statement, int index, object? value)
	{
		QueryArgument? queryArgument = value as QueryArgument;

		if (queryArgument != null)
		{ 
			value = queryArgument!.Value;
		}

		if (value == null)
		{
			SQLite3Native.BindNull(statement, index);
		}
		else
		{
			var clrType = value.GetType();

			if (!ValueConverter.TryGetConverterDefinition(clrType, out var definition))
				throw new NotSupportedException("Don't know how to read " + clrType);

			definition!.StatementSetterBoxed(statement, index, queryArgument?.AgainstColumn, value);
		}
	}

	class Binding
	{
		public string Name { get; set; }

		public object Value { get; set; }

		public int Index { get; set; }
	}

	public override string ToString()
	{
		var builder = new StringBuilder(CommandText);

		for (var i = 0; i < _bindings.Count; i++)
			builder.AppendLine($"  {i - 1}: {_bindings[i].Value}");

		return builder.ToString();
	}
}


internal interface IPreparedCommand : IDisposable
{
	int BindAndExecuteBoxed(object source);
}

/// <summary>
/// Since the insert never changed, we only need to prepare once.
/// </summary>
internal class PreparedInsertCommand<TRowObject>(SQLiteConnection connection, string commandText, TableColumn[] columns) : IPreparedCommand
{
	private bool Initialized;

	private SQLiteConnection Connection = connection;
	private Action<TRowObject, Sqlite3Statement, int>[] converters;

	private Sqlite3Statement Statement;
	private static readonly Sqlite3Statement NullStatement = 0;

	int IPreparedCommand.BindAndExecuteBoxed(object source)
	{
		return BindAndExecute((TRowObject)source);
	}

	public int BindAndExecute(TRowObject source)
	{
		lock (this)
		{
			ObjectDisposedException.ThrowIf(Initialized && Statement == NullStatement, this);

			Connection.Tracer?.Invoke("Executing: " + commandText);

			if (!Initialized)
			{
				Statement = SQLite3Native.Prepare2(Connection.Handle, commandText);
				converters = columns.Select(x => x.GetConverter().StatementSetterGeneric<TRowObject>(x)).ToArray();

				Initialized = true;
			}

			for (int i = 0; i < converters.Length; i++)
			{
				converters[i](source, Statement, i + 1);
			}

			var result = SQLite3Native.Step(Statement);

			switch (result)
			{
				case SQLite3Native.Result.Done:
				{
					int rowsAffected = SQLite3Native.Changes(Connection.Handle);
					SQLite3Native.Reset(Statement);
					return rowsAffected;
				}
				case SQLite3Native.Result.Error:
				{
					string msg = SQLite3Native.GetErrmsg(Connection.Handle);
					SQLite3Native.Reset(Statement);
					throw new SQLiteException(result, msg);
				}
				case SQLite3Native.Result.Constraint when SQLite3Native.ExtendedErrCode(Connection.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull:
					SQLite3Native.Reset(Statement);
					throw new NotNullConstraintViolationException(result, SQLite3Native.GetErrmsg(Connection.Handle), null, null);
				default:
					SQLite3Native.Reset(Statement);
					throw new SQLiteException(result, SQLite3Native.GetErrmsg(Connection.Handle));
			}
		}
	}

	public static IPreparedCommand Create(Type type, SQLiteConnection connection, string commandText, TableColumn[] columns)
	{
		var commandType = typeof(PreparedInsertCommand<>).MakeGenericType(type);
		return (IPreparedCommand)Activator.CreateInstance(commandType, connection, commandText, columns)!;
	}

	public void Dispose()
	{
		var s = Statement;
		Statement = NullStatement;
		Connection = null;
		if (s != NullStatement)
		{
			SQLite3Native.Finalize(s);
		}

		GC.SuppressFinalize(this);
	}

	~PreparedInsertCommand()
	{
		Dispose();
	}
}