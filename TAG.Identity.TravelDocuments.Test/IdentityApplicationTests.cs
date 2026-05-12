using NeuroAccess.Nfc;
using NeuroAccess.Nfc.TravelDocuments;
using NeuroAccess.Nfc.TravelDocuments.DataObjects;
using Paiwise;
using System.Text;
using System.Xml;
using Waher.Content;
using Waher.Content.Images;
using Waher.Persistence;
using Waher.Persistence.Files;
using Waher.Persistence.Serialization;
using Waher.Runtime.Inventory;
using Waher.Runtime.IO;
using Waher.Runtime.Settings;
using Waher.Script;

namespace TAG.Identity.TravelDocuments.Test
{
	[TestClass]
	[DoNotParallelize]
	public sealed class IdentityApplicationTests
	{
		private static ServiceModule? module;
		private static FilesProvider? filesProvider;

		[AssemblyInitialize]
		public static async Task AssemblyInitialize(TestContext _)
		{
			Types.Initialize(
				typeof(IdentityApplicationTests).Assembly,
				typeof(IIdentityApplication).Assembly,
				typeof(ServiceModule).Assembly,
				typeof(TravelDocumentsClient).Assembly,
				typeof(InternetContent).Assembly,
				typeof(ImageCodec).Assembly,
				typeof(Database).Assembly,
				typeof(FilesProvider).Assembly,
				typeof(ObjectSerializer).Assembly,
				typeof(RuntimeSettings).Assembly,
				typeof(Expression).Assembly);

			if (!Database.HasProvider)
			{
				filesProvider = await FilesProvider.CreateAsync("Data", "Default", 8192, 1000, 8192, Encoding.UTF8, 10000, true);
				Database.Register(filesProvider);
			}

			Assert.IsTrue(await Types.StartAllModules(60000));

			foreach (IModule Module in Types.GetLoadedModules())
			{
				if (Module is ServiceModule ServiceModule)
					module = ServiceModule;
			}

			Assert.IsNotNull(module);
		}

		[AssemblyCleanup]
		public static async Task AssemblyCleanup()
		{
			await Types.StopAllModules();

			if (filesProvider is not null)
			{
				await filesProvider.DisposeAsync();
				filesProvider = null;
			}

			module = null;
		}

		[ClassInitialize]
		public static async Task ClassInitialize(TestContext _)
		{
			await RuntimeSettings.SetAsync(typeof(ServiceModule).Namespace + ".DeepFaceUrl", "http://localhost:5000/");
			await RuntimeSettings.SetAsync(typeof(ServiceModule).Namespace + ".AntiSpoofing", false);
			await RuntimeSettings.SetAsync(typeof(ServiceModule).Namespace + ".EnforceUniqueness", false);
		}

		[TestMethod]
		[DataRow("Passport01", "Claims.json", "ProfilePhoto.jpg", "NFC.xml")]
		public async Task Test_01_Supports(string Folder, string ClaimsFile, string PhotoFile, string NfcFile)
		{
			KeyValuePair<string, object>[] Claims = await LoadClaims(Folder, ClaimsFile);
			PersonalInformation PI = Create(Claims);

			IdentityApplication Application = new(
				Guid.NewGuid().ToString() + "@example.org", 
				"urn:nf:iot:leg:id:1.0", true, PI,
				await LoadClaims(Folder,ClaimsFile),
				[
					await LoadProfilePhoto(Folder, PhotoFile)
				],
				[
					LoadDocument(Folder, NfcFile)
				],
				new InternalTestAccount());

			Assert.IsTrue(module!.Supports(Application) > Grade.NotAtAll);
		}

		[TestMethod]
		[DataRow("Passport01", "Claims.json", "ProfilePhoto.jpg", "NFC.xml")]
		[DataRow("Passport02", "Claims.json", "ProfilePhoto.jpg", "NFC.xml")]
		public async Task Test_02_Validate(string Folder, string ClaimsFile, string PhotoFile, string NfcFile)
		{
			KeyValuePair<string, object>[] Claims = await LoadClaims(Folder, ClaimsFile);
			PersonalInformation PI = Create(Claims);

			IdentityApplication Application = new(
				Guid.NewGuid().ToString() + "@example.org",
				"urn:nf:iot:leg:id:1.0", true, PI,
				await LoadClaims(Folder, ClaimsFile),
				[
					await LoadProfilePhoto(Folder, PhotoFile)
				],
				[
					LoadDocument(Folder, NfcFile)
				],
				new InternalTestAccount());

			Assert.IsTrue(module!.Supports(Application) > Grade.NotAtAll);

			double? Distance = await module.ValidateDistance(Application);
			Assert.IsFalse(Application.HasErrors, Application.FirstError?.ErrorMessage ?? "Unspecified error");
		
			Assert.IsTrue(Distance.HasValue, "Distance not evaluated.");
			Console.Out.WriteLine(Distance.Value);

			Application.ClaimValid("PREVIEW", this);

			Assert.AreEqual(0, Application.UnvalidatedClaims.Length, "Application has unvalidated claims.");
			Assert.AreEqual(0, Application.UnvalidatedPhotos.Length, "Application has unvalidated photos.");
			Assert.IsTrue(Application.HasValidatedClaims, "Application has no validated claims.");
			Assert.IsTrue(Application.HasValidatedPhotos, "Application has no validated photos.");
			Assert.IsTrue(Application.IsValid, "Application is not valid.");
		}

