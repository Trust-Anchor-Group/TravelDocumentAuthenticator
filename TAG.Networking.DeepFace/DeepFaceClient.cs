using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using Waher.Content;
using Waher.Content.Getters;
using Waher.Content.Images;
using Waher.Content.Images.Exif;
using Waher.Content.Json;
using Waher.Networking;
using Waher.Networking.Sniffers;

namespace TAG.Networking.DeepFace
{
	/// <summary>
	/// DeepFace client.
	/// </summary>
	public class DeepFaceClient : CommunicationLayer, IDisposable
	{
		private const int MaxResolution = 640;

		private static readonly string[] modelNames =
		[
			"VGG-Face",
			"Facenet",
			"Facenet512",
			"OpenFace",
			"DeepFace",
			"DeepID",
			"ArcFace",
			"Dlib",
			"SFace",
			"GhostFaceNet",
			"Buffalo_L"
		];
		private static readonly string[] detectorBackends =
		[
			"opencv",
			"ssd",
			"dlib",
			"mtcnn",
			"fastmtcnn",
			"retinaface",
			"mediapipe",
			"yolov8n",
			"yolov8m",
			"yolov8l",
			"yolov11n",
			"yolov11s",
			"yolov11m",
			"yolov11l",
			"yolov12n",
			"yolov12s",
			"yolov12m",
			"yolov12l",
			"yunet",
			"centerface"
		];
		private static readonly string[] distanceMetrics =
		[
			"cosine",
			"euclidean",
			"euclidean_l2",
			"angular"
		];
		private readonly Uri endpoint;
		private readonly Uri endpointRepresent;
		private readonly Uri endpointVerify;
		private bool antiSpoofing;

		/// <summary>
		/// DeepFace client.
		/// </summary>
		/// <param name="Endpoint">Endpoint URI of the DeepFace service.</param>
		/// <param name="AntiSpoofing">If anti-spoofing should be enabled.</param>
		public DeepFaceClient(string Endpoint, bool AntiSpoofing, params ISniffer[] Sniffers)
			: this(new Uri(EnsureTrailingSlash(Endpoint), UriKind.Absolute), AntiSpoofing, Sniffers)
		{
		}

		/// <summary>
		/// DeepFace client.
		/// </summary>
		/// <param name="Endpoint">Endpoint URI of the DeepFace service.</param>
		/// <param name="AntiSpoofing">If anti-spoofing should be enabled.</param>
		public DeepFaceClient(Uri Endpoint, bool AntiSpoofing, params ISniffer[] Sniffers)
			: base(true, Sniffers)
		{
			if (Endpoint.OriginalString.EndsWith('/'))
				this.endpoint = Endpoint;
			else
				this.endpoint = new Uri(Endpoint.OriginalString + "/", UriKind.Absolute);

			this.endpointRepresent = new Uri(this.endpoint, "represent");
			this.endpointVerify = new Uri(this.endpoint, "verify");
			this.antiSpoofing = AntiSpoofing;
		}

		private static string EnsureTrailingSlash(string s)
		{
			if (!s.EndsWith('/'))
				return s + "/";
			else
				return s;
		}

