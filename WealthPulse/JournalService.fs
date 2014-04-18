namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Utility
open System
open System.IO
open System.Threading

module JournalService =

    /// Interface for Nancy Dependency Injection
    type IJournalService =
        abstract member Journal : Journal
        abstract member OutstandingPayees : (string * decimal) list

    /// Implementation of IJournalService for Nancy Dependency Injection
    type JournalService() =
        let ledgerFilePath = Environment.GetEnvironmentVariable("LEDGER_FILE")

        let mutable journal = None
        let mutable outstandingPayees = None

        let loadJournal () =    
            let ledgerLastModified = File.GetLastWriteTime(ledgerFilePath)
            do printfn "Parsing ledger file: %s" ledgerFilePath
            let (entries, parseTime) = time <| fun () -> Parser.parseJournalFile ledgerFilePath Text.Encoding.ASCII
            do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
            do printfn "Transactions parsed: %d" <| List.length entries
            do printfn "Ledger last modified: %s" <| ledgerLastModified.ToString()
            journal <- Some <| createJournal entries ledgerLastModified
            outstandingPayees <- Some <| Query.outstandingPayees journal.Value
            
        let reloadWhenModified () =
            while true do
                if File.GetLastWriteTime(ledgerFilePath) > journal.Value.LastModified then loadJournal ()
                do Thread.Sleep(5000)

        let watchThread = new Thread(ThreadStart(reloadWhenModified))
        
        do loadJournal ()
        do watchThread.Start()     
            
        interface IJournalService with
            member this.Journal = journal.Value
            member this.OutstandingPayees = outstandingPayees.Value
