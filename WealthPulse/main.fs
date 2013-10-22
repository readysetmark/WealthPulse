namespace WealthPulse

open WealthPulse.Journal

module Main =

    let time f =
        let start = System.DateTime.Now
        let res = f()
        let finish = System.DateTime.Now
        (res, finish - start)

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
    let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"
    let path = @"C:\Users\Mark\Nexus\Development\ledger\WealthPulse\"

    do main ledgerFilePath path
