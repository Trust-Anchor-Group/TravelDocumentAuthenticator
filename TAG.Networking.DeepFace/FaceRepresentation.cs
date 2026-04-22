using SkiaSharp;

namespace TAG.Networking.DeepFace
{
	/// <summary>
	/// Contains a representation of a face, as returned by the DeepFace service.
	/// </summary>
	/// <param name="FaceConfidence">Confidence level of the face detection.</param>
	/// <param name="Embedding">Embedding vector</param>
	/// <param name="FaceArea">Face area</param>
	/// <param name="LeftEye">Point of left eye.</param>
	/// <param name="RightEye">Point of right eye.</param>
	/// <param name="MouthLeft">Point of left part of mouth.</param>
	/// <param name="MouthRight">Point of right part of mouth.</param>
	/// <param name="Nose">Point of nose.</param>
	public class FaceRepresentation(double FaceConfidence, double[] Embedding, SKRect FaceArea,
		SKPoint? LeftEye, SKPoint? RightEye, SKPoint? MouthLeft, SKPoint? MouthRight,
		SKPoint? Nose)
	{
		/// <summary>
		/// Confidence level of the face detection.
		/// </summary>
		public double FaceConfidence { get; } = FaceConfidence;

		/// <summary>
		/// Embedding vector
		/// </summary>
		public double[] Embedding { get; } = Embedding;

		/// <summary>
		/// Height of the detected face.
		/// </summary>
		public SKRect FaceArea { get; } = FaceArea;

		/// <summary>
		/// Point of left eye.
		/// </summary>
		public SKPoint? LeftEye { get; } = LeftEye;

		/// <summary>
		/// Point of right eye.
		/// </summary>
		public SKPoint? RightEye { get; } = RightEye;

		/// <summary>
		/// Point of left part of mouth.
		/// </summary>
		public SKPoint? MouthLeft { get; } = MouthLeft;

		/// <summary>
		/// Point of right part of mouth.
		/// </summary>
		public SKPoint? MouthRight { get; } = MouthRight;

		/// <summary>
		/// Point of nose.
		/// </summary>
		public SKPoint? Nose { get; } = Nose;
	}
}