		/// <summary>
		/// If anti-spoofing should be enabled or not.
		/// </summary>
		public bool AntiSpoofing
		{
			get => this.antiSpoofing;
			set => this.antiSpoofing = value;
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(SKImage Image)
		{
			return this.Represent(FaceRecognitionModel.Facenet512, Image);
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(FaceRecognitionModel Model,
			SKImage Image)
		{
			return this.Represent(Model, DetectorBackend.RetinaFace, Image);
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, SKImage Image)
		{
			SKImage Image2 = CheckScaleAndRotation(Image, 0);
			bool DisposeImage2 = Image2 != Image;

			try
			{
				using SKData Data = Image2.Encode(SKEncodedImageFormat.Png, 0);
				return this.RepresentScaled(Model, DetectorBackend, Data.ToArray(),
					ImageCodec.ContentTypePng);
			}
			finally
			{
				if (DisposeImage2)
					Image2.Dispose();
			}
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(byte[] Image,
			string ImageContentType)
		{
			return this.Represent(FaceRecognitionModel.Facenet512, Image, ImageContentType);
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(FaceRecognitionModel Model,
			byte[] Image, string ImageContentType)
		{
			return this.Represent(Model, DetectorBackend.RetinaFace, Image, ImageContentType);
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<FaceRepresentation[]> Represent(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, byte[] Image, string ImageContentType)
		{
			Image = CheckScaleAndRotation(Image, ref ImageContentType);
			return this.RepresentScaled(Model, DetectorBackend, Image, ImageContentType);
		}

		private static SKImage CheckScaleAndRotation(SKImage Image, int Rotation)
		{
			// TODO: Is downscaling necessary?

			return Image;

			if (Image.Width <= MaxResolution && Image.Height <= MaxResolution && Rotation == 0)
				return Image;

			double s1 = ((double)MaxResolution) / Image.Width;
			double s2 = ((double)MaxResolution) / Image.Height;
			double s = Math.Min(1, Math.Min(s1, s2));
			int ScaledWidth = (int)(Image.Width * s + 0.5);
			int ScaledHeight = (int)(Image.Height * s + 0.5);
			int ResultWidth;
			int ResultHeight;

			// SkiaSharp now handles rotation properly. (But DeepFace does not, so we still need to generate a new image.)
			//
			//if (Rotation == 90 || Rotation == -90)
			//{
			//	ResultWidth = ScaledHeight;
			//	ResultHeight = ScaledWidth;
			//}
			//else
			//{
			//	ResultWidth = ScaledWidth;
			//	ResultHeight = ScaledHeight;
			//}

			ResultWidth = ScaledWidth;
			ResultHeight = ScaledHeight;

			SKSamplingOptions Options = new(SKFilterMode.Linear,
				SKMipmapMode.Linear);

			using SKSurface Surface = SKSurface.Create(new SKImageInfo(ResultWidth, ResultHeight, 
				SKImageInfo.PlatformColorType, SKAlphaType.Premul));
			SKCanvas Canvas = Surface.Canvas;

			//Canvas.Translate(ScaledWidth >> 1, ScaledHeight >> 1);
			//Canvas.RotateDegrees(Rotation);
			//Canvas.Translate(-(ScaledWidth >> 1), -(ScaledHeight >> 1));

			Canvas.DrawImage(Image, new SKRect(0, 0, ScaledWidth, ScaledHeight), Options);

			// TODO: Remove

			SKImage Image1 = Surface.Snapshot();
			using SKData Data1 = Image1.Encode(SKEncodedImageFormat.Png, 0);
			File.WriteAllBytes("c:\\Temp\\1.png", Data1.ToArray());

			return Surface.Snapshot();
		}

		private static byte[] CheckScaleAndRotation(byte[] EncodedImage, ref string ContentType)
		{
			int Rotation;

			if (EXIF.TryExtractFromJPeg(EncodedImage, out ExifTag[] Tags))
				Rotation = GetImageRotation(Tags);
			else
				Rotation = 0;

			using SKImage Image = SKImage.FromEncodedData(EncodedImage);
			SKImage Image2 = CheckScaleAndRotation(Image, Rotation);

			if (Image == Image2)
				return EncodedImage;

			try
			{
				using SKData Data = Image2.Encode(SKEncodedImageFormat.Png, 0);
				ContentType = ImageCodec.ContentTypePng;

				return Data.ToArray();
			}
			finally
			{
				Image2.Dispose();
			}
		}

		/// <summary>
		/// Gets the rotation angle to use, to display the image correctly in Xamarin Forms.
		/// </summary>
		/// <param name="JpegImage">Binary representation of JPEG image.</param>
		/// <returns>Rotation angle (degrees).</returns>
		public static int GetImageRotation(byte[] JpegImage)
		{
			if (JpegImage is null)
				return 0;

			if (!EXIF.TryExtractFromJPeg(JpegImage, out ExifTag[] Tags))
				return 0;

			return GetImageRotation(Tags);
		}

		/// <summary>
		/// Gets the rotation angle to use, to display the image correctly in Xamarin Forms.
		/// </summary>
		/// <param name="Tags">EXIF Tags encoded in image.</param>
		/// <returns>Rotation angle (degrees).</returns>
		public static int GetImageRotation(ExifTag[] Tags)
		{
			foreach (ExifTag Tag in Tags)
			{
				if (Tag.Name == ExifTagName.Orientation)
				{
					if (Tag.Value is ushort Orientation)
					{
						return Orientation switch
						{
							1 => 0,// Top left. Default orientation.
							2 => 0,// Top right. Horizontally reversed.
							3 => 180,// Bottom right. Rotated by 180 degrees.
							4 => 180,// Bottom left. Rotated by 180 degrees and then horizontally reversed.
							5 => -90,// Left top. Rotated by 90 degrees counterclockwise and then horizontally reversed.
							6 => 90,// Right top. Rotated by 90 degrees clockwise.
							7 => 90,// Right bottom. Rotated by 90 degrees clockwise and then horizontally reversed.
							8 => -90,// Left bottom. Rotated by 90 degrees counterclockwise.
							_ => 0,
						};
					}
				}
			}

			return 0;
		}

		/// <summary>
		/// Generates representations of faces from the given image.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		private async Task<FaceRepresentation[]> RepresentScaled(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, byte[] Image, string ImageContentType)
		{
			Dictionary<string, object> Request = new()
			{
				{ "model_name", modelNames[(int)Model] },
				{ "detector_backend", detectorBackends[(int)DetectorBackend] },
				{ "img", "data:" + ImageContentType+ ";base64," + Convert.ToBase64String(Image) }
			};

			if (this.antiSpoofing)
				Request["anti_spoofing"] = true;

			object Response = await this.Request(this.endpointRepresent, Request);

			if (Response is not Dictionary<string, object> ResponseObj ||
				!ResponseObj.TryGetValue("results", out object? Obj) ||
				Obj is not Array Results)
			{
				throw new Exception("Unexpected response format.");
			}

			int i, c = Results.Length;
			FaceRepresentation[] Result = new FaceRepresentation[c];

			for (i = 0; i < c; i++)
			{
				if (Results.GetValue(i) is not Dictionary<string, object> RepresentationObj ||
					!RepresentationObj.TryGetValue("embedding", out Obj) ||
					Obj is not Array Embedding ||
					!RepresentationObj.TryGetValue("face_confidence", out Obj) ||
					!IsDouble(Obj, out double FaceConfidence) ||
					!RepresentationObj.TryGetValue("facial_area", out Obj) ||
					Obj is not Dictionary<string, object> FacialArea ||
					!FacialArea.TryGetValue("h", out Obj) || Obj is not int Height ||
					!FacialArea.TryGetValue("w", out Obj) || Obj is not int Width ||
					!FacialArea.TryGetValue("x", out Obj) || Obj is not int X ||
					!FacialArea.TryGetValue("y", out Obj) || Obj is not int Y)
				{
					throw new IOException("Unexpected representation returned.");
				}

				int j, d = Embedding.Length;
				double[] EmbeddingValues = new double[d];

				for (j = 0; j < d; j++)
				{
					if (IsDouble(Embedding.GetValue(j), out double d2))
						EmbeddingValues[j] = d2;
					else
						throw new IOException("Unexpected value in embedding.");
				}

				if (!FacialArea.TryGetValue("left_eye", out Obj) ||
					!TryParsePoint(Obj, out SKPoint? LeftEye))
				{
					LeftEye = null;
				}

				if (!FacialArea.TryGetValue("right_eye", out Obj) ||
					!TryParsePoint(Obj, out SKPoint? RightEye))
				{
					RightEye = null;
				}

				if (!FacialArea.TryGetValue("mouth_left", out Obj) ||
					!TryParsePoint(Obj, out SKPoint? MouthLeft))
				{
					MouthLeft = null;
				}

				if (!FacialArea.TryGetValue("mouth_right", out Obj) ||
					!TryParsePoint(Obj, out SKPoint? MouthRight))
				{
					MouthRight = null;
				}

				if (!FacialArea.TryGetValue("nose", out Obj) ||
					!TryParsePoint(Obj, out SKPoint? Nose))
				{
					Nose = null;
				}

				Result[i] = new FaceRepresentation(FaceConfidence, EmbeddingValues,
					new SKRect(X, Y, X + Width, Y + Height),
					LeftEye, RightEye, MouthLeft, MouthRight, Nose);
			}

			return Result;
		}

		/// <summary>
		/// Checks if the given object is a double, or an int that can be safely converted to a double.
		/// </summary>
		/// <param name="Obj">The object to check.</param>
		/// <param name="Value">The resulting double value if the object is a double or an int.</param>
		/// <returns>True if the object is a double or an int, otherwise false.</returns>
		public static bool IsDouble(object? Obj, out double Value)
		{
			if (Obj is double d)
			{
				Value = d;
				return true;
			}
			else if (Obj is int i)
			{
				Value = i;
				return true;
			}
			else
			{
				Value = 0;
				return false;
			}
		}

		public async Task<object> Request(Uri Endpoint, object Payload)
		{
			if (this.HasSniffers)
			{
				this.TransmitText("POST(" + Endpoint.ToString() + "):\r\n" +
					JSON.Encode(Payload, true));
			}

			ContentResponse Response = await InternetContent.PostAsync(Endpoint, Payload,
				new KeyValuePair<string, string>("Accept", JsonCodec.DefaultContentType));

			if (Response.HasError)
			{
				if (Response.Error is WebException ex &&
					ex.Content is Dictionary<string, object> Error &&
					Error.TryGetValue("error", out object? Obj) &&
					Obj is string ErrorMessage)
				{
					int i = ErrorMessage.IndexOf(" - Traceback");
					if (i > 0)
						ErrorMessage = ErrorMessage[..i].TrimEnd();

					if (ErrorMessage.StartsWith("Exception while representing: "))
						ErrorMessage = ErrorMessage[30..];

					this.Error(ErrorMessage);
					throw new Exception(ErrorMessage);
				}
				else
				{
					if (this.HasSniffers)
						this.Error(Response.Error.Message);

					Response.AssertOk();
				}
			}
			else if (this.HasSniffers)
				this.ReceiveText(JSON.Encode(Response.Decoded, true));

			return Response.Decoded;
		}

		private static bool TryParsePoint(object Obj, [NotNullWhen(true)] out SKPoint? Point)
		{
			if (Obj is Array A &&
				A.Length == 2 &&
				A.GetValue(0) is int X &&
				A.GetValue(1) is int Y)
			{
				Point = new SKPoint(X, Y);
				return true;
			}
			else
			{
				Point = null;
				return false;
			}
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(SKImage Image1, SKImage Image2)
		{
			return this.Verify(FaceRecognitionModel.Facenet512, Image1, Image2);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			SKImage Image1, SKImage Image2)
		{
			return this.Verify(Model, DetectorBackend.RetinaFace, Image1, Image2);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, SKImage Image1, SKImage Image2)
		{
			using SKData Data1 = Image1.Encode(SKEncodedImageFormat.Png, 0);
			using SKData Data2 = Image2.Encode(SKEncodedImageFormat.Png, 0);
			return this.Verify(Model, DetectorBackend,
				Data1.ToArray(), ImageCodec.ContentTypePng,
				Data2.ToArray(), ImageCodec.ContentTypePng);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			return this.Verify(FaceRecognitionModel.Facenet512,
				Image1, Image1ContentType, Image2, Image2ContentType);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			return this.Verify(Model, DetectorBackend.RetinaFace,
				Image1, Image1ContentType, Image2, Image2ContentType);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			return this.Verify(Model, DetectorBackend, DistanceMetric.EuclideanL2,
				Image1, Image1ContentType, Image2, Image2ContentType);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(DistanceMetric DistanceMetric,
			SKImage Image1, SKImage Image2)
		{
			return this.Verify(FaceRecognitionModel.Facenet512, DistanceMetric,
				Image1, Image2);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DistanceMetric DistanceMetric, SKImage Image1, SKImage Image2)
		{
			return this.Verify(Model, DetectorBackend.RetinaFace, DistanceMetric,
				Image1, Image2);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, DistanceMetric DistanceMetric,
			SKImage Image1, SKImage Image2)
		{
			using SKData Data1 = Image1.Encode(SKEncodedImageFormat.Png, 0);
			using SKData Data2 = Image2.Encode(SKEncodedImageFormat.Png, 0);
			return this.Verify(Model, DetectorBackend, DistanceMetric,
				Data1.ToArray(), ImageCodec.ContentTypePng,
				Data2.ToArray(), ImageCodec.ContentTypePng);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(DistanceMetric DistanceMetric,
			byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			return this.Verify(FaceRecognitionModel.Facenet512, DistanceMetric,
				Image1, Image1ContentType, Image2, Image2ContentType);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DistanceMetric DistanceMetric, byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			return this.Verify(Model, DetectorBackend.RetinaFace, DistanceMetric,
				Image1, Image1ContentType, Image2, Image2ContentType);
		}

		/// <summary>
		/// Verifies the likeness of two faces in different images.
		/// </summary>
		/// <param name="Model">Face recognition model</param>
		/// <param name="DetectorBackend">Detector backend</param>
		/// <param name="DistanceMetric">The distance metric to use for verification.</param>
		/// <param name="Image">The image containing the faces.</param>
		/// <param name="ImageContentType">Image Content-Type</param>
		/// <returns>An array of face representations.</returns>
		public async Task<VerificationResult> Verify(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, DistanceMetric DistanceMetric,
			byte[] Image1, string Image1ContentType,
			byte[] Image2, string Image2ContentType)
		{
			Dictionary<string, object> Request = new()
			{
				{ "model_name", modelNames[(int)Model] },
				{ "detector_backend", detectorBackends[(int)DetectorBackend] },
				{ "distance_metric", distanceMetrics[(int)DistanceMetric] },
				{ "img1", "data:" + Image1ContentType+ ";base64," + Convert.ToBase64String(Image1) },
				{ "img2", "data:" + Image2ContentType+ ";base64," + Convert.ToBase64String(Image2) }
			};

			if (this.antiSpoofing)
				Request["anti_spoofing"] = true;

			object Response = await this.Request(this.endpointVerify, Request);

			if (Response is not Dictionary<string, object> ResponseObj ||
				!ResponseObj.TryGetValue("confidence", out object? Obj) ||
				!IsDouble(Obj, out double Confidence) ||
				!ResponseObj.TryGetValue("distance", out Obj) ||
				!IsDouble(Obj, out double Distance) ||
				!ResponseObj.TryGetValue("threshold", out Obj) ||
				!IsDouble(Obj, out double Threshold) ||
				!ResponseObj.TryGetValue("verified", out Obj) ||
				Obj is not bool Verified)
			{
				throw new Exception("Unexpected response format.");
			}

			return new VerificationResult(Confidence, Distance, Threshold, Verified);
		}

	}
}
