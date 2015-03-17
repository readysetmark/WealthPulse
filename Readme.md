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

Setup a ``LEDGER_FILE`` environment variable that points to your ledger file.

Optional: Setup a ``WEALTH_PULSE_CONFIG_FILE`` environment variable that points to your Wealth Pulse config file.

Optional: Setup a ``WEALTH_PULSE_PRICES_FILE`` environment variable that points to where you'd like Wealth Pulse to store symbol prices.

Then run ``wealthpulse.exe``.



Wealth Pulse Config File
------------------------

Yet to be written...




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
- [x] Accounts Payable vs Accounts Receivable
	- Dynamically list non-zero accounts with balance in navlist. Link to register report


### Fourth Milestone

Watch File
- [x] Watch ledger file and reload on change
- [x] Handle situation where file cannot be parsed

Documentation
- [x] How to use / setup


Phase 2 Implementation (Commodities)
----------------------

### Objective

*	Add support for investments (ie, multiple commodities) on balance and register reports
*	Provide "real" and "book value" lines on the Net Worth chart
*	Additional investment reports such as overall portfolio return and return by investment
*	Additional expenses reports such as burn rate

2014/06/11
I've prototyped it out enough now that I think what I want to do is make sure I can get current prices for commodities
and then for the balance sheet, it would look something like:
	Assets:Investments		$realvalue	$basis
		Investment1			$realvalue	$basis	num_units	price	price_date
		Investment2			$realvalue	$basis	num_units	price	price_date
		Investment3			$realvalue	$basis	num_units	price	price_date
	TOTAL					$realvalue	$basis
That would be the eventual goal. That assumes that I'm either going to merge units/book value entries in the program
or update the ledger file. In the mean time, I'll have to keep the "units" excluded from the main balance report.
This way I can avoid having to propogate up the account hierarchy all the different commodities.


Commodity Prices
- [ ] Update functions to consider amount commodities
	- [p] Query.balance
	- [p] NancyRunner.presentBalanceData
	- [p] Balance Report JS
	- [ ] NancyRunner.generateNetWorthData
	- [ ] Net Worth JS
	- [ ] Query.register
	- [ ] NancyRunner.presentRegisterData
	- [ ] Register Reprot JS
	- [ ] Query.outstandingPayees
	- [ ] Oustanding Payees JS
	- [ ] Parser.balanceTransactions	


### First Milestone

Prototype using @@ or @ notation for commodity transactions, as per Ledger file spec. I'm not sure if I want to use
Ledger's way of doing this, so going to set up the logic for the balance report and see how it feels with a sample
file.

Prototype
- [x] What do I want the balance report to look like with investments?
	- [x] Update backend functions to handle multiple commodities
	- [x] Update frontend to display all commodities for balance

Prices
- [x] Identify commodities in ledger file
	- [x] Commodity, date of first use, date when balance becomes 0
- [x] Fetch prices from internet, cache and store
	- [x] Fetch prices from first date commodity appears in ledger file until 0 balance reached
	- [x] Store prices in a local cache (and in memory while app is running)

Balance Report
- [x] For leaf accounts, calculate/provide book value, real value, number of units, price, price date (may drop price date)
	- [x] Lookup price in price DB based on end date
- [x] For parent accounts & total, calculate/provide book value and real value
- [x] Update front end
- [x] Omit commodity-related columns if "real value" and "book value" columns are the same for the whole report (based on query parameters)



### Second Milestone

Turns out I don't like using the @@ and @ notation. Recently found Penny, a ledger-inspired app that handles
commodities much more cleanly and a lot closer to how I've been handling them in my own ledger file. This milestone
will be about modifying Wealth Pulse to behave a bit more like Penny than Ledger.

Instead of using the @@/@ notation that Ledger supports/recommends, I will use Basis accounts the same way Penny
documentation recommends. I will probably also have to write the "selloff" function.

