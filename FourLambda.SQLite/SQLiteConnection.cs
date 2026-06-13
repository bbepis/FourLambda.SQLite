//
// Copyright (c) 2009-2024 Krueger Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

#pragma warning disable 1591 // XML Doc Comments

namespace FourLambda.SQLite;

public class SQLiteException : Exception
{
	public SQLite3Native.Result Result { get; private set; }

	protected SQLiteException(SQLite3Native.Result r, string message) : base(message)
	{
		Result = r;
	}

	public static SQLiteException New(SQLite3Native.Result r, string message)
	{
		return new SQLiteException(r, message);
	}
}

public class NotNullConstraintViolationException : SQLiteException
{
	public IEnumerable<TableMapping.Column> Columns { get; protected set; }

	protected NotNullConstraintViolationException(SQLite3Native.Result r, string message)
		: this(r, message, null, null)
	{

	}

	protected NotNullConstraintViolationException(SQLite3Native.Result r, string message, TableMapping mapping, object obj)
		: base(r, message)
	{
		if (mapping != null && obj != null)
		{
			this.Columns = from c in mapping.Columns
				where c.IsNullable == false && c.GetValue(obj) == null
				select c;
		}
	}

	public static new NotNullConstraintViolationException New(SQLite3Native.Result r, string message)
	{
		return new NotNullConstraintViolationException(r, message);
	}

	public static NotNullConstraintViolationException New(SQLite3Native.Result r, string message, TableMapping mapping, object obj)
	{
		return new NotNullConstraintViolationException(r, message, mapping, obj);
	}

	public static NotNullConstraintViolationException New(SQLiteException exception, TableMapping mapping, object obj)
	{
		return new NotNullConstraintViolationException(exception.Result, exception.Message, mapping, obj);
	}
}

[Flags]
public enum SQLiteOpenFlags
{
	ReadOnly = 1, ReadWrite = 2, Create = 4,
	Uri = 0x40, Memory = 0x80,
	NoMutex = 0x8000, FullMutex = 0x10000,
	SharedCache = 0x20000, PrivateCache = 0x40000,
	ProtectionComplete = 0x00100000,
	ProtectionCompleteUnlessOpen = 0x00200000,
	ProtectionCompleteUntilFirstUserAuthentication = 0x00300000,
	ProtectionNone = 0x00400000
}

[Flags]
public enum CreateFlags
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

public interface ISQLiteConnection : IDisposable
{
	Sqlite3DatabaseHandle Handle { get; }
	string DatabasePath { get; }
	int LibVersionNumber { get; }
	bool TimeExecution { get; set; }
	bool Trace { get; set; }
	Action<string> Tracer { get; set; }
	TimeSpan BusyTimeout { get; set; }
	IEnumerable<TableMapping> TableMappings { get; }
	bool IsInTransaction { get; }

