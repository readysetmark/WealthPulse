Wealth Pulse
============

Wealth Pulse is web frontend for a ledger journal file. The ledger journal file
is based on the command line [Ledger][1] journal file format and features
double-entry accounting for personal finance tracking.


Objective
---------

Short-term: provide better looking reports and charts via a web frontend.

Long-term: provide better reporting on investments.


Status
------

I met my "must have" goals for this project in May 2015, and have been using it
with my ledger file exclusively since. That said, it may not meet *your* needs,
as it has been catered for my personal usage.


Dependencies
------------

Journal
*	FParsec 1.0.1
*	FsUnit.xUnit.1.2.1.2
*	Fuchu (unit testing framework)

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

* Setup a ``LEDGER_FILE`` environment variable that points to your ledger file.

* Optional: Setup a ``WEALTH_PULSE_CONFIG_FILE`` environment variable that
points to your Wealth Pulse config file.

* Optional: Setup a ``WEALTH_PULSE_PRICES_FILE`` environment variable that
points to where you'd like Wealth Pulse to store symbol prices.

Server & Browser method:

* Run ``wealthpulse.exe``

* Open a browser to ``http://localhost:5050/``

Electron method:

* Run ``npm install`` to download dependencies

* Run ``npm start`` to start Electron & server



Wealth Pulse Config File
------------------------

Wealth Pulse will scrape Google Finance for prices of stocks & mutual funds.

To utilize this feature, define the ``WEALTH_PULSE_CONFIG_FILE`` environment
variable as the path to a config file.

Within the config file, you can define a mapping of commodity symbol to Google
Finance search string:

	SC [symbol] [search string]

For example:

	SC AAPL aapl
	SC "INI220" MUTF_CA:INI220

The first will search for stock prices for Apple, the second will search for
mutual fund prices for the Tangerine Balanced Portfolio.



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

	:convert
		Applies to balance report only. Converts commodities into their current
		value based on available price information.



Notes on Commodities
--------------------

I've taken the approach recommended by [Penny][2] for tracking commodities.
Follow the link for some great documentation. In my case though, I'm still using
a subset the [Ledger][1] syntax, instead of different syntax, as Penny does.

So a purchase of a mutual fund might look like:

	2015-04-12 * Bank of Olympia - Golden Fund
		Assets:Investments:Olympia:GoldenFund		59.5856 "OGF387"
		Assets:Savings								$-1,272.73
		Basis:OGF387:2013-04-01						-59.5856 "OGF387"
		Basis:OGF387:2013-04-01						$1,272.73
	
	P 2015-04-12 "OGF387" $21.36

A sale of the same mutual fund would look like:

	2015-05-12 * Bank of Olympia - Golden Fund sale
		Assets:Savings								$1,430.05
		Assets:Investments:Olympia:GoldenFund		-59.5856 "OGF387"
		Basis:OGF387:2013-04-01						$-1,272.73
		Basis:OGF387:2013-04-01						59.5856 "OGF387"
		Income:Investments:Olympia:GoldenFund		$-157.32

	P 2015-05-12 "OGF387" $24.00


Project Plan
============

### Phase 1: Basic Reporting

#### Objectives

*	Replace the ledger bal and reg commandline options with a web interface.
*	Provide some basic reporting like net worth, income vs expenses, ...
*	See http://bugsplat.info/static/stan-demo-report.html for some examples


#### First Milestone

Parsing Ledger File
- [x] Basic / optimistic parsing of ledger file
- [x] Autobalance transactions

Initial Static Balance Reports:
- [x] Assets vs Liabilities, ie Net Worth
- [x] Income Statement (current & previous month)
- [x] Net Worth chart


#### Second Milestone

Parsing Ledger File
- [x] Ensure transactions balance (if not autobalanced)

Balance Report
- [x] Combine balance sheet & income report into single balance report with
parameters

Net Worth Chart
- [x] Make it a separate page/report
- [x] Generate "networth" chart from the command bar

Dynamic Website:
- [x] Convert all existing reports to render dynamically instead of a static
page
	- [x] Get barebones nancy working
	- [x] map /, /balancesheet, /currentincomestatement, /previousincomestatement
	to current pages
	- [x] Switch to client-side framework
		- [x] Setup JSON services
		- [x] Setup static file services
		- [x] Create client-side app
	- [x] Setup command bar
	- [x] Highlight active page on navlist


#### Third Milestone

Register Report
- [x] Register report with parameters (ie accounts, date range)
	- [x] build register report generator function
	- [x] create register report template
	- [x] link up to command bar
	- [x] link to from balance reports
- [x] Sorting:
	- [x] Preserve file order for transactions and entries within transactions but
	output in reverse so most recent is on top
		- Need to do sorting at the end so that running total makes sense
