namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Utility

module JournalService =

    /// Interface for Nancy Dependency Injection
    type IJournalService =
        abstract member Journal : Journal

    /// Implementation of IJournalService for Nancy Dependency Injection
    type JournalService() =
        let journal = 
            let ledgerFilePath = System.Environment.GetEnvironmentVariable("LEDGER_FILE")
            do printfn "Parsing ledger file: %s" ledgerFilePath
            let (j, parseTime) = time <| fun () -> Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII
            do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
            do printfn "Transactions parsed: %d" (List.length j.Entries)
            j

        interface IJournalService with
            member this.Journal = journal
