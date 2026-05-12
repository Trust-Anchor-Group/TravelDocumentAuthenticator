AuthenticateSession(Request,"User");
Authorize(User,"Admin.Identity.TravelDocuments");

if Posted matches
{
	"Image": Required(Str(PImage)),
	"ImageContentType": Required(Str(PImageContentType)),
	"TabID": Required(Str(PTabID))
} then 
(
	try
	(
		PushEvent([PTabID], "ShowStep",
		{
			"isText": true,
			"text": "Face representation started."
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
			ImageBin:=Base64Decode(PImage);

			PushEvent([PTabID], "ShowStep",
			{
				"isText": true,
				"text": "Processing Image."
			});

			Representations:=Client.Represent(ImageBin, PImageContentType);

			PushEvent([PTabID], "ShowStep",
			{
				"isText": true,
				"text": "Faces found in Image: "+Str(Representations.Length)
			});

			if Representations.Length=1 then
			(
				PushEvent([PTabID], "ShowStep",
				{
					"isText": true,
					"text": "Confidence of face in Image: "+Str(Representations[0].FaceConfidence)
				});

				if Representations[0].FaceConfidence>=0.95 then
				(
					PushEvent([PTabID], "ShowStep",
					{
						"isText": false,
						"text": JSON.Encode(Representations[0], true)
					});
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