using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

#pragma warning disable 1591 // XML Doc Comments

namespace FourLambda.SQLite;

[Flags]
public enum SQLiteOpenFlags
{
	ReadOnly = 1,
	ReadWrite = 2,
	Create = 4,
	Uri = 0x40,
	Memory = 0x80,
	NoMutex = 0x8000,
	FullMutex = 0x10000,
	SharedCache = 0x20000,
	PrivateCache = 0x40000,
	ProtectionComplete = 0x00100000,
	ProtectionCompleteUnlessOpen = 0x00200000,
	ProtectionCompleteUntilFirstUserAuthentication = 0x00300000,
	ProtectionNone = 0x00400000
}

[Flags]
public enum TableCreateFlags
{
	/// <summary>
	/// Use the default creation options
	/// </summary>
	None = 0x000,
	/// <summary>
	/// Create a primary key index for a property called 'Id' (case-insensitive).
	/// This avoids the need for the [PrimaryKey] attribute.
	/// </summary>
	ImplicitPK = 0x001,
	/// <summary>
	/// Creates all columns as nullable unless explicitly marked with <see cref="NotNullAttribute"/> or <see cref="PrimaryKeyAttribute"/>.
	/// </summary>
	ImplicitNullable = 0x008,
	/// <summary>
	/// Force the primary key property to be auto incrementing.
	/// This avoids the need for the [AutoIncrement] attribute.
	/// The primary key property on the class should have type int or long.
	/// </summary>
	AutoIncPK = 0x004,
	/// <summary>
	/// Create virtual table using FTS3
	/// </summary>
	FullTextSearch3 = 0x100,
	/// <summary>
	/// Create virtual table using FTS4
	/// </summary>
	FullTextSearch4 = 0x200
}

/// <summary>
/// An open connection to a SQLite database.
/// </summary>
public class SQLiteConnection : IDisposable
{
	private bool _open;

	private Stopwatch _sw = new();
	private long _elapsedMilliseconds = 0;
	internal Action<string>? Tracer;

	static readonly Sqlite3DatabaseHandle NullHandle = IntPtr.Zero;
	static readonly Sqlite3BackupHandle NullBackupHandle = IntPtr.Zero;

	public Sqlite3DatabaseHandle Handle { get; private set; }

	/// <summary>
	/// Gets the database path used by this connection.
	/// </summary>
	public string DatabasePath { get; private set; }

	/// <summary>
	/// The version of the SQLite library this wrapper uses.
	/// </summary>
	public static Version LibraryVersion { get; }


	/// <summary>
	/// Sets a busy handler to sleep the specified amount of time when a table is locked.
	/// The handler will sleep multiple times until a total time of <see cref="BusyTimeout"/> has accumulated.
	/// </summary>
	public TimeSpan BusyTimeout
	{
		get;
		set
		{
			field = value;

			if (Handle != NullHandle)
				SQLite3Native.BusyTimeout(Handle, (int)field.TotalMilliseconds);
		}
	}

	static SQLiteConnection()
	{
		var rawVersionNumber = SQLite3Native.LibVersionNumber();
		LibraryVersion = new Version(
			rawVersionNumber / 1_000_000 % 1000,
			rawVersionNumber / 1_000 % 1000,
			rawVersionNumber % 1000
		);
	}

