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
        abstract member LastModified : DateTime
        abstract member GetAndClearException : string option

    /// Implementation of IJournalService for Nancy Dependency Injection
    type JournalService() =
        //let ledgerFilePath = Environment.GetEnvironmentVariable("LEDGER_FILE")
        let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\test_investments.dat"
        let rwlock = new ReaderWriterLock()

        let mutable journal = createJournal List.empty
        let mutable outstandingPayees = List.empty
        let mutable lastModified = File.GetLastWriteTime(ledgerFilePath)
        let mutable exceptionMessage = None

        let loadJournal () =
            let ledgerLastModified = File.GetLastWriteTime(ledgerFilePath)
            do printfn "Parsing ledger file: %s" ledgerFilePath
            try
                let (entries, parseTime) = time <| fun () -> Parser.parseJournalFile ledgerFilePath Text.Encoding.ASCII
                do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
                do printfn "Transactions parsed: %d" <| List.length entries
                do printfn "Ledger last modified: %s" <| ledgerLastModified.ToString()
                rwlock.AcquireWriterLock(Timeout.Infinite)
                try
                    journal <- createJournal entries
                    outstandingPayees <- Query.outstandingPayees journal
                    lastModified <- ledgerLastModified
                    exceptionMessage <- None
                finally
                    rwlock.ReleaseWriterLock()
            with
                ex -> 
                    do printfn "Error parsing ledger: %s" ex.Message
                    rwlock.AcquireWriterLock(Timeout.Infinite)
                    try
                        lastModified <- ledgerLastModified
                        exceptionMessage <- Some ex.Message
                    finally
                        rwlock.ReleaseWriterLock()
            
        let reloadWhenModified () =
            while true do
                if File.GetLastWriteTime(ledgerFilePath) > lastModified then loadJournal ()
                do Thread.Sleep(5000)

        let watchThread = new Thread(ThreadStart(reloadWhenModified))
        
        do loadJournal ()
        do watchThread.Start()     
            
        interface IJournalService with
            member this.Journal = 
                rwlock.AcquireReaderLock(Timeout.Infinite)
                try
                    journal
                finally
                    rwlock.ReleaseReaderLock()

            member this.OutstandingPayees = 
                rwlock.AcquireReaderLock(Timeout.Infinite)
                try
                    outstandingPayees
                finally
                    rwlock.ReleaseReaderLock()

            member this.LastModified = 
                rwlock.AcquireReaderLock(Timeout.Infinite)
                try
                    lastModified
                finally
                    rwlock.ReleaseReaderLock()

            member this.GetAndClearException =
                rwlock.AcquireWriterLock(Timeout.Infinite)
                try
                    let msg = exceptionMessage
                    exceptionMessage <- None
                    msg
                finally
                    rwlock.ReleaseWriterLock()
