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

    type BalanceSheetRow = {
        Account: string;
        Balance: string;
        RowClass: string;
        BalanceClass: string;
        AccountStyle: string;
    }

    type BalanceSheetReportData = {
        Title: string;
        Subtitle: string;
        AccountBalances: BalanceSheetRow list;
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
            { Account = accountDisplay; 
                Balance = amount.ToString("C"); 
                RowClass = (if account = "" then "grand_total" else ""); 
                BalanceClass = (if account = "" then "" elif amount >= 0M then "positive" else "negative"); 
                AccountStyle = (sprintf "padding-left: %dpx;" (paddingLeftBase+(indent*indentPadding))) })
    

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
                let balance = { LinkTitle = "Balance Sheet"; LinkURL = "/balance"; }
                let networth = { LinkTitle = "Net Worth Chart"; LinkURL = "/networth"; }
                let currentIncomeStatement = { LinkTitle = "Income Statement - Current Month"; LinkURL = "/currentincomestatement"; }
                let previousIncomeStatement = { LinkTitle = "Income Statement - Previous Month"; LinkURL = "/previousincomestatement"; }
                let data = { IndexLinks = [balance; networth; currentIncomeStatement; previousIncomeStatement]; }
                data |> box

        do this.Get.["/api/balance"] <-
            fun parameters ->
                let balanceSheetParameters = {
                    AccountsWith = Some ["assets"; "liabilities"]; 
                    ExcludeAccountsWith = Some ["units"]; 
                    PeriodStart = None; 
                    PeriodEnd = None;
                }
                let balanceSheetData = {
                    Title = "Balance Sheet";
                    Subtitle = "As of " + System.DateTime.Now.ToString("MMMM %d, yyyy");
                    AccountBalances = layoutBalanceData <| Query.balance balanceSheetParameters journalService.Journal;
                }
                balanceSheetData |> box

        do this.Get.["/api/networth"] <-
            fun parameters ->
                let netWorthData = {
                    Title = "Net Worth";
                    Data = generateNetWorthData journalService.Journal;
                }
                netWorthData |> box

        do this.Get.["/api/currentincomestatement"] <-
            fun parameters ->
                let currentMonthIncomeStatementParameters = {
                    AccountsWith = Some ["income"; "expenses"]; 
                    ExcludeAccountsWith = None; 
                    PeriodStart = Some (DateUtils.getFirstOfMonth System.DateTime.Today); 
                    PeriodEnd = Some (DateUtils.getLastOfMonth System.DateTime.Today);
                }
                let currentMonthIncomeStatementData = {
                    Title = "Income Statement";
                    Subtitle = "For period of " + currentMonthIncomeStatementParameters.PeriodStart.Value.ToString("MMMM %d, yyyy") + " to " + currentMonthIncomeStatementParameters.PeriodEnd.Value.ToString("MMMM %d, yyyy");
                    AccountBalances = layoutBalanceData <| Query.balance currentMonthIncomeStatementParameters journalService.Journal;
                }
                currentMonthIncomeStatementData |> box

        do this.Get.["/api/previousincomestatement"] <-
            fun parameters ->
                let previousMonthIncomeStatementParameters = {
                    AccountsWith = Some ["income"; "expenses"]; 
                    ExcludeAccountsWith = None; 
                    PeriodStart = Some (DateUtils.getFirstOfMonth (System.DateTime.Today.AddMonths(-1))); 
                    PeriodEnd = Some (DateUtils.getLastOfMonth (System.DateTime.Today.AddMonths(-1)));
                }
                let previousMonthIncomeStatementData = {
                    Title = "Income Statement";
                    Subtitle = "For period of " + previousMonthIncomeStatementParameters.PeriodStart.Value.ToString("MMMM %d, yyyy") + " to " + previousMonthIncomeStatementParameters.PeriodEnd.Value.ToString("MMMM %d, yyyy");
                    AccountBalances = layoutBalanceData <| Query.balance previousMonthIncomeStatementParameters journalService.Journal;
                }
                previousMonthIncomeStatementData |> box


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
