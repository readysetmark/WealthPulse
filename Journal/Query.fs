namespace WealthPulse.Journal

open System

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
        let filterEntries filters journal =
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
        let calculateAccountbalances entries =
            let accountBalanceMap = new System.Collections.Generic.Dictionary<String,Decimal>()
            let addAmountForAccounts entry = 
                fun account -> 
                    match accountBalanceMap.ContainsKey(account) with
                    | true -> accountBalanceMap.[account] <- accountBalanceMap.[account] + fst entry.Amount
                    | false -> accountBalanceMap.[account] <- fst entry.Amount
            let forEachAccountInLineageAddAmount entry =
                List.iter (addAmountForAccounts entry) entry.AccountLineage
            do List.iter forEachAccountInLineageAddAmount entries
            accountBalanceMap.Keys
            |> Seq.map (fun key -> key, accountBalanceMap.[key])
            |> Seq.toList


    open Support


    /// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters,
    /// where accountBalances is a list of (account, amount) tuples.
    let balance (filters : QueryFilters) (journal : Journal) =
        let filteredEntries = filterEntries filters journal

        // sum to get account balances, discard accounts with 0 balance
        let accountBalances =
            filteredEntries
            |> calculateAccountbalances
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
