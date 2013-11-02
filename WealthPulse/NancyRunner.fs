namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Journal.Query

module NancyRunner =

    type WealthPulseModule() as this =
        inherit Nancy.NancyModule()

        do this.Get.["/"] <-
            fun parameters -> "Hello from Nancy." |> box


    let run journalData =
        let configuration = new Nancy.Hosting.Self.HostConfiguration()
        configuration.UrlReservations.CreateAutomatically <- true
        let nancyHost = new Nancy.Hosting.Self.NancyHost(configuration, new System.Uri("http://localhost:5050"))
        nancyHost.Start()
        printfn "WealthPulse server running at http://localhost:5050."
        printfn "Press <enter> to stop."
        System.Console.ReadLine() |> ignore
        nancyHost.Stop()
