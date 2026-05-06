using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using Waher.Content;
using Waher.Content.Images;
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

		/// <summary>
		/// DeepFace client.
		/// </summary>
		/// <param name="Endpoint">Endpoint URI of the DeepFace service.</param>
		public DeepFaceClient(string Endpoint, params ISniffer[] Sniffers)
			: this(new Uri(Endpoint, UriKind.Absolute), Sniffers)
		{
		}

		/// <summary>
		/// DeepFace client.
		/// </summary>
		/// <param name="Endpoint">Endpoint URI of the DeepFace service.</param>
		public DeepFaceClient(Uri Endpoint, params ISniffer[] Sniffers)
			: base(true, Sniffers)
		{
			this.endpoint = Endpoint;
			this.endpointRepresent = new Uri(this.endpoint, "/represent");
			this.endpointVerify = new Uri(this.endpoint, "/verify");
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
			using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 0);
			return this.Represent(Model, DetectorBackend, Data.ToArray(),
				ImageCodec.ContentTypePng);
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
		public async Task<FaceRepresentation[]> Represent(FaceRecognitionModel Model,
			DetectorBackend DetectorBackend, byte[] Image, string ImageContentType)
		{
			object Response = await this.Request(this.endpointRepresent,
				new Dictionary<string, object>()
				{
					{ "model_name", modelNames[(int)Model] },
					{ "detector_backend", detectorBackends[(int)DetectorBackend] },
					{ "img", "data:" + ImageContentType+ ";base64," + Convert.ToBase64String(Image) }
				});

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
				if (this.HasSniffers)
					this.Error(Response.Error.Message);

				Response.AssertOk();
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
			return this.Verify(Model,DetectorBackend, DistanceMetric.EuclideanL2,
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
			object Response = await this.Request(this.endpointVerify,
				new Dictionary<string, object>()
				{
					{ "model_name", modelNames[(int)Model] },
					{ "detector_backend", detectorBackends[(int)DetectorBackend] },
					{ "distance_metric", distanceMetrics[(int)DistanceMetric] },
					{ "img1", "data:" + Image1ContentType+ ";base64," + Convert.ToBase64String(Image1) },
					{ "img2", "data:" + Image2ContentType+ ";base64," + Convert.ToBase64String(Image2) }
				});

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
