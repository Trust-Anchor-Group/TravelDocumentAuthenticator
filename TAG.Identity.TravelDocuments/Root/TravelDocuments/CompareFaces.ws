AuthenticateSession(Request,"User");
Authorize(User,"Admin.Identity.TravelDocuments");

if Posted matches
{
	"Image1": Required(Str(PImage1)),
	"Image2": Required(Str(PImage2)),
	"Image1ContentType": Required(Str(PImage1ContentType)),
	"Image2ContentType": Required(Str(PImage2ContentType)),
	"TabID": Required(Str(PTabID))
} then 
(
	try
	(
		PushEvent([PTabID], "ShowStep",
		{
			"isText": true,
			"text": "Face comparison started."
		});

		DeepFaceUrl := GetSetting("TAG.Identity.TravelDocuments.DeepFaceUrl", "http://localhost:5000/");
		AntiSpoofing := GetSetting("TAG.Identity.TravelDocuments.AntiSpoofing", true);
		MaxDistance := GetSetting("TAG.Identity.TravelDocuments.MaxDistance", 1.04);
		MinDistance := GetSetting("TAG.Identity.TravelDocuments.MinDistance", 0.15);
		ManualDistance := GetSetting("TAG.Identity.TravelDocuments.ManualDistance", 0.40);

		PushEvent([PTabID], "ShowStep",
		{
			"isText": true,
			"text": "Parameters:\r\n"+
				"DeepFace URL: " + DeepFaceUrl+"\r\n"+
				"Anti Spoofing: " + Str(AntiSpoofing)+"\r\n"+
				"Maximum Distance: " + Str(MaxDistance)+"\r\n"+
				"Minimum Distance: " + Str(MinDistance)+"\r\n"+
				"Manual Distance: " + Str(ManualDistance)
		});

		Client:=Create(DeepFaceClient,DeepFaceUrl, AntiSpoofing, TAG.Identity.TravelDocuments.ServiceModule.GetSniffers());
		try
		(
			ImageBin1:=Base64Decode(PImage1);
			ImageBin2:=Base64Decode(PImage2);

			PushEvent([PTabID], "ShowStep",
			{
				"isText": true,
				"text": "Processing Image 1."
			});

			Representations1:=Client.Represent(ImageBin1, PImage1ContentType);

			PushEvent([PTabID], "ShowStep",
			{
				"isText": true,
				"text": "Faces found in Image 1: "+Str(Representations1.Length)
			});

			if Representations1.Length=1 then
			(
				PushEvent([PTabID], "ShowStep",
				{
					"isText": true,
					"text": "Confidence of face in Image 1: "+Str(Representations1[0].FaceConfidence)
				});

				if Representations1[0].FaceConfidence>=0.95 then
				(
					PushEvent([PTabID], "ShowStep",
					{
						"isText": true,
						"text": "Processing Image 2."
					});

					Representations2:=Client.Represent(ImageBin2, PImage2ContentType);

					PushEvent([PTabID], "ShowStep",
					{
						"isText": true,
						"text": "Faces found in Image 2: "+Str(Representations2.Length)
					});

					if Representations2.Length=1 then
					(
						PushEvent([PTabID], "ShowStep",
						{
							"isText": true,
							"text": "Confidence of face in Image 2: "+Str(Representations2[0].FaceConfidence)
						});
					)
				)
			)
		)
		finally
		(
			Client.Dispose();
		)
	)
	catch
	(
		PushEvent([PTabID], "ShowStep",
		{
			"isText": true,
			"text": "Error: "+Exception.Message
		});
	)
)
else
	BadRequest("Invalid posted data.");

""