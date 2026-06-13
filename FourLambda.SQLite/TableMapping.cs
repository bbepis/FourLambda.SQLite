namespace FourLambda.SQLite;

[AttributeUsage(AttributeTargets.Class)]
public class TableAttribute : Attribute
{
	public string Name { get; set; }

	/// <summary>
	/// Flag whether to create the table without rowid (see https://sqlite.org/withoutrowid.html)
	///
	/// The default is <c>false</c> so that sqlite adds an implicit <c>rowid</c> to every table created.
	/// </summary>
	public bool WithoutRowId { get; set; }

	public bool Strict { get; set; }

	public TableAttribute(string name)
	{
		Name = name;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
	public string Name { get; set; }

	public ColumnAttribute(string name)
	{
		Name = name;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyAttribute : Attribute
{
	public int Order { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class AutoIncrementAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class IndexedAttribute : Attribute
{
	public string Name { get; set; }
	public int Order { get; set; }
	public virtual bool Unique { get; set; }

	public IndexedAttribute()
	{
	}

	public IndexedAttribute(string name, int order)
	{
		Name = name;
		Order = order;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class UniqueAttribute : IndexedAttribute
{
	public override bool Unique
	{
		get { return true; }
		set { /* throw?  */ }
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class MaxLengthAttribute : Attribute
{
	public int Value { get; private set; }

	public MaxLengthAttribute(int length)
	{
		Value = length;
	}
}

public sealed class PreserveAttribute : System.Attribute
{
	public bool AllMembers;
	public bool Conditional;
}

/// <summary>
/// Select the collating sequence to use on a column.
/// "BINARY", "NOCASE", and "RTRIM" are supported.
/// "BINARY" is the default.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CollationAttribute : Attribute
{
	public string Value { get; private set; }

	public CollationAttribute(string collation)
	{
		Value = collation;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class NotNullAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class StoreAsTextAttribute : Attribute
{
	/// <summary>
	/// The format argument to pass into <see cref="DateTime.ToString(string)"/> or equivalent methods.
	/// </summary>
	public string? Format { get; set; }
}

public class TableMapping
{
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	public Type MappedType { get; private set; }

	public string TableName { get; private set; }

	public bool WithoutRowId { get; private set; }

	public bool Strict { get; private set; }

	public Column[] Columns { get; private set; }

	public Column[] PrimaryKeyColumns { get; private set; }

	public string? PKWhereSql { get; private set; }

	public CreateFlags CreateFlags { get; private set; }

	internal MapMethod Method { get; private set; } = MapMethod.ByName;

	readonly Column? _autoPk;
	readonly Column[] _insertColumns;
	readonly Column[] _insertOrReplaceColumns;

	public TableMapping(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type,
		CreateFlags createFlags = CreateFlags.None)
	{
		MappedType = type;
		CreateFlags = createFlags;

		var tableAttr = type.GetCustomAttribute<TableAttribute>();

		TableName = (tableAttr != null && !string.IsNullOrEmpty(tableAttr.Name)) ? tableAttr.Name : MappedType.Name;
		WithoutRowId = tableAttr?.WithoutRowId ?? false;
		Strict = tableAttr?.Strict ?? false;

		var members = GetPublicMembers(type);
		var cols = new List<Column>(members.Count);
		foreach (var m in members)
		{
			var ignore = m.IsDefined(typeof(IgnoreAttribute), true);
			if (!ignore)
				cols.Add(new Column(m, createFlags));
		}
		Columns = cols.ToArray();

		PrimaryKeyColumns = Columns.Where(c => c.IsPK).OrderBy(c => c.PKOrder).ToArray();

		if (PrimaryKeyColumns.Length > 1)
		{
			if (PrimaryKeyColumns.Any(c => c.IsAutoInc))
				throw new ArgumentException("Table with composite primary key cannot have auto incrementing");

			if (PrimaryKeyColumns.Count(c => c.PKOrder == 0) > 1)
				throw new ArgumentException("Table with composite primary key must have explicit ordering of individual primary keys");
		}
		else
		{
			_autoPk = PrimaryKeyColumns.FirstOrDefault(x => x.IsAutoInc);
		}

		HasAutoIncPK = _autoPk != null;

		if (PrimaryKeyColumns.Length > 0)
		{
			// TODO: proper string escaping everywhere
			// TODO: cache built queries for SELECT, UPDATE, DELETE
			PKWhereSql = "where " + string.Join(" and ", PrimaryKeyColumns.Select(pk => $"\"{pk.Name}\" = ?"));
		}
		else
		{
			// People should not be calling Get/Find without a PK
			PKWhereSql = null;
		}

		_insertColumns = Columns.Where(c => !c.IsAutoInc).ToArray();
		_insertOrReplaceColumns = Columns.ToArray();
	}

	private IReadOnlyCollection<PropertyInfo> GetPublicMembers(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		Type type)
	{
		var members = new List<PropertyInfo>();
		var memberNames = new HashSet<string>();
		var newMembers = new List<PropertyInfo>();
		do
		{
			var ti = type.GetTypeInfo();
			newMembers.Clear();

			newMembers.AddRange(
				from p in ti.DeclaredProperties
				where !memberNames.Contains(p.Name) &&
				      p.CanRead && p.CanWrite &&
				      p.GetMethod != null && p.SetMethod != null &&
				      p.GetMethod.IsPublic && p.SetMethod.IsPublic &&
				      !p.GetMethod.IsStatic && !p.SetMethod.IsStatic
				select p);

			members.AddRange(newMembers);
			foreach (var m in newMembers)
				memberNames.Add(m.Name);

			type = ti.BaseType;
		}
		while (type != typeof(object));

		return members;
	}

	public bool HasAutoIncPK { get; private set; }

	public void SetAutoIncPK(object obj, long id)
	{
		if (_autoPk != null)
		{
			_autoPk.SetValue(obj, Convert.ChangeType(id, _autoPk.ColumnType, null));
		}
	}

	public Column[] InsertColumns
	{
		get
		{
			return _insertColumns;
		}
	}

	public Column[] InsertOrReplaceColumns
	{
		get
		{
			return _insertOrReplaceColumns;
		}
	}

	public Column FindColumnWithPropertyName(string propertyName)
	{
		var exact = Columns.FirstOrDefault(c => c.PropertyName == propertyName);
		return exact;
	}

	public Column FindColumn(string columnName)
	{
		if (Method != MapMethod.ByName)
			throw new InvalidOperationException($"This {nameof(TableMapping)} is not mapped by name, but {Method}.");

		var exact = Columns.FirstOrDefault(c => c.Name.ToLower() == columnName.ToLower());
		return exact;
	}

	public class Column
	{
		public string Name { get; private set; }

		public PropertyInfo PropertyInfo { get; private set; }

		public string PropertyName => PropertyInfo.Name;

		public Type ColumnType { get; private set; }

		public SqliteCellType SqliteType { get; private set; }

		public string Collation { get; private set; }

		public bool IsAutoInc { get; private set; }
		public bool IsAutoGuid { get; private set; }

		public bool IsPK => PKOrder != null;
		public int? PKOrder { get; private set; }

		public IEnumerable<IndexedAttribute> Indices { get; set; }

		public bool IsNullable { get; private set; }

		public int? MaxStringLength { get; private set; }

		public bool StoreAsText
		{
			get;
			set
			{
				field = value;
				SqliteType = Orm.SqlType(this);
			}
		}

		public string? StoreAsTextFormat { get; set; }

		public Column(PropertyInfo propertyInfo, CreateFlags createFlags = CreateFlags.None)
		{
			PropertyInfo = propertyInfo;
			var memberType = propertyInfo.PropertyType;

			var colAttr = propertyInfo.GetCustomAttribute<ColumnAttribute>();

			Name = colAttr?.Name ?? propertyInfo.Name;

			//If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
			ColumnType = Nullable.GetUnderlyingType(memberType) ?? memberType;
			Collation = Orm.Collation(propertyInfo);

			var memberStoreAsTextAttr = propertyInfo.GetCustomAttribute<StoreAsTextAttribute>();
			StoreAsText = memberStoreAsTextAttr != null || memberType.GetCustomAttribute<StoreAsTextAttribute>() != null;
			StoreAsTextFormat = memberStoreAsTextAttr?.Format;

			PKOrder = Orm.PKOrder(propertyInfo);

			var implicitPk = (createFlags & CreateFlags.ImplicitPK) == CreateFlags.ImplicitPK &&
			                 propertyInfo.Name.Equals(Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase);

			if (PKOrder == null && implicitPk)
				PKOrder = 0;

			var isAuto = Orm.IsAutoInc(propertyInfo) || (IsPK && ((createFlags & CreateFlags.AutoIncPK) == CreateFlags.AutoIncPK));
			IsAutoGuid = isAuto && ColumnType == typeof(Guid);
			IsAutoInc = isAuto && !IsAutoGuid;

			Indices = Orm.GetIndices(propertyInfo);

			var nullabilityInfo = new NullabilityInfoContext().Create(propertyInfo);

			bool explicitlyNull = Nullable.GetUnderlyingType(memberType) != null || nullabilityInfo.WriteState == NullabilityState.Nullable;

			if (explicitlyNull && IsPK)
				throw new ArgumentException("A column marked as primary key cannot be nullable.");

			if (explicitlyNull && Orm.IsMarkedNotNull(propertyInfo))
				throw new ArgumentException("A column cannot be marked as [NotNull] while having an explicitly nullable property type.");
			
			IsNullable = explicitlyNull || ((createFlags.HasFlag(CreateFlags.ImplicitNullable) || nullabilityInfo.WriteState == NullabilityState.Unknown) && !(IsPK || Orm.IsMarkedNotNull(propertyInfo)));
			MaxStringLength = Orm.MaxStringLength(propertyInfo);

			SqliteType = Orm.SqlType(this);
		}

		public void SetValue(object obj, object val)
		{
			if (val != null && ColumnType.GetTypeInfo().IsEnum)
				PropertyInfo.SetValue(obj, Enum.ToObject(ColumnType, val));
			else
				PropertyInfo.SetValue(obj, val);
		}

		public object? GetValue(object obj)
		{
			object? value = PropertyInfo.GetValue(obj);

			if (!StoreAsText || value == null)
				return value;

			return value switch
			{
				DateTime dateTime => dateTime.ToString(StoreAsTextFormat ?? "O"),
				TimeSpan timeSpan => timeSpan.ToString(StoreAsTextFormat ?? "c"),
				DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(StoreAsTextFormat ?? "O"),
				DateOnly dateOnly => dateOnly.ToString(StoreAsTextFormat ?? "O"),
				TimeOnly timeOnly => timeOnly.ToString(StoreAsTextFormat ?? "O"),
				IFormattable formattable => formattable.ToString(StoreAsTextFormat, null),
				_ => value
			};
		}
	}

	internal enum MapMethod
	{
		ByName,
		ByPosition
	}
}

public enum SqliteCellType
{
	Integer,
	Real,
	Text,
	Blob,
	Any
}

public static class Orm
{
	public const int DefaultMaxStringLength = 140;
	public const string ImplicitPkName = "Id";
	public const string ImplicitIndexSuffix = "Id";

	public static Type GetType(object obj)
	{
		if (obj == null)
			return typeof(object);
		var rt = obj as IReflectableType;
		if (rt != null)
			return rt.GetTypeInfo().AsType();
		return obj.GetType();
	}

	public static string SqlDecl(TableMapping.Column p, bool compositeKey)
	{
		var sqliteType = p.SqliteType switch
		{
			SqliteCellType.Integer => "INTEGER",
			SqliteCellType.Real => "REAL",
			SqliteCellType.Text => "TEXT",
			SqliteCellType.Blob => "BLOB",
			SqliteCellType.Any => "ANY",
			_ => throw new ArgumentOutOfRangeException()
		};

		string decl = $"\"{p.Name}\" {sqliteType} ";

		if (!compositeKey)
		{
			if (p.IsPK)
			{
				decl += "primary key ";
			}

			if (p.IsAutoInc)
			{
				decl += "autoincrement ";
			}
		}

		if (!p.IsNullable)
		{
			decl += "not null ";

			if (p.SqliteType == SqliteCellType.Integer)
				decl += "default 0 ";
		}
		if (!string.IsNullOrEmpty(p.Collation))
		{
			decl += "collate " + p.Collation + " ";
		}

		return decl;
	}

	public static SqliteCellType SqlType(TableMapping.Column p)
	{
		var clrType = p.ColumnType;

		if (clrType == typeof(bool) || clrType == typeof(Byte) || clrType == typeof(UInt16) || clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32) || clrType == typeof(UInt32) || clrType == typeof(Int64) || clrType == typeof(UInt64))
		{
			return SqliteCellType.Integer;
		}
		else if (clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal))
		{
			return SqliteCellType.Real;
		}
		else if (clrType == typeof(string) || clrType == typeof(StringBuilder) || clrType == typeof(Uri) || clrType == typeof(UriBuilder))
		{
			return SqliteCellType.Text;
		}
		else if (clrType == typeof(TimeSpan) || clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset) || clrType == typeof(TimeOnly) || clrType == typeof(DateOnly) || clrType.GetTypeInfo().IsEnum)
		{
			return p.StoreAsText ? SqliteCellType.Text : SqliteCellType.Integer;
		}
		else if (clrType == typeof(byte[]))
		{
			return SqliteCellType.Blob;
		}
		else if (clrType == typeof(Guid))
		{
			// TODO: add StoreAsText for blob
			return SqliteCellType.Text;
		}
		else if (clrType == typeof(object))
		{
			return SqliteCellType.Any;
		}
		else
		{
			throw new NotSupportedException("Unable to handle column type " + clrType);
		}
	}

	public static bool IsPK(MemberInfo p)
	{
		return p.GetCustomAttribute<PrimaryKeyAttribute>() != null;
	}

	public static int? PKOrder(MemberInfo p)
	{
		return p.GetCustomAttribute<PrimaryKeyAttribute>()?.Order;
	}

	public static string Collation(MemberInfo p)
	{
		return
			(p.CustomAttributes
				.Where(x => typeof(CollationAttribute) == x.AttributeType)
				.Select(x => {
					var args = x.ConstructorArguments;
					return args.Count > 0 ? ((args[0].Value as string) ?? "") : "";
				})
				.FirstOrDefault()) ?? "";
	}

	public static bool IsAutoInc(MemberInfo p)
	{
		return p.CustomAttributes.Any(x => x.AttributeType == typeof(AutoIncrementAttribute));
	}

	public static FieldInfo GetField(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		TypeInfo t,
		string name)
	{
		var f = t.GetDeclaredField(name);
		if (f != null)
			return f;
		return GetField(t.BaseType.GetTypeInfo(), name);
	}

	public static PropertyInfo GetProperty(
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
		TypeInfo t,
		string name)
	{
		var f = t.GetDeclaredProperty(name);
		if (f != null)
			return f;
		return GetProperty(t.BaseType.GetTypeInfo(), name);
	}

	public static IEnumerable<IndexedAttribute> GetIndices(MemberInfo p)
	{
		return p.GetCustomAttributes<IndexedAttribute>();
	}

	public static int? MaxStringLength(MemberInfo p)
	{
		return p.GetCustomAttributes<MaxLengthAttribute>().FirstOrDefault()?.Value;
	}

	public static int? MaxStringLength(PropertyInfo p) => MaxStringLength((MemberInfo)p);

	public static bool IsMarkedNotNull(MemberInfo p)
	{
		return p.CustomAttributes.Any(x => x.AttributeType == typeof(NotNullAttribute));
	}
}