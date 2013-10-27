namespace WealthPulse.Journal

open System

module Query =

    type BalanceParameters = {
        AccountsWith: string list option;
        ExcludeAccountsWith: string list option;
        PeriodStart: DateTime option;
        PeriodEnd: DateTime option;
    }


    module private Support =
        
        // one of "terms" is in account
        let oneOfIn defaultValue termsOption (account :string) =
            match termsOption with
            | Some terms -> List.exists (fun (token :string) -> (account.ToLower().Contains(token.ToLower()))) terms
            | None       -> defaultValue

        let withinPeriod date periodStartOption periodEndOption =
            match periodStartOption, periodEndOption with
            | Some periodStart, Some periodEnd -> periodStart <= date && date <= periodEnd
            | Some periodStart, None -> periodStart <= date
            | None, Some periodEnd -> date <= periodEnd
            | _, _ -> true


    open Support

    let balance (journal : JournalData) (parameters : BalanceParameters) =
        // TODO: Not fond of the call to "oneOfIn" needing the default parameter, there must be a better way...
        // filter all accounts to selected accounts
        let accounts = 
            journal.AllAccounts
            |> Set.filter (oneOfIn true parameters.AccountsWith)
            |> Set.filter (oneOfIn false parameters.ExcludeAccountsWith >> not)

        // filter entries based on selected accounts
        let entries = 
            [ for entry in journal.Transactions do 
                for account in entry.AccountLineage do 
                    if (Set.contains entry.Account accounts) && (withinPeriod entry.Header.Date parameters.PeriodStart parameters.PeriodEnd) then 
                        yield(account, fst entry.Amount) 
            ]

        // sum to get account balances
        let accountBalances =
            entries
            |> Seq.ofList
            |> Seq.groupBy fst
            |> Seq.map (fun (account, amounts) ->
                let balance = 
                    amounts
                    |> Seq.map snd
                    |> Seq.sum
                (account, balance))
            |> Seq.toList

        // filter out zero account sums
        let accountBalances = List.filter (fun (account, balance) -> balance <> 0M) accountBalances
        
        accounts




    type BalanceSheetRow = {
        Account: string;
        Balance: string;
        RowClass: string;
        BalanceClass: string;
        AccountStyle: string;
    }

    type BalanceSheetReportData = {
        ReportDate: string;
        AccountBalances: BalanceSheetRow list;
        NetWorthBookValue: string;
    }

    type IncomeStatementRow = {
        Account: string;
        Income: string;
        RowClass: string;
        IncomeClass: string;
        AccountStyle: string;
    }

    type IncomeSheetReportData = {
        ReportStart: string;
        ReportEnd: string;
        AccountIncome: IncomeStatementRow list;
    }


    let generateBalanceReportData (journalData: JournalData) (reportDate: System.DateTime) =
        let assetAccounts = 
            journalData.AllAccounts 
            |> Set.filter (fun (account: string) -> account.StartsWith("Assets") && not(account.Contains("Units")))

        let liabilityAccounts = 
            journalData.AllAccounts 
            |> Set.filter (fun (account: string) -> account.StartsWith("Liabilities"))

        let accounts = Set.union assetAccounts liabilityAccounts

        // get a list of all amounts that apply to each account
        let allAccountAmounts = 
            [ for entry in journalData.Transactions do 
                for account in entry.AccountLineage do 
                    if accounts.Contains(entry.Account) && entry.Header.Date <= reportDate then 
                        yield(account, fst entry.Amount, account = entry.Account) 
            ]
    
        // reduce the list to calculate account balances
        let allAccountBalances = 
            [ for account in accounts ->
                allAccountAmounts
                |> List.map (fun (acc, amnt, _) -> (acc, amnt))
                |> List.filter (fun (acc, amnt) -> account = acc)
                |> List.reduce (fun (acc1, amnt1) (_, amnt2) -> (acc1, amnt1+amnt2)) ]
    
        // filter to non-zero accounts only
        let nonZeroAccountBalances =
            allAccountBalances
            |> List.filter (fun (acc, amnt) -> amnt <> 0M)
    
        // filter parent accounts that only have one child
        let accountBalanceList =
            let rec keep (account, amount) (list: (string * decimal) list) = 
                match list with
                | [] -> true
                | (acc, amnt) :: t when acc.StartsWith(account) && acc.Length > account.Length && amnt = amount -> false
                | (acc, amnt) :: t -> keep (account, amount) t
            [ for tuple in nonZeroAccountBalances do
                if (keep tuple nonZeroAccountBalances) then yield tuple ]

        // calculate total balance
        let totalBalance = 
            allAccountAmounts
            |> List.filter (fun (_, _, useForTotal) -> useForTotal)
            |> List.map (fun (acc, amnt, _) -> (acc, amnt))
            |> List.reduce (fun (_, amnt1) (_, amnt2) -> ("", amnt1+amnt2))
        
        let accountBalances =
            let paddingLeftBase = 8
            let indentPadding = 20
            let getAccountDisplay account =
                let rec accountDisplay list (account: string) (parentage: string) indent =
                    match list with
                    | [] -> ((if parentage.Length > 0 then account.Remove(0, parentage.Length+1) else account), indent)
                    | (acc, _) :: t when account.StartsWith(acc) && account <> acc && account.[acc.Length] = ':' -> accountDisplay t account acc (indent+1)
                    | (acc, _) :: t -> accountDisplay t account parentage indent
                accountDisplay accountBalanceList account "" 0
        
            (accountBalanceList @ [totalBalance])
            |> List.map (fun (acc, amnt) -> 
                let accountDisplay, indent = getAccountDisplay acc
                { Account = accountDisplay; 
                  Balance = amnt.ToString("C"); 
                  RowClass = (if acc = "" then "grand_total" else ""); 
                  BalanceClass = (if acc = "" then "" elif amnt >= 0M then "positive" else "negative"); 
                  AccountStyle = (sprintf "padding-left: %dpx;" (paddingLeftBase+(indent*indentPadding))) })
    

        // Net Worth chart

        let allAmountsForMonth = 
            [ for entry in journalData.Transactions do 
                if accounts.Contains(entry.Account) && entry.Header.Date <= reportDate then yield(DateUtils.getLastOfMonth(entry.Header.Date), fst entry.Amount) ]

        let months = allAmountsForMonth |> List.map (fun (date, _) -> date) |> List.filter (fun date -> date >= reportDate.AddMonths(-25)) |> Set.ofList
    
        let allAmountsByMonth =
            let sb = new System.Text.StringBuilder()
            ignore(sb.Append("["))
            [ for date in months ->
                allAmountsForMonth
                |> List.filter (fun (entryDate, _) -> entryDate <= date)
                |> List.reduce (fun (_, amount1) (_, amount2) -> (date, amount1+amount2)) ]
            |> List.iter (fun (date, amount) ->
                if sb.Length = 1 then ignore(sb.Append("")) else ignore(sb.Append(", "))
                ignore(sb.Append("[\"" + date.ToString("dd-MMM-yyyy") + "\", " + amount.ToString() + "]")))
            ignore(sb.Append("]"))
            sb.ToString()
        
        { ReportDate = reportDate.ToString("MMMM %d, yyyy"); AccountBalances = accountBalances; NetWorthBookValue = allAmountsByMonth }



    let generateIncomeReportData (journalData: JournalData) (reportStart: System.DateTime) (reportEnd: System.DateTime) = 
        let incomeAccounts = journalData.AllAccounts |> Set.filter (fun (account: string) -> account.StartsWith("Income"))
        let expenseAccounts = journalData.AllAccounts |> Set.filter (fun (account: string) -> account.StartsWith("Expense"))
        let accounts = Set.union incomeAccounts expenseAccounts

        // get a list of all amounts that apply to each account
        let allAccountAmounts = 
            [ for entry in journalData.Transactions do 
                for account in entry.AccountLineage do 
                    if accounts.Contains(entry.Account) && reportStart <= entry.Header.Date && entry.Header.Date <= reportEnd then yield(account, fst entry.Amount, account = entry.Account) ]
    
        // reduce the list of accounts to ones that had activity in the report period
        let accounts = Set.ofList [ for (acc, _, _) in allAccountAmounts -> acc ]

        // reduce the list to calculate account totals
        let allAccountTotals = 
            [ for account in accounts ->
                allAccountAmounts
                |> List.map (fun (acc, amnt, _) -> (acc, amnt))
                |> List.filter (fun (acc, amnt) -> account = acc)
                |> List.reduce (fun (acc1, amnt1) (_, amnt2) -> (acc1, amnt1+amnt2)) ]
    
        // filter parent accounts that only have one child
        let accountTotalList =
            let rec keep (account, amount) (list: (string * decimal) list) = 
                match list with
                | [] -> true
                | (acc, amnt) :: t when acc.StartsWith(account) && acc.Length > account.Length && amnt = amount -> false
                | (acc, amnt) :: t -> keep (account, amount) t
            [ for tuple in allAccountTotals do
                if (keep tuple allAccountTotals) then yield tuple ]
    
        let incomeTotalList =
            accountTotalList
            |> List.filter (fun (acc, _) -> acc.StartsWith("Income"))

        let expenseTotalList =
            accountTotalList
            |> List.filter (fun (acc, _) -> acc.StartsWith("Expense"))

        // calculate total
        let total = 
            allAccountAmounts
            |> List.filter (fun (_, _, useForTotal) -> useForTotal)
            |> List.map (fun (acc, amnt, _) -> (acc, amnt))
            |> List.reduce (fun (_, amnt1) (_, amnt2) -> ("", amnt1+amnt2))
        
        let accountTotals =
            let paddingLeftBase = 8
            let indentPadding = 20
            let getAccountDisplay account =
                let rec accountDisplay list (account: string) (parentage: string) indent =
                    match list with
                    | [] -> ((if parentage.Length > 0 then account.Remove(0, parentage.Length+1) else account), indent)
                    | (acc, _) :: t when account.StartsWith(acc) && account <> acc && account.[acc.Length] = ':' -> accountDisplay t account acc (indent+1)
                    | (acc, _) :: t -> accountDisplay t account parentage indent
                accountDisplay accountTotalList account "" 0
        
            (incomeTotalList @ expenseTotalList @ [total])
            |> List.map (fun (acc, amnt) -> 
                let accountDisplay, indent = getAccountDisplay acc
                { Account = accountDisplay; Income = amnt.ToString("C"); RowClass = (if acc = "" then "grand_total" else ""); IncomeClass = (if acc = "" then "" elif amnt >= 0M then "negative" else "positive"); AccountStyle = (sprintf "padding-left: %dpx;" (paddingLeftBase+(indent*indentPadding))) })

        { ReportStart = reportStart.ToString("MMMM %d, yyyy"); ReportEnd = reportEnd.ToString("MMMM %d, yyyy"); AccountIncome = accountTotals }


