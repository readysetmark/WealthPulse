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

        /// Apply filters to retrieve journal entries
        //let filterEntries


    open Support

    /// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters,
    /// where accountBalances is a list of (account, amount) tuples.
    let balance (parameters : QueryFilters) (journal : JournalData) =
        // TODO: Not fond of the call to "oneOfIn" needing the default parameter, there must be a better way...
        // filter all accounts to selected accounts
        let accounts = 
            journal.AllAccounts
            |> Set.filter (oneOfIn true parameters.AccountsWith)
            |> Set.filter (oneOfIn false parameters.ExcludeAccountsWith >> not)

        // TODO: Can I get rid of the list comprehension?
        // filter entries based on selected accounts
        let entries = 
            [ for entry in journal.Entries do 
                for account in entry.AccountLineage do 
                    if (Set.contains entry.Account accounts) && (withinPeriod entry.Header.Date parameters.PeriodStart parameters.PeriodEnd) then 
                        yield(account, fst entry.Amount) 
            ]

        // sum to get account balances, discard accounts with 0 balance
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
