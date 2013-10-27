
#I "../Journal/bin/Debug"
#r "FSharp.Core.dll"
#r "FParsec.dll"
#r "FParsecCS.dll"
#r "Journal.dll"

open WealthPulse.Journal

let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"

let journal = Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII

