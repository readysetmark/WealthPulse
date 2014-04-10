Wealth Pulse
============

Wealth Pulse is web frontend for a ledger journal file. The ledger journal file is
based on the command line [Ledger][1] journal file format and features double-entry 
accounting for personal finance tracking.


Objective
---------

Short-term: provide better looking reports and charts via a web frontend.

Long-term: provide better reporting on investments.


Dependencies
------------

Journal
*	FParsec 1.0.1
*	FsUnit.xUnit.1.2.1.2
*	xunit 1.9.1
*	xunit.runners 1.9.1

WealthPulse
*	FParsec 1.0.1
*	Nancy 0.21.1
*	Nancy.Hosting.Self 0.21.1

Frontend
*	React 0.9.0-rc1
*	Underscore 1.6.0
*	Backbone 1.1.1 (routing only)
*	D3 3.0.0
*	jQuery 2.1.0
*	jQuery Hotkeys
*	Bootstrap 2.3.1



How to Run
----------

Setup a ``LEDGER_FILE`` environment variable to point to your ledger file.



Command Bar Supported Commands
------------------------------

You can use the '/' hotkey to reach the command bar.

Commands:

	Balance:   bal [accounts-to-include] [parameters]

	Register:  reg [accounts-to-include] [parameters]

	Net Worth: nw

Parameters:

	:exclude [accounts-to-exclude]

	:period [this month|last month]

	:since [yyyy/mm/dd]

	:upto [yyyy/mm/dd]

	:title [report title]



Implementation Notes
--------------------

Investments & Commodities:
*	I'm basically ignoring these for the moment. The parser will parse them,
but all processing after that point assumes one commodity and basically assumes
only the "amount" field is used. I'll need to revisit this once I get around
to adding investment/commodity support.



Phase 1 Implementation (Reporting)
----------------------

### Objective

*	Replace the ledger bal and reg commandline options with a web interface.
*	Provide some basic reporting like net worth, income vs expenses, ...
*	See http://bugsplat.info/static/stan-demo-report.html for some examples


### First Milestone

Parsing Ledger File
- [x] Basic / optimistic parsing of ledger file
- [x] Autobalance transactions

Initial Static Balance Reports:
- [x] Assets vs Liabilities, ie Net Worth
- [x] Income Statement (current & previous month)
- [x] Net Worth chart


### Second Milestone

Parsing Ledger File
- [x] Ensure transactions balance (if not autobalanced)

Balance Report
- [x] Combine balance sheet & income report into single balance report with parameters

Net Worth Chart
- [x] Make it a separate page/report
- [x] Generate "networth" chart from the command bar

Dynamic Website:
- [x] Convert all existing reports to render dynamically instead of a static page
	- [x] Get barebones nancy working
	- [x] map /, /balancesheet, /currentincomestatement, /previousincomestatement to current pages
	- [x] Switch to client-side framework
		- [x] Setup JSON services
		- [x] Setup static file services
		- [x] Create client-side app
	- [x] Setup command bar
	- [x] Highlight active page on navlist


### Third Milestone

Register Report
- [x] Register report with parameters (ie accounts, date range)
	- [x] build register report generator function
	- [x] create register report template
	- [x] link up to command bar
	- [x] link to from balance reports
- [x] Sorting:
	- [x] Preserve file order for transactions and entries within transactions but output in reverse so most recent is on top
		- Need to do sorting at the end so that running total makes sense
- [ ] Accounts Payable vs Accounts Receivable
	- Dynamically list non-zero accounts with balance in navlist. Link to register report


### Fourth Milestone

Watch File
- [ ] Watch ledger file and reload on change
- [ ] Handle situation where file cannot be parsed

Documentation
- [ ] How to use / setup

Tooling
- [ ] Research how to handle references cross-platform (sln on mac is different from windows??)
- [ ] Write a FAKE script for building / running?


Phase 2 Implementation (Commodities)
----------------------

Commodity Prices
- [ ] Update to handle commodities
- [ ] Detect investment transactions and merge transaction lines (while continuing to use ledger file format) 
- [ ] Identify commodities from ledger file
- [ ] Fetch prices from internet and add to cache
	- [ ] Store commodity prices in a local cache
	- [ ] Prices should go from first date in ledger file to today

Net Worth
- [ ] Update chart with book value line and actual line

Balance Sheet
- [ ] Update Net Worth sheet with actual vs book value columns

Portfolio
- [ ] Overall portfolio return and per investment
- [ ] Expected T3s/T5s to receive for last year (ie had distribution)
- [ ] Rebalancing calculator - for rebalancing investments to proper allocation

Expenses
- [ ] Average in last 3 months, in last year
- [ ] Burn rate - using last 3 months expenses average, how long until savings is gone?
- [ ] Top Expenses over last period

Charts
- [ ] Income Statement chart (monthly, over time)

Nav
- [ ] Configurable nav list
- [ ] Combine reports and payables / receivables into one dict?
- [ ] Default report?


Someday/Maybe/Improvements
--------------------------

Other
- [ ] Display indicator when ajax call is happening
- [ ] Add unit tests

Parsing Ledger File
- [ ] Review / revise parsing & post-processing:
	- [x] Remove mutable fields
	- [x] Make Amount a record type instead of a tuple
	- [ ] Skipping the comment lines during parsing would simplify processing (since first thing we do is drop them)
	- [ ] Transform post-processing to a pipeline that deals with one transaction at a time (completely)
	- [ ] Improve error reporting


Balance Report
- [ ] Can I improve the entry filtering code?
- [x] Can I get rid of the list comprehension?
- [ ] Can I clean it up so the balance query function is just sub-function calls?

All Reports
- [ ] Refactoring/clean up of all reports

Command Bar Enhancements
- [ ] Add fault tolerance to parameter parsing
- [ ] Clean up and improve date/period parsing
	Additions for period: yyyy, last year, this year
- [ ] Autocomplete hints (bootstrap typeahead)



[1]: http://www.ledger-cli.org/