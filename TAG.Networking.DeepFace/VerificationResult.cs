namespace TAG.Networking.DeepFace
{
	/// <summary>
	/// Contains the result of a face verification.
	/// </summary>
	/// <param name="Confidence">Confidence level of the verification.</param>
	/// <param name="Distance">Distance between the embeddings.</param>
	/// <param name="Threshold">Threshold for verification.</param>
	/// <param name="Verified">Indicates whether the face is verified.</param>
	public class VerificationResult(double Confidence, double Distance, double Threshold,
		bool Verified)
	{
		/// <summary>
		/// Confidence level of the face detection.
		/// </summary>
		public double Confidence { get; } = Confidence;

		/// <summary>
		/// Distance between the embeddings.
		/// </summary>
		public double Distance { get; } = Distance;

		/// <summary>
		/// Threshold for verification.
		/// </summary>
		public double Threshold { get; } = Threshold;

		/// <summary>
		/// Indicates whether the face is verified.
		/// </summary>
		public bool Verified { get; } = Verified;
	}
}
