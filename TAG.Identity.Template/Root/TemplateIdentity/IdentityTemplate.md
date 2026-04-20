Title: Identity authenticator settings
Description: Contains a page where the operator of the Neuron can configure the service.
Date: 2024-11-19
Author: Peter Waher
Master: /Master.md
Cache-Control: max-age=0, no-cache, no-store
UserVariable: User
Privilege: Admin.Identity.Template
Login: /Login.md

========================================================================

<form action="IdentityTemplate.md" method="post">
<fieldset>
<legend>Template settings</legend>

**TODO**: On this page you configure the identity authenticator service. Make sure to provide a form with necessary configuration options. 
To simplify persistance of settings, you can use the [`GetSetting` and `SetSetting` script functions](/Script.md#persistenceRelatedFunctionsWaherScriptPersistence).
You can use the host-specific overloads to make settings that can vary depending on the domain that is being used.
Alternatively, you can create your own configuration class or classes in C# and store them explicitly to the object database.

**Note**: If using the `GetSetting` and `SetSetting` script functions to persist settings, make sure to prefix the key names with the namespace
of the service, so they are easily identified if reviewing all available settings in the runtime settings collection.

</fieldset>
</form>