## Ceen.Database ##

The `Ceen.Database` is a micro [object-relation mapper (ORM)](https://en.wikipedia.org/wiki/Object-relational_mapping) with limited focus on the "relation" part, but more focus on flexibility and type-safety.


### Background ###
The motivation for this project is the ubiquitous need for permanent storage in may applications, but particularly for use within a web backend application (i.e. running under `Ceen.Httpd`).

In almost all programming languages and frameworks, there is some support for using relational databases, and within .Net this starts with the `System.Data.IDbConnection` interface. The interface is a backend-agnostic way of issuing commands to an SQL based database.

A well-known challenge is that the relational database uses tables, and data needs to be converted to-and-from the .Net data models. There exists a swath of tools that assist with this mapping and also with the relations between, including [NHibernate](https://nhibernate.info/) and [Entity Framework](https://docs.microsoft.com/en-us/ef/). These frameworks are quite large and supports a myriad of configurations and rules, which can be beneficial for large-scale database solutions.

However, they are often slow for simple things, such as flat-table queries, where special tools such as [Dapper](https://dapper-tutorial.net/dapper) can significantly improve performance. In database applications, and especially with micro services, a small local database with a straightforward schema is prefered.

Unfortunately, Dapper does not support creation of tables, requiring the user to write the SQL queries by hand, and make sure they are compatible with the underlying SQL dialect. Another downside of Dapper is the need to embed strings with the names of properties, causing problems when fields are renamed.


### Database agnostic ###
A crucial component in `Ceen.Database` is the `IDatabaseDialect`, which contains all database specific code. As the project is targeted for micro-services, the only implementation so far is the `SQliteDatabaseDialect`, but adding new providers can be done easily. If you intend to implement a new dialect, the `DatabaseDialectBase` can be derived from, where you would only need to implement the abstract methods.

You can use the `Ceen.Database.DatabaseHelper.CreateConnection()` helper method to create a `System.Data.IDbConnection` instance in a portable manner, or use the specific database methods. Once you have the `System.Data.IDbConnection` instance, you can call `Ceen.Database.DatabaseHelper.GetDialect()` and provide the dialect you want to use. If you do not call this method, the first use of the database instance will assign `Ceen.Database.DatabaseHelper.DefaultDialect` to the instance.

Most methods in `Ceen.Database` are extension methods to `System.Data.IDbConnection` so they will show up if you have `using Ceen.Database;` at the top of your file.

### Mapping data ###
In a type-safe focused language, such as C#, many features of the language are lost if the types are not statically defined. For this reason, a key component in `Ceen.Database` is the use of `TableMapping` where a C# type can be used to describe a database table.

An example:

```csharp
class User 
{
	public string ID;
	public string Displayname;
	public int Level;
	public DateTime LastLogin;
}
```

If you provide a basic [POCO](https://en.wikipedia.org/wiki/Plain_old_CLR_object) class such as this, `Ceen.Database` will identify the `ID` as being the primary key. As the property is of type `string`, the primary keys will be randomly generated `GUID` values. The table name and the column names will match the class, as will the types of the columns.

If you desire, for some reason, to force the names or types, you can apply attributes to the class, as in this example:

```csharp
[Name("User")]
class User 
{
	[PrimaryKey]
	[Name("ID")]
	[DbType("STRING")]
	[Unique]
	public string ID;

	[Ignore]
	public string ComputedText;

	[CreatedTimestamp]
	public DateTime Created;
	[ChangedTimestamp]
	public DateTime LastModified;

	...
}
```

The `Ignore` command allows you to have public properties or fields on the class that are not persisted to the database. The timestamp attributes will inject the current time into the fields when creating or modifying, repectively.

To create the example table, you will need an instance of `System.Data.IDbConnection`, and you can then invoke the `CreateTable` method:

```csharp
using Ceen.Database;

...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
db.CreateTable<User>(); // ifNotExists: true, autoAddColumns: true

```

If you need to, you can always fall back to providing a custom SQL and executing it directly on the connection, or extract the generated SQL through the `IDatabaseDialect` instance (which you can get with `db.GetDialect()`). 

By default, the call will add `IF NOT EXISTS` (or similar based on SQL dialect), such that it is always safe to call this method without checking for table existence. The method will also default to adding new columns, as this can be done with no data loss. Renamed columns are not detected, so you need custom logic to move data if you rename or remove columns, and need to access the data.

### Table rules ###
When working with relational data, and especially in a web-service, it is required to perform some kind of validation on the input data. Some validation can be done on the client to provide fast user feedback, but server-side validation is required to avoid various data-based attacks, such as dumping huge binary strings into the database.

If you prefer, you can add this validation manually in the code that accepts the input, but for some uses, it is simper to add validation directly on the tables. An example could be:

```csharp
class User
{
	[StringNotEmpty]
	[NoNewLines]
	[StringLength(4, 32)]
	public string Username;

	[IntegerRange(1, 200)]
	public int Age;

	[FunctionRule(x => if (x is User u && u.Expires < DateTime.Now) throw new ValidationException("Invalid expires value"))]
	public DateTime Expires;
}
```

These rules are enforced with the `Update` / `Insert` commands, but can also be invoked manually. These rules are not enforced if you access the database directly with an SQL statement.

A related concept is table-wide uniqe values, which can be grouped or single-item, such as:

```csharp
class User
{
	[Unique]
	public string Username;

	[Unique("display-name")]
	public string Firstname;
	[Unique("display-name")]
	public string Lastname;
}
```

In this example, the `Username` field is unique by itself, whereas the `Firstname` and `Lastname` fields combined are unique. Unlike the validation rules, these constraints are enforced via the database schema, and cannot be bypassed. For some use-cases, constraints can also speed up query speeds.

### Data queries ###

With `Ceen.Database` there are multiple layers you can use to issue queries to the database. All the layers are implemented as extensions to `System.Data.IDbConnection` so you need to add `using Ceen.Database;` in order to access them.

At the most rudimentary level, there are helpers to work with custom SQL strings. The entry point for these is the `CreateCommand()` which creates a new `System.Data.IDbCommand` from an SQL string. On the command you can use the helpers `SetParameterValues()` and `AddParameters()` to work with arguments in a way that is SQL-Injection safe. Or simply use the helper methods to `ExecuteReader`, `ExecuteNonQuery`, and `ExecuteScalar` that accepts arguments: 

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
using(var cmd = db.CreateCommand("SELECT ID FROM User WHERE Username = ? AND Password = ?"))
using(var rd = cmd.ExecuteReader(username, password))
{
	...
}

```

Since `Ceen.Database` does not attempt to solve every possible situation, you can always fall back on this approach (or use the connection directly) if you find this to be more efficient.

The next refined layer allows us to return the parsed values in a safely typed manner:

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var user = db.SelectSingle<User>("Username = ? AND Password = ?", username, password);
```

This is similar to the approach used in Dapper, however it has the downside that we have embedded `Username` and `Password` as strings inside the query. If we were to rename these fields, we would need to find all strings that where these are embedded and change them. A simple solution could be to use the C# features to rewrite it as `$"{nameof(User.Username)} = ? AND {nameof(User.Password)} = ?"`. While his solves the problem, we need to take care of mappings where the column name is not the same as the property/field name and also cases where the column name is an SQL keyword. For such cases you can rewrite the example to:

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var map = db.GetTypeMap<User>();
var user = db.SelectSingle<User>($"{map.QuotedPropertyName(nameof(User.Username))} = ? AND {map.QuotedPropertyName(nameof(User.Password))} = ?", username, password);

```

To avoid this slightly cumbersome embedding of strings, we can use a more refined (but also more restricted) way, where we programatically construct the query:

```csharp
using Ceen.Database;
using static Ceen.Database.QueryUtil;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var user = db.SelectSingle<User>(
	And(
		Equal(
			Property(nameof(User.Username)),
			username
		),
		Equal(
			Property(nameof(User.Password)),
			password
		)
	)
);

```

While this avoids embedding strings in the program, it is also cumbersome for simple queries. A further refinement is to use the LinQ support to rewrite the query like this:

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var user = db.SelectSingle<User>(x => x.Username == username && x.Password == password);

```

This more concise syntax has some overhead in parsing the query function, but makes it trivial to read and write. Only a subset of operators are supported, but common query issues, such as datetimes, nulls, greater/less, parenthesis, IN, etc. are supported.

### Create, Retrieve, Update, Delete: CRUD operations ###

Creating an entry is done simply with the `InsertItem` method:

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var x = db.InsertItem(new User { Username = username, Password = password });
Console.WriteLine($"Created user with ID: {x.ID}");
```

The query operations previously described are really just creating the `WHERE` clause of the SQL query. Because of this we can use the filters in the other commands:

```csharp
using System.Linq;
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
// Set expired=true for users who have not logged in within the last 4 days
db.Update<User>( new { Expired = true }, x => x.LastLogin < DateTime.Now.AddDays(-4));
// Delete users who are expired and have not logged in for a year
db.Delete<User>( x => x.Expired && x.LastLogin < DateTime.Now.AddYears(-1));

// Number of active users:
var activeUsers = db.SelectCount<User>(x => x.LastLogin < DateTime.Now.AddDays(-1))
// Top-4 most current users
var users = db.Select(
	db.Query<User>()
	  .Select()
	  .OrderByDesc(x => x.LastLogin)
	  .Limit(4)
).ToArray();

Console.WriteLine($"Found {activeUsers} active users, most recently: {string.Join(", ", users.Select(x => x.Displayname))}");
```

The last method can be used to more programatically construct complicated queries. After constructing a `Query<T>` instance, you can issue multiple actions on this, using a Fluent syntax. The query can be refined to either of the CRUD operations, and can also include multiple tables via joins. Once the query is constructed you can create a re-usable command instance, execute the query, or extract the SQL string.

All exposed methods are essentially wrappers that construct and configure a `Query<T>` instance. An example of using the `Query<T>` to update items using a join shows the flexibility:

```csharp
using Ceen.Database;
class User {
	public string Email;
	public bool Hacked;
}

class HackedList {
	public string HackedID;
	public string Type;
}
...


var db = DatabaseHelper.CreateConnection("sample.sqlite");
// Similar to: UPDATE User SET Hacked=1 WHERE Email IN (SELECT HackedID WHERE Type LIKE "email")
var c = db.Update(
	db.Query<User>()
	  .Update(new { Hacked = true })
	  .WhereIn(nameof(User.Email, 
	  	db.Query<HackedList>()
	  	  .Select(nameof(HackedList.HackedID))
	  	  .Where(x => string.Equals(x.Type, "email", StringComparison.OrdinalIgnoreCase))
	  )
);
```

### Parsing user supplied SQL ###

When writing a web service, it is often required that the client can request filtering, sorting, and pagination. There are many ways to allow the client to perform this, ranging from a fixed set of allowed queries, over some manual construction of query parameters, to something like GraphQL. While all these have benefits, there is something very familiar to SQL.

However, allowing the client to send SQL queries is highly likely to end in data leaks or damage. To solve this issue, `Ceen.Database` supports parsing a limited subset of an SQL WHERE fragment.

The result of the parsing process is a `Query<T>` item that can be used just as before:

```csharp
using Ceen.Database;
...

var db = DatabaseHelper.CreateConnection("sample.sqlite");
var map = db.GetTypeMap<User>();

// User input:
var filter = "name like \"123*\" and age > 1+2+3";
var order = "-name,+id";
var columns = "name,id";
var page = 1;
var pagesize = 10; 

// Build the query from user data
var q = 
	FilterParser
		.ParseFilter(map, filter)
		.Order(FilterParser.ParseOrder(map, order))
		.Select(columns.Split(","));

// Add more restrictions (will be AND'ed to any existing query):
q = 
	q.Where(x => x.Name != "root")
	 .Limit(pagesize)
	 .Offset(page);

// Execute the query
db.Select(q);

```

### SQLite and multithreading ###
When writing a web service, it is paramount that it can handle multiple requests simultaneously. This is generally not a problem, but some databases, notably SQLite, do not support concurrent access.

To support such databases, `Ceen.Extras` includes the class `GuardedConnection` which wraps any `System.Data.IDbInstance`, and provides the `RunInTransactionAsync` method that uses an `AsyncLock` to guard the database from concurrent access. The guarding disables the lock if the database does not require it.

An added benefit from this helper is that the returned instance is always wrapped in an implicit transaction. All operations performed within the callback method are performed on the same transaction. If an error occurs, the transaction is rolled back, and otherwise comitted. An example of using the `GuardedConnection` method:

```csharp
using Ceen.Database;
using Ceen.Extras;
...

static GuardedConnection _sharedCon = new GuardedConnection(DatabaseHelper.CreateConnection("sample.sqlite"));

var users = await _sharedCon.RunInTransactionAsync(con => con.SelectCount<User>(x => x.Active));

```