One thing I will have to decide is if I will parse prices out of the ledger file at all. Gut feeling right now is
that I should. I think the .pricedb file should __only__ have downloaded prices, and the ledger file should __only__
have prices that were manually input.

I will keep track of two price DBs in memory: the downloaded prices and the ledger file prices. When performing a price
lookup, check the downloaded prices file first, and the ledger file if no price exists.

Types
- [x] Update Entry type -- will only have one amount, no commodity field
- [x] Price on SymbolPrice record should be an Amount, not just a decimal
- [ ] Move price DBs into Journal record
- [ ] Rename Entry to Posting
- [ ] Include a list of account levels field on Posting?
	- Also change Account to a Subaccount list and Subaccount = String
- [ ] Remove EntryType field from Posting (no longer supporting "virtual" accounts)


Journal Parsing
- [x] Remove multiple commodity parsing logic. ie. remove @@ and @ options
- [x] Parse price lines from journal file
	- [x] Get initial parsing working
	- [x] Should fix parsing up to be more precise (amount MUST have symbol)
	- [x] Create price db from parsed prices
- [ ] Get line numbers for headers, postings, prices and comment lines
- [ ] Add unit tests
- [ ] Rework post-parse processing

PriceDB
- [x] Fix pricedb parsing and serialization
	- [x] Use FParsec parser instead of regex
	- [x] Price should be an Amount with a symbol
- [ ] Pricedb and Journal should use the same parser combinator

Balance Report
- [ ] Update logic for calculating basis and real value for commodities
	- I'm checking this in in a horrible state. Still to do:
		- calculate basis total -- not sure how to do this cleanly right now
		- need to generate parent accounts w/ amounts (used to do this, but removed and it should be done as a later step in the balance report)
- [x] Need query function for latest price as of date (check .pricedb then ledger prices)
- [ ] Can I use LINQ for querying?

Documentation
- [ ] Use of commodities within file
- [ ] Configuration file
	- [ ] Scraping Google Finance for prices


### Third Milestone

Net Worth Report
- [ ] Provide "real value" and "book value" lines

Register Report
- [ ] Prototype... what should it look like?




Someday/Maybe/Improvements
--------------------------

Nav
- [ ] Configurable nav list
- [ ] Combine reports and payables / receivables into one dict?
- [ ] Default report?
- [ ] Display indicator when ajax call is happening

Tooling
- [ ] Research how to handle references cross-platform (sln on mac is different from windows??)
- [ ] Write a FAKE script for building / running?
- [ ] Add unit tests

Parsing Ledger File
- [ ] Review / revise parsing & post-processing:
	- [x] Remove mutable fields
	- [x] Make Amount a record type instead of a tuple
	- [ ] Skipping the comment lines during parsing would simplify processing (since first thing we do is drop them)
	- [ ] Make specifying a commodity mandatory? Cleans up some code and I always do it...
	- [ ] Transform post-processing to a pipeline that deals with one transaction at a time (completely)
	- [ ] Improve error reporting
	- [ ] Consider removing the virtual transaction types once commodity support is complete

Balance Report
- [ ] Can I improve the entry filtering code?
- [x] Can I get rid of the list comprehension?
- [ ] Can I clean it up so the balance query function is just sub-function calls?
- [ ] Clean up computeCommodityValues (get rid of side-effects)

Portfolio
- [ ] Overall portfolio return and per investment
- [ ] Expected T3s/T5s to receive for last year (ie had distribution)
	- I should be able to get this with a register report query

Expenses
- [ ] Average in last 3 months, in last year
- [ ] Burn rate - using last 3 months expenses average, how long until savings is gone?
- [ ] Top Expenses over last period

Charts
- [ ] Income Statement chart (monthly, over time)

Command Bar Enhancements
- [ ] Add paramters:
	:payee
	:excludepayee
- [ ] Add fault tolerance to parameter parsing
- [ ] Clean up and improve date/period parsing
	Additions for period: yyyy, last year, this year
- [ ] Autocomplete hints (bootstrap typeahead)


[1]: http://www.ledger-cli.org/