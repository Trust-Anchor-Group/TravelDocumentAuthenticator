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
		private readonly Uri endpoint;
		private readonly Uri endpointRepresent;

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
			Dictionary<string, object> Request = new()
			{
				{ "model_name", modelNames[(int)Model] },
				{ "detector_backend", detectorBackends[(int)DetectorBackend] },
				{ "img", "data:" + ImageContentType+ ";base64," + Convert.ToBase64String(Image) }
			};

			if (this.HasSniffers)
			{
				this.TransmitText("POST(" + this.endpointRepresent.ToString() + "):\r\n" +
					JSON.Encode(Request, true));
			}

			ContentResponse Response = await InternetContent.PostAsync(
				this.endpointRepresent, Request,
				new KeyValuePair<string, string>("Accept", JsonCodec.DefaultContentType));

			if (Response.HasError)
			{
				if (this.HasSniffers)
					this.Error(Response.Error.Message);

				Response.AssertOk();
			}
			else if (this.HasSniffers)
				this.ReceiveText(JSON.Encode(Response.Decoded, true));

			if (Response.Decoded is not Dictionary<string, object> ResponseObj ||
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
					Obj is not double FaceConfidence ||
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
					if (Embedding.GetValue(j) is double d2)
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
	}
}
