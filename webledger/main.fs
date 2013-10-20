module Webledger

open Webledger.Journal


let time f =
    let start = System.DateTime.Now
    let res = f()
    let finish = System.DateTime.Now
    (res, finish - start)

let parse file encoding =
    time (fun () -> Parser.parseJournalFile file encoding)
    //Parser.parseJournalFile file encoding

//let main ledgerFilePath path =
//    let journal = parse ledgerFilePath System.Text.Encoding.ASCII
//    let reportList = Report.generateAllReports journalData path
//    reportList
    

//let ledgerFilePath = @"C:\Users\Mark\Nexus\Development\ledger\healthycoffers-fs\templates\stan.dat"
let ledgerFilePath = @"C:\Users\Mark\Nexus\Documents\finances\ledger\ledger.dat"
let path = @"C:\Users\Mark\Nexus\Development\ledger\healthycoffers-fs\"

//let reportList = main ledgerFilePath path

let journal, timeSpan = parse ledgerFilePath System.Text.Encoding.ASCII
printfn "Parsed ledger file in %A seconds." timeSpan.TotalSeconds

