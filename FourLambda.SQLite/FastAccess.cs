namespace FourLambda.SQLite;

internal class FastColumnSetter
{
	/// <summary>
	/// Gets a <see cref="MethodInfo"/> for a generic <see cref="GetFastSetterMethodInfoUnsafe"/> method, suppressing AOT warnings.
	/// </summary>
	/// <param name="mappedType">The type of the destination object that the query will read into.</param>
	/// <returns>The generic <see cref="MethodInfo"/> instance.</returns>
	/// <remarks>This should only be called when <paramref name="mappedType"/> is a reference type.</remarks>
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This method is only ever called when 'mappedType' is a reference type.")]
	internal static MethodInfo GetFastSetterMethodInfoUnsafe(Type mappedType)
	{
		return typeof(FastColumnSetter)
			.GetMethod(nameof(GetFastSetter),
				BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(mappedType);
	}

	/// <summary>
	/// Creates a delegate that can be used to quickly set object members from query columns.
	///
	/// Note that this frontloads the slow reflection-based type checking for columns to only happen once at the beginning of a query,
	/// and then afterwards each row of the query can invoke the delegate returned by this function to get much better performance (up to 10x speed boost, depending on query size and platform).
	/// </summary>
	/// <typeparam name="T">The type of the destination object that the query will read into</typeparam>
	/// <param name="conn">The active connection.  Note that this is primarily needed in order to read preferences regarding how certain data types (such as TimeSpan / DateTime) should be encoded in the database.</param>
	/// <param name="column">The table mapping used to map the statement column to a member of the destination object type</param>
	/// <returns>
	/// A delegate for fast-setting of object members from statement columns.
	///
	/// If no fast setter is available for the requested column (enums in particular cause headache), then this function returns null.
	/// </returns>
	internal static Action<object, Sqlite3Statement, int> GetFastSetter<T>(SQLiteConnection conn, TableColumn column)
	{
		Action<object, Sqlite3Statement, int> fastSetter = null;

		Type clrType = column.PropertyInfo.PropertyType;

		var clrTypeInfo = clrType.GetTypeInfo();
		if (clrTypeInfo.IsGenericType && clrTypeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			clrType = clrTypeInfo.GenericTypeArguments[0];
			clrTypeInfo = clrType.GetTypeInfo();
		}

		if (clrType == typeof(string))
		{
			fastSetter = CreateTypedSetterDelegate<T, string>(column, (stmt, index) => {
				return SQLite3Native.ColumnString(stmt, index);
			});
		}
		else if (clrType == typeof(Int32))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, int>(column, (stmt, index) => {
				return SQLite3Native.ColumnInt(stmt, index);
			});
		}
		else if (clrType == typeof(bool))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, bool>(column, (stmt, index) => {
				return SQLite3Native.ColumnInt(stmt, index) == 1;
			});
		}
		else if (clrType == typeof(double))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, double>(column, (stmt, index) => {
				return SQLite3Native.ColumnDouble(stmt, index);
			});
		}
		else if (clrType == typeof(float))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, float>(column, (stmt, index) => {
				return (float)SQLite3Native.ColumnDouble(stmt, index);
			});
		}
		else if (clrType == typeof(TimeSpan))
		{
			if (!column.StoreAsText)
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, TimeSpan>(column, (stmt, index) => {
					return new TimeSpan(SQLite3Native.ColumnInt64(stmt, index));
				});
			}
			else
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, TimeSpan>(column, (stmt, index) => {
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column.StoreAsTextFormat != null && TimeSpan.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, TimeSpanStyles.None, out var resultTime))
						return resultTime;

					if (TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out resultTime))
						return resultTime;

					resultTime = TimeSpan.Parse(text);
					return resultTime;
				});
			}
		}
		else if (clrType == typeof(DateTime))
		{
			if (!column.StoreAsText)
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateTime>(column, (stmt, index) => {
					return new DateTime(SQLite3Native.ColumnInt64(stmt, index));
				});
			}
			else
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateTime>(column, (stmt, index) => {
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column.StoreAsTextFormat != null && DateTime.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return DateTime.Parse(text);
				});
			}
		}
		else if (clrType == typeof(DateTimeOffset))
		{
			if (!column.StoreAsText)
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateTimeOffset>(column, (stmt, index) => {
					return new DateTimeOffset(SQLite3Native.ColumnInt64(stmt, index), TimeSpan.Zero);
				});
			}
			else
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateTimeOffset>(column, (stmt, index) => {
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column.StoreAsTextFormat != null && DateTimeOffset.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
						return resultTime;

					return DateTimeOffset.Parse(text);
				});
			}
		}
		else if (clrType == typeof(DateOnly))
		{
			if (!column.StoreAsText)
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateOnly>(column, (stmt, index) => {
					return DateOnly.FromDateTime(new DateTime(SQLite3Native.ColumnInt64(stmt, index)));
				});
			}
			else
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, DateOnly>(column, (stmt, index) => {
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column.StoreAsTextFormat != null && DateOnly.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultTime))
						return resultTime;

					return DateOnly.Parse(text);
				});
			}
		}
		else if (clrType == typeof(TimeOnly))
		{
			if (!column.StoreAsText)
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, TimeOnly>(column, (stmt, index) => {
					return new TimeOnly(SQLite3Native.ColumnInt64(stmt, index));
				});
			}
			else
			{
				fastSetter = CreateNullableTypedSetterDelegate<T, TimeOnly>(column, (stmt, index) => {
					var text = SQLite3Native.ColumnString(stmt, index);

					if (column.StoreAsTextFormat != null && TimeOnly.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultTime))
						return resultTime;

					return TimeOnly.Parse(text);
				});
			}
		}
		else if (clrTypeInfo.IsEnum)
		{
			// NOTE: Not sure of a good way (if any?) to do a strongly-typed fast setter like this for enumerated types -- for now, return null and column sets will revert back to the safe (but slow) Reflection-based method of column prop.Set()
		}
		else if (clrType == typeof(Int64))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, Int64>(column, (stmt, index) => {
				return SQLite3Native.ColumnInt64(stmt, index);
			});
		}
		else if (clrType == typeof(UInt64))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, UInt64>(column, (stmt, index) => {
				return (ulong)SQLite3Native.ColumnInt64(stmt, index);
			});
		}
		else if (clrType == typeof(UInt32))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, UInt32>(column, (stmt, index) => {
				return (uint)SQLite3Native.ColumnInt64(stmt, index);
			});
		}
		else if (clrType == typeof(decimal))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, decimal>(column, (stmt, index) => {
				return (decimal)SQLite3Native.ColumnDouble(stmt, index);
			});
		}
		else if (clrType == typeof(Byte))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, Byte>(column, (stmt, index) => {
				return (byte)SQLite3Native.ColumnInt(stmt, index);
			});
		}
		else if (clrType == typeof(UInt16))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, UInt16>(column, (stmt, index) => {
				return (ushort)SQLite3Native.ColumnInt(stmt, index);
			});
		}
		else if (clrType == typeof(Int16))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, Int16>(column, (stmt, index) => {
				return (short)SQLite3Native.ColumnInt(stmt, index);
			});
		}
		else if (clrType == typeof(sbyte))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, sbyte>(column, (stmt, index) => {
				return (sbyte)SQLite3Native.ColumnInt(stmt, index);
			});
		}
		else if (clrType == typeof(byte[]))
		{
			fastSetter = CreateTypedSetterDelegate<T, byte[]>(column, (stmt, index) => {
				return SQLite3Native.ColumnByteArray(stmt, index);
			});
		}
		else if (clrType == typeof(Guid))
		{
			fastSetter = CreateNullableTypedSetterDelegate<T, Guid>(column, (stmt, index) => {
				var text = SQLite3Native.ColumnString(stmt, index);
				return new Guid(text);
			});
		}
		else
		{
			// NOTE: Will fall back to the slow setter method in the event that we are unable to create a fast setter delegate for a particular column type
		}
		return fastSetter;
	}

	/// <summary>
	/// This creates a strongly typed delegate that will permit fast setting of column values given a Sqlite3Statement and a column index.
	///
	/// Note that this is identical to CreateTypedSetterDelegate(), but has an extra check to see if it should create a nullable version of the delegate.
	/// </summary>
	/// <typeparam name="TObjectType">The type of the object whose member column is being set</typeparam>
	/// <typeparam name="TColumnMemberType">The CLR type of the member in the object which corresponds to the given SQLite columnn</typeparam>
	/// <param name="column">The column mapping that identifies the target member of the destination object</param>
	/// <param name="getColumnValue">A lambda that can be used to retrieve the column value at query-time</param>
	/// <returns>A strongly-typed delegate</returns>
	private static Action<object, Sqlite3Statement, int> CreateNullableTypedSetterDelegate<TObjectType, TColumnMemberType>(TableColumn column, Func<Sqlite3Statement, int, TColumnMemberType> getColumnValue) where TColumnMemberType : struct
	{
		var clrTypeInfo = column.PropertyInfo.PropertyType.GetTypeInfo();
		bool isNullable = false;

		if (clrTypeInfo.IsGenericType && clrTypeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			isNullable = true;
		}

		if (isNullable)
		{
			var setProperty = (Action<TObjectType, TColumnMemberType?>)Delegate.CreateDelegate(
				typeof(Action<TObjectType, TColumnMemberType?>), null,
				column.PropertyInfo.GetSetMethod());

			return (o, stmt, i) => {
				var colType = SQLite3Native.ColumnType(stmt, i);
				if (colType != SQLite3Native.ColType.Null)
					setProperty.Invoke((TObjectType)o, getColumnValue.Invoke(stmt, i));
			};
		}

		return CreateTypedSetterDelegate<TObjectType, TColumnMemberType>(column, getColumnValue);
	}

	/// <summary>
	/// This creates a strongly typed delegate that will permit fast setting of column values given a Sqlite3Statement and a column index.
	/// </summary>
	/// <typeparam name="TObjectType">The type of the object whose member column is being set</typeparam>
	/// <typeparam name="TColumnMemberType">The CLR type of the member in the object which corresponds to the given SQLite column</typeparam>
	/// <param name="column">The column mapping that identifies the target member of the destination object</param>
	/// <param name="getColumnValue">A lambda that can be used to retrieve the column value at query-time</param>
	/// <returns>A strongly-typed delegate</returns>
	private static Action<object, Sqlite3Statement, int> CreateTypedSetterDelegate<TObjectType, TColumnMemberType>(TableColumn column, Func<Sqlite3Statement, int, TColumnMemberType> getColumnValue)
	{
		var setProperty = (Action<TObjectType, TColumnMemberType>)Delegate.CreateDelegate(
			typeof(Action<TObjectType, TColumnMemberType>), null,
			column.PropertyInfo.GetSetMethod());

		return (o, stmt, i) => {
			var colType = SQLite3Native.ColumnType(stmt, i);
			if (colType != SQLite3Native.ColType.Null)
				setProperty.Invoke((TObjectType)o, getColumnValue.Invoke(stmt, i));
		};
	}
}