using Waher.Networking.SASL;
using Waher.Persistence;

namespace TAG.Identity.TravelDocuments.Test
{
	public class InternalTestAccount : IAccount
	{
		public InternalTestAccount()
		{
		}

		public DateTime Created => DateTime.Now;
		public CaseInsensitiveString UserName => nameof(InternalTestAccount);
		public string Password => string.Empty;
		public bool Enabled => true;
		public bool HasPrivilege(string PrivilegeID) => true;
		public Task LoggedIn(string RemoteEndPoint) => Task.CompletedTask;
	}
}
