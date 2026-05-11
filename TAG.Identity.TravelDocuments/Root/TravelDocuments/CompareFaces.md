Title: Compare Faces
Description: Allows the operator to test the service by comparing two face images.
Date: 2026-05-11
Author: Peter Waher
Master: /Master.md
Cache-Control: max-age=0, no-cache, no-store
JavaScript: CompareFaces.js
JavaScript: /Events.js
UserVariable: User
Privilege: Admin.Identity.TravelDocuments
Login: /Login.md

========================================================================

<form>
<fieldset>
<legend>Compare Faces</legend>

<p>
<label for="Image1">Image 1:</label>  
<input type="file" id="Image1" name="Image1" accept="image/*" required 
       title="Select the first image to compare"/>
</p>

<p>
<label for="Image2">Image 2:</label>  
<input type="file" id="Image2" name="Image2" accept="image/*" required 
       title="Select the second image to compare"/>
</p>

<button type="button" class="posButton" onclick="CompareFaces()">Compare</button>
</fieldset>

<fieldset style="display:none" id="Output">
<legend>Output</legend>
<div id="OutputContent"></div>
</fieldset>
</form>