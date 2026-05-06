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