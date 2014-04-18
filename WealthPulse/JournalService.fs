namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Utility

module JournalService =

    /// Interface for Nancy Dependency Injection
    type IJournalService =
        abstract member Journal : Journal
        abstract member OutstandingPayees : (string * decimal) list

    /// Implementation of IJournalService for Nancy Dependency Injection
    type JournalService() =
        let journal = 
            let ledgerFilePath = System.Environment.GetEnvironmentVariable("LEDGER_FILE")
            let ledgerLastModified = System.IO.File.GetLastWriteTime(ledgerFilePath)
            do printfn "Parsing ledger file: %s" ledgerFilePath
            let (entries, parseTime) = time <| fun () -> Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII
            do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
            do printfn "Transactions parsed: %d" <| List.length entries
            do printfn "Ledger last modified: %s" <| ledgerLastModified.ToString()
            createJournal entries ledgerLastModified
            

        let outstandingPayees =
            Query.outstandingPayees journal

        interface IJournalService with
            member this.Journal = journal
            member this.OutstandingPayees = outstandingPayees