- [x] Accounts Payable vs Accounts Receivable
	- Dynamically list non-zero accounts with balance in navlist. Link to register
	report


#### Fourth Milestone

Watch File
- [x] Watch ledger file and reload on change
- [x] Handle situation where file cannot be parsed

Documentation
- [x] How to use / setup



### Phase 2: Commodities

#### Objectives

*	Add support for investments (ie, multiple commodities) on balance and register
reports
*	Provide "real" and "book value" lines on the Net Worth chart

**2014/06/11**: I've prototyped it out enough now that I think what I want to do
is make sure I can get current prices for commodities and then for the balance
sheet, it would look something like:

	Assets:Investments		$realvalue	$basis
		Investment1			$realvalue	$basis	num_units	price	price_date
		Investment2			$realvalue	$basis	num_units	price	price_date
		Investment3			$realvalue	$basis	num_units	price	price_date
	TOTAL					$realvalue	$basis

That would be the eventual goal. That assumes that I'm either going to merge
units/book value entries in the program or update the ledger file. In the mean
time, I'll have to keep the "units" excluded from the main balance report. This
way I can avoid having to propogate up the account hierarchy all the different
commodities.


#### First Milestone

Prototype using @@ or @ notation for commodity transactions, as per Ledger file
spec. I'm not sure if I want to use Ledger's way of doing this, so going to set
up the logic for the balance report and see how it feels with a sample file.

Prototype
- [x] What do I want the balance report to look like with investments?
	- [x] Update backend functions to handle multiple commodities
	- [x] Update frontend to display all commodities for balance

Prices
- [x] Identify commodities in ledger file
	- [x] Commodity, date of first use, date when balance becomes 0
- [x] Fetch prices from internet, cache and store
	- [x] Fetch prices from first date commodity appears in ledger file until 0
	balance reached
	- [x] Store prices in a local cache (and in memory while app is running)

Balance Report
- [x] For leaf accounts, calculate/provide book value, real value, number of
units, price, price date (may drop price date)
	- [x] Lookup price in price DB based on end date
- [x] For parent accounts & total, calculate/provide book value and real value
- [x] Update front end
- [x] Omit commodity-related columns if "real value" and "book value" columns
are the same for the whole report (based on query parameters)


#### Second Milestone

Turns out I don't like using the @@ and @ notation. Recently found Penny, a
ledger-inspired app that handles commodities much more cleanly and a lot closer
to how I've been handling them in my own ledger file. This milestone will be
about modifying Wealth Pulse to behave a bit more like Penny than Ledger.

Instead of using the @@/@ notation that Ledger supports/recommends, I will use
Basis accounts the same way Penny documentation recommends. I will probably also
have to write the "selloff" function.

One thing I will have to decide is if I will parse prices out of the ledger file
at all. Gut feeling right now is that I should. I think the `.pricedb` file
should __only__ have downloaded prices, and the ledger file should __only__ have
prices that were manually input.

I will keep track of two price DBs in memory: the downloaded prices and the
ledger file prices. When performing a price lookup, check the downloaded prices
file first, and the ledger file if no price exists.

Types
- [x] Update Entry type -- will only have one amount, no commodity field
- [x] Price on SymbolPrice record should be an Amount, not just a decimal
- [x] Symbol type should be value + quoted or format
- [x] Rename Entry to Posting
- [x] Remove EntryType field from Posting (no longer supporting "virtual"
accounts)
- [x] Move price DBs into Journal record

Journal Parsing
- [x] Remove multiple commodity parsing logic. ie. remove @@ and @ options
- [x] Parse price lines from journal file
	- [x] Get initial parsing working
	- [x] Should fix parsing up to be more precise (amount MUST have symbol)
	- [x] Create price db from parsed prices
- [x] Get line numbers for headers, postings, prices
- [x] Add unit tests for parsers
- [x] Rework post-parse processing

PriceDB
- [x] Fix pricedb parsing and serialization
	- [x] Use FParsec parser instead of regex
	- [x] Price should be an Amount with a symbol
- [x] Pricedb and Journal should use the same parser combinator
- [x] Parse config file using FParsec
- [x] Review & restore commented code in symbolprices.cs and journalservice.cs

Balance Report
- [x] Update logic for calculating basis and real value for commodities
- [x] Need query function for latest price as of date (check `.pricedb` then
ledger prices)
- [x] Make ":convert" a report option

Net Worth Report
- [x] Provide "real value" and "basis value" lines

Documentation
- [x] Use of commodities within file
- [x] Configuration file
	- [x] Scraping Google Finance for prices


#### Third Milestone

Price Fetching
- [x] Review identifySymbolUsage
- [x] Review and enable price fetching logic

Outstanding Payees
- [x] Handle multiple commodities

