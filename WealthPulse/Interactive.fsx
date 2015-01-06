
#I "../Journal/bin/Debug"
#r "FSharp.Core.dll"
#r "FParsec.dll"
#r "FParsecCS.dll"
#r "Journal.dll"

FParsec.CharParsers.run Journal.Parser.Combinators.parsePrice "P 2014/12/14 AAPL $13.33"
open Journal

//let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"
let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\test_investments.dat"
let configPath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.config"
let pricesPath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.pricedb"

let prices = Parser.parsePricesFile pricesPath System.Text.Encoding.ASCII

let entries = Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII
let journal = Journal.createJournal entries


(*
    Load Symbol Config
*)

let symbolConfigs = SymbolPrices.loadSymbolConfig configPath
SymbolPrices.printSymbolConfigs symbolConfigs


(*
    Load Prices DB
*)

let priceDB = SymbolPrices.loadSymbolPriceDB pricesPath
SymbolPrices.printSymbolPriceDB priceDB


(*
    Update Price DB
*)

let symbolUsages = Query.identifySymbolUsage journal
let newPriceDB = SymbolPrices.updateSymbolPriceDB symbolUsages symbolConfigs priceDB
SymbolPrices.saveSymbolPriceDB pricesPath newPriceDB

(*
    Following section is for output commodity usage
*)
(*
let commodityMap = Query.identifyCommodities journal

let streamWriter = new System.IO.StreamWriter(@"C:\Users\Mark\Nexus\Documents\finances\ledger\commodities.txt")
fprintfn streamWriter "%-9s\t%-14s\t%-17s" "Commodity" "First Appeared" "Zero Balance Date"

let displayCommodityMap (sw : System.IO.StreamWriter) (key : Journal.Commodity) (value : Query.CommodityUsage) =
    fprintfn sw "%-9s\t%-14s\t%-17s" (value.Commodity) (value.FirstAppeared.ToString("yyyy/MM/dd")) (if Option.isSome value.ZeroBalanceDate then (Option.get value.ZeroBalanceDate).ToString("yyyy/MM/dd") else "")

Map.iter (displayCommodityMap streamWriter) commodityMap

streamWriter.Close()
*)