	/// <summary>
	/// Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
	/// </summary>
	/// <param name="databasePath">
	/// Specifies the path to the database file.
	/// </param>
	public SQLiteConnection(string databasePath)
		: this(new SQLiteConnectionString(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
	{
	}

	/// <summary>
	/// Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
	/// </summary>
	/// <param name="databasePath">
	/// Specifies the path to the database file.
	/// </param>
	/// <param name="openFlags">
	/// Flags controlling how the connection should be opened.
	/// </param>
	public SQLiteConnection(string databasePath, SQLiteOpenFlags openFlags)
		: this(new SQLiteConnectionString(databasePath, openFlags))
	{
	}

	/// <summary>
	/// Constructs a new SQLiteConnection and opens a SQLite database specified by databasePath.
	/// </summary>
	/// <param name="connectionString">
	/// Details on how to find and open the database.
	/// </param>
	public SQLiteConnection(SQLiteConnectionString connectionString)
	{
		if (connectionString == null)
			throw new ArgumentNullException(nameof(connectionString));

		DatabasePath = connectionString.DatabasePath ?? throw new InvalidOperationException("DatabasePath must be specified");

		// open using the byte[]
		// in the case where the path may include Unicode
		// force open to using UTF-8 using sqlite3_open_v2
		var result = SQLite3Native.Open(connectionString.DatabasePath, out var handle, (int)connectionString.OpenFlags, connectionString.VfsName);

		Handle = handle;
		if (result != SQLite3Native.Result.OK)
		{
			throw new SQLiteException(result, $"Could not open database file: {DatabasePath} ({result})");
		}
		_open = true;

		BusyTimeout = TimeSpan.FromSeconds(1.0);
		Tracer = line => Debug.WriteLine(line);

		try
		{
			connectionString.PreKeyAction?.Invoke(this);
			if (connectionString.Key.StringKey != null)
			{
				SetKey(connectionString.Key.StringKey);
			}
			else if (connectionString.Key.ByteKey != null)
			{
				SetKey(connectionString.Key.ByteKey);
			}

			connectionString.PostKeyAction?.Invoke(this);
		}
		catch
		{
			Dispose(false);

			throw;
		}
	}

	public void SetDebugLogger(Action<string>? logger)
	{
		Tracer = logger;
	}

	/// <summary>
	/// Enable or disable extension loading.
	/// </summary>
	public void EnableLoadExtension(bool enabled)
	{
		SQLite3Native.Result r = SQLite3Native.EnableLoadExtension(Handle, enabled ? 1 : 0);
		if (r != SQLite3Native.Result.OK)
		{
			string msg = SQLite3Native.GetErrmsg(Handle);
			throw new SQLiteException(r, msg);
		}
	}

	/// <summary>
	/// Enables the write ahead logging. WAL is significantly faster in most scenarios
	/// by providing better concurrency and better disk IO performance than the normal
	/// journal mode. You only need to call this function once in the lifetime of the database.
	/// </summary>
	public void EnableWriteAheadLogging()
	{
		ExecuteScalar<string>("PRAGMA journal_mode=WAL");
	}

	/// <summary>
	/// Convert an input string to a quoted SQL string that can be safely used in queries.
	/// </summary>
	/// <returns>The quoted string.</returns>
	/// <param name="unsafeString">The unsafe string to quote.</param>
	public static string EscapeAndQuote(string unsafeString)
	{
		// TODO: Doesn't call sqlite3_mprintf("%Q", u) because we're waiting on https://github.com/ericsink/SQLitePCL.raw/issues/153
		if (unsafeString == null)
			return "NULL";

		var safe = unsafeString.Replace("'", "''");
		return $"'{safe}'";
	}

	#region Encryption

	/// <summary>
	/// Sets the key used to encrypt/decrypt the database with "pragma key = ...".
	/// This must be the first thing you call before doing anything else with this connection
	/// if your database is encrypted.
	/// This only has an effect if you are using the SQLCipher nuget package.
	/// </summary>
	/// <param name="key">Encryption key plain text that is converted to the real encryption key using PBKDF2 key derivation</param>
	void SetKey(string key)
	{
		if (key == null)
			throw new ArgumentNullException(nameof(key));
		var q = EscapeAndQuote(key);
		ExecuteScalar<string>("pragma key = " + q);
	}

	/// <summary>
	/// Sets the key used to encrypt/decrypt the database.
	/// This must be the first thing you call before doing anything else with this connection
	/// if your database is encrypted.
	/// This only has an effect if you are using the SQLCipher nuget package.
	/// </summary>
	/// <param name="key">256-bit (32 byte) or 48 bytes (384-bit) encryption key</param>
	void SetKey(byte[] key)
	{
		if (key == null)
			throw new ArgumentNullException(nameof(key));
		if (key.Length != 32 && key.Length != 48)
			throw new ArgumentException("Key must be 32 bytes (256-bit) or 48 bytes (384-bit)", nameof(key));
		var s = string.Join("", key.Select(x => x.ToString("X2")));
		ExecuteScalar<string>("pragma key = \"x'" + s + "'\"");
	}

	/// <summary>
	/// Change the encryption key for a SQLCipher database with "pragma rekey = ...".
	/// </summary>
	/// <param name="key">Encryption key plain text that is converted to the real encryption key using PBKDF2 key derivation</param>
	public void ReKey(string key)
	{
		if (key == null)
			throw new ArgumentNullException(nameof(key));
		var q = EscapeAndQuote(key);
		ExecuteScalar<string>("pragma rekey = " + q);
	}

	/// <summary>
	/// Change the encryption key for a SQLCipher database.
	/// </summary>
	/// <param name="key">256-bit (32 byte) or 384-bit (48 bytes) encryption key data</param>
	public void ReKey(byte[] key)
	{
		if (key == null)
			throw new ArgumentNullException(nameof(key));
		if (key.Length != 32 && key.Length != 48)
			throw new ArgumentException("Key must be 32 bytes (256-bit) or 48 bytes (384-bit)", nameof(key));
		var s = string.Join("", key.Select(x => x.ToString("X2")));
		ExecuteScalar<string>("pragma rekey = \"x'" + s + "'\"");
	}

	#endregion

	#region Transactions

	private int _transactionDepth = 0;

	/// <summary>
	/// Whether <see cref="BeginTransaction"/> has been called and the database is waiting for a <see cref="Commit"/>.
	/// </summary>
	public bool IsInTransaction => _transactionDepth > 0;

	/// <summary>
	/// Begins a new transaction. Call <see cref="Commit"/> to end the transaction.
	/// </summary>
	/// <example cref="System.InvalidOperationException">Throws if a transaction has already begun.</example>
	public void BeginTransaction()
	{
		// The BEGIN command only works if the transaction stack is empty,
		//    or in other words if there are no pending transactions.
		// If the transaction stack is not empty when the BEGIN command is invoked,
		//    then the command fails with an error.
		// Rather than crash with an error, we will just ignore calls to BeginTransaction
		//    that would result in an error.
		if (Interlocked.CompareExchange(ref _transactionDepth, 1, 0) == 0)
		{
			try
			{
				Execute("begin transaction");
			}
			catch (Exception ex)
			{
				var sqlExp = ex as SQLiteException;
				if (sqlExp != null)
				{
					// It is recommended that applications respond to the errors listed below
					//    by explicitly issuing a ROLLBACK command.
					// TODO: This rollback failsafe should be localized to all throw sites.
					switch (sqlExp.Result)
					{
						case SQLite3Native.Result.IOError:
						case SQLite3Native.Result.Full:
						case SQLite3Native.Result.Busy:
						case SQLite3Native.Result.NoMem:
						case SQLite3Native.Result.Interrupt:
							RollbackTo(null, true);
							break;
					}
				}
				else
				{
					// Call decrement and not VolatileWrite in case we've already
					//    created a transaction point in SaveTransactionPoint since the catch.
					Interlocked.Decrement(ref _transactionDepth);
				}

				throw;
			}
		}
		else
		{
			// Calling BeginTransaction on an already open transaction is invalid
			throw new InvalidOperationException("Cannot begin a transaction while already in a transaction.");
		}
	}

	private int transactionPointCounter = 0;

	/// <summary>
	/// Creates a savepoint in the database at the current point in the transaction timeline.
	/// Begins a new transaction if one is not in progress.
	///
	/// Call <see cref="RollbackTo(string)"/> to undo transactions since the returned savepoint.
	/// Call <see cref="Release"/> to commit transactions after the savepoint returned here.
	/// Call <see cref="Commit"/> to end the transaction, committing all changes.
	/// </summary>
	/// <returns>A string naming the savepoint.</returns>
	public string SaveTransactionPoint()
	{
		int depth = Interlocked.Increment(ref _transactionDepth) - 1;
		string retVal = $"S{Interlocked.Increment(ref transactionPointCounter):0000}D{depth}";

		try
		{
			Execute("savepoint " + retVal);
		}
		catch (Exception ex)
		{
			if (ex is SQLiteException sqlExp)
			{
				// It is recommended that applications respond to the errors listed below
				//    by explicitly issuing a ROLLBACK command.
				// TODO: This rollback failsafe should be localized to all throw sites.
				switch (sqlExp.Result)
				{
					case SQLite3Native.Result.IOError:
					case SQLite3Native.Result.Full:
					case SQLite3Native.Result.Busy:
					case SQLite3Native.Result.NoMem:
					case SQLite3Native.Result.Interrupt:
						RollbackTo(null, true);
						break;
				}
			}
			else
			{
				Interlocked.Decrement(ref _transactionDepth);
			}

			throw;
		}

		return retVal;
	}

	/// <summary>
	/// Rolls back the transaction that was begun by <see cref="BeginTransaction"/> or <see cref="SaveTransactionPoint"/>.
	/// </summary>
	public void Rollback()
	{
		RollbackTo(null, false);
	}

	/// <summary>
	/// Rolls back the savepoint created by <see cref="BeginTransaction"/> or SaveTransactionPoint.
	/// </summary>
	/// <param name="savepoint">The name of the savepoint to roll back to, as returned by <see cref="SaveTransactionPoint"/>.  If savepoint is null or empty, this method is equivalent to a call to <see cref="Rollback"/></param>
	public void RollbackTo(string savepoint)
	{
		RollbackTo(savepoint, false);
	}

	/// <summary>
	/// Rolls back the transaction that was begun by <see cref="BeginTransaction"/>.
	/// </summary>
	/// <param name="savepoint">The name of the savepoint to roll back to, as returned by <see cref="SaveTransactionPoint"/>.  If savepoint is null or empty, this method is equivalent to a call to <see cref="Rollback"/></param>
	/// <param name="noThrow">true to avoid throwing exceptions, false otherwise</param>
	void RollbackTo(string? savepoint, bool noThrow)
	{
		// Rolling back without a TO clause rolls backs all transactions
		//    and leaves the transaction stack empty.
		try
		{
			if (string.IsNullOrEmpty(savepoint))
			{
				if (Interlocked.Exchange(ref _transactionDepth, 0) > 0)
				{
					Execute("rollback");
				}
			}
			else
			{
				DoSavePointExecute(savepoint, "rollback to ");
			}
		}
		catch (SQLiteException)
		{
			if (!noThrow)
				throw;
		}
		// No need to rollback if there are no transactions open.
	}

	/// <summary>
	/// Releases a savepoint returned from <see cref="SaveTransactionPoint"/>.  Releasing a savepoint
	///    makes changes since that savepoint permanent if the savepoint began the transaction,
	///    or otherwise the changes are permanent pending a call to <see cref="Commit"/>.
	///
	/// The RELEASE command is like a COMMIT for a SAVEPOINT.
	/// </summary>
	/// <param name="savepoint">The name of the savepoint to release.  The string should be the result of a call to <see cref="SaveTransactionPoint"/></param>
	public void Release(string savepoint)
	{
		try
		{
			DoSavePointExecute(savepoint, "release ");
		}
		catch (SQLiteException ex)
		{
			if (ex.Result == SQLite3Native.Result.Busy)
			{
				// Force a rollback since most people don't know this function can fail
				// Don't call Rollback() since the _transactionDepth is 0 and it won't try
				// Calling rollback makes our _transactionDepth variable correct.
				// Writes to the database only happen at depth=0, so this failure will only happen then.
				try
				{
					Execute("rollback");
				}
				catch
				{
					// rollback can fail in all sorts of wonderful version-dependent ways. Let's just hope for the best
				}
			}
			throw;
		}
	}

	void DoSavePointExecute(string savepoint, string cmd)
	{
		// Validate the savepoint
		int firstLen = savepoint.IndexOf('D');
		if (firstLen >= 2 && savepoint.Length > firstLen + 1)
		{
			if (int.TryParse(savepoint.Substring(firstLen + 1), out var depth))
			{
				// TODO: Mild race here, but inescapable without locking almost everywhere.
				if (0 <= depth && depth < _transactionDepth)
				{
					Volatile.Write(ref _transactionDepth, depth);
					Execute(cmd + savepoint);
					return;
				}
			}
		}

		throw new ArgumentException("savePoint is not valid, and should be the result of a call to SaveTransactionPoint.", "savePoint");
	}

	/// <summary>
	/// Commits the transaction that was begun by <see cref="BeginTransaction"/>.
	/// </summary>
	public void Commit()
	{
		if (Interlocked.Exchange(ref _transactionDepth, 0) != 0)
		{
			try
			{
				Execute("commit");
			}
			catch
			{
				// Force a rollback since most people don't know this function can fail
				// Don't call Rollback() since the _transactionDepth is 0 and it won't try
				// Calling rollback makes our _transactionDepth variable correct.
				try
				{
					Execute("rollback");
				}
				catch
				{
					// rollback can fail in all sorts of wonderful version-dependent ways. Let's just hope for the best
				}
				throw;
			}
		}
		// Do nothing on a commit with no open transaction
	}

	/// <summary>
	/// Creates a disposable transaction scope that will call <see cref="Rollback"/> on dispose if <see cref="TransactionScope.Commit"/> has not been called.<br/>
	/// Use this in a <see langword="using"/> block to ensure that the data you manipulate within the block is safely rolled back on any exception.
	/// </summary>
	public TransactionScope CreateTransactionScope()
	{
		return new TransactionScope(this, SaveTransactionPoint());
	}

	/// <summary>
	/// A disposable barrier for transactions.
	/// </summary>
	public class TransactionScope : IDisposable
	{
		private readonly SQLiteConnection _connection;
		private readonly string _savepoint;
		private bool _didCommit = false;

		internal TransactionScope(SQLiteConnection connection, string savepoint)
		{
			_connection = connection;
			_savepoint = savepoint;
		}

		/// <summary>
		/// Causes the current active transaction to be committed.
		/// </summary>
		public void Commit()
		{
			_connection.Release(_savepoint);
			_didCommit = true;
		}

		public void Dispose()
		{
			if (!_didCommit)
				_connection.RollbackTo(null, true);
		}
	}

	#endregion

	#region Table

	private readonly Dictionary<Type, (TableMapping mapping, bool wasCreated)> _generatedMappingCache = new();

	/// <summary>
	/// Retrieves the mapping that is automatically generated for the given type.
	/// </summary>
	/// <typeparam name="T">
	/// The type whose mapping to the database is returned.
	/// </typeparam>
	/// <param name="createFlags">
	/// Optional flags that alter how the mapping is generated.
	/// </param>
	/// <returns>
	/// The mapping represents the schema of the columns of the database and contains
	/// methods to set and get properties of objects.
	/// </returns>
	public TableMapping GetMapping<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(
		TableCreateFlags createFlags = TableCreateFlags.None)
	{
		return GetMapping(typeof(T), createFlags);
	}

	/// <summary>
	/// Retrieves the mapping that is automatically generated for the given type.
	/// </summary>
	/// <param name="type">
	/// The type whose mapping to the database is returned.
	/// </param>
	/// <param name="createFlags">
	/// Optional flags that alter how the mapping is generated.
	/// </param>
	/// <returns>
	/// The mapping represents the schema of the columns of the database and contains
	/// methods to set and get properties of objects.
	/// </returns>
	public TableMapping GetMapping(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type,
		TableCreateFlags createFlags = TableCreateFlags.None)
	{
		lock (_generatedMappingCache)
		{
			var map = _generatedMappingCache.GetValueOrDefault(type);

			if (map == default || (createFlags != map.mapping.CreateFlags && !map.wasCreated))
			{
				map = (TableMappingBuilder.FromType(type, createFlags).Build(), false);
				_generatedMappingCache[type] = map;
			}

			return map.mapping;
		}
	}

	/// <summary>
	/// Query the built-in sqlite table_info table for a specific tables columns.
	/// </summary>
	/// <returns>The columns contained in the table.</returns>
	/// <param name="tableName">Table name.</param>
	public List<ColumnDefinition> GetTableInfo(string tableName)
	{
		var query = "select name, \"notnull\" from pragma_table_info(\'" + tableName + "\')";
		return Query<(string name, int notnull)>(query)
			.Select(x =>
				new ColumnDefinition(x.name, typeof(object))
				{
					IsNullable = x.notnull == 0
				})
			.ToList();
	}

	/// <summary>
	/// Returns a queryable interface to the table represented by the given type.
	/// </summary>
	/// <returns>
	/// A queryable object that is able to translate Where, OrderBy, and Take
	/// queries into native SQL.
	/// </returns>
	public TableQuery<T> Table<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>() where T : new()
	{
		return new TableQuery<T>(this, GetMapping(typeof(T)));
	}

	/// <summary>
	/// Returns a queryable interface to the table represented by the given type.
	/// </summary>
	/// <returns>
	/// A queryable object that is able to translate Where, OrderBy, and Take
	/// queries into native SQL.
	/// </returns>
	public TableQuery<T> Table<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map) where T : new()
	{
		return new TableQuery<T>(this, map);
	}

	public enum CreateTableResult
	{
		Created,
		Migrated,
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="GetMapping"/>.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	T>(TableCreateFlags createFlags = TableCreateFlags.None)
	{
		return CreateTable(typeof(T), createFlags);
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="GetMapping"/>.
	/// </summary>
	/// <param name="type">Type to reflect to a database table.</param>
	/// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type, TableCreateFlags createFlags = TableCreateFlags.None)
	{
		var map = GetMapping(type, createFlags);

		if (map.Columns.Length == 0)
			throw new Exception($"Cannot create a table without columns (does '{type.FullName}' have public properties?)");

		return CreateTable(map);
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified mapping, including any constraints or indexes.
	/// </summary>
	/// <param name="map">Type to reflect to a database table.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable(TableMapping map)
	{
		if (map.Columns.Length == 0)
			throw new Exception("Cannot create a table without columns");

		// Check if the table exists
		var result = CreateTableResult.Created;
		var existingCols = GetTableInfo(map.TableName);

		// Create or migrate it
		if (existingCols.Count == 0)
		{
			// Facilitate virtual tables a.k.a. full-text search.
			bool fts3 = (map.CreateFlags & TableCreateFlags.FullTextSearch3) != 0;
			bool fts4 = (map.CreateFlags & TableCreateFlags.FullTextSearch4) != 0;

			bool fts = fts3 || fts4;
			var @virtual = fts ? "VIRTUAL " : string.Empty;
			var @using = fts3 ? "USING FTS3 " : fts4 ? "USING FTS4 " : string.Empty;

			// Build query.
			var stringBuilder = new StringBuilder();

			stringBuilder.Append($"CREATE {@virtual}TABLE IF NOT EXISTS \"{map.TableName}\" {@using}(\n");

			var definitions = new List<string>();

			foreach (var column in map.Columns)
				definitions.Add(column.GetCreationSql());

			if (map.PrimaryKeyColumns.Length > 0)
			{
				var autoIncrement = map.PrimaryKeyColumns.Any(x => x.IsAutoInc) ? " AUTOINCREMENT" : "";
				definitions.Add($"PRIMARY KEY ({string.Join(", ", map.PrimaryKeyColumns.Select(x => x.Name))}{autoIncrement})");
			}

			for (var i = 0; i < definitions.Count; i++)
			{
				stringBuilder.Append(definitions[i]);

				if (i < definitions.Count - 1)
					stringBuilder.Append(',');

				stringBuilder.Append('\n');
			}

			stringBuilder.Append(")");

			if (map.WithoutRowId)
			{
				stringBuilder.Append(" WITHOUT ROWID");
			}
			if (map.Strict)
			{
				stringBuilder.Append(" STRICT");
			}

			Execute(stringBuilder.ToString());
		}
		else
		{
			result = CreateTableResult.Migrated;
			MigrateTable(map, existingCols);
		}


		var indexes = new Dictionary<string, (string IndexName, bool Unique, List<(string ColumnName, int Order)> Columns)>();

		foreach (var column in map.Columns)
		{
			foreach (var columnIndex in column.Indices)
			{
				var indexName = columnIndex.Name ?? $"{map.TableName}_{column.Name}";

				if (!indexes.TryGetValue(indexName, out var indexInfo))
				{
					indexInfo = (indexName, columnIndex.Unique, new());
					indexes.Add(indexName, indexInfo);
				}

				if (columnIndex.Unique != indexInfo.Unique)
					throw new Exception("All the columns in an index must have the same value for their Unique property");

				indexInfo.Columns.Add((column.Name, columnIndex.Order));
			}
		}

		foreach (var indexName in indexes.Keys)
		{
			var index = indexes[indexName];

			var columns = index.Columns
				.OrderBy(i => i.Order)
				.Select(i => i.ColumnName)
				.ToArray();

			CreateIndex(map.TableName, columns, indexName, index.Unique);
		}

		if (map.MappedType != null)
			_generatedMappingCache[map.MappedType] = (map, true);

		return result;
	}

	private void MigrateTable(TableMapping map, List<ColumnDefinition> existingCols)
	{
		var toBeAdded = map.Columns
			.Where(newCol =>
				existingCols.All(existing => !string.Equals(existing.Name, newCol.Name, StringComparison.OrdinalIgnoreCase)))
			.ToArray();

		if (toBeAdded.Any(x => x.IsPK))
		{
			throw new InvalidOperationException("A column set as a primary key cannot be added to an existing table.");
		}

		foreach (var p in toBeAdded)
		{
			var addCol = $"alter table \"{map.TableName}\" add column {p.GetCreationSql()}";
			Execute(addCol);
		}
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="GetMapping"/>.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[RequiresUnreferencedCode("This method requires 'DynamicallyAccessedMemberTypes.All' on each input 'Type' instance.")]
	public Dictionary<Type, CreateTableResult> CreateTables(TableCreateFlags createFlags = TableCreateFlags.None, params Type[] types)
	{
		var results = new Dictionary<Type, CreateTableResult>();
		foreach (Type type in types)
		{
			var aResult = CreateTable(type, createFlags);
			results[type] = aResult;
		}
		return results;
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="GetMapping"/>.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	public Dictionary<TableMapping, CreateTableResult> CreateTables(params TableMapping[] mappings)
	{
		var results = new Dictionary<TableMapping, CreateTableResult>();
		foreach (var map in mappings)
		{
			var aResult = CreateTable(map);
			results[map] = aResult;
		}
		return results;
	}


	/// <summary>
	/// Executes a "drop table" on the table associated with the object. This is irreversible.
	/// </summary>
	public int DropTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		return DropTable(GetMapping(typeof(T)));
	}

	/// <summary>
	/// Executes a "drop table" on the table specified in the mapping. This is irreversible.
	/// </summary>
	public int DropTable(TableMapping map)
	{
		var query = $"drop table if exists \"{map.TableName}\"";
		return Execute(query);
	}

	#endregion

	#region Index

	/// <summary>
	/// Creates an index for the specified object property.
	/// e.g. CreateIndex&lt;Client&gt;(c => c.Name);
	/// </summary>
	/// <typeparam name="T">Type to reflect to a database table.</typeparam>
	/// <param name="property">Property to index.</param>
	/// <param name="indexName">The name of the index.</param>
	/// <param name="unique">Whether the index should be unique.</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, object>> property, string? indexName = null, bool unique = false)
	{
		MemberExpression mx;
		// TODO: handle anonymous object creation
		if (property.Body.NodeType == ExpressionType.Convert)
		{
			mx = ((UnaryExpression)property.Body).Operand as MemberExpression;
		}
		else
		{
			mx = (property.Body as MemberExpression);
		}
		var propertyInfo = mx.Member as PropertyInfo;
		if (propertyInfo == null)
		{
			throw new ArgumentException("The lambda expression 'property' should point to a valid Property");
		}

		var propName = propertyInfo.Name;

		var map = GetMapping<T>();
		var colName = map.FindColumnWithPropertyName(propName).Name;

		return CreateIndex(map.TableName, colName, null, unique);
	}

	/// <summary>
	/// Creates an index for the specified table and column.
	/// </summary>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnName">Name of the column to index</param>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string tableName, string columnName, string? indexName = null, bool unique = false)
	{
		return CreateIndex(tableName, [columnName], indexName, unique);
	}

	/// <summary>
	/// Creates an index for the specified table and columns.
	/// </summary>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnNames">An array of column names to index</param>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string tableName, string[] columnNames, string? indexName = null, bool unique = false)
	{
		indexName ??= $"{tableName}_{string.Join("_", columnNames)}";
		var sql = $"create {(unique ? "unique" : "")} index if not exists \"{indexName}\" on \"{tableName}\" (\"{string.Join("\", \"", columnNames)}\")";
		return Execute(sql);
	}

	#endregion

	#region Execute

	/// <summary>
	/// Creates a new SQLiteCommand given the command text with arguments. Place a '?'
	/// in the command text for each of the arguments.
	/// </summary>
	/// <param name="cmdText">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="parameters">
	/// Arguments to substitute for the occurrences of '?' in the command text.
	/// </param>
	/// <returns>
	/// A <see cref="SQLiteCommand"/>
	/// </returns>
	public SQLiteCommand CreateCommand(string cmdText, params object[] parameters)
	{
		if (!_open)
			throw new SQLiteException(SQLite3Native.Result.Error, "Cannot create commands from unopened database");

		var cmd = new SQLiteCommand(this);
		cmd.CommandText = cmdText;

		foreach (var o in parameters)
			cmd.Bind(o);

		return cmd;
	}

	/// <summary>
	/// Creates a new SQLiteCommand given the command text with named arguments. Place "@abcd" or "$abcd"
	/// in the command text for each of the arguments. "abcd" represents an alphanumeric identifier.
	/// For example, @name, :name and $name can all be used in the query.
	/// </summary>
	/// <param name="cmdText">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="namedParameters">
	/// Arguments to substitute for the occurrences of "[@:$]VVV" in the command text.
	/// </param>
	/// <returns>
	/// A <see cref="SQLiteCommand" />
	/// </returns>
	public SQLiteCommand CreateCommand(string cmdText, Dictionary<string, object> namedParameters)
	{
		if (!_open)
			throw new SQLiteException(SQLite3Native.Result.Error, "Cannot create commands from unopened database");

		SQLiteCommand cmd = new SQLiteCommand(this);
		cmd.CommandText = cmdText;

		foreach (var kv in namedParameters)
			cmd.Bind(kv.Key, kv.Value);

		return cmd;
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// Use this method instead of Query when you don't expect rows back. Such cases include
	/// INSERTs, UPDATEs, and DELETEs.
	/// You can set the Trace or TimeExecution properties of the connection
	/// to profile execution.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// The number of rows modified in the database as a result of this execution.
	/// </returns>
	public int Execute(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);

		if (Tracer != null)
		{
			_sw.Reset();
			_sw.Start();
		}

		var rows = cmd.ExecuteNonQuery();

		if (Tracer != null)
		{
			_sw.Stop();
			_elapsedMilliseconds += _sw.ElapsedMilliseconds;
			Tracer?.Invoke($"Finished in {_sw.ElapsedMilliseconds} ms ({_elapsedMilliseconds / 1000.0:0.0} s total)");
		}

		return rows;
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// Use this method when return primitive values.
	/// You can set the Trace or TimeExecution properties of the connection
	/// to profile execution.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// The number of rows modified in the database as a result of this execution.
	/// </returns>
	public T ExecuteScalar<T>(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);

		if (Tracer != null)
		{
			_sw.Reset();
			_sw.Start();
		}

		var rows = cmd.ExecuteScalar<T>();

		if (Tracer != null)
		{
			_sw.Stop();
			_elapsedMilliseconds += _sw.ElapsedMilliseconds;
			Tracer?.Invoke($"Finished in {_sw.ElapsedMilliseconds} ms ({_elapsedMilliseconds / 1000.0:0.0} s total)");
		}

		return rows;
	}

	#endregion

	#region Query

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the mapping automatically generated for
	/// the given type.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This can be of three category of types:<br/>
	/// - A regular class/struct that contains column definitions as properties.<br/>
	/// - A <see cref="ValueTuple"/> with up to 7 arguments. Note that the values are loaded positionally and not by name; make sure the positions of the values match up with the statement.<br/>
	/// - A scalar type (e.g. string, int). The value in the first column is parsed as this scalar type and returned.
	/// </typeparam>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteQuery<T>();
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the mapping automatically generated for
	/// the given type.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This type must correspond with the data type the <see cref="TableMapping"/> was constructed with, or a base type (including <see cref="object"/>).
	/// </typeparam>
	/// <param name="map">
	/// A <see cref="TableMapping"/> to use to convert the resulting rows
	/// into objects.
	/// </param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteQuery<T>(map);
	}

	/// <summary>
	/// Creates a query using the table defined by the <typeparam name="T">type</typeparam>, and returns fetched data.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This must be a type that can be used to build a table definition.
	/// </typeparam>
	/// <param name="predicate">
	/// A predicate on which to filter rows on, as a WHERE query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, bool>> predicate) where T : new()
	{
		return Table<T>().Where(predicate);
	}

	/// <summary>
	/// Creates a query using the table defined by the <typeparam name="T">type</typeparam>, and returns fetched data.
	/// </summary>
	/// <param name="map">
	/// A <see cref="TableMapping"/> to use to convert the resulting rows into objects.
	/// </param>
	/// <typeparam name="T">
	///	The type to load data into. This type must correspond with the data type the <see cref="TableMapping"/> was constructed with, or a base type (including <see cref="object"/>).
	/// </typeparam>
	/// <param name="predicate">
	/// A predicate on which to filter rows on, as a WHERE query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, Expression<Func<T, bool>> predicate) where T : new()
	{
		return Table<T>(map).Where(predicate);
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that the type has primary key(s) defined.
	/// </summary>
	/// <param name="primaryKey">
	/// The primary key. Provide multiple objects for a table with a composite primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public T? Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(params object[] primaryKey) where T : new()
	{
		return Find<T>(GetMapping<T>(), primaryKey);
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the provided map.
	/// Use of this method requires that the map has primary keys defined.
	/// </summary>
	/// <param name="primaryKey">
	/// The primary key. Provide multiple objects for a table with a composite primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public T? Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, params object[] primaryKey)
	{
		if (map.PrimaryKeyColumns.Length == 0)
			throw new ArgumentException("Cannot use Find() on a table that does not have a primary key");

		if (primaryKey.Length == 0)
			throw new ArgumentException("A primary key must be provided");

		return Query<T>(map, $"select * from \"{map.TableName}\" {map.PKWhereSql}", primaryKey).FirstOrDefault();
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the predicate from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="predicate">
	/// A predicate for which object to find.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public T Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, bool>> predicate) where T : new()
	{
		return Table<T>().Where(predicate).FirstOrDefault();
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the predicate from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="predicate">
	/// A predicate for which object to find.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public T Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, Expression<Func<T, bool>> predicate) where T : new()
	{
		return Table<T>(map).Where(predicate).FirstOrDefault();
	}

	#endregion

	#region Insert

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// </summary>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public int Insert<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(T obj, InsertConflictAction conflictAction = InsertConflictAction.Abort)
	{
		if (obj == null)
			return 0;

		var map = typeof(T) == typeof(object) ? GetMapping(obj.GetType()) : GetMapping<T>();

		return Insert(map, obj, conflictAction);
	}

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// </summary>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <param name="extra">
	/// Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
	/// </param>
	/// <param name="objType">
	/// The type of object to insert.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public int Insert<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, T obj, InsertConflictAction conflictAction = InsertConflictAction.Abort)
	{
		if (obj == null || map == null)
		{
			return 0;
		}

		if (map.PrimaryKeyColumns.Length == 1 && map.PrimaryKeyColumns[0].IsAutoGuid)
		{
			if (map.PrimaryKeyColumns[0].GetValue(obj).Equals(Guid.Empty))
			{
				map.PrimaryKeyColumns[0].SetValue(obj, Guid.NewGuid());
			}
		}
		
		var cols = conflictAction == InsertConflictAction.Replace ? map.InsertOrReplaceColumns : map.InsertColumns;
		var vals = new object[cols.Length];
		for (var i = 0; i < vals.Length; i++)
		{
			vals[i] = cols[i].GetValue(obj);
		}

		var insertCmd = GetInsertCommand(map, conflictAction);
		int count;

		lock (insertCmd)
		{
			// We lock here to protect the prepared statement returned via GetInsertCommand.
			// A SQLite prepared statement can be bound for only one operation at a time.
			try
			{
				count = insertCmd.ExecuteNonQuery(vals);
			}
			catch (SQLiteException ex)
			{
				if (SQLite3Native.ExtendedErrCode(Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
				{
					throw new NotNullConstraintViolationException(ex.Result, ex.Message, map, obj);
				}
				throw;
			}

			if (map.HasAutoIncPK)
			{
				var id = SQLite3Native.LastInsertRowid(Handle);
				map.SetAutoIncPK(obj, id);
			}
		}

		return count;
	}

	/// <summary>
	/// Inserts all specified objects.
	/// </summary>
	/// <param name="objects">
	/// An <see cref="IEnumerable"/> of the objects to insert.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public int InsertAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] 
		T>(IEnumerable<T> objects, InsertConflictAction conflictAction = InsertConflictAction.Abort, bool runInTransaction = true)
	{
		return InsertAll(GetMapping<T>(), objects, conflictAction, runInTransaction);
	}

	/// <summary>
	/// Inserts all specified objects.
	/// </summary>
	/// <param name="objects">
	/// An <see cref="IEnumerable"/> of the objects to insert.
	/// </param>
	/// <param name="objType">
	/// The type of object to insert.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public int InsertAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(
		TableMapping map,
		IEnumerable<T> objects,
		InsertConflictAction conflictAction = InsertConflictAction.Abort,
		bool runInTransaction = true)
	{
		var insertCommand = GetInsertCommand(map, conflictAction);
		var cols = conflictAction == InsertConflictAction.Replace ? map.InsertOrReplaceColumns : map.InsertColumns;
		var vals = new object[cols.Length];

		int InnerLoop()
		{
			int count = 0;

			foreach (var obj in objects)
			{
				for (var i = 0; i < vals.Length; i++)
				{
					vals[i] = cols[i].GetValue(obj);
				}

				count += insertCommand.ExecuteNonQuery(vals);
				map.SetAutoIncPK(obj, SQLite3Native.LastInsertRowid(Handle));
			}

			return count;
		}

		if (runInTransaction)
		{
			var count = 0;

			using (var scope = CreateTransactionScope())
			{
				count = InnerLoop();
				scope.Commit();
			}

			return count;
		}

		return InnerLoop();
	}

	readonly Dictionary<Tuple<TableMapping, InsertConflictAction>, PreparedInsertCommand> _insertCommandMap = new Dictionary<Tuple<TableMapping, InsertConflictAction>, PreparedInsertCommand>();

	PreparedInsertCommand GetInsertCommand(TableMapping map, InsertConflictAction conflictAction)
	{
		PreparedInsertCommand prepCmd;

		// TODO: this feels like a ticking timebomb. if someone migrates or changes a mapping, this dictionary is going to be out of date yet still used
		var key = Tuple.Create(map, conflictAction);

		lock (_insertCommandMap)
		{
			if (_insertCommandMap.TryGetValue(key, out prepCmd))
			{
				return prepCmd;
			}
		}

		prepCmd = CreateInsertCommand(map, conflictAction);

		lock (_insertCommandMap)
		{
			if (_insertCommandMap.TryGetValue(key, out var existing))
			{
				prepCmd.Dispose();
				return existing;
			}

			_insertCommandMap.Add(key, prepCmd);
		}

		return prepCmd;
	}

	PreparedInsertCommand CreateInsertCommand(TableMapping map, InsertConflictAction conflictAction)
	{
		var cols = map.InsertColumns;
		string insertSql;

		string orAction = conflictAction switch
		{
			InsertConflictAction.Abort => "",
			InsertConflictAction.Fail => " OR FAIL",
			InsertConflictAction.Ignore => " OR IGNORE",
			InsertConflictAction.Replace => " OR REPLACE",
			InsertConflictAction.Rollback => " OR ROLLBACK",
			_ => ""
		};

		if (cols.Length == 0 && map.Columns.Length == 1 && map.Columns[0].IsAutoInc)
		{
			insertSql = $"INSERT{orAction} INTO \"{map.TableName}\" DEFAULT VALUES";
		}
		else
		{
			if (conflictAction == InsertConflictAction.Replace)
			{
				cols = map.InsertOrReplaceColumns;
			}

			var columnNames = string.Join(",", cols.Select(c => $"\"{c.Name}\""));
			var valueSlots = string.Join(",", Enumerable.Repeat("?", cols.Length));

			insertSql = $"INSERT{orAction} INTO \"{map.TableName}\"({columnNames})VALUES({valueSlots})";
		}

		var insertCommand = new PreparedInsertCommand(this, insertSql);
		return insertCommand;
	}

	#endregion

	#region Update

	/// <summary>
	/// Updates all of the columns of a table using the specified object
	/// except for its primary key.
	/// The object is required to have a primary key.
	/// </summary>
	/// <param name="obj">
	/// The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <returns>
	/// The number of rows updated.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	public int Update<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(T obj)
	{
		if (obj == null)
			return 0;

		var map = typeof(T) == typeof(object) ? GetMapping(obj.GetType()) : GetMapping<T>();

		return Update(map, obj);
	}

	/// <summary>
	/// Updates all of the columns of a table using the specified object
	/// except for its primary key.
	/// The object is required to have a primary key.
	/// </summary>
	/// <param name="obj">
	/// The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <param name="objType">
	/// The type of object to insert.
	/// </param>
	/// <returns>
	/// The number of rows updated.
	/// </returns>
	public int Update<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, T obj)
	{
		if (obj == null)
			return 0;

		if (map.PrimaryKeyColumns.Length == 0)
		{
			throw new NotSupportedException($"Cannot update {map.TableName}: it has no PK");
		}

		// TODO: optimize this & all non-setup LINQ
		var cols = map.Columns.Where(p => !p.IsPK);
		var vals = cols.Select(c => c.GetValue(obj));
		var ps = new List<object>(vals);
		if (ps.Count == 0)
		{
			// There is a PK but no accompanying data,
			// so reset the PK to make the UPDATE work.
			cols = map.Columns;
			vals = cols.Select(c => c.GetValue(obj));
			ps = new List<object>(vals);
		}

		foreach (var pk in map.PrimaryKeyColumns)
			ps.Add(pk.GetValue(obj));

		var q = string.Format("update \"{0}\" set {1} {2}", map.TableName,
			string.Join(",", cols.Select(c => "\"" + c.Name + "\" = ? ").ToArray()),
			map.PKWhereSql);

		try
		{
			return Execute(q, ps.ToArray());
		}
		catch (SQLiteException ex)
		{
			if (ex.Result == SQLite3Native.Result.Constraint && SQLite3Native.ExtendedErrCode(Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
			{
				throw new NotNullConstraintViolationException(ex.Result, ex.Message, map, obj);
			}

			throw;
		}
	}

	/// <summary>
	/// Updates all specified objects.
	/// </summary>
	/// <param name="objects">
	/// An <see cref="IEnumerable"/> of the objects to insert.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	public int UpdateAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(IEnumerable<T> objects, bool runInTransaction = true)
	{
		return UpdateAll(GetMapping<T>(), objects, runInTransaction);
	}

	/// <summary>
	/// Updates all specified objects.
	/// </summary>
	/// <param name="objects">
	/// An <see cref="IEnumerable"/> of the objects to insert.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	public int UpdateAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(TableMapping map, IEnumerable<T> objects, bool runInTransaction = true)
	{
		// TODO: needs a prepared statement

		int InnerLoop()
		{
			int count = 0;

			foreach (var obj in objects)
			{
				count += Update(map, obj);
			}

			return count;
		}

		if (runInTransaction)
		{
			var count = 0;

			using (var scope = CreateTransactionScope())
			{
				count = InnerLoop();
				scope.Commit();
			}

			return count;
		}

		return InnerLoop();
	}

	#endregion

	#region Delete

	/// <summary>
	/// Deletes the given object from the database using its primary key.
	/// </summary>
	/// <param name="objectToDelete">
	/// The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <returns>
	/// The number of rows deleted.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'objectToDelete'.")]
	public int Delete(object objectToDelete)
	{
		var map = GetMapping(Orm.GetType(objectToDelete));

		if (map.PrimaryKeyColumns.Length == 0)
		{
			throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
		}

		var q = $"delete from \"{map.TableName}\" {map.PKWhereSql}";

		var count = Execute(q, map.PrimaryKeyColumns.Select(x => x.GetValue(objectToDelete)).ToArray());

		return count;
	}

	/// <summary>
	/// Deletes the object with the specified primary key.
	/// </summary>
	/// <param name="primaryKey">
	/// The primary key of the object to delete.
	/// </param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	/// <typeparam name='T'>
	/// The type of object.
	/// </typeparam>
	public int Delete<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(params object[] primaryKey)
	{
		return Delete(GetMapping<T>(), primaryKey);
	}

	/// <summary>
	/// Deletes the object with the specified primary key.
	/// </summary>
	/// <param name="primaryKey">
	/// The primary key of the object to delete.
	/// </param>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public int Delete(TableMapping map, params object[] primaryKey)
	{
		if (map.PrimaryKeyColumns.Length == 0)
			throw new ArgumentException("Cannot delete with this table mapping as it has no primary key");

		var q = $"delete from \"{map.TableName}\" {map.PKWhereSql}";
		var count = Execute(q, primaryKey);

		return count;
	}

	/// <summary>
	/// Deletes all the objects from the specified table.
	/// WARNING WARNING: Let me repeat. It deletes ALL the objects from the
	/// specified table. Do you really want to do that?
	/// </summary>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	/// <typeparam name='T'>
	/// The type of objects to delete.
	/// </typeparam>
	public int DeleteAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		return DeleteAll(GetMapping<T>());
	}

	/// <summary>
	/// Deletes all the objects from the specified table.
	/// WARNING WARNING: Let me repeat. It deletes ALL the objects from the
	/// specified table. Do you really want to do that?
	/// </summary>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public int DeleteAll(TableMapping map)
	{
		var query = $"delete from \"{map.TableName}\"";
		var count = Execute(query);

		return count;
	}

	#endregion

	/// <summary>
	/// Backup the entire database to the specified path.
	/// </summary>
	/// <param name="destinationDatabasePath">Path to backup file.</param>
	/// <param name="databaseName">The name of the database to backup (usually "main").</param>
	public void Backup(string destinationDatabasePath, string databaseName = "main")
	{
		// Open the destination
		var r = SQLite3Native.Open(destinationDatabasePath, out var destHandle);
		if (r != SQLite3Native.Result.OK)
		{
			throw new SQLiteException(r, "Failed to open destination database");
		}

		// Init the backup
		var backup = SQLite3Native.BackupInit(destHandle, databaseName, Handle, databaseName);
		if (backup == NullBackupHandle)
		{
			SQLite3Native.Close(destHandle);
			throw new Exception("Failed to create backup");
		}

		// Perform it
		SQLite3Native.BackupStep(backup, -1);
		SQLite3Native.BackupFinish(backup);

		// Check for errors
		r = SQLite3Native.GetResult(destHandle);
		string msg = "";
		if (r != SQLite3Native.Result.OK)
		{
			msg = SQLite3Native.GetErrmsg(destHandle);
		}

		// Close everything and report errors
		SQLite3Native.Close(destHandle);
		if (r != SQLite3Native.Result.OK)
		{
			throw new SQLiteException(r, msg);
		}
	}

	~SQLiteConnection()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_open || Handle == NullHandle)
			return;

		var useClose2 = LibraryVersion >= new Version(3, 7, 14);

		try
		{
			if (disposing)
			{
				lock (_insertCommandMap)
				{
					foreach (var sqlInsertCommand in _insertCommandMap.Values)
					{
						sqlInsertCommand.Dispose();
					}
					_insertCommandMap.Clear();
				}

				var r = useClose2 ? SQLite3Native.Close2(Handle) : SQLite3Native.Close(Handle);
				if (r != SQLite3Native.Result.OK)
				{
					string msg = SQLite3Native.GetErrmsg(Handle);
					throw new SQLiteException(r, msg);
				}
			}
			else
			{
				var r = useClose2 ? SQLite3Native.Close2(Handle) : SQLite3Native.Close(Handle);
			}
		}
		finally
		{
			Handle = NullHandle;
			_open = false;
		}
	}
}

/// <summary>
/// Represents a parsed connection string.
/// </summary>
public class SQLiteConnectionString
{
	/// <summary>
	/// Specifies the path to the database file.
	/// </summary>
	public string DatabasePath { get; }

	/// <summary>
	/// Specifies the encryption key to use on the database. Can be cast from a string or a byte[].
	/// </summary>
	public DatabaseKey Key { get; init; }

	/// <summary>
	/// Flags controlling how the connection should be opened.
	/// </summary>
	public SQLiteOpenFlags OpenFlags { get; init; }

	/// <summary>
	/// Executes prior to setting key for SQLCipher databases
	/// </summary>
	public Action<SQLiteConnection>? PreKeyAction { get; init; }

	/// <summary>
	/// Executes after setting key for SQLCipher databases
	/// </summary>
	public Action<SQLiteConnection>? PostKeyAction { get; init; }

	/// <summary>
	/// Specifies the Virtual File System to use on the database.
	/// </summary>
	public string? VfsName { get; init; }

	/// <summary>
	/// Constructs a new SQLiteConnectionString with all the data needed to open an SQLiteConnection.
	/// </summary>
	/// <param name="databasePath">
	/// Specifies the path to the database file.
	/// </param>
	public SQLiteConnectionString(string databasePath)
		: this(databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite)
	{
	}

	/// <summary>
	/// Constructs a new SQLiteConnectionString with all the data needed to open an SQLiteConnection.
	/// </summary>
	/// <param name="databasePath">
	/// Specifies the path to the database file.
	/// </param>
	/// <param name="key">
	/// Specifies the encryption key to use on the database. Can be cast from a string or a byte[].
	/// </param>
	public SQLiteConnectionString(string databasePath, DatabaseKey key)
		: this(databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite, key)
	{
	}

	/// <summary>
	/// Constructs a new SQLiteConnectionString with all the data needed to open an SQLiteConnection.
	/// </summary>
	/// <param name="databasePath">
	/// Specifies the path to the database file.
	/// </param>
	/// <param name="openFlags">
	/// Flags controlling how the connection should be opened.
	/// </param>
	/// <param name="key">
	/// Specifies the encryption key to use on the database. Can be cast from a string or a byte[].
	/// </param>
	public SQLiteConnectionString(string databasePath, SQLiteOpenFlags openFlags, DatabaseKey key = default)
	{
		Key = key;
		OpenFlags = openFlags;

		DatabasePath = databasePath;
	}
}

/// <summary>
/// https://sqlite.org/lang_conflict.html
/// </summary>
public enum InsertConflictAction
{
	/// <summary>
	/// Default behavior. Upon conflict, the insert simply fails and does nothing.
	/// </summary>
	Abort,
	/// <summary>
	/// Upon conflict, the insert simply fails and does nothing. However, if this insert was part of a much larger statement, the statement still saves any changes made.
	/// </summary>
	Fail,
	/// <summary>
	/// Upon conflict, this insert is ignored and nothing happens without a failure happening.
	/// </summary>
	Ignore,
	/// <summary>
	/// Upon conflict, the existing row is deleted and the insert is attempted again.
	/// </summary>
	Replace,
	/// <summary>
	/// Upon conflict, the insert fails and any active transaction is rolled back. If there is no active transaction, it acts the same as <see cref="Abort"/>.
	/// </summary>
	Rollback
}

/// <summary>
/// Represents a key that can be used to encrypt/decrypt a database.
/// </summary>
public struct DatabaseKey
{
	public string? StringKey { get; }
	public byte[]? ByteKey { get; }

	public DatabaseKey(string key)
	{
		StringKey = key;
	}

	/// <param name="key">Key must be 32 bytes (256-bit) or 48 bytes (384-bit).</param>
	public DatabaseKey(byte[] key)
	{
		ByteKey = key;
	}

	public static implicit operator DatabaseKey(string key) => new(key);
	public static implicit operator DatabaseKey(byte[] key) => new(key);
}