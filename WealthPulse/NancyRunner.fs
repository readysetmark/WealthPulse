namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Journal.Query
open WealthPulse.JournalService

module NancyRunner =

    type NavLink = {
        Title: string;
        URL: string;
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

    type QueryParametersParserState =
        | Accounts
        | ExcludeAccounts
        | Period
        | Since
        | Upto
        | Title

    type QueryParametersParseState = {
        State: QueryParametersParserState;
        Accounts: string list;
        ExcludeAccounts: string list;
        Period: string list;
        Since: string list;
        Upto: string list;
        Title: string list;
    }

    /// Parse "parameters" query value
    /// TODO: this is "rosy path"
    let parseQueryParameters (parameters :string) =
        let parseDate = 
            function
            | "" -> None
            | s -> Some <| System.DateTime.Parse(s)

        let parseDatePipeline = List.rev >> String.concat " " >> parseDate

        let getNextState (term :string) =
            match term.ToLower() with
            | ":exclude" -> ExcludeAccounts
            | ":period" -> Period
            | ":since" -> Since
            | ":upto" -> Upto
            | ":title" -> Title
            | otherwise -> failwith ("Unsupported parameter: " + term)

        let accumulateParameters state (term :string) = 
            match state.State, term with
            | Accounts, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | Accounts, t -> {state with Accounts = t :: state.Accounts}
            | ExcludeAccounts, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | ExcludeAccounts, t -> {state with ExcludeAccounts = t :: state.ExcludeAccounts}
            | Period, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | Period, t -> {state with Period = t :: state.Period}
            | Since, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | Since, t -> {state with Since = t :: state.Since}
            | Upto, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | Upto, t -> {state with Upto = t :: state.Upto}
            | Title, t when t.StartsWith(":") -> {state with State = getNextState <| t}
            | Title, t -> {state with Title = t :: state.Title}

        let words = List.ofArray <| parameters.Split([|' '|])
        let starting_state = {State = Accounts; Accounts = []; ExcludeAccounts = []; Period = []; Since = []; Upto = []; Title = [];}
        let state = List.fold accumulateParameters starting_state words
        let title = state.Title |> List.rev |> String.concat " "
        let since = parseDatePipeline state.Since
        let upto = parseDatePipeline state.Upto
        let period = state.Period |> List.rev |> String.concat " " 
        let periodStart, periodEnd =
            match period.ToLower() with
            | "this month" -> (Some <| DateUtils.getFirstOfMonth System.DateTime.Today, Some <| DateUtils.getLastOfMonth System.DateTime.Today)
            | "last month" -> (Some <| DateUtils.getFirstOfMonth (System.DateTime.Today.AddMonths(-1)), Some <| DateUtils.getLastOfMonth (System.DateTime.Today.AddMonths(-1)))
            | "" -> None, None
            | otherwise -> failwith ("Invalid period parameter: " + period)
        {
            AccountsWith = if List.isEmpty state.Accounts then None else Some <| List.rev state.Accounts;
            ExcludeAccountsWith = if List.isEmpty state.ExcludeAccounts then None else Some <| List.rev state.ExcludeAccounts;
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
        }, if System.String.Empty = title then None else Some title



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

    /// Access values out of the Nancy.DynamicDictionary
    /// see http://stackoverflow.com/questions/17640218/accessing-dynamic-property-in-f/
    let (?) (parameters:obj) param =
        (parameters :?> Nancy.DynamicDictionary).[param]
    

    type WealthPulseModule(journalService : IJournalService) as this =
        inherit Nancy.NancyModule()
        let journalService = journalService

        do this.Get.["/"] <-
            fun parameters ->
                this.View.["index.html"] |> box

        do this.Get.["/api/nav"] <-
            fun parameters ->
                let balance = { Title = "Balance Sheet"; URL = "#/balance"; }
                let networth = { Title = "Net Worth Chart"; URL = "#/networth"; }
                let currentIncomeStatement = { Title = "Income Statement - Current Month"; URL = "#/currentincomestatement"; }
                let previousIncomeStatement = { Title = "Income Statement - Previous Month"; URL = "#/previousincomestatement"; }
                [balance; networth; currentIncomeStatement; previousIncomeStatement] |> box

        do this.Get.["/api/balance"] <-
            fun parameters ->
                let queryParameterValue = this.Request.Query?parameters :?> Nancy.DynamicDictionaryValue

                let queryParameters = 
                    match queryParameterValue.HasValue with
                    | true -> Some <| parseQueryParameters (queryParameterValue.ToString())
                    | otherwise -> None

                do printfn "queryParameters = %A" queryParameters

                let balanceSheetParameters = 
                    match queryParameters with
                    | Some (parameters, _) -> parameters
                    | None -> {AccountsWith = None; ExcludeAccountsWith = None; PeriodStart = None; PeriodEnd = None;}

                do printfn "balanceSheetParameters = %A" balanceSheetParameters

                let title =
                    match queryParameters with
                    | Some (_, Some title) -> title
                    | _ -> "Balance"

                let dateFormat = "MMMM %d, yyyy"
                let subtitle =
                    match balanceSheetParameters.PeriodStart, balanceSheetParameters.PeriodEnd with
                    | Some periodStart, Some periodEnd -> "For the period of " + periodStart.ToString(dateFormat) + " to " + periodEnd.ToString(dateFormat)
                    | Some periodStart, None -> "Since " + periodStart.ToString(dateFormat)
                    | _, Some periodEnd -> "Up to " + periodEnd.ToString(dateFormat)
                    | _, _ -> "As of " + System.DateTime.Now.ToString(dateFormat)

                let balanceSheetData = {
                    Title = title;
                    Subtitle = subtitle;
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
