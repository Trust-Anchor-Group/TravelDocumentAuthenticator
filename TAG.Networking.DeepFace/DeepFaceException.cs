namespace TAG.Networking.DeepFace
{
	/// <summary>
	/// Exception resulting from an error returned by DeepFace.
	/// </summary>
	public class DeepFaceException : Exception
	{
		/// <summary>
		/// Exception resulting from an error returned by DeepFace.
		/// </summary>
		/// <param name="Message">Error message</param>
		public DeepFaceException(string Message)
			: base(Message)
		{
		}
	}
}
