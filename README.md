Travel Documents Authenticator
=================================

This repository provides an identity application authenticator based on information read from
Travel Documents.

Building Solution
--------------------

To build the solution, you the `TravelDocumentAuthenticator` solution project folder needs to
be a sibling folder to the [`NeuroAccessMaui`](https://github.com/Trust-Anchor-Group/NeuroAccessMaui)
repository solution folder (i.e. both the `TravelDocumentAuthenticator` and `NeuroAccessMaui` folders
need to reside in the same parent folder). The `TravelDocumentAuthenticator` solution uses
the `NeuroAccess.Nfc` project to decode and replay encrypted NFC communication, as part of the
validation procedure to ensure spoofing of the travel document is not possible. 
