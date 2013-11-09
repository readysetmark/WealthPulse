//namespace WealthPulse
//
//open WealthPulse.Journal
//
//module Main =
//
//    let time f =
//        let start = System.DateTime.Now
//        let res = f()
//        let finish = System.DateTime.Now
//        (res, finish - start)
//
//    let parse file encoding =
//        time (fun () -> Parser.parseJournalFile file encoding)
//        //Parser.parseJournalFile file encoding
//
//    let main ledgerFilePath path =
//        do printfn "Parsing ledger file: %s" ledgerFilePath
//        let (journal, parseTime) = parse ledgerFilePath System.Text.Encoding.ASCII
//        do printfn "Parsed ledger file in %A seconds." parseTime.TotalSeconds
//        
//        let reportList = WealthPulse.StaticRunner.generateAllReports journal path
//        do printfn "Generated reports: %A" reportList
//        
//    //let ledgerFilePath = @"C:\Users\Mark\Nexus\Development\ledger\WealthPulse\templates\stan.dat"
//    //let ledgerFilePath = System.Environment.GetEnvironmentVariable("LEDGER_FILE")
//    //let path = @"C:\Users\Mark\Nexus\Development\ledger\WealthPulse\"
//
//    //do main ledgerFilePath path
//    NancyRunner.run


module NancySimple

type Hello = {
    Name: string;
}

type WebServerModule() as this =
    inherit Nancy.NancyModule()

    do this.Get.["/"] <- 
        fun parameters ->
            //Nustache.Core.Render.FileToString("hello.nustache", {Name="Smith"}) |> box
            this.View.["hello.nustache", {Name="Smith"}] |> box


let configuration = new Nancy.Hosting.Self.HostConfiguration()
configuration.UrlReservations.CreateAutomatically <- true
let nancyHost = new Nancy.Hosting.Self.NancyHost(configuration, new System.Uri("http://localhost:1234"))
nancyHost.Start()
System.Console.ReadLine() |> ignore
nancyHost.Stop()

