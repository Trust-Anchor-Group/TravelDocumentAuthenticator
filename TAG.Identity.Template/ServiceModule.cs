using Paiwise;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Runtime.Inventory;

namespace TAG.Identity.Template
{
	/// <summary>
	/// Service Module template hosting an Identity Authenticator Service, working to verify, accept, reject or defer identity applications made 
	/// on the the TAG Neuron(R).
	/// </summary>
	/// <remarks>
	/// The <see cref="IConfigurableModule"/> interface controls the service life cycle, and how the service is presented to users, 
	/// both clients that want to use the identity authenticator service, as well as administrators, who need to configure the service.
	/// 
	/// Classes implementing this interface, containing a default constructor, will be found and instantiated using the
	/// <see cref="Waher.Runtime.Inventory.Types"/> static class.
	/// </remarks>
	public class ServiceModule : IConfigurableModule, IIdentityAuthenticatorService
	{
		private static bool running = false;

		/// <summary>
		/// Users are required to have this privilege in order to access this service via the Admin interface.
		/// </summary>
		internal const string RequiredPrivilege = "Admin.Identity.Template";

		/// <summary>
		/// Service Module template hosting the service within the TAG Neuron(R).
		/// </summary>
		/// <remarks>
		/// Default constructor necessary, for the Neuron(R) to roperly instantiate the service during startup.
		/// </remarks>
		/// </summary>
		public ServiceModule()
		{
		}

		/// <summary>
		/// If the service module is running or not.
		/// </summary>
		public static bool Running => running;

		#region IModule

		/// <summary>
		/// Method called when service is loaded and started.
		/// </summary>
		public Task Start()
		{
			running = true;

			Log.Debug("Template Identity Authenticator started."); // TODO: Remove when proper Start method implemented.

			return Task.CompletedTask;
		}

		/// <summary>
		/// Method called when service is terminated and stopped.
		/// </summary>
		public Task Stop()
		{
			running = false;

			Log.Debug("Template Identity Authenticator stopped."); // TODO: Remove when proper Stop method implemented.

			return Task.CompletedTask;
		}

		#endregion

		#region IConfigurableModule

		/// <summary>
		/// Determines how the service is displayed on the Neuron(R) administration page.
		/// </summary>
		/// <returns>Set of configurable pages the service published.</returns>
		public Task<IConfigurablePage[]> GetConfigurablePages()
		{
			return Task.FromResult(new IConfigurablePage[]
				{
					new ConfigurablePage("Identity Authenticator Template", "/TemplateIdentity/IdentityTemplate.md", RequiredPrivilege)
				});
		}

		#endregion

		#region IIdentityAuthenticatorService

		/// <summary>
		/// If the interface understands objects such as <paramref name="Object"/>.
		/// </summary>
		/// <param name="Object">Object</param>
		/// <returns>How well objects of this type are supported.</returns>
		public Grade Supports(IIdentityApplication Identity)
		{
			// TODO: Revise the properties in the identity application to see if this identity authenticator service
			//       is applicable and can validate it.

			return Grade.NotAtAll;
		}

		/// <summary>
		/// Validates an identity application.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		public async Task Validate(IIdentityApplication Application)
		{
			try
			{
				// TODO: Verify application and return corresponding result.
			}
			catch (Exception ex)
			{
				Application.ReportError(ex.Message, null, null, ValidationErrorType.Service, this);
			}
		}

		#endregion
	}
}
