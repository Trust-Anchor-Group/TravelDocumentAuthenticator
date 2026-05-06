Title: DeepFace Sniffer
Description: Allows the user to view DeepFace communication.
Date: 2026-05-06
Author: Peter Waher
Master: /Master.md
JavaScript: /Events.js
JavaScript: /Sniffers/Sniffer.js
CSS: /Sniffers/Sniffer.css
UserVariable: User
Privilege: Admin.Communication.Sniffer
Privilege: Admin.Communication.DeepFace
Login: /Login.md
Parameter: SnifferId

========================================================================

DeepFace Communication
===========================

On this page, you can follow the DeepFace API communication made from the machine to the 
DeepFace back-end. The sniffer will automatically be terminated after some time to avoid 
performance degradation and leaks. Sniffers should only be used as a tool for 
troubleshooting.

{{
TAG.Identity.TravelDocuments.ServiceModule.RegisterSniffer(SnifferId,Request,"User",["Admin.Communication.Sniffer","Admin.Communication.DeepFace"])
}}
