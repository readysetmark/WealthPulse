
#I "../Journal/bin/Debug"
#r "FSharp.Core.dll"
#r "FParsec.dll"
#r "FParsecCS.dll"
#r "Journal.dll"

open WealthPulse

let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"

let entries = Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII
let journal = Journal.createJournal entries

