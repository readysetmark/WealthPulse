namespace WealthPulse

open WealthPulse.Journal

module JournalService =

    /// Interface for Nancy Dependency Injection
    type IJournalService =
        abstract member Journal: JournalData


    type JournalService() =
        let mutable journal = None

        interface IJournalService with
            member this.Journal =
                match journal with
                | Some journal -> journal
                | none -> 
                    let ledgerFilePath = System.Environment.GetEnvironmentVariable("LEDGER_FILE")
                    do 
                        printfn "Parsing ledger file: %s" ledgerFilePath
                        journal <- Some (Parser.parseJournalFile ledgerFilePath System.Text.Encoding.ASCII)
                    journal.Value