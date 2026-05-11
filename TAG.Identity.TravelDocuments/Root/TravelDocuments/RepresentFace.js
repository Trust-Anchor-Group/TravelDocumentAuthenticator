async function RepresentFace()
{
	var Image = document.getElementById("Image");

	var Output = document.getElementById("Output");
	Output.setAttribute("style", "display:block");

	var OutputContent = document.getElementById("OutputContent");
	OutputContent.innerHTML = "";

	await CallServer("RepresentFace.ws",
		{
			"Image": await FileToBase64(Image.files[0]),
			"ImageContentType": Image.files[0].type,
			"TabID": TabID
		});
}
