using NeuroAccess.Nfc;
using NeuroAccess.Nfc.TravelDocuments;
using NeuroAccess.Nfc.TravelDocuments.ISO19794;
using Paiwise;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;

namespace TAG.Identity.TravelDocuments
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
		internal const string RequiredPrivilege = "Admin.Identity.TravelDocuments";

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
			return Task.CompletedTask;
		}

		/// <summary>
		/// Method called when service is terminated and stopped.
		/// </summary>
		public Task Stop()
		{
			running = false;
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
					new ConfigurablePage("Travel Documents Authenticator", "/TravelDocuments/TravelDocuments.md", RequiredPrivilege)
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
			string PreviewId = GetPreviewId(Identity);
			if (string.IsNullOrEmpty(PreviewId))
				return Grade.NotAtAll;

			KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Identity);

			if (P.Key is null || P.Value is null)
				return Grade.NotAtAll;
			else
				return Grade.Excellent;
		}

		private static string GetPreviewId(IIdentityApplication Application)
		{
			foreach (KeyValuePair<string, object> Claim in Application.Claims)
			{
				if (Claim.Key == "PREVIEW" && Claim.Value is string s)
					return s;
			}

			return string.Empty;
		}

		private static KeyValuePair<XmlDocument, DocumentInformation> GetNfcDocument(IIdentityApplication Application)
		{
			foreach (XmlDocument Doc in Application.Documents)
			{
				if (Doc.DocumentElement is not null &&
					Doc.DocumentElement.LocalName == "SnifferOutput" &&
					Doc.DocumentElement.NamespaceURI == "http://waher.se/Schema/SnifferOutput.xsd")
				{
					IEnumerator e = Doc.DocumentElement.GetEnumerator();

					if (!e.MoveNext() ||
						e.Current is not XmlElement E ||
						E.LocalName != "Info")
					{
						continue;
					}

					string Mrz = IsoDepReplay.GetRows(E).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

					if (!MrzExtensions.ParseMrz(Mrz, out DocumentInformation DocInfo))
						continue;

					return new KeyValuePair<XmlDocument, DocumentInformation>(Doc, DocInfo);
				}
			}

			return new KeyValuePair<XmlDocument, DocumentInformation>(null, null);
		}

		/// <summary>
		/// Validates an identity application.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		public async Task Validate(IIdentityApplication Application)
		{
			try
			{
				KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);
				XmlDocument Nfc = P.Key;
				DocumentInformation DocInfo = P.Value;
				Representation Face = null;

				if (Nfc is null || DocInfo is null)
					return;

				// Validate NFC replay document.

				try
				{
					IsoDepReplay Replay = new(Nfc);
					using TravelDocumentsClient Client = new(Replay, Replay.DocumentInfo, null);

					AuthenticateResult AuthResult = await Client.Authenticate();
					if (AuthResult != AuthenticateResult.Success)
						throw new Exception("Unable to authenticate NFC document: " + AuthResult.ToString());

					DocumentInformation DocInfo2 = null;

					Client.MrzUpdated += (_, e) =>
					{
						DocInfo2 = Client.Mrz.DocumentInformation;
						return Task.CompletedTask;
					};

					Client.BiometricEncodingFaceUpdated += (_, e) =>
					{
						if (Client.BiometricEncodingFace is not null)
							Face = Client.BiometricEncodingFace[0].BiometricDataBlock?.Record?.Representations[0];

						return Task.CompletedTask;
					};

					string OnboardingNeuron = await RuntimeSettings.GetAsync("Onboarding.DomainName", "id.tagroot.io");

					ReadTravelDocumentResult Result = await Client.ReadTravelDocument(OnboardingNeuron);
					if (Result != ReadTravelDocumentResult.Success)
						throw new Exception("Unable to read embedded Travel Document: " + Result.ToString());

					if (DocInfo is null)
						throw new Exception("MRZ not available in embedded Travel Document.");

					if (DocInfo.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty) !=
						DocInfo2.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty))
					{
						throw new Exception("MRZ provided in authentication not same as MRZ encoded in Travel Document.");
					}

					if (Face is null)
						throw new Exception("Face not available in embedded Travel Document.");
				}
				catch (Exception ex)
				{
					Log.Exception(ex);
					Application.InvalidateAllClaims("NFC document invalid.", "en", "NfcInvalid");
					Application.InvalidateAllPhotos("NFC document invalid.", "en", "NfcInvalid");
					return;
				}

				PersonalInformation PersonalInfo = PersonalInformation.Create(Application.Claims);

			}
			catch (Exception ex)
			{
				Application.ReportError(ex.Message, null, null, ValidationErrorType.Service, this);
			}
		}

		#endregion
	}
}
