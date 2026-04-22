using Paiwise;
using Waher.Runtime.Inventory;

namespace TAG.Identity.TravelDocuments.Test.PersonalNumbers
{
	/// <summary>
	/// Personal Number Schemes available in different countries.
	/// </summary>
	public class PersonalNumberValidator : IPersonalNumberValidator
	{
		/// <summary>
		/// If the validator supports personal numbers from a given country.
		/// </summary>
		/// <param name="CountryCode">ISO 3166-1 Country Codes.</param>
		/// <returns>How well personal numbers from this country code are supported.</returns>
		public Grade Supports(string CountryCode)
		{
			return PersonalNumberSchemes.Supports(CountryCode);
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
		public Task<bool?> IsValid(string CountryCode, string PersonalNumber)
		{
			return PersonalNumberSchemes.IsValid(CountryCode, PersonalNumber);
		}

		/// <summary>
		/// Normmalizes a personal number entered in lax mode.
		/// </summary>
		/// <param name="CountryCode">ISO 3166-1 Country Codes.</param>
		/// <param name="PersonalNumber">Personal number.</param>
		/// <returns>Normalized personal number.</returns>
		public Task<string> Normalize(string CountryCode, string PersonalNumber)
		{
			return PersonalNumberSchemes.Normalize(CountryCode, PersonalNumber);
		}
	}
}
