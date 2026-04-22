using NeuroAccess.Nfc;
using NeuroAccess.Nfc.TravelDocuments;
using NeuroAccess.Nfc.TravelDocuments.DataObjects;
using NeuroAccess.Nfc.TravelDocuments.ISO19794;
using Paiwise;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TAG.Identity.TravelDocuments.Data;
using Waher.Content;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Persistence;
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

			IPhoto ProfilePhoto = GetProfilePhoto(Application);
			if (ProfilePhoto is null)
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
				if (Claim.Key == PersonalInformation.PreviewTag && Claim.Value is string s)
					return s;
			}

			return string.Empty;
		}

		private static IPhoto GetProfilePhoto(IIdentityApplication Application)
		{
			foreach (IPhoto Photo in Application.Photos)
			{
				string FileName = Path.GetFileName(Photo.FileName);
				int i = FileName.IndexOf('.');
				if (i >= 0)
					FileName = FileName[..i];

				if (FileName == "ProfilePhoto")
					return Photo;
			}

			return null;
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

				IPhoto ProfilePhoto = GetProfilePhoto(Application);
				if (ProfilePhoto is null)
					return;

				KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);
				XmlDocument Nfc = P.Key;
				DocumentInformation DocInfo = P.Value;
				Representation Face = null;
				AdditionalPersonalDetails AdditionalDetails = null;

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

					Client.PersonalInformationUpdated += (_, e) =>
					{
						AdditionalDetails = Client.PersonalInformation;
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

				// Validate Personal Information claims

				PersonalInformation PersonalInfo = PersonalInformation.Create(Application.Claims);

				DateTime? BirthDate = PersonalInfo.BirthDate;
				if (BirthDate.HasValue)
				{
					if (BirthDate > DateTime.Today)
					{
						Application.ClaimInvalid(PersonalInformation.BirthDayTag, "Future birth date.", "en", "FutureBirthDate", this);
						Application.ClaimInvalid(PersonalInformation.BirthMonthTag, "Future birth date.", "en", "FutureBirthDate", this);
						Application.ClaimInvalid(PersonalInformation.BirthYearTag, "Future birth date.", "en", "FutureBirthDate", this);
					}
					else if (!string.IsNullOrEmpty(DocInfo.DateOfBirth) &&
						DocInfo.DateOfBirth.Length == 6 &&
						int.TryParse(DocInfo.DateOfBirth[..2], out int BirthYear) &&
						int.TryParse(DocInfo.DateOfBirth[2..4], out int BirthMonth) &&
						int.TryParse(DocInfo.DateOfBirth[4..6], out int BirthDay))
					{
						if (BirthDay == BirthDate.Value.Day)
							Application.ClaimValid(PersonalInformation.BirthDayTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.BirthDayTag, "Birth Day invalid.", "en", "BirthDayInvalid", this);

						if (BirthMonth == BirthDate.Value.Month)
							Application.ClaimValid(PersonalInformation.BirthMonthTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.BirthMonthTag, "Birth Month invalid.", "en", "BirthMonthInvalid", this);

						if (BirthYear == (BirthDate.Value.Year % 100))
							Application.ClaimValid(PersonalInformation.BirthYearTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.BirthYearTag, "Birth Year invalid.", "en", "BirthYearInvalid", this);
					}

					if (PersonalInfo.AgeAbove.HasValue)
					{
						if (PersonalInfo.Age >= PersonalInfo.AgeAbove.Value)
							Application.ClaimValid(PersonalInformation.AgeAboveTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.AgeAboveTag, "Age not reached.", "en", "AgeNotReached", this);
					}
				}
				else if (PersonalInfo.AgeAbove.HasValue &&
					!string.IsNullOrEmpty(DocInfo.DateOfBirth) &&
					DocInfo.DateOfBirth.Length == 6 &&
					int.TryParse(DocInfo.DateOfBirth[..2], out int BirthYear) &&
					int.TryParse(DocInfo.DateOfBirth[2..4], out int BirthMonth) &&
					int.TryParse(DocInfo.DateOfBirth[4..6], out int BirthDay))
				{
					try
					{
						if (BirthYear > DateTime.Today.Year % 100)
							BirthYear += 1900;
						else
							BirthYear += 2000;

						DateTime BirthDate2 = new(BirthYear, BirthMonth, BirthDay);
						if (BirthDate2 > DateTime.Today)
						{
							BirthYear -= 100;
							BirthDate2 = new(BirthYear, BirthMonth, BirthDay);
						}

						int Age = Duration.GetDurationBetween(BirthDate2, DateTime.Today).Years;

						if (Age >= PersonalInfo.AgeAbove.Value)
							Application.ClaimValid(PersonalInformation.AgeAboveTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.AgeAboveTag, "Age not reached.", "en", "AgeNotReached", this);
					}
					catch (Exception ex)
					{
						Application.ReportError(ex.Message, "en", "InconsistentBirthDate",
							ValidationErrorType.Service, this);
					}
				}

				if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.Country) &&
					!string.IsNullOrEmpty(DocInfo.IssuingState))
				{
					if (ISO_3166_1.CompareCountryCode(PersonalInfo.Country, DocInfo.IssuingState))
						Application.ClaimValid(PersonalInformation.CountryTag, this);
					else
						Application.ClaimInvalid(PersonalInformation.CountryTag, "Country invalid.", "en", "CountryCodeMismatch", this);
				}

				if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.Nationality) &&
					!string.IsNullOrEmpty(DocInfo.Nationality))
				{
					if (ISO_3166_1.CompareCountryCode(PersonalInfo.Nationality, DocInfo.Nationality))
						Application.ClaimValid(PersonalInformation.NationalityTag, this);
					else
						Application.ClaimInvalid(PersonalInformation.NationalityTag, "Nationality invalid.", "en", "NationalityInvalid", this);
				}

				if (PersonalInfo.Gender.HasValue && !string.IsNullOrEmpty(DocInfo.Gender))
				{
					Paiwise.Gender? ExpectedGender = DocInfo.Gender.ToUpper() switch
					{
						"M" => (Paiwise.Gender?)Paiwise.Gender.Male,
						"F" => (Paiwise.Gender?)Paiwise.Gender.Female,
						_ => null,
					};

					if (ExpectedGender.HasValue)
					{
						if (PersonalInfo.Gender.Value == ExpectedGender.Value)
							Application.ClaimValid(PersonalInformation.GenderTag, this);
						else
							Application.ClaimInvalid(PersonalInformation.GenderTag, "Gender invalid.", "en", "GenderInvalid", this);
					}
				}

				if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.PersonalNumber))
				{
					string Country = PersonalInfo.Country;

					if (string.IsNullOrEmpty(Country) &&
						!string.IsNullOrEmpty(DocInfo.IssuingState) &&
						ISO_3166_1.TryGetCountryByCode(DocInfo.IssuingState, out ISO_3166_Country Country2))
					{
						Country = Country2.Alpha2;
					}

					if (!string.IsNullOrEmpty(Country))
					{
						IPersonalNumberValidator PNrValidator = Types.FindBest<IPersonalNumberValidator, string>(Country);
						if (PNrValidator is not null)
						{
							bool? Valid = await PNrValidator.IsValid(Country, PersonalInfo.PersonalNumber);

							if (Valid.HasValue)
							{
								if (!Valid.Value)
									Application.ClaimInvalid(PersonalInformation.PersonalNumberTag, "Personal number invalid according to national rules.", "en", "PersonalNumberInvalid", this);
								else
								{
									string NormalizedPNr = await PNrValidator.Normalize(Country, PersonalInfo.PersonalNumber);

									if (NormalizedPNr != PersonalInfo.PersonalNumber)
										Application.ClaimInvalid(PersonalInformation.PersonalNumberTag, "Personal number not normalized.", "en", "PNrNotNormalized", this);
									else
									{
										bool? AdditionalInfoAsPNr = null;
										bool? OptionalDataAsPNr = null;
										bool? DocumentNrAsPNr = null;

										if (!string.IsNullOrEmpty(AdditionalDetails?.PersonalNumber) &&
											((await PNrValidator.IsValid(Country, AdditionalDetails.PersonalNumber)) ?? false))
										{
											AdditionalInfoAsPNr = await PNrValidator.Normalize(
												Country, AdditionalDetails.PersonalNumber) == NormalizedPNr;
										}

										if (!string.IsNullOrEmpty(DocInfo.OptionalData) &&
											((await PNrValidator.IsValid(Country, DocInfo.OptionalData)) ?? false))
										{
											OptionalDataAsPNr = await PNrValidator.Normalize(
												Country, DocInfo.OptionalData) == NormalizedPNr;
										}

										if (!string.IsNullOrEmpty(DocInfo.DocumentNumber) &&
											((await PNrValidator.IsValid(Country, DocInfo.DocumentNumber)) ?? false))
										{
											DocumentNrAsPNr = await PNrValidator.Normalize(
												Country, DocInfo.DocumentNumber) == NormalizedPNr;
										}

										if ((AdditionalInfoAsPNr ?? false) ||
											(OptionalDataAsPNr ?? false) ||
											(DocumentNrAsPNr ?? false))
										{
											Application.ClaimValid(PersonalInformation.PersonalNumberTag, this);
										}


										if (!(AdditionalInfoAsPNr ?? true) ||
											!(OptionalDataAsPNr ?? true) ||
											!(DocumentNrAsPNr ?? true))
										{
											Application.ClaimInvalid(PersonalInformation.PersonalNumberTag, "Personal number does not match.", "en", "PersonalNumberMismatch", this);
										}
									}
								}
							}
						}
					}
					else
						Application.ClaimInvalid(PersonalInformation.PersonalNumberTag, "Country not specified, or available in MRZ.", "en", "CountryNotSpecified", this);
				}

				string PrimaryIdentifier = Append(DocInfo.PrimaryIdentifier);
				if (!string.IsNullOrEmpty(PrimaryIdentifier))
				{
					if (IcaoNameComparer.AreNamesSimilar(PrimaryIdentifier, PersonalInfo.LastNames))
						Application.ClaimValid(PersonalInformation.LastNamesTag, this);
					else if (IcaoNameComparer.AreNamesSimilar(PrimaryIdentifier,
						PersonalInfo.MiddleNames + " " + PersonalInfo.LastNames))
					{
						Application.ClaimValid(PersonalInformation.MiddleNamesTag, this);
						Application.ClaimValid(PersonalInformation.LastNamesTag, this);
					}
					else if (IcaoNameComparer.AreNamesSimilar(PrimaryIdentifier,
						PersonalInfo.LastNames + " " + PersonalInfo.MiddleNames))
					{
						Application.ClaimValid(PersonalInformation.MiddleNamesTag, this);
						Application.ClaimValid(PersonalInformation.LastNamesTag, this);
					}
					else if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.LastNames))
					{
						Application.ClaimInvalid(PersonalInformation.LastNamesTag, "Last name(s) invalid.", "en",
							"LastNameInvalid", this);
					}
				}

				string SecondaryIdentifier = Append(DocInfo.SecondaryIdentifier);
				if (!string.IsNullOrEmpty(SecondaryIdentifier))
				{
					if (IcaoNameComparer.AreNamesSimilar(SecondaryIdentifier, PersonalInfo.FirstName))
						Application.ClaimValid(PersonalInformation.FirstNameTag, this);
					else if (IcaoNameComparer.AreNamesSimilar(SecondaryIdentifier,
						PersonalInfo.FirstName + " " + PersonalInfo.MiddleNames))
					{
						Application.ClaimValid(PersonalInformation.FirstNameTag, this);
						Application.ClaimValid(PersonalInformation.MiddleNamesTag, this);
					}
					else if (IcaoNameComparer.AreNamesSimilar(SecondaryIdentifier,
						PersonalInfo.MiddleNames + " " + PersonalInfo.FirstName))
					{
						Application.ClaimValid(PersonalInformation.FirstNameTag, this);
						Application.ClaimValid(PersonalInformation.MiddleNamesTag, this);
					}
					else if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.FirstName))
					{
						Application.ClaimInvalid(PersonalInformation.FirstNameTag, "First name(s) invalid.", "en",
							"FirstNameInvalid", this);
					}
				}

				if (!string.IsNullOrEmpty(AdditionalDetails?.FullName))
				{
					if (IcaoNameComparer.AreNamesSimilar(AdditionalDetails.FullName,
						PersonalInfo.FullName))
					{
						Application.ClaimValid(PersonalInformation.FullNameTag, this);
						Application.ClaimValid(PersonalInformation.FirstNameTag, this);
						Application.ClaimValid(PersonalInformation.MiddleNamesTag, this);
						Application.ClaimValid(PersonalInformation.LastNamesTag, this);
					}
					else
					{
						Application.ClaimInvalid(PersonalInformation.FullNameTag, "Full name invalid.", "en",
							"FullNameInvalid", this);
						Application.ClaimInvalid(PersonalInformation.FirstNameTag, "Full name invalid.", "en",
							"FullNameInvalid", this);
						Application.ClaimInvalid(PersonalInformation.MiddleNamesTag, "Full name invalid.", "en",
							"FullNameInvalid", this);
						Application.ClaimInvalid(PersonalInformation.LastNamesTag, "Full name invalid.", "en",
							"FullNameInvalid", this);
					}
				}
			}
			catch (Exception ex)
			{
				Application.ReportError(ex.Message, null, null, ValidationErrorType.Service, this);
			}
		}

		private static string Append(string[] Names)
		{
			int c = Names?.Length ?? 0;
			if (c == 0)
				return null;

			if (c == 1)
				return Names[0];

			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < c; i++)
			{
				if (i > 0)
					sb.Append(' ');

				sb.Append(Names[i]);
			}

			return sb.ToString();
		}

		private static void FailAll(IIdentityApplication Application, string Message)
		{
			Application.InvalidateAllClaims(Message, "en", "NfcInvalid");
			Application.InvalidateAllPhotos(Message, "en", "NfcInvalid");
		}

		#endregion
	}
}
