﻿namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Journal.Query
open WealthPulse.JournalService

module NancyRunner =

    type NavReport = {
        key: string;
        title: string;
        report: string;
        query: string;
    }

    type NavBar = {
        reports: NavReport list;
    }

    type BalanceSheetRow = {
        key: string;
        account: string;
        accountStyle: Map<string,string>;
        balance: string;
        balanceClass: string;
        rowClass: string;
    }

    type BalanceSheetReportData = {
        title: string;
        subtitle: string;
        balances: BalanceSheetRow list;
    }

    type LineChartPoint = {
        X: string;
        Y: string;
        Hover: string;
    }

    type LineChartReportData = {
        Title: string;
        Data: LineChartPoint list;
    }

    
    /// Access values out of the Nancy.DynamicDictionary
    /// see http://stackoverflow.com/questions/17640218/accessing-dynamic-property-in-f/
    let (?) (parameters:obj) param =
        (parameters :?> Nancy.DynamicDictionary).[param] :?> Nancy.DynamicDictionaryValue


    /// Parse request query values
    let parseQueryParameters (query :obj) defaultTitle =
        let parseArrayVal (arrayVal : Nancy.DynamicDictionaryValue) =
            match arrayVal.HasValue with
            | true -> Some <| List.ofArray (arrayVal.ToString().Split([|' '|]))
            | otherwise -> None

        let parseDateVal (dateVal : Nancy.DynamicDictionaryValue) = 
            match dateVal.HasValue with
            | true -> Some <| System.DateTime.Parse(dateVal.ToString())
            | otherwise -> None

        let titleVal = query?title
        let since = parseDateVal query?since
        let upto = parseDateVal query?upto
        let period = query?period
        let periodStart, periodEnd =
            match period.HasValue with
            | true when period.ToString() = "this month" -> 
                (Some <| DateUtils.getFirstOfMonth System.DateTime.Today, Some <| DateUtils.getLastOfMonth System.DateTime.Today)
            | true when period.ToString() = "last month" -> 
                (Some <| DateUtils.getFirstOfMonth (System.DateTime.Today.AddMonths(-1)), Some <| DateUtils.getLastOfMonth (System.DateTime.Today.AddMonths(-1)))
            | false -> None, None
            | otherwise -> failwith ("Invalid period parameter: " + period.ToString())
        {
            AccountsWith = parseArrayVal query?accountsWith;
            ExcludeAccountsWith = parseArrayVal query?excludeAccountsWith;
            PeriodStart =
                match since, periodStart with
                | Some _, _ -> since
                | _, Some _ -> periodStart
                | _, _ -> None;
            PeriodEnd =
                match upto, periodEnd with
                | Some _, _ -> upto
                | _, Some _ -> periodEnd
                | _, _ -> None;
        },
        match titleVal.HasValue with
        | true -> titleVal.ToString()
        | otherwise -> defaultTitle
           



    let layoutBalanceData (accountBalances, totalBalance) =
        let paddingLeftBase = 8
        let indentPadding = 20
        let getAccountDisplay account =
            let rec accountDisplay list (account: string) (parentage: string) indent =
                match list with
                | [] -> ((if parentage.Length > 0 then account.Remove(0, parentage.Length+1) else account), indent)
                | (acc, _) :: t when account.StartsWith(acc) && account <> acc && account.[acc.Length] = ':' -> accountDisplay t account acc (indent+1)
                | (acc, _) :: t -> accountDisplay t account parentage indent
            accountDisplay accountBalances account "" 0
        let accountBalances =
            List.sortBy fst accountBalances

        (accountBalances @ [("", totalBalance)])
        |> List.map (fun (account, (amount : decimal)) -> 
            let accountDisplay, indent = getAccountDisplay account
            { key = account;
              account = accountDisplay; 
              accountStyle = Map.ofArray [|("padding-left", (sprintf "%dpx" (paddingLeftBase+(indent*indentPadding))))|]; 
              balance = amount.ToString("C");
              balanceClass = account.Split([|':'|]).[0].ToLower();
              rowClass = 
                match account with
                | "" -> "grand_total"
                | otherwise -> ""; })
              
    

    let generateNetWorthData journalData =
        let generatePeriodBalance month =
            let parameters = {
                AccountsWith = Some ["assets"; "liabilities"];
                ExcludeAccountsWith = Some ["units"];
                PeriodStart = None;
                PeriodEnd = Some (DateUtils.getLastOfMonth(month));
            }
            let _, totalBalance = Query.balance parameters journalData
            {
                X = month.ToString("dd-MMM-yyyy"); 
                Y = totalBalance.ToString(); 
                Hover = month.ToString("MMM yyyy") + ": " + totalBalance.ToString("C");
            }

        let firstMonth = DateUtils.getFirstOfMonth(System.DateTime.Today).AddMonths(-25)
        let months = seq { for i in 0 .. 25 do yield firstMonth.AddMonths(i) }

        let netWorthData =
            months
            |> Seq.map generatePeriodBalance
            |> Seq.toList

        netWorthData


    type WealthPulseModule(journalService : IJournalService) as this =
        inherit Nancy.NancyModule()
        let journalService = journalService

        do this.Get.["/"] <-
            fun parameters ->
                this.View.["index.html"] |> box

        
        do this.Get.["/api/nav"] <-
            fun parameters ->
                let nav = {
                    reports = [{ key = "Balance Sheet";
                                 title = "Balance Sheet";
                                 report = "balance";
                                 query = "accountsWith=assets+liabilities&excludeAccountsWith=units"; };
                               { key = "Net Worth";
                                 title = "Net Worth";
                                 report= "networth";
                                 query = ""; };
                               { key = "Income Statement - Current Month";
                                 title = "Income Statement - Current Month";
                                 report = "balance";
                                 query = "accountsWith=income+expenses&period=this+month&title=Income+Statement"; };
                               { key = "Income Statement - Previous Month";
                                 title = "Income Statement - Previous Month";
                                 report = "balance";
                                 query = "accountsWith=income+expenses&period=last+month&title=Income+Statement"; }];
                }
                nav |> box

        
        do this.Get.["/api/balance"] <-
            fun parameters ->
                let dateFormat = "MMMM %d, yyyy"
                let balanceParameters, title = parseQueryParameters this.Request.Query "Balance"

                let subtitle =
                    match balanceParameters.PeriodStart, balanceParameters.PeriodEnd with
                    | Some periodStart, Some periodEnd -> "For the period of " + periodStart.ToString(dateFormat) + " to " + periodEnd.ToString(dateFormat)
                    | Some periodStart, None -> "Since " + periodStart.ToString(dateFormat)
                    | _, Some periodEnd -> "Up to " + periodEnd.ToString(dateFormat)
                    | _, _ -> "As of " + System.DateTime.Now.ToString(dateFormat)

                let balanceSheetData = {
                    title = title;
                    subtitle = subtitle;
                    balances = layoutBalanceData <| Query.balance balanceParameters journalService.Journal;
                }
                balanceSheetData |> box


        do this.Get.["/api/networth"] <-
            fun parameters ->
                let netWorthData = {
                    Title = "Net Worth";
                    Data = generateNetWorthData journalService.Journal;
                }
                netWorthData |> box



    let run =
        let url = "http://localhost:5050"
        let configuration = new Nancy.Hosting.Self.HostConfiguration()
        configuration.UrlReservations.CreateAutomatically <- true
        let nancyHost = new Nancy.Hosting.Self.NancyHost(configuration, new System.Uri(url))
        nancyHost.Start()
        printfn "WealthPulse server running at %s" url
        printfn "Press <enter> to stop."
        System.Console.ReadLine() |> ignore
        nancyHost.Stop()
