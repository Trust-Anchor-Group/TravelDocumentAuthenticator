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

Authentication Errors
------------------------

When authenticating claims in Identity applications, the service reports errors as it 
invalidates claims. The following table lists the error codes using by the service:

| Error Code               | Default Error                                                                  | Description |
|--------------------------|--------------------------------------------------------------------------------|-------------|
| `AgeNotReached`          | `Age not reached.`                                                             | The Identity application contains an age claim (`AGEABOVE`) that has not been reached based on the birth date in the travel document. |
| `BirthDayInvalid`        | `Birth Day invalid.`                                                           | The Identity application contains a birth day (`BDAY`) that does not match the birth day in the travel document. |
| `BirthMonthInvalid`      | `Birth Month invalid.`                                                         | The Identity application contains a birth month (`BMONTH`) that does not match the birth month in the travel document. |
| `BirthYearInvalid`       | `Birth Year invalid.`                                                          | The Identity application contains a birth year (`BYEAR`) that does not match the birth year in the travel document. |
| `CountryCodeMismatch`    | `Country invalid.`                                                             | The Identity application contains a country claim (`COUNTRY`) that does not match the country code in the travel document. |
| `CountryNotSpecified`    | `Country not specified, or available in MRZ.`                                  | The Identity application contains a personal number claim (`PNR`) without specifying a country, either as a claim, or in the travel document. |
| `FutureBirthDate`        | `Future birth date.`                                                           | The travel document contains a birth date that is in the future. |
| `GenderInvalid`	       | `Gender invalid.`                                                              | The Identity application contains a gender claim (`GENDER`) that does not match the gender in the travel document. |
| `InconsistentBirthDate`  | N/A                                                                            | The Travel Document contains inconsistent birth date information. |
| `NationalityInvalid`     | `Nationality invalid.`                                                         | The Identity application contains a nationality claim (`NATIONALITY`) that does not match the nationality in the travel document. |
| `NfcInvalid`             | N/A                                                                            | The NFC data available as an attachment is invalid and fails cryptographic validation checks. |
| `PersonalNumberInvalid`  | `Personal number invalid according to national rules.`                         | The Identity application contains a personal number claim (`PNR`) that does not match the personal number schemes for the country specified. |
| `PersonalNumberMismatch` | `Personal number does not match.`                                              | The Identity application contains a personal number claim (`PNR`) that does not match the personal number in the travel document. |
| `PNrNotNormalized`       | `Personal number not normalized.`                                              | The Identity application contains a personal number claim (`PNR`) that is not normalized according to the rules specified for the country. |
| `LastNameInvalid`        | `Last name(s) invalid.`                                                        | The Identity application contains a last name claim (`LAST`) that does not match the last name(s) (primary identifier) in the travel document. |
| `FirstNameInvalid`       | `First name(s) invalid.`                                                       | The Identity application contains a first name claim (`FIRST`) that does not match the first name(s) (secondary identifier) in the travel document. |
| `FullNameInvalid`	       | `Full name invalid.`                                                           | The Identity application contains a full name claim (`FULLNAME`) that does not match the full name in the travel document. |
| `NoFace`			       | `No face detected in profile photo.`                                           | A face could not be detected in the profile photo provided. |
| `MultipleFaces`          | `Multiple faces detected in profile photo.`                                    | Multiple faces were detected in the profile photo provided. |
| `LowQualityPhoto`        | `Low quality profile photo.`                                                   | The confidence level in the face provided in the profile photo is too low. |
| `PhotoMismatch`          | `Profile photo does not match photo in Travel Document.`                       | The face provided in the profile photo does not match the face in the travel document. |
| `PhotoTooSimilar`        | N/A                                                                            | The face provided in the profile photo is too similar to the face in the travel document, indicating a potential spoofing attempt. Retaking profile photo is recommended. If application was rejected, images were too similar. If application was left for manual review, it indicates images were very similar but not identical. |
| `DuplicateApplication`   | `An application with the same personal information has already been accepted.` | An application with the same personal information (e.g. same name and birth date) has already been accepted, and uniqueness of applications is enforced. |

### TODO

The `TravelDocumentAuthenticator` service can use the anti-spoofing feature of DeepFace, if
available. To enable anti-spoofing, first DeepFace needs to be installed with Torch. (See DeepFace
documentation how this is done.) Once Torch is installed with DeepFace, you enable anti-spoofing
protection from the Settings page.