async function CompareFaces()
{
	var Image1 = document.getElementById("Image1");
	var Image2 = document.getElementById("Image2");

	var Output = document.getElementById("Output");
	Output.setAttribute("style", "display:block");

	var OutputContent = document.getElementById("OutputContent");
	OutputContent.innerHTML = "";

	await CallServer("CompareFaces.ws",
		{
			"Image1": await FileToBase64(Image1.files[0]),
			"Image2": await FileToBase64(Image2.files[0]),
			"TabID": TabID
		});
}

async function FileToBase64(File)
{
	var Buffer = await File.arrayBuffer();
	var Bytes = new Uint8Array(Buffer);
	var Binary = "";
	var i;
	var c = Bytes.length;

	for (i = 0; i < c; i++)
		Binary += String.fromCharCode(Bytes[i]);

	return btoa(Binary);
}

function ShowStep(Data)
{
	var OutputContent = document.getElementById("OutputContent");

	if (Data.isText)
	{
		var P = document.createElement("P");
		P.innerText = Data.text;

		OutputContent.appendChild(P);
	}
	else
	{
		var Pre = document.createElement("PRE");
		var Code = document.createElement("CODE");

		Code.innerText = Data.text;

		Pre.appendChild(Code);
		OutputContent.appendChild(Pre);
	}
}