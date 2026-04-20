using NeuroAccess.Nfc;
using NeuroAccess.Nfc.TravelDocuments;
using NeuroAccess.Nfc.TravelDocuments.ISO19794;
using Paiwise;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
		public Grade Supports(IIdentityApplication Application)
		{
			string PreviewId = GetPreviewId(Application);
			if (string.IsNullOrEmpty(PreviewId))
				return Grade.NotAtAll;

			KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);

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
				string PreviewId = GetPreviewId(Application);
				if (string.IsNullOrEmpty(PreviewId))
					return;

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
					byte[] LocalKeySeed = Encoding.UTF8.GetBytes(PreviewId);
					using TravelDocumentsClient Client = new(Replay, Replay.DocumentInfo, LocalKeySeed);

					AuthenticateResult AuthResult = await Client.Authenticate();
					if (AuthResult != AuthenticateResult.Success)
					{
						FailAll(Application, "Unable to authenticate NFC document: " + AuthResult.ToString());
						return;
					}

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
					{
						FailAll(Application, "Unable to read embedded Travel Document: " + Result.ToString());
						return;
					}

					if (DocInfo is null)
					{
						FailAll(Application, "MRZ not available in embedded Travel Document.");
						return;
					}

					if (DocInfo.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty) !=
						DocInfo2.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty))
					{
						FailAll(Application, "MRZ provided in authentication not same as MRZ encoded in Travel Document.");
						return;
					}

					if (Face is null)
					{
						FailAll(Application, "Face not available in embedded Travel Document.");
						return;
					}
				}
				catch (Exception ex)
				{
					// Manipulation of NFC document results in automatic failure.

					Log.Exception(ex);
					FailAll(Application, ex.Message);
					return;
				}

				PersonalInformation PersonalInfo = PersonalInformation.Create(Application.Claims);

				DateTime? BirthDate = PersonalInfo.BirthDate;
				if (BirthDate.HasValue)
				{
					if (BirthDate > DateTime.Today)
					{
						Application.ClaimInvalid("BDAY", "Future birth date.", "en", "FutureBirthDate", this);
						Application.ClaimInvalid("BMONTH", "Future birth date.", "en", "FutureBirthDate", this);
						Application.ClaimInvalid("BYEAR", "Future birth date.", "en", "FutureBirthDate", this);
					}
					else if (!string.IsNullOrEmpty(DocInfo.DateOfBirth) &&
						DocInfo.DateOfBirth.Length == 6 &&
						int.TryParse(DocInfo.DateOfBirth[..2], out int BirthYear) &&
						int.TryParse(DocInfo.DateOfBirth[2..4], out int BirthMonth) &&
						int.TryParse(DocInfo.DateOfBirth[4..6], out int BirthDay))
					{
						if (BirthDay == BirthDate.Value.Day)
							Application.ClaimValid("BDAY", this);
						else
							Application.ClaimInvalid("BDAY", "Birth Day invalid.", "en", "BirthDayInvalid", this);

						if (BirthMonth == BirthDate.Value.Month)
							Application.ClaimValid("BMONTH", this);
						else
							Application.ClaimInvalid("BMONTH", "Birth Month invalid.", "en", "BirthMonthInvalid", this);

						if (BirthYear == (BirthDate.Value.Year % 100))
							Application.ClaimValid("BYEAR", this);
						else
							Application.ClaimInvalid("BYEAR", "Birth Year invalid.", "en", "BirthYearInvalid", this);
					}

					if (PersonalInfo.AgeAbove.HasValue)
					{
						if (PersonalInfo.Age >= PersonalInfo.AgeAbove.Value)
							Application.ClaimValid("AGEABOVE", this);
						else
							Application.ClaimInvalid("AGEABOVE", "Age not reached.", "en", "AgeNotReached", this);
					}
				}

				/*
				foreach (KeyValuePair<string, object> P2 in Application.Claims)
				{
					switch (P2.Key)
					{
						case "FIRST":
							Result.FirstName = P.Value.ToString();
							break;

						case "MIDDLE":
							Result.MiddleNames = P.Value.ToString();
							break;

						case "LAST":
							Result.LastNames = P.Value.ToString();
							break;

						case "FULLNAME":
							Result.FullName = P.Value.ToString();
							break;

						case "ADDR":
							Result.Address = P.Value.ToString();
							break;

						case "ADDR2":
							Result.Address2 = P.Value.ToString();
							break;

						case "ZIP":
							Result.PostalCode = P.Value.ToString();
							break;

						case "AREA":
							Result.Area = P.Value.ToString();
							break;

						case "CITY":
							Result.City = P.Value.ToString();
							break;

						case "REGION":
							Result.Region = P.Value.ToString();
							break;

						case "COUNTRY":
							Result.Country = P.Value.ToString();
							break;

						case "NATIONALITY":
							Result.Nationality = P.Value.ToString();
							break;

						case "GENDER":
							if (P.Value is Gender Gender)
								Result.Gender = Gender;
							else
							{
								switch (P.Value.ToString().ToLower())
								{
									case "m":
										Result.Gender = Gender.Male;
										break;

									case "f":
										Result.Gender = Gender.Female;
										break;

									case "x":
										Result.Gender = Gender.Other;
										break;
								}
							}
							break;

						case "BDAY":
							if (int.TryParse(P.Value.ToString(), out int i) && i >= 1 && i <= 31)
								Result.BirthDay = i;
							break;

						case "BMONTH":
							if (int.TryParse(P.Value.ToString(), out i) && i >= 1 && i <= 12)
								Result.BirthMonth = i;
							break;

						case "BYEAR":
							if (int.TryParse(P.Value.ToString(), out i) && i >= 1900 && i <= 2100)
								Result.BirthYear = i;
							break;

						case "AGEABOVE":
							if (int.TryParse(P.Value.ToString(), out i) && i >= 0)
								Result.AgeAbove = i;
							break;

						case "PNR":
							Result.PersonalNumber = P.Value.ToString();
							break;

						case "ORGNAME":
							Result.OrgName = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGDEPT":
							Result.OrgDepartment = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGROLE":
							Result.OrgRole = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGADDR":
							Result.OrgAddress = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGADDR2":
							Result.OrgAddress2 = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGZIP":
							Result.OrgPostalCode = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGAREA":
							Result.OrgArea = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGCITY":
							Result.OrgCity = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGREGION":
							Result.OrgRegion = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGCOUNTRY":
							Result.OrgCountry = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "ORGNR":
							Result.OrgNumber = P.Value.ToString();
							Result.HasOrg = true;
							break;

						case "PHONE":
							Result.Phone = P.Value.ToString();
							break;

						case "EMAIL":
							Result.EMail = P.Value.ToString();
							break;

						case "JID":
							Result.Jid = P.Value.ToString();
							break;

					}
				}
				*/
			}
			catch (Exception ex)
			{
				Application.ReportError(ex.Message, null, null, ValidationErrorType.Service, this);
			}
		}

		private static void FailAll(IIdentityApplication Application, string Message)
		{
			Application.InvalidateAllClaims(Message, "en", "NfcInvalid");
			Application.InvalidateAllPhotos(Message, "en", "NfcInvalid");
		}

		#endregion
	}
}
