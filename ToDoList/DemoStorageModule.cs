using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Security.Login;

namespace ToDoList
{
	public class DemoStorageModule : DatabaseStorageModule
	{
		public DemoStorageModule()
			: base()
		{
		}

		public override void AfterConfigure()
		{
			base.AfterConfigure();

			using (var cmd = m_connection.CreateCommand())
			{
				cmd.CommandText = $@"SELECT COUNT(*) FROM ""{LoginEntryTablename}""";
				var res = cmd.ExecuteScalar();
				if (res == null || res == DBNull.Value || ((long)res) == 0)
					base.AddLoginEntryAsync(LoginHandler.CreateUser("demouser", "demo", "demo")).Wait();
			}
		}
	}
}
