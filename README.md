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

| Error Code              | Default Error                                          | Description |
|-------------------------|--------------------------------------------------------|-------------|
| `FutureBirthDate`       | `Future birth date.`                                   | The travel document contains a birth date that is in the future. |
| `BirthDayInvalid`       | `Birth Day invalid.`                                   | The Identity application contains a birth day (`BDAY`) that does not match the birth day in the travel document. |
| `BirthMonthInvalid`     | `Birth Month invalid.`                                 | The Identity application contains a birth month (`BMONTH`) that does not match the birth month in the travel document. |
| `BirthYearInvalid`      | `Birth Year invalid.`                                  | The Identity application contains a birth year (`BYEAR`) that does not match the birth year in the travel document. |
| `AgeNotReached`         | `Age not reached.`                                     | The Identity application contains an age claim (`AGEABOVE`) that has not been reached based on the birth date in the travel document. |
| `InconsistentBirthDate` | N/A                                                    | The Travel Document contains inconsistent birth date information. |
| `CountryInvalid`        | `Country invalid.`                                     | The Identity application contains a country claim (`COUNTRY`) that does not match the country code in the travel document. |
| `NationalityInvalid`    | `Nationality invalid.`                                 | The Identity application contains a nationality claim (`NATIONALITY`) that does not match the nationality in the travel document. |
| `GenderInvalid`	      | `Gender invalid.`                                      | The Identity application contains a gender claim (`GENDER`) that does not match the gender in the travel document. |
| `PersonalNumberInvalid` | `Personal number invalid according to national rules.` | The Identity application contains a personal number claim (`PNR`) that does not match the personal number schemes for the country specified. |
| `CountryNotSpecified`   | `Country not specified, or available in MRZ.`          | The Identity application contains a personal number claim (`PNR`) without specifying a country, either as a claim, or in the travel document. |
| `PNrNotNormalized`      | `Personal number not normalized.`                      | The Identity application contains a personal number claim (`PNR`) that is not normalized according to the rules specified for the country. |
| `PNrMismatch`           | `Personal number does not match.`                      | The Identity application contains a personal number claim (`PNR`) that does not match the personal number in the travel document. |
| `NfcInvalid`            | N/A                                                    | The NFC data available as an attachment is invalid and fails cryptographic validation checks. |
