namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Journal.Query
open WealthPulse.JournalService

module NancyRunner =

    type IndexLink = {
        LinkTitle: string;
        LinkURL: string;
    }

    type IndexData = {
        IndexLinks: IndexLink list;
    }

    type Hello = {
        Name: string;
    }

    type WealthPulseModule(journalService : IJournalService) as this =
        inherit Nancy.NancyModule()
        let journalService = journalService

        do this.Get.["/"] <-
            fun parameters ->
                this.View.["hello.nustache", {Name = "Nancy"}] |> box
//                let balance = { LinkTitle = "Balance Sheet"; LinkURL = "/balance"; }
//                let networth = { LinkTitle = "Net Worth Chart"; LinkURL = "/networth"; }
//                let currentIncomeStatement = { LinkTitle = "Income Statement - Current Month"; LinkURL = "/currentincomestatement"; }
//                let previousIncomeStatement = { LinkTitle = "Income Statement - Previous Month"; LinkURL = "/previousincomestatement"; }
//                let data = { IndexLinks = [balance; networth; currentIncomeStatement; previousIncomeStatement]; }
//                this.View.["index", data] |> box


    let run =
        let configuration = new Nancy.Hosting.Self.HostConfiguration()
        configuration.UrlReservations.CreateAutomatically <- true
        let nancyHost = new Nancy.Hosting.Self.NancyHost(configuration, new System.Uri("http://localhost:5050"))
        nancyHost.Start()
        printfn "WealthPulse server running at http://localhost:5050."
        printfn "Press <enter> to stop."
        System.Console.ReadLine() |> ignore
        nancyHost.Stop()
