Title: Travel Documents Authenticator settings
Description: Contains a page where the operator of the Neuron can configure the travel documents authenticator service.
Date: 2026-04-20
Author: Peter Waher
Master: /Master.md
Cache-Control: max-age=0, no-cache, no-store
JavaScript: /Sniffers/Sniffer.js
JavaScript: /Events.js
JavaScript: Settings.js
UserVariable: User
Privilege: Admin.Identity.TravelDocuments
Login: /Login.md

========================================================================

<form action="Settings.md" method="post" enctype="multipart/form-data">
<fieldset>
<legend>DeepFace settings</legend>

{{
if exists(Posted) then
(
	SetSetting("TAG.Identity.TravelDocuments.DeepFaceUrl",Posted.Endpoint);
	SetSetting("TAG.Identity.TravelDocuments.AntiSpoofing",Boolean(Posted.AntiSpoofing) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.MaxDistance",Number(Posted.MaxDistance));
	SetSetting("TAG.Identity.TravelDocuments.MinDistance",Number(Posted.MinDistance));
	SetSetting("TAG.Identity.TravelDocuments.ManualDistance",Number(Posted.ManualDistance));

	SetSetting("TAG.Identity.TravelDocuments.EnforceUniqueness",Boolean(Posted.EnforceUniqueness) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludeDocumentNumber",Boolean(Posted.IncludeDocumentNumber) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludeCountry",Boolean(Posted.IncludeCountry) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludeBirthDate",Boolean(Posted.IncludeBirthDate) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludePrimaryIdentifier",Boolean(Posted.IncludePrimaryIdentifier) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludeSecondaryIdentifier",Boolean(Posted.IncludeSecondaryIdentifier) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.IncludeOptionalData",Boolean(Posted.IncludeOptionalData) ??? false);
	SetSetting("TAG.Identity.TravelDocuments.LifeCycleDays",Number(Posted.LifeCycleDays) ??? 3652);
	SetSetting("TAG.Identity.TravelDocuments.Salt",Str(Posted.Salt));

	SeeOther("Settings.md")
);
""
}}

<p>
<label for="Endpoint">DeepFace Endpoint URL:</label>  
<input type="url" id="Endpoint" name="Endpoint" autofocus required title="DeepFace Endpoint URL"
	value='{{GetSetting("TAG.Identity.TravelDocuments.DeepFaceUrl","http://localhost:5000/")}}'/>
</p>

<p>
<input type="checkbox" id="AntiSpoofing" name="AntiSpoofing" 
	title="Enables anti-spoofing protection, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.AntiSpoofing",true) ? "checked" : ""}}/>
<label for="AntiSpoofing">Anti-spoofing (requires Torch to be installed with DeepFace).</label>
</p>

<p>
<label for="MaxDistance">Maximum Distance:</label>  
<input type="number" min="0" id="MaxDistance" name="MaxDistance" step="any" inputmode="decimal"
	value='{{Str(GetSetting("TAG.Identity.TravelDocuments.MaxDistance",1.04))}}' required 
	title="Maximum distance between two Facenet512 vectors (Euclidean L2 Norm) to be considered representations of the same face."/>
</p>

<p>
<label for="MinDistance">Minimum Distance:</label>  
<input type="number" min="0" id="MinDistance" name="MinDistance" step="any" inputmode="decimal" 
	value='{{Str(GetSetting("TAG.Identity.TravelDocuments.MinDistance",0.15))}}' required 
	title="Minimum distance between two Facenet512 vectors (Euclidean L2 Norm) to be considered representations from the same photo source."/>
</p>

<p>
<label for="ManualDistance">Manual Distance:</label>  
<input type="number" min="0" id="ManualDistance" name="ManualDistance" step="any" inputmode="decimal"
	value='{{Str(GetSetting("TAG.Identity.TravelDocuments.ManualDistance",0.40))}}' required 
	title="Distance between two Facenet512 vectors (Euclidean L2 Norm) below which an application has to be reviewed manually."/>
</p>

</fieldset>


<fieldset>
<legend>Uniqueness enforcement</legend>

<p>
<input type="checkbox" id="EnforceUniqueness" name="EnforceUniqueness" 
	title="Ensures Travel Document cannot be used to create identities outside of acceptable ranges, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.EnforceUniqueness",false) ? "checked" : ""}}/>
<label for="EnforceUniqueness">Enforce uniqueness of applications.</label>
</p>

<p>
<input type="checkbox" id="IncludeDocumentNumber" name="IncludeDocumentNumber" 
	title="Includes document number from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludeDocumentNumber",true) ? "checked" : ""}}/>
<label for="IncludeDocumentNumber">Include document number in uniqueness verification.</label>
</p>

<p>
<input type="checkbox" id="IncludeCountry" name="IncludeCountry" 
	title="Includes the country code from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludeCountry",true) ? "checked" : ""}}/>
<label for="IncludeCountry">Include country in uniqueness verification.</label>
</p>

<p>
<input type="checkbox" id="IncludeBirthDate" name="IncludeBirthDate" 
	title="Includes the birth date from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludeBirthDate",true) ? "checked" : ""}}/>
<label for="IncludeBirthDate">Include birth date in uniqueness verification.</label>
</p>

<p>
<input type="checkbox" id="IncludePrimaryIdentifier" name="IncludePrimaryIdentifier" 
	title="Includes the primary identifier from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludePrimaryIdentifier",true) ? "checked" : ""}}/>
<label for="IncludePrimaryIdentifier">Include primary identifier in uniqueness verification.</label>
</p>

<p>
<input type="checkbox" id="IncludeSecondaryIdentifier" name="IncludeSecondaryIdentifier" 
	title="Includes the secondary identifier from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludeSecondaryIdentifier",true) ? "checked" : ""}}/>
<label for="IncludeSecondaryIdentifier">Include secondary identifier in uniqueness verification.</label>
</p>

<p>
<input type="checkbox" id="IncludeOptionalData" name="IncludeOptionalData" 
	title="Includes the optional data from the Travel Document in the verification process, if checked." 
	{{GetSetting("TAG.Identity.TravelDocuments.IncludeOptionalData",false) ? "checked" : ""}}/>
<label for="IncludeOptionalData">Include optional data in uniqueness verification.</label>
</p>

<p>
<label for="LifeCycleDays">Life Cycle Days:</label>  
<input type="number" id="LifeCycleDays" name="LifeCycleDays" min="0" step="1" max="3652"
	value='{{Str(GetSetting("TAG.Identity.TravelDocuments.LifeCycleDays",3652.0))}}' required 
	title="Number of days the uniqueness hash digests are kept."/>
</p>

<p>
<label for="Salt">Salt:</label>  
<input type="text" id="Salt" name="Salt" 
	value='{{Str(GetSetting("TAG.Identity.TravelDocuments.Salt",""))}}' required 
	title="Salt used in uniqueness hash digests calculation."/>
</p>

<button type='button' onclick='RandomizeSalt()'>Create Random Salt</button>

</fieldset>


<button type="submit" class="posButton">Apply</button>

<fieldset>
<legend>Tools</legend>

<button type="button" class="posButton"{{
if User.HasPrivilege("Admin.Communication.DeepFace") and User.HasPrivilege("Admin.Communication.Sniffer") then
	" onclick=\"OpenSniffer('Sniffer.md')\""
else
	" disabled"
}}>Sniffer</button>

<button type='button' onclick='OpenUrl("RepresentFace.md")'>Represent Face</button>
<button type='button' onclick='OpenUrl("CompareFaces.md")'>Compare Faces</button>

</fieldset>
</form>