		[TestMethod]
		[DataRow("Passport01", "Claims.json", "PassportPhoto.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoBlackWhite.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoCropped.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoRotated.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoSkewed.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoFlipped.png", "NFC.xml")]
		[DataRow("Passport01", "Claims.json", "PassportPhotoBlur.png", "NFC.xml")]
		public async Task Test_03_Invalidate(string Folder, string ClaimsFile, string PhotoFile, string NfcFile)
		{
			KeyValuePair<string, object>[] Claims = await LoadClaims(Folder, ClaimsFile);
			PersonalInformation PI = Create(Claims);

			IdentityApplication Application = new(
				Guid.NewGuid().ToString() + "@example.org",
				"urn:nf:iot:leg:id:1.0", true, PI,
				await LoadClaims(Folder, ClaimsFile),
				[
					await LoadProfilePhoto(Folder, PhotoFile)
				],
				[
					LoadDocument(Folder, NfcFile)
				],
				new InternalTestAccount());

			Assert.IsTrue(module!.Supports(Application) > Grade.NotAtAll);

			double? Distance = await module.ValidateDistance(Application);

			if (!Application.HasErrors)
			{
				Console.Out.WriteLine(Distance?.ToString() ?? "Distance not available");

				Assert.AreEqual(0, Application.ValidatedClaims.Length, "Application has validated claims.");
				Assert.AreEqual(1, Application.InvalidatedPhotos.Length, "Application has not invalidated photos.");
				Assert.IsFalse(Application.HasValidatedClaims, "Application has validated claims.");
				Assert.IsFalse(Application.HasValidatedPhotos, "Application has validated photos.");
				Assert.IsFalse(Application.IsValid, "Application is valid.");
			}
		}

		[TestMethod]
		[DataRow("Passport02", "f9642411-08a4-4db7-8e50-c1bcbdbe016f", "NFC.xml")]
		public async Task Test_04_ReplayNfcReadoutOnly(string Folder, string PreviewId, string NfcFile)
		{
			XmlDocument Nfc = LoadDocument(Folder, NfcFile);
			IsoDepReplay Replay = new(Nfc);
			byte[] LocalKeySeed = Encoding.UTF8.GetBytes(PreviewId);
			using TravelDocumentsClient Client = new(Replay, Replay.DocumentInfo, LocalKeySeed);

			Client.ReadDG1 = true;
			Client.ReadDG2 = true;
			Client.ReadDG11 = false;
			Client.ReadDG12 = false;

			AuthenticateResult AuthResult = await Client.Authenticate();
			Assert.AreEqual(AuthenticateResult.Success, AuthResult, "NFC replay authentication failed.");

			string OnboardingNeuron = await RuntimeSettings.GetAsync("Onboarding.DomainName", "id.tagroot.io");
			ReadTravelDocumentResult ReadResult = await Client.ReadTravelDocument(OnboardingNeuron);

			Assert.AreEqual(ReadTravelDocumentResult.Success, ReadResult, "NFC replay read failed.");
			Assert.IsTrue(HasFaceRepresentation(Client), "Face not available in embedded Travel Document.");
		}

		private static string GetPath(string Folder, string FileName)
		{
			return Path.GetFullPath(Path.Combine("..", "..", "..", "SensitiveData", Folder, FileName));
		}

		private static async Task<KeyValuePair<string, object>[]> LoadClaims(string Folder, string FileName)
		{
			string s = await Files.ReadAllTextAsync(GetPath(Folder, FileName));
			Dictionary<string, object> Result = (Dictionary<string, object>)JSON.Parse(s);
			return [.. Result];
		}

		private static async Task<Photo> LoadProfilePhoto(string Folder, string FileName)
		{
			string ContentType = InternetContent.GetContentType(Path.GetExtension(FileName));
			byte[] Binary = await File.ReadAllBytesAsync(GetPath(Folder, FileName));
			string ProfilePhotoName = "ProfilePhoto" + Path.GetExtension(FileName);

			return new Photo(ContentType, ProfilePhotoName, Binary);
		}

