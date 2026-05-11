Title: Represent Face
Description: Allows the operator to test the service by representing a face image.
Date: 2026-05-11
Author: Peter Waher
Master: /Master.md
Cache-Control: max-age=0, no-cache, no-store
JavaScript: RepresentFace.js
JavaScript: CompareFaces.js
JavaScript: /Events.js
UserVariable: User
Privilege: Admin.Identity.TravelDocuments
Login: /Login.md

========================================================================

<form>
<fieldset>
<legend>Represent Face</legend>

<p>
<label for="Image">Image:</label>  
<input type="file" id="Image" name="Image" accept="image/*" required 
       title="Select the image to represent"/>
</p>

<button type="button" class="posButton" onclick="RepresentFace()">Represent</button>
</fieldset>

<fieldset style="display:none" id="Output">
<legend>Output</legend>
<div id="OutputContent"></div>
</fieldset>
</form>