Register Report
- [x] Handle Multiple commodities

Balance Report
- [x] Can I get rid of the list comprehension?
- [x] Can I clean it up so the balance query function is just sub-function
calls?
- [x] Clean up computeCommodityValues (get rid of side-effects)



### Phase 3: Post-1.0 Someday/Maybe Improvements

Electron
* Use electron to make a bundled app instead of requiring an always-running
server process with a web browser pointed at it
- [x] Do proof-of-concept with Electron pointed to current server -- it works!
- Turn it into an Electron app instead of accessing through a browser:
	- [x] Launch F# server at startup
	- [x] Launch electron browser window pointed to WP server
	- [x] Update documentation with launch instructions (npm start in dev)
	- [x] Delay launching browser window until server ready
	- [x] Retrieve port (or full address) from server output
	- [ ] Determine correct path to wealth pulse server depending on:
		- dev mode vs bundled app
		- platform (OS X vs Windows)
		>> Partially done
	- [ ] Icon and rename and change 'Electron' app name to 'WealthPulse'

Tooling
- [ ] Switch tests to xUnit/FSUnit
- [ ] Setup CI (TravisCI?)
- [ ] Add a real logger
- [ ] Use npm/FAKE/grunt/gulp automation?
- Automate:
	- [ ] Building/bundling a shippable "app"
		- OS X:
			- Copy `node_modules/electron-prebuilt/dist/Electron.app` to `dist`
			- Copy `package.json` and `WealthPulseApp` to
			`dist/Electron.app/Contents/Resources/app`
			- Copy F# app to `dist/Electron.app/Contents/Resources/app`
		- Windows: TBD

Price Scraping
- [x] Retry after delay if fetching prices fails (happens if no internet is
available) instead of waiting a full day to retry
- [ ] Only write to `.pricedb` if new prices were found
- [ ] When writing to `.pricedb`, write to temp file first, then replace
`.pricedb` file (avoid clobbering a file if app exits during write)
- [ ] Consider using Akka.net actors?

UI
- [ ] Use bower or npm to retrieve dependencies, rather than including sources
for all dependencies in the git repo
- [ ] Switch to elm or cycle.js instead of React?
	- [ ] Spike Elm
	- [ ] Spike cycle.js
- [ ] If keeping with React...
	- [ ] Update to latest React
	- [ ] Switch to React Router instead of Backbone router
- [ ] Update to Bootstrap from v2 to v3 (or v4...)
- [ ] Display indicator when ajax call is happening
- [ ] Combine reports and payables / receivables into one dict?

Types
- [ ] Include a list of account levels field on Posting?
	- How am I actually using Account & AccountLineage in the app?
	- Also change Account to a Subaccount list and Subaccount = String
- [ ] Review all types for consistency
- [ ] Symbol Price: Hard-coded line number to -1 -- feels like a hack

Reports
- [ ] Add unit tests
- [ ] Break functions into smaller units?
- [ ] Use LINQ?
- [ ] Balance: Can I improve the entry filtering code?
- [ ] Income Statement: Should be able to pull up income statement for any month 
	- Add a dropdown for picking period?

New Report Ideas
- [ ] Portfolio
	- [ ] Overall portfolio return and per investment
	- [ ] Expected T3s/T5s to receive for last year (ie had distribution)
		- I should be able to get this with a register report query
- [ ] Expenses
	- [ ] Average in last 3 months, in last year
	- [ ] Burn rate - using last 3 months expenses average, how long until savings
	is gone?
	- [ ] Top Expenses over last period
- [ ] Charts
	- [ ] Income Statement chart (monthly, over time)

Parsing Ledger File
- [ ] Skipping the comment lines during parsing would simplify processing (since
first thing we do is drop them)
- [ ] Transform post-processing to a pipeline that deals with one transaction at
a time (completely), or does things in parallel
- [ ] Improve error reporting during parsing and balance checking
- [ ] Validate accounts based on a master list? To prevent typos and whatnot...

Command Bar Enhancements
- [ ] Add parameters:
	:payee
	:excludepayee
	:uncleared or :cleared
- [ ] Add fault tolerance to parameter parsing
- [ ] Clean up and improve date/period parsing
	Additions for period: yyyy, last year, this year
- [ ] Autocomplete hints (bootstrap typeahead)

Nav
- [ ] Configurable nav list
- [ ] Configurable default report
- [ ] Handle outstanding payees with multiple commodity amounts a bit nicer
(renders poorly right now)

Commodities
- [ ] Write the equivalent of Penny's sell-off command

Server
- [ ] Switch to Suave instead of NancyFX?
- [ ] Let server launch on any port?
- [ ] Server should handle SIGTERM signal gracefully



[1]: http://www.ledger-cli.org/
[2]: http://massysett.github.io/penny/
