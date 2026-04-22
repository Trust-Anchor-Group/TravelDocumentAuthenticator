using System.Xml;
using Waher.Content.Xml;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Runtime.Inventory;
using Waher.Script;

namespace TAG.Identity.TravelDocuments.Test.PersonalNumbers
{
	/// <summary>
	/// Personal Number Schemes available in different countries.
	/// </summary>
	public static class PersonalNumberSchemes
	{
		private static readonly Dictionary<string, LinkedList<PersonalNumberScheme>> schemesByCode = 
			new(StringComparer.InvariantCultureIgnoreCase);

		internal static void Load()
		{
			try
			{
				lock (schemesByCode)
				{
					string FileName = "PersonalNumberSchemes.xml";

					if (!string.IsNullOrEmpty(Gateway.AppDataFolder))
						FileName = Path.Combine(Gateway.AppDataFolder, FileName);

					XmlDocument Doc = new();
					Doc.Load(FileName);

					foreach (XmlNode N in Doc.DocumentElement!.ChildNodes)
					{
						if (N is XmlElement E && E.LocalName == "Entry")
						{
							string Country = XML.Attribute(E, "country");
							string? Variable = null;
							string? LaxVariable = null;
							Expression? Pattern = null;
							Expression? LaxPattern = null;
							Expression? Check = null;
							Expression? Normalize = null;

							try
							{
								foreach (XmlNode N2 in E.ChildNodes)
								{
									if (N2 is XmlElement E2)
									{
										switch (E2.LocalName)
										{
											case "Pattern":
												Pattern = new Expression(E2.InnerText, FileName);
												Variable = XML.Attribute(E2, "variable");
												break;

											case "LaxPattern":
												LaxPattern = new Expression(E2.InnerText, FileName);
												LaxVariable = XML.Attribute(E2, "variable");
												break;

											case "Check":
												Check = new Expression(E2.InnerText, FileName);
												break;

											case "Normalize":
												Normalize = new Expression(E2.InnerText, FileName);
												break;
										}
									}
								}
							}
							catch (Exception ex)
							{
								Log.Exception(ex);
								continue;
							}

							if (Pattern is null || string.IsNullOrEmpty(Variable))
								continue;

							if (!schemesByCode.TryGetValue(Country, out LinkedList<PersonalNumberScheme>? Schemes))
							{
								Schemes = new LinkedList<PersonalNumberScheme>();
								schemesByCode[Country] = Schemes;
							}

							Schemes.AddLast(new PersonalNumberScheme(Variable, Pattern, Check, LaxVariable, LaxPattern, Normalize));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
		}

		/// <summary>
		/// If the validator supports personal numbers from a given country.
		/// </summary>
		/// <param name="CountryCode">ISO 3166-1 Country Codes.</param>
		/// <returns>How well personal numbers from this country code are supported.</returns>
		public static Grade Supports(string CountryCode)
		{
			if (schemesByCode.Count == 0)
				Load();

			if (schemesByCode.ContainsKey(CountryCode))
				return Grade.Ok;
			else
				return Grade.NotAtAll;
		}

		/// <summary>
		/// Checks if a personal number is valid, in accordance with registered personal number schemes.
		/// </summary>
		/// <param name="CountryCode">ISO 3166-1 Country Codes.</param>
		/// <param name="PersonalNumber">Personal Number</param>
		/// <returns>
		/// true = valid
		/// false = invalid
		/// null = no registered schemes for country.
		/// </returns>
		public static async Task<bool?> IsValid(string CountryCode, string PersonalNumber)
		{
			if (schemesByCode.Count == 0)
				Load();

			if (schemesByCode.TryGetValue(CountryCode, out LinkedList<PersonalNumberScheme>? Schemes))
			{
				foreach (PersonalNumberScheme Scheme in Schemes)
				{
					bool? Valid = await Scheme.IsValid(PersonalNumber);
					if (Valid.HasValue)
						return Valid;
				}

				return false;
			}
			else
				return null;
		}

		/// <summary>
		/// Normmalizes a personal number entered in lax mode.
		/// </summary>
		/// <param name="CountryCode">ISO 3166-1 Country Codes.</param>
		/// <param name="PersonalNumber">Personal number.</param>
		/// <returns>Normalized personal number.</returns>
		public static async Task<string> Normalize(string CountryCode, string PersonalNumber)
		{
			if (schemesByCode.Count == 0)
				Load();

			if (schemesByCode.TryGetValue(CountryCode, out LinkedList<PersonalNumberScheme>? Schemes))
			{
				foreach (PersonalNumberScheme Scheme in Schemes)
				{
					string? s = await Scheme.Normalize(PersonalNumber);
					if (!string.IsNullOrEmpty(s))
						return s;
				}

				return PersonalNumber;
			}
			else
				return PersonalNumber;
		}
	}
}
