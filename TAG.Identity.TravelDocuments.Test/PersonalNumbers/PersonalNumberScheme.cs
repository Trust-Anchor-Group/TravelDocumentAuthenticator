using Waher.Networking.HTTP;
using Waher.Script;

namespace TAG.Identity.TravelDocuments.Test.PersonalNumbers
{
	/// <summary>
	/// Checks personal numbers against a personal number scheme.
	/// </summary>
	/// <param name="VariableName">Name of variable to use in script for the personal number.</param>
	/// <param name="Pattern">Expression checking if the scheme applies to a personal number.</param>
	/// <param name="Check">Optional expression, checking if the contents of the personal number is valid.</param>
	/// <param name="LaxVariableName">Name of variable to use in script for the personal number, in lax mode.</param>
	/// <param name="LaxPattern">Expression checking if the scheme applies to a personal number, in lax mode.</param>
	/// <param name="Normalize">Expression to normalize a personal number acceptable in lax mode, as a personal number in strict mode.</param>
	public class PersonalNumberScheme(string VariableName, Expression Pattern, 
		Expression? Check, string? LaxVariableName, Expression? LaxPattern, 
		Expression? Normalize)
	{
		private readonly string variableName = VariableName;
		private readonly string? laxVariableName = LaxVariableName;
		private readonly Expression pattern = Pattern;
		private readonly Expression? laxPattern = LaxPattern;
		private readonly Expression? check = Check;
		private readonly Expression? normalize = Normalize;

		/// <summary>
		/// Checks personal numbers against a personal number scheme.
		/// </summary>
		/// <param name="VariableName">Name of variable to use in script for the personal number.</param>
		/// <param name="Pattern">Expression checking if the scheme applies to a personal number.</param>
		public PersonalNumberScheme(string VariableName, Expression Pattern)
			: this(VariableName, Pattern, null, null, null, null)
		{
		}

		/// <summary>
		/// Checks if a personal number is valid according to the personal number scheme.
		/// </summary>
		/// <param name="PersonalNumber">String representation of the personal number.</param>
		/// <returns>
		/// true = valid
		/// false = invalid
		/// null = scheme not applicable
		/// </returns>
		public async Task<bool?> IsValid(string PersonalNumber)
		{
			try
			{
				SessionVariables Variables = HttpServer.CreateSessionVariables();
				Variables[this.variableName] = PersonalNumber;

				object Result = await this.pattern.EvaluateAsync(Variables);

				if (Result is bool b)
				{
					if (!b)
						return null;

					if (this.check is not null)
					{
						Result = await this.check.EvaluateAsync(Variables);
						return Result is bool b2 && b2;
					}
					else
						return true;
				}
				else
					return null;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Normmalizes a personal number entered in lax mode.
		/// </summary>
		/// <param name="PersonalNumber">Personal number.</param>
		/// <returns>Normalized personal number.</returns>
		public async Task<string?> Normalize(string PersonalNumber)
		{
			if (this.normalize is null)
				return null;

			try
			{
				SessionVariables Variables = HttpServer.CreateSessionVariables();
				object Result;

				if (this.laxPattern is null)
				{
					Variables.Add(this.variableName, PersonalNumber);
					Result = await this.pattern.EvaluateAsync(Variables);
				}
				else
				{
					Variables.Add(this.laxVariableName ?? this.variableName, PersonalNumber);
					Result = await this.laxPattern.EvaluateAsync(Variables);
				}

				if (Result is not bool b || !b)
					return null;

				Result = await this.normalize.EvaluateAsync(Variables);

				return Result?.ToString();
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
