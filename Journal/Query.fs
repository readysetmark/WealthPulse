namespace WealthPulse

open System
open Journal

module Query =

    type CommodityAmountMap = System.Collections.Generic.Dictionary<Commodity option,Amount>

    type QueryFilters = {
        AccountsWith: string list option;
        ExcludeAccountsWith: string list option;
        PeriodStart: DateTime option;
        PeriodEnd: DateTime option;
    }

    type AccountBalance = {
        Account: string;
        Balance: Amount list
    }

    type CommodityUsage = {
        Commodity: Commodity;
        FirstAppeared: System.DateTime;
        ZeroBalanceDate: System.DateTime option;
    } 

    module private Support =
        
        // account contains one of "terms" if "terms" provided, otherwise defaultValue
        let containsOneOf defaultValue termsOption (account :string) =
            match termsOption with
            | Some terms -> List.exists (fun (token :string) -> (account.ToLower().Contains(token.ToLower()))) terms
            | None       -> defaultValue

        
        let withinPeriod date periodStartOption periodEndOption =
            match periodStartOption, periodEndOption with
            | Some periodStart, Some periodEnd -> periodStart <= date && date <= periodEnd
            | Some periodStart, None -> periodStart <= date
            | None, Some periodEnd -> date <= periodEnd
            | _, _ -> true


        /// Add an amount to a CommodityAmountMap
        let addAmountForCommodity (caMap : CommodityAmountMap) (amount : Amount) =
            match caMap.ContainsKey(amount.Commodity) with
            | true -> caMap.[amount.Commodity] <- {Amount = caMap.[amount.Commodity].Amount + amount.Amount;
                                                    Commodity = amount.Commodity}
            | false -> caMap.[amount.Commodity] <- amount
        

        /// Apply filters to retrieve journal entries
        let filterEntries (filters : QueryFilters) (journal : Journal) =
            // apply account filters to construct a set of accounts
            // TODO: Not fond of the call to "containsOneOf" needing the default parameter, there must be a better way...
            let accounts = 
                journal.AllAccounts
                |> Set.filter (containsOneOf true filters.AccountsWith)
                |> Set.filter (containsOneOf false filters.ExcludeAccountsWith >> not)

            // filter entries based on selected accounts and period filters
            // TODO: Can I separate account filter and period filter and pipeline them within the List.filter?
            journal.Entries
            |> List.filter (fun entry -> (Set.contains entry.Account accounts) 
                                         && (withinPeriod entry.Header.Date filters.PeriodStart filters.PeriodEnd))

        
        /// Returns a list of AccountBalance records summed for all accounts in the account lineage for each entry in entries
        let calculateAccountBalances entries =
            let accountBalanceMap = new System.Collections.Generic.Dictionary<String,CommodityAmountMap>()
            let addAmountForAccounts entry = 
                fun account -> 
                    match accountBalanceMap.ContainsKey(account) with
                    | true -> addAmountForCommodity accountBalanceMap.[account] entry.Amount
                    | false -> accountBalanceMap.[account] <- new CommodityAmountMap()
                               addAmountForCommodity accountBalanceMap.[account] entry.Amount
            let forEachAccountInLineageAddAmount entry =
                List.iter (addAmountForAccounts entry) entry.AccountLineage
            do List.iter forEachAccountInLineageAddAmount entries
            accountBalanceMap.Keys
            |> Seq.map (fun key -> {Account = key; Balance = List.sort <| Seq.toList accountBalanceMap.[key].Values})
            |> Seq.toList


        /// Groups entries by header and returns (date, payee, entries) tuples
        let calculateRegisterLines entries =
            let runningTotal = ref 0M
            // return (account, amount, total) for an entry
            // using localized side-effects here to simplify computation of running total
            let calculateEntryLine (entry : Entry) =
                runningTotal := !runningTotal + entry.Amount.Amount
                (entry.Account, entry.Amount.Amount, !runningTotal)
            entries
            |> Seq.groupBy (fun entry -> entry.Header)
            |> Seq.map (fun (header, entries) -> header.Date, header.Description, Seq.map calculateEntryLine entries |> Seq.toList |> List.rev)

            

    open Support


    /// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters,
    /// where accountBalances is a list of (account, amount) tuples.
    let balance (filters : QueryFilters) (journal : Journal) =
        let filteredEntries = filterEntries filters journal

        // sum to get account balances, discard accounts with 0 balance
        let accountBalances =
            filteredEntries
            |> calculateAccountBalances
            |> List.map (fun accountBalance -> {accountBalance with Balance = List.filter (fun balance -> balance.Amount <> 0M) accountBalance.Balance})
            |> List.filter (fun accountBalance -> (List.length accountBalance.Balance) > 0)
        
        // filter parent accounts where amount is the same as the (assumed) single child
        let accountBalances =
            let amountsEqual a b =
                (List.sort a) = (List.sort b)
            let existsChildAccountWithSameAmount allBalances accountBalance =
                allBalances
                |> List.exists (fun otherAccountBalance -> otherAccountBalance.Account.StartsWith(accountBalance.Account) 
                                                           && otherAccountBalance.Account.Length > accountBalance.Account.Length 
                                                           && (amountsEqual otherAccountBalance.Balance accountBalance.Balance))
                |> not
            accountBalances
            |> List.filter (existsChildAccountWithSameAmount accountBalances)

        // calculate total balance
        let totalBalance = 
            let sumBalances balances =
                let caMap = new CommodityAmountMap()
                List.iter (addAmountForCommodity caMap) balances
                caMap.Values
                |> Seq.toList
                |> List.sort
            accountBalances
            |> List.filter (fun accountBalance -> Set.contains accountBalance.Account journal.MainAccounts)
            |> List.collect (fun accountBalance -> accountBalance.Balance)
            |> sumBalances

        (accountBalances, totalBalance)


    /// Returns a list of (date, payee, entries) tuples that match the filters,
    /// where lines is a list of (account, amount, total) tuples.
    let register (filters : QueryFilters) (journal : Journal) =
        let filteredEntries = filterEntries filters journal
        filteredEntries
        |> calculateRegisterLines
        |> Seq.toList
        |> List.rev


    /// Returns a sorted list of (payee, amount) tuples
    let outstandingPayees (journal : Journal) =
        let calculatePayeeAmounts (payees : Map<string,decimal>) (entry : Entry) =
            if entry.Account.StartsWith("Assets:Receivables:") || entry.Account.StartsWith("Liabilities:Payables:") then 
                let payee = entry.Account.Replace("Assets:Receivables:", "").Replace("Liabilities:Payables:", "")
                let currentAmount = if payees.ContainsKey(payee) then payees.[payee] else 0M
                Map.add payee (currentAmount + entry.Amount.Amount) payees
            else payees
        journal.Entries
        |> List.fold calculatePayeeAmounts Map.empty
        |> Map.filter (fun _ amount -> amount <> 0M)
        |> Map.toList


    /// Returns a list of commodities used in the journal
    let identifyCommodities (journal : Journal) =
        let buildCommodityMap map entry =
            match entry.Amount.Commodity with
            | Some c -> match Map.tryFind c map with
                        | Some cu -> match cu.FirstAppeared > entry.Header.Date with
                                     | true -> Map.add c {cu with FirstAppeared = entry.Header.Date} map
                                     | false -> map 
                        | otherwise -> Map.add c {Commodity = c; FirstAppeared = entry.Header.Date; ZeroBalanceDate = None;} map
            | otherwise -> map
        let determineZeroBalanceDate entries commodity (cu : CommodityUsage) =
            // need to fill this part in
            let entriesWithCommodity = entries
                                       |> List.filter (fun (e : Entry) -> match e.Amount.Commodity with
                                                                          | Some c when c = commodity -> true
                                                                          | otherwise -> false)
            let balance = entriesWithCommodity |> List.fold (fun balance entry -> balance + entry.Amount.Amount) 0M
            let lastDate = entriesWithCommodity |> List.fold (fun date entry -> if entry.Header.Date > date then entry.Header.Date else date) (List.head entriesWithCommodity).Header.Date
            match balance with
            | 0M -> {cu with ZeroBalanceDate = Some lastDate}
            | otherwise -> cu
        journal.Entries
        |> List.fold buildCommodityMap Map.empty
        |> Map.map (determineZeroBalanceDate journal.Entries)
