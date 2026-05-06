Title: Travel Documents Authenticator settings
Description: Contains a page where the operator of the Neuron can configure the travel documents authenticator service.
Date: 2026-04-20
Author: Peter Waher
Master: /Master.md
Cache-Control: max-age=0, no-cache, no-store
JavaScript: /Sniffers/Sniffer.js
UserVariable: User
Privilege: Admin.Identity.TravelDocuments
Login: /Login.md

========================================================================

<form action="Settings.md" method="post">
<fieldset>
<legend>Travel Documents Authenticator settings</legend>

{{
if exists(Posted) then
(
	SetSetting("TAG.Identity.TravelDocuments.DeepFaceUrl",Posted.Endpoint);
	SetSetting("TAG.Identity.TravelDocuments.AntiSpoofing",Boolean(Posted.AntiSpoofing) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.MaxDistance",Number(Posted.MaxDistance));
	SetSetting("TAG.Identity.TravelDocuments.MinDistance",Number(Posted.MinDistance));
	SetSetting("TAG.Identity.TravelDocuments.ManualDistance",Number(Posted.ManualDistance));

	SeeOther("Settings.md")
);
""
}}

<p>
<label for="Endpoint">DeepFace Endpoint URL:</label>  
<input type="url" id="Endpoint" name="Endpoint" value='{{GetSetting("TAG.Identity.TravelDocuments.DeepFaceUrl","http://localhost:5000/")}}' autofocus required title="DeepFace Endpoint URL"/>
</p>

<p>
<input type="checkbox" id="AntiSpoofing" name="AntiSpoofing" title="Enables anti-spoofing protection, if checked." {{GetSetting("TAG.Identity.TravelDocuments.AntiSpoofing",true) ? "checked" : ""}}/>
<label for="AntiSpoofing">Anti-spoofing (requires Torch to be installed with DeepFace).</label>
</p>

<p>
<label for="MaxDistance">Maximum Distance:</label>  
<input type="number" min="0" id="MaxDistance" name="MaxDistance" value='{{GetSetting("TAG.Identity.TravelDocuments.MaxDistance",1.04)}}' required title="Maximum distance between two Facenet512 vectors (Euclidean L2 Norm) to be considered representations of the same face."/>
</p>

<p>
<label for="MinDistance">Minimum Distance:</label>  
<input type="number" min="0" id="MinDistance" name="MinDistance" value='{{GetSetting("TAG.Identity.TravelDocuments.MinDistance",0.15)}}' required title="Minimum distance between two Facenet512 vectors (Euclidean L2 Norm) to be considered representations from the same photo source."/>
</p>

<p>
<label for="ManualDistance">Manual Distance:</label>  
<input type="number" min="0" id="ManualDistance" name="ManualDistance" value='{{GetSetting("TAG.Identity.TravelDocuments.ManualDistance",0.40)}}' required title="Distance between two Facenet512 vectors (Euclidean L2 Norm) below which an application has to be reviewed manually."/>
</p>

<button type="submit" class="posButton">Apply</button>
</fieldset>

<fieldset>
<legend>Tools</legend>
<button type="button" class="posButton"{{
if User.HasPrivilege("Admin.Communication.DeepFace") and User.HasPrivilege("Admin.Communication.Sniffer") then
	" onclick=\"OpenSniffer('Sniffer.md')\""
else
	" disabled"
}}>Sniffer</button>
</fieldset>
</form>