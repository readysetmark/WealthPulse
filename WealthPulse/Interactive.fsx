
#I "../Journal/bin/Debug"
#r "FSharp.Core.dll"
#r "FParsec.dll"
#r "FParsecCS.dll"
#r "Journal.dll"

open WealthPulse

let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"

let entries = Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII
let journal = Journal.createJournal entries


(*
    Following section is for output commodity usage
*)
let commodityMap = Query.identifyCommodities journal

let streamWriter = new System.IO.StreamWriter(@"C:\Users\Mark\Nexus\Documents\finances\ledger\commodities.txt")
fprintfn streamWriter "%-9s\t%-14s\t%-17s" "Commodity" "First Appeared" "Zero Balance Date"

let displayCommodityMap (sw : System.IO.StreamWriter) (key : Journal.Commodity) (value : Query.CommodityUsage) =
    fprintfn sw "%-9s\t%-14s\t%-17s" (value.Commodity) (value.FirstAppeared.ToString("yyyy/MM/dd")) (if Option.isSome value.ZeroBalanceDate then (Option.get value.ZeroBalanceDate).ToString("yyyy/MM/dd") else "")

Map.iter (displayCommodityMap streamWriter) commodityMap

streamWriter.Close()