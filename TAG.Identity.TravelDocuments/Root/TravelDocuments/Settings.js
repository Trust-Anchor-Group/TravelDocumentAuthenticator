function RandomizeSalt()
{
    var xhttp = new XMLHttpRequest();
    xhttp.onreadystatechange = function ()
    {
        if (xhttp.readyState === 4)
        {
            if (xhttp.status === 200)
            {
                var Input = document.getElementById("Salt");

                Input.value = xhttp.responseText;
                Input.type = "text";
            }
            else
                ShowError(xhttp);
        }
    };

    xhttp.open("POST", "/Settings/RandomizePassword", true);
    xhttp.send();
}
