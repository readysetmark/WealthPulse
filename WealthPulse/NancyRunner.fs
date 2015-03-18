namespace WealthPulse

open Journal
open Journal.Types
open Journal.Query
open WealthPulse.JournalService

module NancyRunner =

    type NavCommand = {
        report: string;
        query: string;
    }

    type NavPayee = {
        payee: string;
        command: NavCommand;
        amount: string;
        amountClass: string;
    }

    type NavReport = {
        key: string;
        title: string;
        report: string;
        query: string;
    }

    type NavBar = {
        reports: NavReport list;
        payees: NavPayee list;
        journalLastModified: string;
        exceptionMessage: string;
    }

    type BalanceSheetRow = {
        key: string;
        account: string;
        accountStyle: Map<string,string>;
        balance: string list;
        balanceClass: string;
        basisBalance: string list;
        commodityBalance: string;
        price: string;
        priceDate: string;
        rowClass: string;
    }

    type BalanceSheetReportData = {
        title: string;
        subtitle: string;
        balances: BalanceSheetRow list;
    }

    type RegisterEntry = {
        account: string;
        amount: string;
        total: string;
    }

    type RegisterTransaction = {
        date: string;
        payee: string;
        entries: RegisterEntry list;
    }

    type RegisterReportData = {
        title: string;
        subtitle: string;
        register: RegisterTransaction list;
    }

    type LineChartPoint = {
        date: string;
        amount: string;
        hover: string;
    }

    type LineChartReportData = {
        title: string;
        data: LineChartPoint list;
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
           

    /// Generate a subtitle based on query parameter period
    let generateSubtitle queryParameters =
        let dateFormat = "MMMM %d, yyyy"
        match queryParameters.PeriodStart, queryParameters.PeriodEnd with
        | Some periodStart, Some periodEnd -> "For the period of " + periodStart.ToString(dateFormat) + " to " + periodEnd.ToString(dateFormat)
        | Some periodStart, None -> "Since " + periodStart.ToString(dateFormat)
        | _, Some periodEnd -> "Up to " + periodEnd.ToString(dateFormat)
        | _, _ -> "As of " + System.DateTime.Now.ToString(dateFormat)

    
    /// Format an Amount type with the amount and symbol
    let formatAmount (amount :Amount option) =
        match amount with
        | Some amount ->
            let numberFormat = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.Clone() :?> System.Globalization.NumberFormatInfo
            match amount.Symbol with
            | Some s when s.Value <> "$" ->
                numberFormat.CurrencyPositivePattern <- 3  // n $
                numberFormat.CurrencyNegativePattern <- 15 // (n $)
                numberFormat.CurrencySymbol <- s.Value
                numberFormat.CurrencyDecimalDigits <- 3
            | otherwise -> ()
            amount.Amount.ToString("C", numberFormat)
        | otherwise -> ""


    /// Transform balance report data for presentation
    let presentBalanceData (accountBalances, (totalBalance, totalBasisBalance)) =
        let paddingLeftBase = 8
        let indentPadding = 20
        let getAccountDisplay account =
            let rec accountDisplay list (account: string) (parentage: string) indent =
                match list with
                | [] -> ((if parentage.Length > 0 then account.Remove(0, parentage.Length+1) else account), indent)
                | accB :: t when account.StartsWith(accB.Account) && account <> accB.Account && account.[accB.Account.Length] = ':' -> accountDisplay t account accB.Account (indent+1)
                | _ :: t -> accountDisplay t account parentage indent
            accountDisplay accountBalances account "" 0
        let accountBalances =
            List.sortBy (fun a -> a.Account) accountBalances

        (accountBalances @ [{Account=""; Balance=totalBalance; Basis=totalBasisBalance; Commodity=None; Price=None; PriceDate=None;}])
        |> List.map (fun accountBalance -> 
            let accountDisplay, indent = getAccountDisplay accountBalance.Account
            { key = accountBalance.Account;
              account = accountDisplay; 
              accountStyle = Map.ofArray [|("padding-left", (sprintf "%dpx" (paddingLeftBase+(indent*indentPadding))))|]; 
              balance = List.map (Some >> formatAmount) accountBalance.Balance//formatAmount <| Some accountBalance.Balance
              balanceClass = accountBalance.Account.Split([|':'|]).[0].ToLower();
              basisBalance = List.map (Some >> formatAmount) accountBalance.Basis;
              commodityBalance = formatAmount accountBalance.Commodity;
              price = formatAmount accountBalance.Price;
              priceDate = match accountBalance.PriceDate with
                          | Some priceDate -> priceDate.ToString("dd-MMM-yyyy")
                          | otherwise -> ""
              rowClass = 
                match accountBalance.Account with
                | "" -> "grand_total"
                | otherwise -> ""; })
    
    
    /// Transform register report data for presentation
    let presentRegisterData transactions =
        let presentEntry (account, amount :decimal, total :decimal) = 
            {account = account; amount = amount.ToString("C"); total = total.ToString("C")}
        let presentTransaction (date :System.DateTime, payee, entries) = 
            {date = date.ToString("yyyy-MM-dd"); payee = payee; entries = List.map presentEntry entries}
        transactions
        |> List.map presentTransaction
    

    let generateNetWorthData journalData symbolPriceDB =
        let generatePeriodBalance month =
            let parameters = {
                AccountsWith = Some ["assets"; "liabilities"];
                ExcludeAccountsWith = Some ["units"];
                PeriodStart = None;
                PeriodEnd = Some (DateUtils.getLastOfMonth(month));
            }
            let _, (totalBalance, totalRealBalance) = Query.balance parameters journalData symbolPriceDB
            let dollarAmount = (List.find (fun (a:Amount) -> a.Symbol = Some({Value="$"; Quoted=false})) totalBalance)
            {
                date = month.ToString("dd-MMM-yyyy"); 
                amount = dollarAmount.Amount.ToString();//totalBalance.Amount.ToString(); 
                hover = month.ToString("MMM yyyy") + ": " + (formatAmount <| Some dollarAmount);
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
                let presentPayee (payee : string, amount : decimal) =
                    let command = {report = "register"; query = "accountsWith=assets%3Areceivables%3A" + payee.ToLower() + "+liabilities%3Apayables%3A" + payee.ToLower();} : NavCommand
                    {payee = payee; command = command; amount = amount.ToString("C"); amountClass = if amount >= 0M then "positive" else "negative";}
                let nav = {
                    reports = [{ key = "Balance Sheet";
                                 title = "Balance Sheet";
                                 report = "balance";
                                 query = "accountsWith=assets+liabilities"; };
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
                    payees = List.map presentPayee journalService.OutstandingPayees;
                    journalLastModified = journalService.JournalLastModified.ToString();
                    exceptionMessage = 
                        match journalService.GetAndClearException with
                        | Some msg -> msg
                        | None -> null;
                }
                nav |> box

        
        do this.Get.["/api/balance"] <-
            fun parameters ->
                let queryParameters, title = parseQueryParameters this.Request.Query "Balance"
                let balanceSheetData = {
                    title = title;
                    subtitle = generateSubtitle queryParameters;
                    balances = presentBalanceData <| Query.balance queryParameters journalService.Journal journalService.SymbolPriceDB;
                }
                balanceSheetData |> box

        
        do this.Get.["/api/register"] <-
            fun parameters ->
                let queryParameters, title = parseQueryParameters this.Request.Query "Register"
                let registerData = {
                    title = title;
                    subtitle = generateSubtitle queryParameters;
                    register = presentRegisterData <| Query.register queryParameters journalService.Journal;
                }
                registerData |> box


        do this.Get.["/api/networth"] <-
            fun parameters ->
                let netWorthData = {
                    title = "Net Worth";
                    data = generateNetWorthData journalService.Journal journalService.SymbolPriceDB;
                }
                netWorthData |> box



    let run =
        let url = "http://localhost:5055"
        Nancy.Json.JsonSettings.MaxJsonLength <- System.Int32.MaxValue  // increase max JSON response length (default is 100 kb)
        let configuration = new Nancy.Hosting.Self.HostConfiguration()
        configuration.UrlReservations.CreateAutomatically <- true
        let nancyHost = new Nancy.Hosting.Self.NancyHost(configuration, new System.Uri(url))
        nancyHost.Start()
        printfn "WealthPulse server running at %s" url
