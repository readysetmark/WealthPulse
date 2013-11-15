namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Utility

module Main =

    let parse file encoding =
        time (fun () -> Parser.parseJournalFile file encoding)
        //Parser.parseJournalFile file encoding

    let main ledgerFilePath path =
        do printfn "Parsing ledger file: %s" ledgerFilePath
        let (journal, parseTime) = parse ledgerFilePath System.Text.Encoding.ASCII
        do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
        
        let reportList = WealthPulse.StaticRunner.generateAllReports journal path
        do printfn "Generated reports: %A" reportList
        
    //let ledgerFilePath = @"C:\Users\Mark\Nexus\Development\ledger\WealthPulse\templates\stan.dat"
    //let ledgerFilePath = System.Environment.GetEnvironmentVariable("LEDGER_FILE")
    //let path = @"C:\Users\Mark\Nexus\Development\ledger\WealthPulse\"

    //do main ledgerFilePath path
    NancyRunner.run