		private static XmlDocument LoadDocument(string Folder, string FileName)
		{
			XmlDocument Doc = new();
			Doc.Load(GetPath(Folder, FileName));

			return Doc;
		}

		private static bool HasFaceRepresentation(TravelDocumentsClient Client)
		{
			if (Client.BiometricEncodingFace is null)
				return false;

			foreach (BiometricInformationTemplate Template in Client.BiometricEncodingFace)
			{
				NeuroAccess.Nfc.TravelDocuments.ISO19794.Representation[]? Representations =
					Template.BiometricDataBlock?.Record?.Representations;
				if (Representations is not null && Representations.Length > 0)
					return true;
			}

			return false;
		}

		public static PersonalInformation Create(IEnumerable<KeyValuePair<string, object>> Properties)
		{
			PersonalInformation Result = new();

			foreach (KeyValuePair<string, object> P in Properties)
			{
				switch (P.Key)
				{
					case "FIRST":
						Result.FirstName = P.Value?.ToString();
						break;

					case "MIDDLE":
						Result.MiddleNames = P.Value?.ToString();
						break;

					case "LAST":
						Result.LastNames = P.Value?.ToString();
						break;

					case "FULLNAME":
						Result.FullName = P.Value?.ToString();
						break;

					case "ADDR":
						Result.Address = P.Value?.ToString();
						break;

					case "ADDR2":
						Result.Address2 = P.Value?.ToString();
						break;

					case "ZIP":
						Result.PostalCode = P.Value?.ToString();
						break;

					case "AREA":
						Result.Area = P.Value?.ToString();
						break;

					case "CITY":
						Result.City = P.Value?.ToString();
						break;

					case "REGION":
						Result.Region = P.Value?.ToString();
						break;

					case "COUNTRY":
						Result.Country = P.Value?.ToString();
						break;

					case "NATIONALITY":
						Result.Nationality = P.Value?.ToString();
						break;

					case "GENDER":
						if (P.Value is Gender Gender)
							Result.Gender = Gender;
						else
						{
							switch (P.Value?.ToString()?.ToLower())
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
						if (int.TryParse(P.Value?.ToString(), out int i) && i >= 1 && i <= 31)
							Result.BirthDay = i;
						break;

					case "BMONTH":
						if (int.TryParse(P.Value?.ToString(), out i) && i >= 1 && i <= 12)
							Result.BirthMonth = i;
						break;

					case "BYEAR":
						if (int.TryParse(P.Value?.ToString(), out i) && i >= 1900 && i <= 2100)
							Result.BirthYear = i;
						break;

					case "PNR":
						Result.PersonalNumber = P.Value?.ToString();
						break;

					case "ORGNAME":
						Result.OrgName = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGDEPT":
						Result.OrgDepartment = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGROLE":
						Result.OrgRole = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGADDR":
						Result.OrgAddress = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGADDR2":
						Result.OrgAddress2 = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGZIP":
						Result.OrgPostalCode = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGAREA":
						Result.OrgArea = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGCITY":
						Result.OrgCity = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGREGION":
						Result.OrgRegion = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGCOUNTRY":
						Result.OrgCountry = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "ORGNR":
						Result.OrgNumber = P.Value?.ToString();
						Result.HasOrg = true;
						break;

					case "PHONE":
						Result.Phone = P.Value?.ToString();
						break;

					case "EMAIL":
						Result.EMail = P.Value?.ToString();
						break;

					case "JID":
						Result.Jid = P.Value?.ToString();
						break;
				}
			}

			Result.HasBirthDate =
				Result.BirthDay.HasValue &&
				Result.BirthMonth.HasValue &&
				Result.BirthYear.HasValue &&
				Result.BirthDay.Value <= DateTime.DaysInMonth(Result.BirthYear.Value, Result.BirthMonth.Value);

			if (!Result.HasBirthDate)
			{
				Result.BirthDay = null;
				Result.BirthMonth = null;
				Result.BirthYear = null;
			}

			if (CaseInsensitiveString.IsNullOrEmpty(Result.FullName))
				Result.FullName = Waher.Networking.XMPP.Contracts.LegalIdentity.JoinNames(Result.FirstName, Result.MiddleNames, Result.LastNames);
			else if (CaseInsensitiveString.IsNullOrEmpty(Result.FirstName) &&
				CaseInsensitiveString.IsNullOrEmpty(Result.MiddleNames) &&
				CaseInsensitiveString.IsNullOrEmpty(Result.LastNames))
			{
				Waher.Networking.XMPP.Contracts.LegalIdentity.SeparateNames(Result.FullName, out Result.FirstName, out Result.MiddleNames, out Result.LastNames);
			}

			return Result;
		}

	}
}
