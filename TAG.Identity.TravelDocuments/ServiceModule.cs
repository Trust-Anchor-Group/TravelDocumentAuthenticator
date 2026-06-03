using CoreJ2K;
using CoreJ2K.Util;
using NeuroAccess.Nfc;
using NeuroAccess.Nfc.TravelDocuments;
using NeuroAccess.Nfc.TravelDocuments.DataObjects;
using NeuroAccess.Nfc.TravelDocuments.ISO19794;
using Paiwise;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TAG.Identity.TravelDocuments.Data;
using TAG.Networking.DeepFace;
using Waher.Content;
using Waher.Content.Images;
using Waher.Content.Images.Exif;
using Waher.Content.Xml;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Networking;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
using Waher.Persistence;
using Waher.Runtime.HashStore;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;
using Waher.Security;

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
	public class ServiceModule : IConfigurableModule, IIdentityAuthenticatorService,
		IIdentityStatefulService
	{
		private static bool running = false;

		/// <summary>
		/// Reference to client sniffer for Stripe communication.
		/// </summary>
		private static XmlFileSniffer xmlFileSniffer = null;

		/// <summary>
		/// Sniffable object that can be sniffed on dynamically.
		/// </summary>
		private static readonly CommunicationLayer observable = new(false);

		/// <summary>
		/// Sniffer proxy, forwarding sniffer events to <see cref="observable"/>.
		/// </summary>
		private static readonly SnifferProxy snifferProxy = new(observable);

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
		public async Task Stop()
		{
			running = false;

			if (xmlFileSniffer is not null)
			{
				await xmlFileSniffer.DisposeAsync();
				xmlFileSniffer = null;
			}
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
					new ConfigurablePage("Travel Documents Authenticator", "/TravelDocuments/Settings.md", RequiredPrivilege)
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
			// TODO: Remove

			StringBuilder sb = new();
			sb.AppendLine("Checking support.");
			sb.AppendLine();
			sb.AppendLine("Claims available in application:");

			foreach (KeyValuePair<string, object> P2 in Application.Claims)
				sb.AppendLine(P2.Key);

			sb.AppendLine();
			sb.AppendLine("Photos available in application:");

			foreach (IPhoto P3 in Application.Photos)
				sb.AppendLine(P3.FileName);

			sb.AppendLine();
			sb.AppendLine("Documents available in application:");

			foreach (XmlDocument Doc in Application.Documents)
				sb.AppendLine(Doc.DocumentElement.NamespaceURI + "#" + Doc.DocumentElement.LocalName);

			Log.Debug(sb.ToString());

			string PreviewId = GetPreviewId(Application);
			if (string.IsNullOrEmpty(PreviewId))
			{
				// TODO: Remove
				Log.Debug("No Preview ID found.");

				return Grade.NotAtAll;
			}

			IPhoto ProfilePhoto = GetProfilePhoto(Application);
			if (ProfilePhoto is null)
			{
				// TODO: Remove
				Log.Debug("No Profile Photo found.");

				return Grade.NotAtAll;
			}

			KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);

			if (P.Key is null || P.Value is null)
			{
				// TODO: Remove
				Log.Debug("No NFC document found.");

				return Grade.NotAtAll;
			}
			else
			{
				// TODO: Remove
				Log.Debug("Support Excellent.");

				return Grade.Excellent;
			}
		}

		private static string GetPreviewId(IIdentityApplication Application)
		{
			foreach (KeyValuePair<string, object> Claim in Application.Claims)
			{
				if (Claim.Key == PersonalInformation.PreviewTag && Claim.Value is string s)
					return s;
			}

			if (Application.Preview)
			{
				foreach (KeyValuePair<string, object> Claim in Application.Claims)
				{
					if (Claim.Key == "ID" && Claim.Value is string s)
					{
						int i = s.IndexOf('@');
						if (i > 0)
							s = s[..i];

						return s;
					}
				}
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
					DocumentInformation DocInfo = GetNfcDocument(Doc);

					if (DocInfo is not null)
						return new KeyValuePair<XmlDocument, DocumentInformation>(Doc, DocInfo);
				}
			}

			return new KeyValuePair<XmlDocument, DocumentInformation>(null, null);
		}

		private static DocumentInformation GetNfcDocument(XmlDocument Doc)
		{
			IEnumerator e = Doc.DocumentElement.GetEnumerator();

			if (!e.MoveNext() ||
				e.Current is not XmlElement E ||
				E.LocalName != "Info")
			{
				return null;
			}

			string Mrz = IsoDepReplay.GetRows(E).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

			if (!MrzExtensions.ParseMrz(Mrz, out DocumentInformation DocInfo))
				return null;

			return DocInfo;
		}

		/// <summary>
		/// Validates an identity application.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		public Task Validate(IIdentityApplication Application)
		{
			return this.ValidateDistance(Application);
		}

		/// <summary>
		/// Validates an identity application.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		public Task<double?> ValidateDistance(IIdentityApplication Application)
		{
			return this.ValidateDistance(Application, GetSniffers());
		}

		/// <summary>
		/// Gets registered DeepFace sniffers.
		/// </summary>
		/// <returns>Array of sniffers.</returns>
		public static ISniffer[] GetSniffers()
		{
			xmlFileSniffer ??= new XmlFileSniffer(Gateway.AppDataFolder + "DeepFace" + Path.DirectorySeparatorChar +
				"Log %YEAR%-%MONTH%-%DAY%T%HOUR%.xml",
				Gateway.AppDataFolder + "Transforms" + Path.DirectorySeparatorChar + "SnifferXmlToHtml.xslt",
				7, BinaryPresentationMethod.Base64);

			return [xmlFileSniffer, snifferProxy];
		}

		/// <summary>
		/// Validates an identity application.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		/// <param name="Sniffers">Optional sniffers.</param>
		public async Task<double?> ValidateDistance(IIdentityApplication Application,
			params ISniffer[] Sniffers)
		{
			SKBitmap TravelDocumentFaceBitmap = null;

			try
			{
				string PreviewId = GetPreviewId(Application);
				if (string.IsNullOrEmpty(PreviewId))
					return null;

				IPhoto ProfilePhoto = GetProfilePhoto(Application);
				if (ProfilePhoto is null)
					return null;

				KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);
				XmlDocument Nfc = P.Key;
				DocumentInformation DocInfo = P.Value;
				Representation TravelDocumentFace = null;
				AdditionalPersonalDetails AdditionalDetails = null;

				if (Nfc is null || DocInfo is null)
					return null;

				bool PermitInvalidation = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".PermitInvalidation", true);

				// Validate NFC replay document.

				try
				{
					IsoDepReplay Replay = new(Nfc);
					byte[] LocalKeySeed = PreviewId == "TEST" ? null : Encoding.UTF8.GetBytes(PreviewId);
					using TravelDocumentsClient Client = new(Replay, Replay.DocumentInfo, LocalKeySeed);

					AuthenticateResult AuthResult = await Client.Authenticate();
					if (AuthResult != AuthenticateResult.Success)
					{
						this.FailAll(Application, "Unable to authenticate NFC document: " +
							AuthResult.ToString(), PermitInvalidation);

						return null;
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
							TravelDocumentFace = Client.BiometricEncodingFace[0].BiometricDataBlock?.Record?.Representations[0];

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
						this.FailAll(Application, "Unable to read embedded Travel Document: " +
							Result.ToString(), PermitInvalidation);

						return null;
					}

					if (DocInfo is null)
					{
						this.FailAll(Application, "MRZ not available in embedded Travel Document.",
							PermitInvalidation);

						return null;
					}

					if (DocInfo.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty) !=
						DocInfo2.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty))
					{
						this.FailAll(Application, "MRZ provided in authentication not same as MRZ encoded in Travel Document.",
							PermitInvalidation);

						return null;
					}

					if (TravelDocumentFace is null)
					{
						this.FailAll(Application, "Face not available in embedded Travel Document.",
							PermitInvalidation);

						return null;
					}

					InterleavedImage TravelDocumentImage = J2kImage.FromBytes(TravelDocumentFace.ImageData);
					TravelDocumentFaceBitmap = TravelDocumentImage.As<SKBitmap>();
				}
				catch (Exception ex)
				{
					// Manipulation of NFC document results in automatic failure.

					Log.Exception(ex);
					this.FailAll(Application, ex.Message, PermitInvalidation);
					return null;
				}

				// Validate Profile Photo (only check personal information, if photos match, but are not the same)

				string DeepFaceUrl = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".DeepFaceUrl", "http://localhost:5000/");
				bool AntiSpoofing = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".AntiSpoofing", true);
				bool RequireRecentPhoto = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".RequireRecentPhoto", true);
				double MaxDistance = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".MaxDistance", 1.04);    // Facenet512, with Euclidean L2 Norm, typical threshold.
				double MinDistance = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".MinDistance", 0.15);    // Empirical value to avoid clients using an edited passport photo as profile picture.
				double ManualDistance = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".ManualDistance", 0.40);    // Empirical value that leads to manual approval if above Minimum distance, and belwot his threshold.

				using DeepFaceClient DeepFace = new(DeepFaceUrl, AntiSpoofing, Sniffers);
				double Distance;

				try
				{
					if (RequireRecentPhoto)
					{
						if (ProfilePhoto.ContentType == ImageCodec.ContentTypeJpg)
						{
							if (EXIF.TryExtractFromJPeg(ProfilePhoto.Binary, out ExifTag[] Tags))
							{
								DateTime? DateTime = null;
								DateTime? DateTimeOriginal = null;
								DateTime? DateTimeDigitized = null;

								foreach (ExifTag Tag in Tags)
								{
									switch (Tag.Name)
									{
										case ExifTagName.DateTime:
											if (TryParseExifDateTime(Tag.Value, out DateTime TP))
												DateTime = TP;
											break;

										case ExifTagName.DateTimeOriginal:
											if (TryParseExifDateTime(Tag.Value, out TP))
												DateTimeOriginal = TP;
											break;

										case ExifTagName.DateTimeDigitized:
											if (TryParseExifDateTime(Tag.Value, out TP))
												DateTimeDigitized = TP;
											break;
									}
								}

								if (!DateTime.HasValue)
									DateTime = DateTimeDigitized;

								if (DateTimeOriginal.HasValue)
									DateTime = DateTimeOriginal;

								if (!DateTime.HasValue)
								{
									this.InvalidatePhoto(Application, ProfilePhoto,
										"No valid timestamp found in photo.", "en", "InvalidFormat",
										PermitInvalidation);

									return null;
								}
								else if (Math.Abs(System.DateTime.Now.Subtract(DateTime.Value).TotalDays) >= 1)
								{
									this.InvalidatePhoto(Application, ProfilePhoto,
										"Photo must be recent.", "en", "PhotoNotRecent",
										PermitInvalidation);

									return null;
								}
							}
							else
							{
								this.InvalidatePhoto(Application, ProfilePhoto,
									"No meta-data found in photo.", "en", "InvalidFormat",
									PermitInvalidation);

								return null;
							}
						}
						else
						{
							this.InvalidatePhoto(Application, ProfilePhoto,
								"Profile photo must be a JPEG photo.", "en", "InvalidFormat",
								PermitInvalidation);

							return null;
						}
					}

					FaceRepresentation[] ProfilePhotoRepresentations = await DeepFace.Represent(
							ProfilePhoto.Binary, ProfilePhoto.ContentType);

					if (ProfilePhotoRepresentations.Length == 0)
					{
						this.InvalidatePhoto(Application, ProfilePhoto,
							"No face detected in profile photo.", "en", "NoFace",
							PermitInvalidation);

						return null;
					}
					else if (ProfilePhotoRepresentations.Length > 1)
					{
						this.InvalidatePhoto(Application, ProfilePhoto,
							"Multiple faces detected in profile photo.", "en", "MultipleFaces",
							PermitInvalidation);

						return null;
					}
					else if (ProfilePhotoRepresentations[0].FaceConfidence < .95)
					{
						this.InvalidatePhoto(Application, ProfilePhoto,
							"Low quality profile photo.", "en", "LowQualityPhoto",
							PermitInvalidation);

						return null;
					}

					using SKImage TravelDocumentFaceImage = SKImage.FromBitmap(TravelDocumentFaceBitmap);

					DeepFace.AntiSpoofing = false;  // No need for the cryptographically protected passport photo. Saves time and resources.

					FaceRepresentation[] TravelDocumentRepresentations = await DeepFace.Represent(TravelDocumentFaceImage);

					if (TravelDocumentRepresentations.Length == 0)
					{
						this.FailAll(Application, "No face detected in NFC document.",
							PermitInvalidation);
						return null;
					}
					else if (TravelDocumentRepresentations.Length > 1)
					{
						this.FailAll(Application, "More than one face detected in NFC document.",
							PermitInvalidation);

						return null;
					}
					else if (TravelDocumentRepresentations[0].FaceConfidence < .95)
					{
						this.FailAll(Application, "Photo in Travel Document too low quality.",
							PermitInvalidation);

						return null;
					}

					Distance = ComputeNormalizedEuclideanDistance(
						TravelDocumentRepresentations[0].Embedding,
						ProfilePhotoRepresentations[0].Embedding);

					using SKData Data = TravelDocumentFaceImage.Encode(SKEncodedImageFormat.Png, 0);

					if (Distance > MaxDistance)
					{
						this.InvalidatePhoto(Application, ProfilePhoto,
							"Profile photo does not match photo in Travel Document.", "en",
							"PhotoMismatch", PermitInvalidation);

						return Distance;
					}
					else if (Distance < MinDistance)
					{
						this.InvalidatePhoto(Application, ProfilePhoto,
							"Profile photo too similar to passport photo.", "en", "PhotoTooSimilar",
							PermitInvalidation);

						return Distance;
					}
					else if (Distance < ManualDistance)
					{
						Application.ReportError("Profile photo too similar to passport photo.", "en",
							"PhotoTooSimilar", ValidationErrorType.Client, this);
						return Distance;
					}

					Application.PhotoValid(ProfilePhoto, this);
				}
				finally
				{
					foreach (ISniffer Sniffer in Sniffers)
						DeepFace.Remove(Sniffer);
				}


				// Validate Personal Information claims

				PersonalInformation PersonalInfo = PersonalInformation.Create(Application.Claims);

				bool EnforceUniqueness = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".EnforceUniqueness", false);
				byte[] Hash = null;

				if (EnforceUniqueness)
				{
					Hash = await ComputeUniquenessHashDigest(DocInfo);

					if (await PersistedHashes.VerifyHash(Hash))
					{
						this.FailAll(Application, "An application with the same personal information has already been accepted.",
							"en", "DuplicateApplication", PermitInvalidation);

						return Distance;
					}
				}

				DateTime? BirthDate = PersonalInfo.BirthDate;
				if (BirthDate.HasValue)
				{
					if (BirthDate > DateTime.Today)
					{
						this.InvalidateClaims(Application,
						[
							PersonalInformation.BirthDayTag,
							PersonalInformation.BirthMonthTag,
							PersonalInformation.BirthYearTag
						], "Future birth date.", "en", "FutureBirthDate", PermitInvalidation);
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
						{
							this.InvalidateClaim(Application, PersonalInformation.BirthDayTag,
								"Birth Day invalid.", "en", "BirthDayInvalid", PermitInvalidation);
						}

						if (BirthMonth == BirthDate.Value.Month)
							Application.ClaimValid(PersonalInformation.BirthMonthTag, this);
						else
						{
							this.InvalidateClaim(Application, PersonalInformation.BirthMonthTag,
								"Birth Month invalid.", "en", "BirthMonthInvalid", PermitInvalidation);
						}

						if (BirthYear == (BirthDate.Value.Year % 100))
							Application.ClaimValid(PersonalInformation.BirthYearTag, this);
						else
						{
							this.InvalidateClaim(Application, PersonalInformation.BirthYearTag,
								"Birth Year invalid.", "en", "BirthYearInvalid", PermitInvalidation);
						}
					}

					if (PersonalInfo.AgeAbove.HasValue)
					{
						if (PersonalInfo.Age >= PersonalInfo.AgeAbove.Value)
							Application.ClaimValid(PersonalInformation.AgeAboveTag, this);
						else
						{
							this.InvalidateClaim(Application, PersonalInformation.AgeAboveTag,
								"Age not reached.", "en", "AgeNotReached", PermitInvalidation);
						}
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
						{
							this.InvalidateClaim(Application, PersonalInformation.AgeAboveTag,
								"Age not reached.", "en", "AgeNotReached", PermitInvalidation);
						}
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
					{
						this.InvalidateClaim(Application, PersonalInformation.CountryTag,
							"Country invalid.", "en", "CountryCodeMismatch", PermitInvalidation);
					}
				}

				if (!CaseInsensitiveString.IsNullOrEmpty(PersonalInfo.Nationality) &&
					!string.IsNullOrEmpty(DocInfo.Nationality))
				{
					if (ISO_3166_1.CompareCountryCode(PersonalInfo.Nationality, DocInfo.Nationality))
						Application.ClaimValid(PersonalInformation.NationalityTag, this);
					else
					{
						this.InvalidateClaim(Application, PersonalInformation.NationalityTag,
							"Nationality invalid.", "en", "NationalityInvalid", PermitInvalidation);
					}
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
						{
							this.InvalidateClaim(Application, PersonalInformation.GenderTag,
								"Gender invalid.", "en", "GenderInvalid", PermitInvalidation);
						}
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
							string s = await PNrValidator.Normalize(Country, PersonalInfo.PersonalNumber);

							if (s != PersonalInfo.PersonalNumber)
							{
								this.InvalidateClaim(Application, PersonalInformation.PersonalNumberTag,
									"Personal number not normalized.", "en", "PNrNotNormalized", PermitInvalidation);
							}
							else
							{
								bool? Valid = await PNrValidator.IsValid(Country, PersonalInfo.PersonalNumber);

								if (Valid.HasValue)
								{
									if (!Valid.Value)
									{
										this.InvalidateClaim(Application, PersonalInformation.PersonalNumberTag,
											"Personal number invalid according to national rules.", "en", "PersonalNumberInvalid",
											PermitInvalidation);
									}
									else
									{
										bool? AdditionalInfoAsPNr = null;
										bool? OptionalDataAsPNr = null;
										bool? DocumentNrAsPNr = null;

										if (!string.IsNullOrEmpty(AdditionalDetails?.PersonalNumber))
										{
											s = await PNrValidator.Normalize(Country, AdditionalDetails?.PersonalNumber);
											if (!string.IsNullOrEmpty(s) &&
												(await PNrValidator.IsValid(Country, s) ?? false))
											{
												AdditionalInfoAsPNr = s == PersonalInfo.PersonalNumber;
											}
										}

										if (!string.IsNullOrEmpty(DocInfo.OptionalData))
										{
											s = await PNrValidator.Normalize(Country, DocInfo.OptionalData);
											if (!string.IsNullOrEmpty(s) &&
												(await PNrValidator.IsValid(Country, s) ?? false))
											{
												OptionalDataAsPNr = s == PersonalInfo.PersonalNumber;
											}
										}

										if (!string.IsNullOrEmpty(DocInfo.DocumentNumber))
										{
											s = await PNrValidator.Normalize(Country, DocInfo.DocumentNumber);
											if (!string.IsNullOrEmpty(s) &&
												(await PNrValidator.IsValid(Country, s) ?? false))
											{
												DocumentNrAsPNr = s == PersonalInfo.PersonalNumber;
											}
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
											this.InvalidateClaim(Application, PersonalInformation.PersonalNumberTag,
												"Personal number does not match.", "en", "PersonalNumberMismatch",
												PermitInvalidation);
										}
									}
								}
							}
						}
					}
					else
					{
						this.InvalidateClaim(Application, PersonalInformation.PersonalNumberTag,
							"Country not specified, or available in MRZ.", "en", "CountryNotSpecified",
							PermitInvalidation);
					}
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
						this.InvalidateClaim(Application, PersonalInformation.LastNamesTag,
							"Last name(s) invalid.", "en", "LastNameInvalid", PermitInvalidation);
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
						this.InvalidateClaim(Application, PersonalInformation.FirstNameTag,
							"First name(s) invalid.", "en", "FirstNameInvalid", PermitInvalidation);
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
						this.InvalidateClaims(Application,
							[
								PersonalInformation.FullNameTag,
								PersonalInformation.FirstNameTag,
								PersonalInformation.MiddleNamesTag,
								PersonalInformation.LastNamesTag
							],
							"Full name invalid.", "en", "FullNameInvalid", PermitInvalidation);
					}
				}

				if (Hash is not null &&
					Application.IsValid.HasValue &&
					Application.IsValid.Value)
				{
					double LifeCycleDays = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".LifeCycleDays", 3652.0);
					DateTime Expires = DateTime.UtcNow.Date.AddDays(Math.Round(LifeCycleDays));

					await PersistedHashes.AddHash(Expires, Hash);
				}

				return Distance;
			}
			catch (Exception ex)
			{
				string Language = null;
				string Code = null;

				if (ex.Message.Contains("Face could not be detected", StringComparison.InvariantCultureIgnoreCase))
				{
					Language = "en";
					Code = "NoFace";
				}

				Application.ReportError(ex.Message, Language, Code, ValidationErrorType.Service, this);
				return null;
			}
			finally
			{
				TravelDocumentFaceBitmap?.Dispose();
			}
		}

		/// <summary>
		/// Validates a recording of a communication session with a travel document.
		/// </summary>
		/// <param name="PreviewId">Preview ID.</param>
		/// <param name="NfcCommunication">NFC recording.</param>
		/// <param name="Sniffers">Optional sniffers.</param>
		/// <returns>If communication represents a valid session or not.</returns>
		public static async Task<bool> ValidateNfcCommunication(string PreviewId,
			XmlDocument NfcCommunication, params ISniffer[] Sniffers)
		{
			SKBitmap TravelDocumentFaceBitmap = null;

			if (NfcCommunication is null)
				return false;

			try
			{
				DocumentInformation DocInfo = GetNfcDocument(NfcCommunication);
				Representation TravelDocumentFace = null;
				AdditionalPersonalDetails AdditionalDetails = null;

				if (DocInfo is null)
					return false;

				// Validate NFC replay document.

				IsoDepReplay Replay = new(NfcCommunication);
				byte[] LocalKeySeed = PreviewId == "TEST" ? null : Encoding.UTF8.GetBytes(PreviewId);
				using TravelDocumentsClient Client = new(Replay, Replay.DocumentInfo, LocalKeySeed, Sniffers);

				AuthenticateResult AuthResult = await Client.Authenticate();
				if (AuthResult != AuthenticateResult.Success)
					return false;

				DocumentInformation DocInfo2 = null;

				Client.MrzUpdated += (_, e) =>
				{
					DocInfo2 = Client.Mrz.DocumentInformation;
					return Task.CompletedTask;
				};

				Client.BiometricEncodingFaceUpdated += (_, e) =>
				{
					if (Client.BiometricEncodingFace is not null)
						TravelDocumentFace = Client.BiometricEncodingFace[0].BiometricDataBlock?.Record?.Representations[0];

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
					return false;

				if (DocInfo is null)
					return false;

				if (DocInfo.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty) !=
					DocInfo2.MRZ_Information.Replace("\n", string.Empty).Replace("\r", string.Empty))
				{
					return false;
				}

				if (TravelDocumentFace is null)
					return false;

				InterleavedImage TravelDocumentImage = J2kImage.FromBytes(TravelDocumentFace.ImageData);
				TravelDocumentFaceBitmap = TravelDocumentImage.As<SKBitmap>();

				return false;
			}
			catch (Exception)
			{
				return false;
			}
			finally
			{
				TravelDocumentFaceBitmap?.Dispose();
			}
		}

		private void InvalidateClaim(IIdentityApplication Application, string Claim,
			string Error, string ErrorLanguage, string ErrorCode, bool PermitInvalidation)
		{
			if (PermitInvalidation)
				Application.ClaimInvalid(Claim, Error, ErrorLanguage, ErrorCode, this);
			else
			{
				Application.ReportError(Error, ErrorLanguage, ErrorCode,
					ValidationErrorType.Client, this);
			}
		}

		private void InvalidateClaims(IIdentityApplication Application, string[] Claims,
			string Error, string ErrorLanguage, string ErrorCode, bool PermitInvalidation)
		{
			if (PermitInvalidation)
			{
				foreach (string Claim in Claims)
					Application.ClaimInvalid(Claim, Error, ErrorLanguage, ErrorCode, this);
			}
			else
			{
				Application.ReportError(Error, ErrorLanguage, ErrorCode,
					ValidationErrorType.Client, this);
			}
		}

		private void InvalidatePhoto(IIdentityApplication Application, IPhoto Photo,
			string Error, string ErrorLanguage, string ErrorCode, bool PermitInvalidation)
		{
			if (PermitInvalidation)
				Application.PhotoInvalid(Photo, Error, ErrorLanguage, ErrorCode, this);
			else
			{
				Application.ReportError(Error, ErrorLanguage, ErrorCode,
					ValidationErrorType.Client, this);
			}
		}

		private static async Task<byte[]> ComputeUniquenessHashDigest(DocumentInformation DocInfo)
		{
			bool IncludeDocumentNumber = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludeDocumentNumber", true);
			bool IncludeCountry = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludeCountry", true);
			bool IncludeBirthDate = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludeBirthDate", true);
			bool IncludePrimaryIdentifier = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludePrimaryIdentifier", true);
			bool IncludeSecondaryIdentifier = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludeSecondaryIdentifier", true);
			bool IncludeOptionalData = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".IncludeOptionalData", false);
			string Salt = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".Salt", string.Empty);
			StringBuilder sb = new();

			sb.Append(Salt);

			if (IncludeDocumentNumber)
			{
				sb.Append('|');
				sb.Append(DocInfo.DocumentNumber ?? string.Empty);
			}

			if (IncludeCountry)
			{
				sb.Append('|');
				sb.Append(DocInfo.IssuingState ?? string.Empty);
			}

			if (IncludeBirthDate)
			{
				sb.Append('|');
				sb.Append(DocInfo.DateOfBirth ?? string.Empty);
			}

			if (IncludePrimaryIdentifier)
			{
				foreach (string Name in DocInfo.PrimaryIdentifier ?? [])
				{
					sb.Append('|');
					sb.Append(Name);
				}
			}

			if (IncludeSecondaryIdentifier)
			{
				foreach (string Name in DocInfo.SecondaryIdentifier ?? [])
				{
					sb.Append('|');
					sb.Append(Name);
				}
			}

			if (IncludeOptionalData)
			{
				sb.Append('|');
				sb.Append(DocInfo.OptionalData ?? string.Empty);
			}

			return Hashes.ComputeSHA256Hash(Encoding.UTF8.GetBytes(sb.ToString()));
		}

		/// <summary>
		/// Computes the Euclidean L2 norm distance between two vectors, of potentially 
		/// different lengths. If the vectors are of different lengths, the missing values 
		/// are considered to be zero.
		/// </summary>
		/// <param name="V1">The first vector.</param>
		/// <param name="V2">The second vector.</param>
		/// <returns>The Euclidean distance between the two vectors.</returns>
		public static double ComputeNormalizedEuclideanDistance(double[] V1, double[] V2)
		{
			double l1 = 0;
			double l2 = 0;

			double d = 0;
			int c1 = V1.Length;
			int c2 = V2.Length;
			int c = Math.Min(c1, c2);
			double v;
			int i;

			for (i = 0; i < c1; i++)
			{
				v = V1[i];
				l1 += v * v;
			}

			for (i = 0; i < c2; i++)
			{
				v = V2[i];
				l2 += v * v;
			}

			if (l1 == 0 || l2 == 0)
				return double.MaxValue;

			l1 = 1.0 / Math.Sqrt(l1);
			l2 = 1.0 / Math.Sqrt(l2);

			for (i = 0; i < c; i++)
			{
				v = (V1[i] * l1) - (V2[i] * l2);
				d += v * v;
			}

			if (i < c1)
			{
				for (; i < c1; i++)
				{
					v = V1[i] * l1;
					d += v * v;
				}
			}
			else if (i < c2)
			{
				for (; i < c2; i++)
				{
					v = V2[i] * l2;
					d += v * v;
				}
			}

			return Math.Sqrt(d);
		}

		private static bool TryParseExifDateTime(object Value, out DateTime Timestamp)
		{
			if (Value is string s)
				return TryParseExifDateTime(s, out Timestamp);
			else
			{
				Timestamp = DateTime.MinValue;
				return false;
			}
		}

		private static bool TryParseExifDateTime(string Value, out DateTime Timestamp)
		{
			int i = Value.IndexOf(' ');
			if (i > 0)
				Value = Value[..i].Replace(':', '-') + 'T' + Value[(i + 1)..];

			return XML.TryParse(Value, out Timestamp);
		}

		private static string Append(string[] Names)
		{
			int c = Names?.Length ?? 0;
			if (c == 0)
				return null;

			if (c == 1)
				return Names[0];

			StringBuilder sb = new();

			for (int i = 0; i < c; i++)
			{
				if (i > 0)
					sb.Append(' ');

				sb.Append(Names[i]);
			}

			return sb.ToString();
		}

		private void FailAll(IIdentityApplication Application, string Message,
			bool PermitInvalidation)
		{
			this.FailAll(Application, Message, "en", "NfcInvalid", PermitInvalidation);
		}

		private void FailAll(IIdentityApplication Application, string Message,
			string Language, string Code, bool PermitInvalidation)
		{
			if (PermitInvalidation)
			{
				Application.InvalidateAllClaims(Message, Language, Code);
				Application.InvalidateAllPhotos(Message, Language, Code);
			}
			else
				Application.ReportError(Message, Language, Code, ValidationErrorType.Client, this);
		}

		/// <summary>
		/// Registers a web sniffer on the ShuftiPro client.
		/// </summary>
		/// <param name="SnifferId">Sniffer ID</param>
		/// <param name="Request">HTTP Request for sniffer page.</param>
		/// <param name="UserVariable">Name of user variable.</param>
		/// <param name="Privileges">Privileges required to view content.</param>
		/// <returns>Code to embed into page.</returns>
		public static string RegisterSniffer(string SnifferId, HttpRequest Request,
			string UserVariable, params string[] Privileges)
		{
			return Gateway.AddWebSniffer(SnifferId, Request, observable, UserVariable, Privileges);
		}

		#endregion

		#region IIdentityStatefulService

		/// <summary>
		/// Called when an Identity application state has been updated.
		/// </summary>
		/// <param name="Application">Identity application.</param>
		public async Task ApplicationUpdated(IIdentityApplicationState Application)
		{
			if (Application.State != IdentityState.Approved)
				return;

			string PreviewId = GetPreviewId(Application);
			if (string.IsNullOrEmpty(PreviewId))
				return;

			KeyValuePair<XmlDocument, DocumentInformation> P = GetNfcDocument(Application);
			DocumentInformation DocInfo = P.Value;

			if (DocInfo is null)
				return;

			byte[] Hash = await ComputeUniquenessHashDigest(DocInfo);
			double LifeCycleDays = await RuntimeSettings.GetAsync(typeof(ServiceModule).Namespace + ".LifeCycleDays", 3652.0);
			DateTime Expires = DateTime.UtcNow.Date.AddDays(Math.Round(LifeCycleDays));

			await PersistedHashes.AddHash(Expires, Hash);   // Makes sure hash is available in hash store. Only adds record if one is not already added.
		}

		#endregion
	}
}