	void Backup(string destinationDatabasePath, string databaseName = "main");
	void BeginTransaction();
	void Close();
	void Commit();
	SQLiteCommand CreateCommand(string cmdText, params object[] ps);
	SQLiteCommand CreateCommand(string cmdText, Dictionary<string, object> args);
	int CreateIndex(string indexName, string tableName, string[] columnNames, bool unique = false);
	int CreateIndex(string indexName, string tableName, string columnName, bool unique = false);
	int CreateIndex(string tableName, string columnName, bool unique = false);
	int CreateIndex(string tableName, string[] columnNames, bool unique = false);
	int CreateIndex<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, object>> property, bool unique = false);
	CreateTableResult CreateTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(CreateFlags createFlags = CreateFlags.None);
	CreateTableResult CreateTable(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type ty, CreateFlags createFlags = CreateFlags.None);
	CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new();
	CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new();
	CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T4>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new()
		where T4 : new();
	CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T5>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new()
		where T4 : new()
		where T5 : new();
	[RequiresUnreferencedCode("This method requires 'DynamicallyAccessedMemberTypes.All' on each input 'Type' instance.")]
	CreateTablesResult CreateTables(CreateFlags createFlags = CreateFlags.None, params Type[] types);
	IEnumerable<T> DeferredQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new();
	IEnumerable<object> DeferredQuery(TableMapping map, string query, params object[] args);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'objectToDelete'.")]
	int Delete(object objectToDelete);
	int Delete<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(object primaryKey);
	int Delete(object primaryKey, TableMapping map);
	int DeleteAll<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>();
	int DeleteAll(TableMapping map);
	int DropTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>();
	int DropTable(TableMapping map);
	void EnableLoadExtension(bool enabled);
	void EnableWriteAheadLogging();
	int Execute(string query, params object[] args);
	T ExecuteScalar<T>(string query, params object[] args);
	T Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(object pk) where T : new();
	object Find(object pk, TableMapping map);
	T Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, bool>> predicate) where T : new();
	T FindWithQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new();
	object FindWithQuery(TableMapping map, string query, params object[] args);
	T Get<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(object pk) where T : new();
	object Get(object pk, TableMapping map);
	T Get<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, bool>> predicate) where T : new();
	TableMapping GetMapping(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type, CreateFlags createFlags = CreateFlags.None);
	TableMapping GetMapping<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(CreateFlags createFlags = CreateFlags.None);
	List<SQLiteConnection.ColumnInfo> GetTableInfo(string tableName);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	int Insert(object obj);
	int Insert(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	int Insert(object obj, string extra);
	int Insert(
		object obj,
		string extra,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	int InsertAll(IEnumerable objects, bool runInTransaction = true);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	int InsertAll(IEnumerable objects, string extra, bool runInTransaction = true);
	int InsertAll(
		IEnumerable objects,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType,
		bool runInTransaction = true);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	int InsertOrReplace(object obj);
	int InsertOrReplace(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType);
	List<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new();
	List<object> Query(TableMapping map, string query, params object[] args);
	List<T> QueryScalars<T>(string query, params object[] args);
	void ReKey(string key);
	void ReKey(byte[] key);
	void Release(string savepoint);
	void Rollback();
	void RollbackTo(string savepoint);
	void RunInTransaction(Action action);
	string SaveTransactionPoint();
	TableQuery<T> Table<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>() where T : new();
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	int Update(object obj);
	int Update(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType);
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	int UpdateAll(IEnumerable objects, bool runInTransaction = true);
}

/// <summary>
/// An open connection to a SQLite database.
/// </summary>
[Preserve(AllMembers = true)]
public partial class SQLiteConnection : IDisposable
{
	private bool _open;
	private TimeSpan _busyTimeout;
	readonly static Dictionary<string, TableMapping> _mappings = new Dictionary<string, TableMapping>();
	private System.Diagnostics.Stopwatch _sw;
	private long _elapsedMilliseconds = 0;

	private int _transactionDepth = 0;
	private Random _rand = new Random();

	public Sqlite3DatabaseHandle Handle { get; private set; }
	static readonly Sqlite3DatabaseHandle NullHandle = default(Sqlite3DatabaseHandle);
	static readonly Sqlite3BackupHandle NullBackupHandle = default(Sqlite3BackupHandle);

	/// <summary>
	/// Gets the database path used by this connection.
	/// </summary>
	public string DatabasePath { get; private set; }

	/// <summary>
	/// Gets the SQLite library version number. 3007014 would be v3.7.14
	/// </summary>
	public int LibVersionNumber { get; private set; }

	/// <summary>
	/// Whether Trace lines should be written that show the execution time of queries.
	/// </summary>
	public bool TimeExecution { get; set; }

	/// <summary>
	/// Whether to write queries to <see cref="Tracer"/> during execution.
	/// </summary>
	public bool Trace { get; set; }

	/// <summary>
	/// The delegate responsible for writing trace lines.
	/// </summary>
	/// <value>The tracer.</value>
	public Action<string> Tracer { get; set; }

#if USE_SQLITEPCL_RAW && !NO_SQLITEPCL_RAW_BATTERIES
		static SQLiteConnection ()
		{
			SQLitePCL.Batteries_V2.Init ();
		}
#endif

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
		if (connectionString.DatabasePath == null)
			throw new InvalidOperationException("DatabasePath must be specified");

		DatabasePath = connectionString.DatabasePath;

		LibVersionNumber = SQLite3Native.LibVersionNumber();

		Sqlite3DatabaseHandle handle;

		// open using the byte[]
		// in the case where the path may include Unicode
		// force open to using UTF-8 using sqlite3_open_v2
		var r = SQLite3Native.Open(connectionString.DatabasePath, out handle, (int)connectionString.OpenFlags, connectionString.VfsName);

		Handle = handle;
		if (r != SQLite3Native.Result.OK)
		{
			throw SQLiteException.New(r, string.Format("Could not open database file: {0} ({1})", DatabasePath, r));
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
	static string Quote(string unsafeString)
	{
		// TODO: Doesn't call sqlite3_mprintf("%Q", u) because we're waiting on https://github.com/ericsink/SQLitePCL.raw/issues/153
		if (unsafeString == null)
			return "NULL";
		var safe = unsafeString.Replace("'", "''");
		return "'" + safe + "'";
	}

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
		var q = Quote(key);
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
		var q = Quote(key);
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

	/// <summary>
	/// Enable or disable extension loading.
	/// </summary>
	public void EnableLoadExtension(bool enabled)
	{
		SQLite3Native.Result r = SQLite3Native.EnableLoadExtension(Handle, enabled ? 1 : 0);
		if (r != SQLite3Native.Result.OK)
		{
			string msg = SQLite3Native.GetErrmsg(Handle);
			throw SQLiteException.New(r, msg);
		}
	}

	/// <summary>
	/// Sets a busy handler to sleep the specified amount of time when a table is locked.
	/// The handler will sleep multiple times until a total time of <see cref="BusyTimeout"/> has accumulated.
	/// </summary>
	public TimeSpan BusyTimeout
	{
		get { return _busyTimeout; }
		set
		{
			_busyTimeout = value;
			if (Handle != NullHandle)
			{
				SQLite3Native.BusyTimeout(Handle, (int)_busyTimeout.TotalMilliseconds);
			}
		}
	}

	/// <summary>
	/// Returns the mappings from types to tables that the connection
	/// currently understands.
	/// </summary>
	public IEnumerable<TableMapping> TableMappings
	{
		get
		{
			lock (_mappings)
			{
				return new List<TableMapping>(_mappings.Values);
			}
		}
	}

	/// <summary>
	/// Retrieves the mapping that is automatically generated for the given type.
	/// </summary>
	/// <param name="type">
	/// The type whose mapping to the database is returned.
	/// </param>
	/// <param name="createFlags">
	/// Optional flags allowing implicit PK and indexes based on naming conventions
	/// </param>
	/// <returns>
	/// The mapping represents the schema of the columns of the database and contains
	/// methods to set and get properties of objects.
	/// </returns>
	public TableMapping GetMapping(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type,
		CreateFlags createFlags = CreateFlags.None)
	{
		TableMapping map;
		var key = type.FullName;
		lock (_mappings)
		{
			if (_mappings.TryGetValue(key, out map))
			{
				if (createFlags != CreateFlags.None && createFlags != map.CreateFlags)
				{
					map = new TableMapping(type, createFlags);
					_mappings[key] = map;
				}
			}
			else
			{
				map = new TableMapping(type, createFlags);
				_mappings.Add(key, map);
			}
		}
		return map;
	}

	/// <summary>
	/// Retrieves the mapping that is automatically generated for the given type.
	/// </summary>
	/// <param name="createFlags">
	/// Optional flags allowing implicit PK and indexes based on naming conventions
	/// </param>
	/// <returns>
	/// The mapping represents the schema of the columns of the database and contains
	/// methods to set and get properties of objects.
	/// </returns>
	public TableMapping GetMapping<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(CreateFlags createFlags = CreateFlags.None)
	{
		return GetMapping(typeof(T), createFlags);
	}

	private struct IndexedColumn
	{
		public int Order;
		public string ColumnName;
	}

	private struct IndexInfo
	{
		public string IndexName;
		public string TableName;
		public bool Unique;
		public List<IndexedColumn> Columns;
	}

	/// <summary>
	/// Executes a "drop table" on the database.  This is non-recoverable.
	/// </summary>
	public int DropTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>()
	{
		return DropTable(GetMapping(typeof(T)));
	}

	/// <summary>
	/// Executes a "drop table" on the database.  This is non-recoverable.
	/// </summary>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	public int DropTable(TableMapping map)
	{
		var query = string.Format("drop table if exists \"{0}\"", map.TableName);
		return Execute(query);
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(CreateFlags createFlags = CreateFlags.None)
	{
		return CreateTable(typeof(T), createFlags);
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <param name="ty">Type to reflect to a database table.</param>
	/// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type ty, CreateFlags createFlags = CreateFlags.None)
	{
		var map = GetMapping(ty, createFlags);

		if (map.Columns.Length == 0)
			throw new Exception($"Cannot create a table without columns (does '{ty.FullName}' have public properties?)");

		return CreateTable(map);
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <param name="ty">Type to reflect to a database table.</param>
	/// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public CreateTableResult CreateTable(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		TableMapping map)
	{
		// Present a nice error if no columns specified
		if (map.Columns.Length == 0)
			throw new Exception("Cannot create a table without columns");

		// Check if the table exists
		var result = CreateTableResult.Created;
		var existingCols = GetTableInfo(map.TableName);

		// Create or migrate it
		if (existingCols.Count == 0)
		{
			// Facilitate virtual tables a.k.a. full-text search.
			// TODO: fix
			//bool fts3 = (createFlags & CreateFlags.FullTextSearch3) != 0;
			//bool fts4 = (createFlags & CreateFlags.FullTextSearch4) != 0;
			bool fts3 = false;
			bool fts4 = false;

			bool fts = fts3 || fts4;
			var @virtual = fts ? "virtual " : string.Empty;
			var @using = fts3 ? "using fts3 " : fts4 ? "using fts4 " : string.Empty;

			// Build query.
			var query = "create " + @virtual + "table if not exists \"" + map.TableName + "\" " + @using + "(\n";

			var isCompositePk = map.PrimaryKeyColumns.Length > 1;
			var decls = new List<string>();

			foreach (var column in map.Columns)
				decls.Add(Orm.SqlDecl(column, isCompositePk));

			if (isCompositePk)
				decls.Add($"PRIMARY KEY ({string.Join(", ", map.PrimaryKeyColumns.Select(x => x.Name))})");

			var decl = string.Join(",\n", decls);
			query += decl;
			query += ")";
			if (map.WithoutRowId)
			{
				query += " without rowid";
			}
			if (map.Strict)
			{
				query += " strict";
			}

			Execute(query);
		}
		else
		{
			result = CreateTableResult.Migrated;
			MigrateTable(map, existingCols);
		}

		var indexes = new Dictionary<string, IndexInfo>();
		foreach (var c in map.Columns)
		{
			foreach (var i in c.Indices)
			{
				var iname = i.Name ?? map.TableName + "_" + c.Name;
				IndexInfo iinfo;
				if (!indexes.TryGetValue(iname, out iinfo))
				{
					iinfo = new IndexInfo
					{
						IndexName = iname,
						TableName = map.TableName,
						Unique = i.Unique,
						Columns = new List<IndexedColumn>()
					};
					indexes.Add(iname, iinfo);
				}

				if (i.Unique != iinfo.Unique)
					throw new Exception("All the columns in an index must have the same value for their Unique property");

				iinfo.Columns.Add(new IndexedColumn
				{
					Order = i.Order,
					ColumnName = c.Name
				});
			}
		}

		foreach (var indexName in indexes.Keys)
		{
			var index = indexes[indexName];
			var columns = index.Columns.OrderBy(i => i.Order).Select(i => i.ColumnName).ToArray();
			CreateIndex(indexName, index.TableName, columns, index.Unique);
		}

		return result;
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database for each type. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This method preserves metadata for all type arguments.")]
	public CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
	{
		return CreateTables(createFlags, typeof(T), typeof(T2));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database for each type. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This method preserves metadata for all type arguments.")]
	public CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new()
	{
		return CreateTables(createFlags, typeof(T), typeof(T2), typeof(T3));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database for each type. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This method preserves metadata for all type arguments.")]
	public CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T4>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new()
		where T4 : new()
	{
		return CreateTables(createFlags, typeof(T), typeof(T2), typeof(T3), typeof(T4));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database for each type. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This method preserves metadata for all type arguments.")]
	public CreateTablesResult CreateTables<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T5>(CreateFlags createFlags = CreateFlags.None)
		where T : new()
		where T2 : new()
		where T3 : new()
		where T4 : new()
		where T5 : new()
	{
		return CreateTables(createFlags, typeof(T), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database for each type. It also
	/// creates any specified indexes on the columns of the table. It uses
	/// a schema automatically generated from the specified type. You can
	/// later access this schema by calling GetMapping.
	/// </summary>
	/// <returns>
	/// Whether the table was created or migrated for each type.
	/// </returns>
	[RequiresUnreferencedCode("This method requires 'DynamicallyAccessedMemberTypes.All' on each input 'Type' instance.")]
	public CreateTablesResult CreateTables(CreateFlags createFlags = CreateFlags.None, params Type[] types)
	{
		var result = new CreateTablesResult();
		foreach (Type type in types)
		{
			var aResult = CreateTable(type, createFlags);
			result.Results[type] = aResult;
		}
		return result;
	}

	/// <summary>
	/// Creates an index for the specified table and columns.
	/// </summary>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnNames">An array of column names to index</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string indexName, string tableName, string[] columnNames, bool unique = false)
	{
		const string sqlFormat = "create {2} index if not exists \"{3}\" on \"{0}\"(\"{1}\")";
		var sql = string.Format(sqlFormat, tableName, string.Join("\", \"", columnNames), unique ? "unique" : "", indexName);
		return Execute(sql);
	}

	/// <summary>
	/// Creates an index for the specified table and column.
	/// </summary>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnName">Name of the column to index</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string indexName, string tableName, string columnName, bool unique = false)
	{
		return CreateIndex(indexName, tableName, new string[] { columnName }, unique);
	}

	/// <summary>
	/// Creates an index for the specified table and column.
	/// </summary>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnName">Name of the column to index</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string tableName, string columnName, bool unique = false)
	{
		return CreateIndex(tableName + "_" + columnName, tableName, columnName, unique);
	}

	/// <summary>
	/// Creates an index for the specified table and columns.
	/// </summary>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnNames">An array of column names to index</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex(string tableName, string[] columnNames, bool unique = false)
	{
		return CreateIndex(tableName + "_" + string.Join("_", columnNames), tableName, columnNames, unique);
	}

	/// <summary>
	/// Creates an index for the specified object property.
	/// e.g. CreateIndex&lt;Client&gt;(c => c.Name);
	/// </summary>
	/// <typeparam name="T">Type to reflect to a database table.</typeparam>
	/// <param name="property">Property to index</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public int CreateIndex<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, object>> property, bool unique = false)
	{
		MemberExpression mx;
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

		return CreateIndex(map.TableName, colName, unique);
	}

	[Preserve(AllMembers = true)]
	public class ColumnInfo
	{
		//			public int cid { get; set; }

		[Column("name")]
		public string Name { get; set; }

		//			[Column ("type")]
		//			public string ColumnType { get; set; }

		public int notnull { get; set; }

		//			public string dflt_value { get; set; }

		//			public int pk { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}

	/// <summary>
	/// Query the built-in sqlite table_info table for a specific tables columns.
	/// </summary>
	/// <returns>The columns contains in the table.</returns>
	/// <param name="tableName">Table name.</param>
	public List<ColumnInfo> GetTableInfo(string tableName)
	{
		var query = "pragma table_info(\"" + tableName + "\")";
		return Query<ColumnInfo>(query);
	}

	void MigrateTable(TableMapping map, List<ColumnInfo> existingCols)
	{
		var toBeAdded = new List<TableMapping.Column>();

		foreach (var p in map.Columns)
		{
			var found = false;
			foreach (var c in existingCols)
			{
				found = (string.Compare(p.Name, c.Name, StringComparison.OrdinalIgnoreCase) == 0);
				if (found)
					break;
			}
			if (!found)
			{
				toBeAdded.Add(p);
			}
		}

		if (toBeAdded.Any(x => x.IsPK))
		{
			throw new InvalidOperationException("A column set as a primary key cannot be added to an existing table.");
		}

		foreach (var p in toBeAdded)
		{
			var addCol = $"alter table \"{map.TableName}\" add column {Orm.SqlDecl(p, map.PrimaryKeyColumns.Length > 1)}";
			Execute(addCol);
		}
	}

	/// <summary>
	/// Creates a new SQLiteCommand. Can be overridden to provide a sub-class.
	/// </summary>
	/// <seealso cref="SQLiteCommand.OnInstanceCreated"/>
	protected virtual SQLiteCommand NewCommand()
	{
		return new SQLiteCommand(this);
	}

	/// <summary>
	/// Creates a new SQLiteCommand given the command text with arguments. Place a '?'
	/// in the command text for each of the arguments.
	/// </summary>
	/// <param name="cmdText">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="ps">
	/// Arguments to substitute for the occurences of '?' in the command text.
	/// </param>
	/// <returns>
	/// A <see cref="SQLiteCommand"/>
	/// </returns>
	public SQLiteCommand CreateCommand(string cmdText, params object[] ps)
	{
		if (!_open)
			throw SQLiteException.New(SQLite3Native.Result.Error, "Cannot create commands from unopened database");

		var cmd = NewCommand();
		cmd.CommandText = cmdText;
		foreach (var o in ps)
		{
			cmd.Bind(o);
		}
		return cmd;
	}

	/// <summary>
	/// Creates a new SQLiteCommand given the command text with named arguments. Place a "[@:$]VVV"
	/// in the command text for each of the arguments. VVV represents an alphanumeric identifier.
	/// For example, @name :name and $name can all be used in the query.
	/// </summary>
	/// <param name="cmdText">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of "[@:$]VVV" in the command text.
	/// </param>
	/// <returns>
	/// A <see cref="SQLiteCommand" />
	/// </returns>
	public SQLiteCommand CreateCommand(string cmdText, Dictionary<string, object> args)
	{
		if (!_open)
			throw SQLiteException.New(SQLite3Native.Result.Error, "Cannot create commands from unopened database");

		SQLiteCommand cmd = NewCommand();
		cmd.CommandText = cmdText;
		foreach (var kv in args)
		{
			cmd.Bind(kv.Key, kv.Value);
		}
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
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// The number of rows modified in the database as a result of this execution.
	/// </returns>
	public int Execute(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);

		if (TimeExecution)
		{
			if (_sw == null)
			{
				_sw = new Stopwatch();
			}
			_sw.Reset();
			_sw.Start();
		}

		var r = cmd.ExecuteNonQuery();

		if (TimeExecution)
		{
			_sw.Stop();
			_elapsedMilliseconds += _sw.ElapsedMilliseconds;
			Tracer?.Invoke(string.Format("Finished in {0} ms ({1:0.0} s total)", _sw.ElapsedMilliseconds, _elapsedMilliseconds / 1000.0));
		}

		return r;
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
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// The number of rows modified in the database as a result of this execution.
	/// </returns>
	public T ExecuteScalar<T>(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);

		if (TimeExecution)
		{
			if (_sw == null)
			{
				_sw = new Stopwatch();
			}
			_sw.Reset();
			_sw.Start();
		}

		var r = cmd.ExecuteScalar<T>();

		if (TimeExecution)
		{
			_sw.Stop();
			_elapsedMilliseconds += _sw.ElapsedMilliseconds;
			Tracer?.Invoke($"Finished in {_sw.ElapsedMilliseconds} ms ({_elapsedMilliseconds / 1000.0:0.0} s total)");
		}

		return r;
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the mapping automatically generated for
	/// the given type.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// </returns>
	public List<T> Query<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new()
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteQuery<T>();
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns the first column of each row of the result.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for the first column of each row returned by the query.
	/// </returns>
	public List<T> QueryScalars<T>(string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteQueryScalars<T>().ToList();
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the mapping automatically generated for
	/// the given type.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<T> DeferredQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new()
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteDeferredQuery<T>();
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the specified mapping. This function is
	/// only used by libraries in order to query the database via introspection. It is
	/// normally not used.
	/// </summary>
	/// <param name="map">
	/// A <see cref="TableMapping"/> to use to convert the resulting rows
	/// into objects.
	/// </param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// </returns>
	public List<T> Query<T>(TableMapping map, string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteQuery<T>(map);
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// It returns each row of the result using the specified mapping. This function is
	/// only used by libraries in order to query the database via introspection. It is
	/// normally not used.
	/// </summary>
	/// <param name="map">
	/// A <see cref="TableMapping"/> to use to convert the resulting rows
	/// into objects.
	/// </param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// An enumerable with one result for each row returned by the query.
	/// The enumerator (retrieved by calling GetEnumerator() on the result of this method)
	/// will call sqlite3_step on each call to MoveNext, so the database
	/// connection must remain open for the lifetime of the enumerator.
	/// </returns>
	public IEnumerable<object> DeferredQuery(TableMapping map, string query, params object[] args)
	{
		var cmd = CreateCommand(query, args);
		return cmd.ExecuteDeferredQuery<object>(map);
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
		return new TableQuery<T>(this);
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

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that
	/// the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
	/// </summary>
	/// <param name="pk">
	/// The primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key. Throws a not found exception
	/// if the object is not found.
	/// </returns>
	public T Get<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(object pk) where T : new()
	{
		var map = GetMapping(typeof(T));

		if (map.PKWhereSql == null)
			throw new ArgumentException("Cannot use Get() on a table that does not have a primary key");

		if (map.PrimaryKeyColumns.Length > 1)
			throw new NotImplementedException(); // TODO: implement

		return Query<T>($"select * from \"{map.TableName}\" {map.PKWhereSql}", pk).First();
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that
	/// the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
	/// </summary>
	/// <param name="pk">
	/// The primary key.
	/// </param>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	/// <returns>
	/// The object with the given primary key. Throws a not found exception
	/// if the object is not found.
	/// </returns>
	public T Get<T>(object pk, TableMapping map)
	{
		if (map.PKWhereSql == null)
			throw new ArgumentException("Cannot use Get() on a table that does not have a primary key");

		if (map.PrimaryKeyColumns.Length > 1)
			throw new NotImplementedException(); // TODO: implement

		return Query<T>(map, $"select * from \"{map.TableName}\" {map.PKWhereSql}", pk).First();
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the predicate from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="predicate">
	/// A predicate for which object to find.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate. Throws a not found exception
	/// if the object is not found.
	/// </returns>
	public T Get<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Expression<Func<T, bool>> predicate) where T : new()
	{
		return Table<T>().Where(predicate).First();
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that
	/// the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
	/// </summary>
	/// <param name="pk">
	/// The primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public T Find<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(object pk) where T : new()
	{
		var map = GetMapping(typeof(T));

		if (map.PKWhereSql == null)
			throw new ArgumentException("Cannot use Find() on a table that does not have a primary key");

		if (map.PrimaryKeyColumns.Length > 0)
			throw new NotImplementedException(); // TODO: implement

		return Query<T>($"select * from \"{map.TableName}\" {map.PKWhereSql}", pk).FirstOrDefault();
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that
	/// the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
	/// </summary>
	/// <param name="pk">
	/// The primary key.
	/// </param>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public T Find<T>(object pk, TableMapping map)
	{
		if (map.PKWhereSql == null)
			throw new ArgumentException("Cannot use Find() on a table that does not have a primary key");

		if (map.PrimaryKeyColumns.Length > 0)
			throw new NotImplementedException(); // TODO: implement

		return Query<T>(map, $"select * from \"{map.TableName}\" {map.PKWhereSql}", pk).FirstOrDefault();
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
	/// Attempts to retrieve the first object that matches the query from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public T FindWithQuery<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(string query, params object[] args) where T : new()
	{
		return Query<T>(query, args).FirstOrDefault();
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the query from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="map">
	/// The TableMapping used to identify the table.
	/// </param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurences of '?' in the query.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public object FindWithQuery<T>(TableMapping map, string query, params object[] args)
	{
		return Query<T>(map, query, args).FirstOrDefault();
	}

	/// <summary>
	/// Whether <see cref="BeginTransaction"/> has been called and the database is waiting for a <see cref="Commit"/>.
	/// </summary>
	public bool IsInTransaction
	{
		get { return _transactionDepth > 0; }
	}

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
		string retVal = "S" + _rand.Next(short.MaxValue) + "D" + depth;

		try
		{
			Execute("savepoint " + retVal);
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
	void RollbackTo(string savepoint, bool noThrow)
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
			int depth;
			if (Int32.TryParse(savepoint.Substring(firstLen + 1), out depth))
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
	/// Executes <paramref name="action"/> within a (possibly nested) transaction by wrapping it in a SAVEPOINT. If an
	/// exception occurs the whole transaction is rolled back, not just the current savepoint. The exception
	/// is rethrown.
	/// </summary>
	/// <param name="action">
	/// The <see cref="Action"/> to perform within a transaction. <paramref name="action"/> can contain any number
	/// of operations on the connection but should never call <see cref="BeginTransaction"/> or
	/// <see cref="Commit"/>.
	/// </param>
	public void RunInTransaction(Action action)
	{
		try
		{
			var savePoint = SaveTransactionPoint();
			action();
			Release(savePoint);
		}
		catch (Exception)
		{
			Rollback();
			throw;
		}
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
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	public int InsertAll(System.Collections.IEnumerable objects, bool runInTransaction = true)
	{
		var c = 0;
		if (runInTransaction)
		{
			RunInTransaction(() => {
				foreach (var r in objects)
				{
					c += Insert(r);
				}
			});
		}
		else
		{
			foreach (var r in objects)
			{
				c += Insert(r);
			}
		}
		return c;
	}

	/// <summary>
	/// Inserts all specified objects.
	/// </summary>
	/// <param name="objects">
	/// An <see cref="IEnumerable"/> of the objects to insert.
	/// </param>
	/// <param name="extra">
	/// Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of all objects in 'objects'.")]
	public int InsertAll(System.Collections.IEnumerable objects, string extra, bool runInTransaction = true)
	{
		var c = 0;
		if (runInTransaction)
		{
			RunInTransaction(() => {
				foreach (var r in objects)
				{
					c += Insert(r, extra);
				}
			});
		}
		else
		{
			foreach (var r in objects)
			{
				c += Insert(r, extra);
			}
		}
		return c;
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
	public int InsertAll(
		System.Collections.IEnumerable objects,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType,
		bool runInTransaction = true)
	{
		var c = 0;
		if (runInTransaction)
		{
			RunInTransaction(() => {
				foreach (var r in objects)
				{
					c += Insert(r, objType);
				}
			});
		}
		else
		{
			foreach (var r in objects)
			{
				c += Insert(r, objType);
			}
		}
		return c;
	}

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
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	public int Insert(object obj)
	{
		if (obj == null)
		{
			return 0;
		}
		return Insert(obj, "", Orm.GetType(obj));
	}

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// If a UNIQUE constraint violation occurs with
	/// some pre-existing object, this function deletes
	/// the old object.
	/// </summary>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	public int InsertOrReplace(object obj)
	{
		if (obj == null)
		{
			return 0;
		}
		return Insert(obj, "OR REPLACE", Orm.GetType(obj));
	}

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// </summary>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <param name="objType">
	/// The type of object to insert.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public int Insert(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType)
	{
		return Insert(obj, "", objType);
	}

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// If a UNIQUE constraint violation occurs with
	/// some pre-existing object, this function deletes
	/// the old object.
	/// </summary>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <param name="objType">
	/// The type of object to insert.
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	public int InsertOrReplace(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType)
	{
		return Insert(obj, "OR REPLACE", objType);
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
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	[RequiresUnreferencedCode("This method requires ''DynamicallyAccessedMemberTypes.All' on the runtime type of 'obj'.")]
	public int Insert(object obj, string extra)
	{
		if (obj == null)
		{
			return 0;
		}
		return Insert(obj, extra, Orm.GetType(obj));
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
	public int Insert(
		object obj,
		string extra,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType)
	{
		if (obj == null || objType == null)
		{
			return 0;
		}

		var map = GetMapping(objType);
		return Insert(obj, extra, map);
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
	public int Insert(
		object obj,
		string extra,
		TableMapping map)
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

		var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

		var cols = replacing ? map.InsertOrReplaceColumns : map.InsertColumns;
		var vals = new object[cols.Length];
		for (var i = 0; i < vals.Length; i++)
		{
			vals[i] = cols[i].GetValue(obj);
		}

		var insertCmd = GetInsertCommand(map, extra);
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
				if (SQLite3Native.ExtendedErrCode(this.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
				{
					throw NotNullConstraintViolationException.New(ex.Result, ex.Message, map, obj);
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

	readonly Dictionary<Tuple<string, string>, PreparedSqlLiteInsertCommand> _insertCommandMap = new Dictionary<Tuple<string, string>, PreparedSqlLiteInsertCommand>();

	PreparedSqlLiteInsertCommand GetInsertCommand(TableMapping map, string extra)
	{
		PreparedSqlLiteInsertCommand prepCmd;

		var key = Tuple.Create(map.MappedType.FullName, extra);

		lock (_insertCommandMap)
		{
			if (_insertCommandMap.TryGetValue(key, out prepCmd))
			{
				return prepCmd;
			}
		}

		prepCmd = CreateInsertCommand(map, extra);

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

	PreparedSqlLiteInsertCommand CreateInsertCommand(TableMapping map, string extra)
	{
		var cols = map.InsertColumns;
		string insertSql;
		if (cols.Length == 0 && map.Columns.Length == 1 && map.Columns[0].IsAutoInc)
		{
			insertSql = string.Format("insert {1} into \"{0}\" default values", map.TableName, extra);
		}
		else
		{
			var replacing = string.Compare(extra, "OR REPLACE", StringComparison.OrdinalIgnoreCase) == 0;

			if (replacing)
			{
				cols = map.InsertOrReplaceColumns;
			}

			insertSql = string.Format("insert {3} into \"{0}\"({1}) values ({2})", map.TableName,
				string.Join(",", (from c in cols
					select "\"" + c.Name + "\"").ToArray()),
				string.Join(",", (from c in cols
					select "?").ToArray()), extra);

		}

		var insertCommand = new PreparedSqlLiteInsertCommand(this, insertSql);
		return insertCommand;
	}

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
	public int Update(object obj)
	{
		if (obj == null)
		{
			return 0;
		}
		return Update(obj, Orm.GetType(obj));
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
	public int Update(
		object obj,
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type objType)
	{
		int rowsAffected = 0;
		if (obj == null || objType == null)
		{
			return 0;
		}

		var map = GetMapping(objType);

		if (map.PrimaryKeyColumns.Length == 0)
		{
			throw new NotSupportedException("Cannot update " + map.TableName + ": it has no PK");
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
			rowsAffected = Execute(q, ps.ToArray());
		}
		catch (SQLiteException ex)
		{

			if (ex.Result == SQLite3Native.Result.Constraint && SQLite3Native.ExtendedErrCode(this.Handle) == SQLite3Native.ExtendedResult.ConstraintNotNull)
			{
				throw NotNullConstraintViolationException.New(ex, map, obj);
			}

			throw;
		}

		return rowsAffected;
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
	public int UpdateAll(System.Collections.IEnumerable objects, bool runInTransaction = true)
	{
		var c = 0;
		if (runInTransaction)
		{
			RunInTransaction(() => {
				foreach (var r in objects)
				{
					c += Update(r);
				}
			});
		}
		else
		{
			foreach (var r in objects)
			{
				c += Update(r);
			}
		}
		return c;
	}

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

		var q = string.Format("delete from \"{0}\" {1}", map.TableName, map.PKWhereSql);

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
		T>(object primaryKey)
	{
		return Delete(primaryKey, GetMapping(typeof(T)));
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
	public int Delete(object primaryKey, TableMapping map)
	{
		if (map.PrimaryKeyColumns.Length == 0)
		{
			throw new NotSupportedException("Cannot delete " + map.TableName + ": it has no PK");
		}
		var q = string.Format("delete from \"{0}\" {1}", map.TableName, map.PKWhereSql);
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
		var map = GetMapping(typeof(T));
		return DeleteAll(map);
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
		var query = string.Format("delete from \"{0}\"", map.TableName);
		var count = Execute(query);

		return count;
	}

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
			throw SQLiteException.New(r, "Failed to open destination database");
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
			throw SQLiteException.New(r, msg);
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

	public void Close()
	{
		Dispose(true);
	}

	protected virtual void Dispose(bool disposing)
	{
		var useClose2 = LibVersionNumber >= 3007014;

		if (_open && Handle != NullHandle)
		{
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
						throw SQLiteException.New(r, msg);
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

public enum CreateTableResult
{
	Created,
	Migrated,
}

public class CreateTablesResult
{
	public Dictionary<Type, CreateTableResult> Results { get; private set; }

	public CreateTablesResult()
	{
		Results = new Dictionary<Type, CreateTableResult>();
	}
}