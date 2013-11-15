Wealth Pulse
============

Wealth Pulse is web frontend for a ledger journal file. The ledger journal file is
based on the command line [Ledger][1] journal file format and features double-entry 
accounting for personal finance tracking.


Objective
---------

Short-term, the focus is on what ledger cannot do right now: tables and charts.

Medium-term, I may deviate from the ledger file format to my own file format,
so that I can handle investments better.

Long-term, the idea is to replace the command line ledger with my own tool that
does all reporting via a web interface. Editing will still be done by text file,
though in the long-long term, perhaps a front end for adding/editing 
transactions would be a possibility.


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
*	Nancy.ViewEngines.Nustache 0.21.1
	- Well, actually, this was built against Nustache 1.0.0.0, so either need to use that version of the DLL
	or build a new one from source -- built new version from source for now. This should be fixed in the next
	release of Nancy.
*	Nustache 1.13.8.22


How to Run
----------

*	Setup LEDGER_FILE environment variable to point to your ledger file



Command Bar Supported Commands
------------------------------
[NOTE: Not implemented yet]

Commands:

	balance [accounts-to-include] [parameters]

	register [accounts-to-include] [parameters]

Parameters:

	:excluding [accounts-to-exclude]

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
- [] Review / revise parsing & post-processing:
	- [] Ensure transactions balance (if not autobalanced)
	- [] Can I remove mutable fields?
	- [] Add unit tests

Balance Report
- [x] Combine balance sheet & income report into single balance report with parameters
- [] Can I improve the account filtering code?
- [] Can I get rid of the list comprehension?
- [] Can I clean it up so the main function is just sub-function calls?

Net Worth Chart
- [x] Make it a separate page/report
- [] Can I patch Nustache so that it serializes objects to json?
	I shouldn't need the JavascriptSerializer to convert LineChartPoint list to JSON list of objects.

Dynamic Website:
- [] Convert all existing reports to render dynamically instead of a static page
	- [x] Get barebones nancy working
	- [x] map /, /balancesheet, /currentincomestatement, /previousincomestatement to current pages
	- [] Update rendering:
		- [] Convert to use bootstrap css
		- [] fix html/css (use proper elements ie h1, ul, etc...)
		- [] Convert to use d3.js for charts
	- [] turn into "one page" app that takes GET parameters for what to show (with command bar)
	- [] watch ledger file and reload on change
		- [] handle situation where file cannot be parsed


### Third Milestone

Register Report
- [] Register report with parameters (ie accounts, date range)
	- [] build register report generator function
	- [] create register report template
	- [] link up to command bar
	- [] link to from balance reports
- [] Sorting:
	- [] Preserve file order for transactions and entries within transactions but output in reverse so most recent is on top
		- Need to do sorting at the end so that running total makes sense
- [] Accounts Payable vs Accounts Receivable
	- Dynamically list non-zero accounts with balance in navlist. Link to register report

All Reports
- [] Refactoring/clean up of all reports


### Fourth Milestone

Command Bar Enhancements
- [] Clean up and improve date/period parsing
	Additions for period: yyyy, last year, this year
- [] Generate "networth" chart from the command bar
- [] Autocomplete hints (bootstrap typeahead)

Documentation
- [] github wiki
	- [] how to use / setup


Phase 2 Implementation (Commodities)
----------------------

Commodity Prices
- [] Update to handle commodities
- [] (While continuing to use ledger file format) Detect investment transactions and merge transaction lines
- [] Identify commodities from ledger file
- [] Fetch prices from internet and add to cache
	- [] Store commodity prices in a local cache
	- [] Prices should go from first date in ledger file to today

Net Worth
- [] Update chart with book value line and actual line

Balance Sheet
- [] Update Net Worth sheet with actual vs book value columns

Portfolio
- [] Overall portfolio return and per investment
- [] Expected T3s/T5s to receive for last year (ie had distribution)
- [] Rebalancing calculator - for rebalancing investments to proper allocation

Expenses
- [] Average in last 3 months, in last year
- [] Burn rate - using last 3 months expenses average, how long until savings is gone?
- [] Top Expenses over last period

Charts
- [] Income Statement chart (monthly, over time)

Nav
- [] Configurable nav list
- [] Combine reports and payables / receivables into one dict?
- [] Default report?


[1]: http://www.ledger-cli.org/			"Ledger command-line accounting system"