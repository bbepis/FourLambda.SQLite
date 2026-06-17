using System.Linq.Expressions;

namespace FourLambda.SQLite;

#pragma warning disable 1591
public static class SQLiteConnectionAsyncExtensions
{
	#region Execute Async

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// Use this method instead of Query when you don't expect rows back. Such cases include
	/// INSERTs, UPDATEs, and DELETEs.
	/// You can set the Trace or TimeExecution properties of the connection
	/// to profile execution.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// The number of rows modified in the database as a result of this execution.
	/// </returns>
	public static Task<int> ExecuteAsync(this SQLiteConnection connection, string query, params object[] args)
	{
		return Task.Run(() => connection.Execute(query, args));
	}

	/// <summary>
	/// Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
	/// in the command text for each of the arguments and then executes that command.
	/// Use this method when return primitive values.
	/// You can set the Trace or TimeExecution properties of the connection
	/// to profile execution.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// The scalar value from the first column of the first row of the result.
	/// </returns>
	public static Task<T> ExecuteScalarAsync<T>(this SQLiteConnection connection, string query, params object[] args)
	{
		return Task.Run(() => connection.ExecuteScalar<T>(query, args));
	}

	#endregion

	#region Query Async

	private static async IAsyncEnumerable<T> ConvertToAsyncEnumerable<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(Func<IEnumerable<T>> innerEnumerable)
	{
		using var enumerator = await Task.Run(() => innerEnumerable().GetEnumerator());

		while (await Task.Run(enumerator.MoveNext))
		{
			yield return enumerator.Current;
		}
	}

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
	/// <param name="connection">The database connection.</param>
	/// <returns>
	/// An async enumerable with one result for each row returned by the query.
	/// </returns>
	public static IAsyncEnumerable<T> QueryAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection)
	{
		return ConvertToAsyncEnumerable(() => connection.Query<T>());
	}

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
	/// <param name="connection">The database connection.</param>
	/// <param name="query">
	/// The fully escaped SQL.
	/// </param>
	/// <param name="args">
	/// Arguments to substitute for the occurrences of '?' in the query.
	/// </param>
	/// <returns>
	/// An async enumerable with one result for each row returned by the query.
	/// </returns>
	public static IAsyncEnumerable<T> QueryAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, string query, params object[] args)
	{
		return ConvertToAsyncEnumerable(() => connection.Query<T>(query, args));
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
	/// <param name="connection">The database connection.</param>
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
	/// An async enumerable with one result for each row returned by the query.
	/// </returns>
	public static IAsyncEnumerable<T> QueryAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, string query, params object[] args)
	{
		return ConvertToAsyncEnumerable(() => connection.Query<T>(map, query, args));
	}

	/// <summary>
	/// Creates a query using the table defined by the <typeparamref name="T"/> type, and returns fetched data.
	/// </summary>
	/// <typeparam name="T">
	///	The type to load data into. This must be a type that can be used to build a table definition.
	/// </typeparam>
	/// <param name="connection">The database connection.</param>
	/// <param name="predicate">
	/// A predicate on which to filter rows on, as a WHERE query.
	/// </param>
	/// <returns>
	/// An async enumerable with one result for each row returned by the query.
	/// </returns>
	public static IAsyncEnumerable<T> QueryAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, Expression<Func<T, bool>> predicate) where T : new()
	{
		return ConvertToAsyncEnumerable(() => connection.Query<T>(predicate));
	}

	/// <summary>
	/// Creates a query using the table defined by the <typeparamref name="T"/> type, and returns fetched data.
	/// </summary>
	/// <param name="connection">The database connection.</param>
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
	/// An async enumerable with one result for each row returned by the query.
	/// </returns>
	public static IAsyncEnumerable<T> QueryAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, Expression<Func<T, bool>> predicate) where T : new()
	{
		return ConvertToAsyncEnumerable(() => connection.Query<T>(map, predicate));
	}

	#endregion

	#region Find Async

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the table
	/// associated with the specified type. Use of this method requires that the type has primary key(s) defined.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to find.</typeparam>
	/// <param name="primaryKey">
	/// The primary key. Provide multiple objects for a table with a composite primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public static Task<T?> FindAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, params object[] primaryKey) where T : new()
	{
		return Task.Run(() => connection.Find<T>(primaryKey));
	}

	/// <summary>
	/// Attempts to retrieve an object with the given primary key from the provided map.
	/// Use of this method requires that the map has primary keys defined.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to find.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="primaryKey">
	/// The primary key. Provide multiple objects for a table with a composite primary key.
	/// </param>
	/// <returns>
	/// The object with the given primary key or null
	/// if the object is not found.
	/// </returns>
	public static Task<T?> FindAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, params object[] primaryKey)
	{
		return Task.Run(() => connection.Find<T>(map, primaryKey));
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the predicate from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to find.</typeparam>
	/// <param name="predicate">
	/// A predicate for which object to find.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public static Task<T> FindAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, Expression<Func<T, bool>> predicate) where T : new()
	{
		return Task.Run(() => connection.Find<T>(predicate));
	}

	/// <summary>
	/// Attempts to retrieve the first object that matches the predicate from the table
	/// associated with the specified type.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to find.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="predicate">
	/// A predicate for which object to find.
	/// </param>
	/// <returns>
	/// The object that matches the given predicate or null
	/// if the object is not found.
	/// </returns>
	public static Task<T> FindAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, Expression<Func<T, bool>> predicate) where T : new()
	{
		return Task.Run(() => connection.Find<T>(map, predicate));
	}

	#endregion

	#region Insert Async

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to insert.</typeparam>
	/// <param name="obj">
	/// The object to insert.
	/// </param>
	/// <param name="conflictAction">The conflict action to use on constraint violations.</param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public static Task<int> InsertAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, T obj, InsertConflictAction conflictAction = InsertConflictAction.Abort)
	{
		return Task.Run(() => connection.Insert<T>(obj, conflictAction));
	}

	/// <summary>
	/// Inserts the given object (and updates its
	/// auto incremented primary key if it has one).
	/// The return value is the number of rows added to the table.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to insert.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="item">
	/// The object to insert.
	/// </param>
	/// <param name="conflictAction">The conflict action to use on constraint violations.</param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public static Task<int> InsertAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, T item, InsertConflictAction conflictAction = InsertConflictAction.Abort)
	{
		return Task.Run(() => connection.Insert<T>(map, item, conflictAction));
	}

	/// <summary>
	/// Inserts all specified objects.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to insert.</typeparam>
	/// <param name="objects">
	/// An <see cref="IEnumerable{T}"/> of the objects to insert.
	/// </param>
	/// <param name="conflictAction">The conflict action to use on constraint violations.</param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public static Task<int> InsertAllAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, IEnumerable<T> objects, InsertConflictAction conflictAction = InsertConflictAction.Abort, bool runInTransaction = true)
	{
		return Task.Run(() => connection.InsertAll<T>(objects, conflictAction, runInTransaction));
	}

	/// <summary>
	/// Inserts all specified objects.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to insert.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="objects">
	/// An <see cref="IEnumerable{T}"/> of the objects to insert.
	/// </param>
	/// <param name="conflictAction">The conflict action to use on constraint violations.</param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the inserts should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows added to the table.
	/// </returns>
	public static Task<int> InsertAllAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, IEnumerable<T> objects, InsertConflictAction conflictAction = InsertConflictAction.Abort, bool runInTransaction = true)
	{
		return Task.Run(() => connection.InsertAll<T>(map, objects, conflictAction, runInTransaction));
	}

	#endregion

	#region Update Async

	/// <summary>
	/// Updates all of the columns of a table using the specified object
	/// except for its primary key.
	/// The object is required to have a primary key.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to update.</typeparam>
	/// <param name="obj">
	/// The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <returns>
	/// The number of rows updated.
	/// </returns>
	public static Task<int> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, T obj)
	{
		return Task.Run(() => connection.Update<T>(obj));
	}

	/// <summary>
	/// Updates all of the columns of a table using the specified object
	/// except for its primary key.
	/// The object is required to have a primary key.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to update.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="obj">
	/// The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <returns>
	/// The number of rows updated.
	/// </returns>
	public static Task<int> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, T obj)
	{
		return Task.Run(() => connection.Update<T>(map, obj));
	}

	/// <summary>
	/// Updates all specified objects.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to update.</typeparam>
	/// <param name="objects">
	/// An <see cref="IEnumerable{T}"/> of the objects to update.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the updates should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	public static Task<int> UpdateAllAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, IEnumerable<T> objects, bool runInTransaction = true)
	{
		return Task.Run(() => connection.UpdateAll<T>(objects, runInTransaction));
	}

	/// <summary>
	/// Updates all specified objects.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to update.</typeparam>
	/// <param name="map">The table mapping.</param>
	/// <param name="objects">
	/// An <see cref="IEnumerable{T}"/> of the objects to update.
	/// </param>
	/// <param name="runInTransaction">
	/// A boolean indicating if the updates should be wrapped in a transaction.
	/// </param>
	/// <returns>
	/// The number of rows modified.
	/// </returns>
	public static Task<int> UpdateAllAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableMapping map, IEnumerable<T> objects, bool runInTransaction = true)
	{
		return Task.Run(() => connection.UpdateAll<T>(map, objects, runInTransaction));
	}

	#endregion

	#region Delete Async

	/// <summary>
	/// Deletes the given object from the database using its primary key.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="item">
	/// The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
	/// </param>
	/// <returns>
	/// The number of rows deleted.
	/// </returns>
	public static Task<int> DeleteAsync(this SQLiteConnection connection, object item)
	{
		return Task.Run(() => connection.Delete(item));
	}

	/// <summary>
	/// Deletes the object with the specified primary key.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of object to delete.</typeparam>
	/// <param name="primaryKey">
	/// The primary key of the object to delete.
	/// </param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public static Task<int> DeleteAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, params object[] primaryKey)
	{
		return Task.Run(() => connection.Delete<T>(primaryKey));
	}

	/// <summary>
	/// Deletes the object with the specified primary key.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="map">The table mapping used to identify the table.</param>
	/// <param name="primaryKey">
	/// The primary key of the object to delete.
	/// </param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public static Task<int> DeleteAsync(this SQLiteConnection connection, TableMapping map, params object[] primaryKey)
	{
		return Task.Run(() => connection.Delete(map, primaryKey));
	}

	/// <summary>
	/// Deletes all the objects from the specified table.
	/// WARNING WARNING: Let me repeat. It deletes ALL the objects from the
	/// specified table. Do you really want to do that?
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">The type of objects to delete.</typeparam>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public static Task<int> DeleteAllAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection)
	{
		return Task.Run(() => connection.DeleteAll<T>());
	}

	/// <summary>
	/// Deletes all the objects from the specified table.
	/// WARNING WARNING: Let me repeat. It deletes ALL the objects from the
	/// specified table. Do you really want to do that?
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="map">The table mapping used to identify the table.</param>
	/// <returns>
	/// The number of objects deleted.
	/// </returns>
	public static Task<int> DeleteAllAsync(this SQLiteConnection connection, TableMapping map)
	{
		return Task.Run(() => connection.DeleteAll(map));
	}

	#endregion

	#region Schema Async

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="SQLiteConnection.GetMapping{T}"/>.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">Type to reflect to a database table.</typeparam>
	/// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public static Task<SQLiteConnection.CreateTableResult> CreateTableAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, TableCreateFlags createFlags = TableCreateFlags.None)
	{
		return Task.Run(() => connection.CreateTable<T>(createFlags));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified type, including any constraints or indexes.
	/// Mapping is automatically generated via <see cref="SQLiteConnection.GetMapping"/>.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="type">Type to reflect to a database table.</param>
	/// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public static Task<SQLiteConnection.CreateTableResult> CreateTableAsync(
		this SQLiteConnection connection,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
		TableCreateFlags createFlags = TableCreateFlags.None)
	{
		return Task.Run(() => connection.CreateTable(type, createFlags));
	}

	/// <summary>
	/// Executes a "create table if not exists" on the database using the specified mapping, including any constraints or indexes.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="map">Type to reflect to a database table.</param>
	/// <returns>
	/// Whether the table was created or migrated.
	/// </returns>
	public static Task<SQLiteConnection.CreateTableResult> CreateTableAsync(this SQLiteConnection connection, TableMapping map)
	{
		return Task.Run(() => connection.CreateTable(map));
	}

	/// <summary>
	/// Executes a "drop table" on the table associated with the object. This is irreversible.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">Type of the table to drop.</typeparam>
	/// <returns>The number of rows affected.</returns>
	public static Task<int> DropTableAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection)
	{
		return Task.Run(() => connection.DropTable<T>());
	}

	/// <summary>
	/// Executes a "drop table" on the table specified in the mapping. This is irreversible.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="map">The table mapping identifying the table to drop.</param>
	/// <returns>The number of rows affected.</returns>
	public static Task<int> DropTableAsync(this SQLiteConnection connection, TableMapping map)
	{
		return Task.Run(() => connection.DropTable(map));
	}

	/// <summary>
	/// Creates an index for the specified object property.
	/// e.g. CreateIndex&lt;Client&gt;(c => c.Name);
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <typeparam name="T">Type to reflect to a database table.</typeparam>
	/// <param name="property">Property to index.</param>
	/// <param name="indexName">The name of the index.</param>
	/// <param name="unique">Whether the index should be unique.</param>
	/// <returns>Zero on success.</returns>
	public static Task<int> CreateIndexAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(this SQLiteConnection connection, Expression<Func<T, object>> property, string? indexName = null, bool unique = false)
	{
		return Task.Run(() => connection.CreateIndex<T>(property, indexName, unique));
	}

	/// <summary>
	/// Creates an index for the specified table and column.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnName">Name of the column to index</param>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public static Task<int> CreateIndexAsync(this SQLiteConnection connection, string tableName, string columnName, string? indexName = null, bool unique = false)
	{
		return Task.Run(() => connection.CreateIndex(tableName, columnName, indexName, unique));
	}

	/// <summary>
	/// Creates an index for the specified table and columns.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="tableName">Name of the database table</param>
	/// <param name="columnNames">An array of column names to index</param>
	/// <param name="indexName">Name of the index to create</param>
	/// <param name="unique">Whether the index should be unique</param>
	/// <returns>Zero on success.</returns>
	public static Task<int> CreateIndexAsync(this SQLiteConnection connection, string tableName, string[] columnNames, string? indexName = null, bool unique = false)
	{
		return Task.Run(() => connection.CreateIndex(tableName, columnNames, indexName, unique));
	}

	/// <summary>
	/// Query the built-in sqlite table_info table for a specific tables columns.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="tableName">Table name.</param>
	/// <returns>The columns contained in the table.</returns>
	public static Task<ColumnDefinition[]> GetTableInfoAsync(this SQLiteConnection connection, string tableName)
	{
		return Task.Run(() => connection.GetTableInfo(tableName));
	}

	#endregion

	#region Misc Async

	/// <summary>
	/// Backup the entire database to the specified path.
	/// </summary>
	/// <param name="connection">The database connection.</param>
	/// <param name="destinationDatabasePath">Path to backup file.</param>
	/// <param name="databaseName">The name of the database to backup (usually "main").</param>
	public static Task BackupAsync(this SQLiteConnection connection, string destinationDatabasePath, string databaseName)
	{
		return Task.Run(() => connection.Backup(destinationDatabasePath, databaseName));
	}

	#endregion
}
