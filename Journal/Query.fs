namespace WealthPulse

open System
open Journal

module Query =

    type QueryFilters = {
        AccountsWith: string list option;
        ExcludeAccountsWith: string list option;
        PeriodStart: DateTime option;
        PeriodEnd: DateTime option;
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

        /// Returns a list of (account, balance) tuples summed for all accounts in the account lineage for each entry in entries
        let calculateAccountBalances entries =
            let accountBalanceMap = new System.Collections.Generic.Dictionary<String,Decimal>()
            let addAmountForAccounts entry = 
                fun account -> 
                    match accountBalanceMap.ContainsKey(account) with
                    | true -> accountBalanceMap.[account] <- accountBalanceMap.[account] + entry.Amount.Amount
                    | false -> accountBalanceMap.[account] <- entry.Amount.Amount
            let forEachAccountInLineageAddAmount entry =
                List.iter (addAmountForAccounts entry) entry.AccountLineage
            do List.iter forEachAccountInLineageAddAmount entries
            accountBalanceMap.Keys
            |> Seq.map (fun key -> key, accountBalanceMap.[key])
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
            |> Seq.filter (fun (account, balance) -> balance <> 0M)
            |> Seq.toList
        
        // filter parent accounts where amount is the same as the (assumed) single child
        let accountBalances =
            let existsChildAccountWithSameAmount allBalances (account, amount) =
                allBalances
                |> List.exists (fun ((otherAccount : String), otherAmount) -> otherAccount.StartsWith(account) && otherAccount.Length > account.Length && otherAmount = amount)
                |> not
            accountBalances
            |> List.filter (existsChildAccountWithSameAmount accountBalances)

        // calculate total balance
        let totalBalance = 
            accountBalances
            |> List.filter (fun (account, _) -> Set.contains account journal.MainAccounts)
            |> List.map (fun (_, amount) -> amount)
            |> List.sum

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
        let calculatePayeeAmounts (payees : Map<string,decimal>) entry =
            if entry.Account.StartsWith("Assets:Receivables:") || entry.Account.StartsWith("Liabilities:Payables:") then 
                let payee = entry.Account.Replace("Assets:Receivables:", "").Replace("Liabilities:Payables:", "")
                let currentAmount = if payees.ContainsKey(payee) then payees.[payee] else 0M
                Map.add payee (currentAmount + entry.Amount.Amount) payees
            else payees
        journal.Entries
        |> List.fold calculatePayeeAmounts Map.empty
        |> Map.filter (fun _ amount -> amount <> 0M)
        |> Map.toList
