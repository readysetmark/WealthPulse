
// for fsharpi

#I "Journal/bin/Debug";;
#r "FSharp.Core.dll";;
#r "FParsec.dll";;
#r "FParsecCS.dll";;
#r "Journal.dll";;

open Journal;;
open FParsec;;

runParserOnFile Parser.Combinators.journal () "/Users/mark/Nexus/Documents/finances/ledger/test_investments_penny.dat" System.Text.Encoding.UTF8;;
runParserOnFile Parser.Combinators.journal () "JournalTest/testfiles/simple.dat" System.Text.Encoding.UTF8;;



// for visual f#

#I "../Journal/bin/Debug"
#r "FSharp.Core.dll"
#r "FParsec.dll"
#r "FParsecCS.dll"
#r "Journal.dll"

open Journal

//let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"
let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\test_investments.dat"
let configPath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.config"
let pricesPath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.pricedb"

//let ledgerFilePath = @"/Users/mark/Nexus/Documents/finances/ledger/ledger.dat"
let ledgerFilePath = @"/Users/Mark/Nexus/Documents/finances/ledger/test_investments_penny.dat"
let configPath = @"/Users/Mark/Nexus/Documents/finances/ledger/.config"
let pricesPath = @"/Users/Mark/Nexus/Documents/finances/ledger/.pricedb"


let postings, priceDB = Parser.parseJournalFile ledgerFilePath System.Text.Encoding.UTF8
let journal = Types.Journal.create postings priceDB


(*
    Load Symbol Config
*)

let symbolConfigs = SymbolPrices.loadSymbolConfig configPath
Types.SymbolConfigCollection.prettyPrint symbolConfigs


(*
    Load Prices DB
*)

let priceDB = SymbolPrices.loadSymbolPriceDB pricesPath
Types.SymbolPriceDB.prettyPrint priceDB


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