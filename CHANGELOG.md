# Changelog

Notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed

- Fix registry (`reg`) command, which would fail if there were two or more postings with the same date, payee, account, and amount.
- Fix price scraping, which started grabbing prices outside the active range for dormant investments due to what seems like a recent change in Google Finance and how it handles the date query parameters. Worse, every time the price scraping routine ran, it would store a new copy of the unneeded price, causing the pricedb file to grow fast.


## [1.0.0] - 2015-05-02

Sorry, I didn't keep a changelog during this time, but it includes everything up to the end of Phase 2, as documented in the readme.