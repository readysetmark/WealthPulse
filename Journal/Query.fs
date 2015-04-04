module Journal.Query

open System
open Journal.Types
open Journal.SymbolPrices

type SymbolAmountMap = System.Collections.Generic.Dictionary<Symbol,Amount>

type QueryFilters = {
    AccountsWith: string list option;
    ExcludeAccountsWith: string list option;
    PeriodStart: DateTime option;
    PeriodEnd: DateTime option;
}

type AccountBalance = {
    Account: string;
    Balance: Amount list;
    Basis: Amount list;
    Commodity: Amount option;
    Price: Amount option;
    PriceDate: DateTime option;
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

    /// Add an amount to a SymbolAmountMap
    let addAmountForCommodity (map : SymbolAmountMap) (amount : Amount) =
        match map.ContainsKey(amount.Symbol) with
        | true -> 
            map.[amount.Symbol] <- {Value = map.[amount.Symbol].Value + amount.Value;
                                    Symbol = amount.Symbol;
                                    Format = SymbolLeftWithSpace;}
        | false -> 
            map.[amount.Symbol] <- amount

    /// Apply filters to retrieve journal postings
    let filterPostings (filters : QueryFilters) (journal : Journal) =
        // apply account filters to construct a set of accounts
        // TODO: Not fond of the call to "containsOneOf" needing the default parameter, there must be a better way...
        let accounts = 
            journal.AllAccounts
            |> Set.filter (containsOneOf true filters.AccountsWith)
            |> Set.filter (containsOneOf false filters.ExcludeAccountsWith >> not)

        // filter postings based on selected accounts and period filters
        // TODO: Can I separate account filter and period filter and pipeline them within the List.filter?
        journal.Postings
        |> List.filter (fun posting -> (Set.contains posting.Account accounts) 
                                        && (withinPeriod posting.Header.Date filters.PeriodStart filters.PeriodEnd))

    
    /// Sums a list of postings by account and returns a list of AccountBalance records
    let sumPostingsByAccount (postings : list<Posting>) =
        let accountBalanceMap = new System.Collections.Generic.Dictionary<Account,SymbolAmountMap>()
        let addAmountForAccount (posting : Posting) =
            match accountBalanceMap.ContainsKey(posting.Account) with
            | true -> addAmountForCommodity accountBalanceMap.[posting.Account] posting.Amount
            | false -> accountBalanceMap.[posting.Account] <- new SymbolAmountMap()
                       addAmountForCommodity accountBalanceMap.[posting.Account] posting.Amount
        do List.iter addAmountForAccount postings
        accountBalanceMap.Keys
        |> Seq.map (fun key -> {Account = key; 
                                Balance = List.sort <| Seq.toList accountBalanceMap.[key].Values;
                                Basis = List.sort <| Seq.toList accountBalanceMap.[key].Values;
                                Commodity = None;
                                Price = None;
                                PriceDate = None;})
        |> Seq.toList


    /// Returns a list of AccountBalance records summed for all accounts in the account lineage for each posting in postings
    (* commenting this out. was replaced by sumEntriesByAccount, but might want/need some of this logic
       for generating parent accounts later in a later step of the balance report
    let xcalculateAccountBalances entries =
        let accountBalanceMap = new System.Collections.Generic.Dictionary<Account,SymbolAmountMap>()
        let addAmountForAccounts entry =
            fun account ->
                match accountBalanceMap.ContainsKey(account) with
                | true -> addAmountForCommodity accountBalanceMap.[account] entry.Amount
                | false -> accountBalanceMap.[account] <- new SymbolAmountMap()
                           addAmountForCommodity accountBalanceMap.[account] entry.Amount
        let forEachAccountInLineageAddAmount entry =
            List.iter (addAmountForAccounts entry) entry.AccountLineage
        do List.iter forEachAccountInLineageAddAmount entries
        accountBalanceMap.Keys
        |> Seq.map (fun key -> {Account = key; 
                                Balance = List.sort <| Seq.toList accountBalanceMap.[key].Values;
                                Basis = None;
                                Commodity = None;
                                Price = None;
                                PriceDate = None;})
        |> Seq.toList
    *)

    /// Returns a list of AccountBalance records summed for all accounts in the account lineage for each posting in postings.
    /// For accounts with commodities, we also calculate the total number of units, which is not propagated to parent accounts.
    (* commenting out for now as this version need a complete overhaul
    let calculateAccountBalances entries =
        let accountBalanceMap = new System.Collections.Generic.Dictionary<string, AccountBalance>()
        let ensureSymbolsMatch (amount1 : Amount) (amount2 : Amount) =
            match amount1.Symbol, amount2.Symbol with
            | Some s1, Some s2 when s1 <> s2 -> failwith (sprintf "Symbols do not match: %s and %s" s1 s2)
            | Some s1, None                  -> failwith (sprintf "Symbols do not match: %s and <no symbol>" s1)
            | None, Some s2                  -> failwith (sprintf "Symbols do not match: <no symbol> and %s" s2)
            | otherwise                      -> ()
        let createAccountBalanceForAccount (account : string) (entry : Entry) =
            match account = entry.Account with
            | true  ->  {
                            Account = account;
                            Balance = entry.Amount;
                            RealBalance = None;
                            Commodity = None; //entry.Commodity; -- TODO review
                            Price = None;
                            PriceDate = None;
                        }
            | false ->  {
                            Account = account;
                            Balance = entry.Amount;
                            RealBalance = None;
                            Commodity = None;
                            Price = None;
                            PriceDate = None;
                        }
        let updateAccountBalanceForAccount (account : string) (balance : AccountBalance) (entry : Entry) =
            do ensureSymbolsMatch balance.Balance entry.Amount
            // TODO review
//            match account = entry.Account, entry.Commodity with
//            | true, Some _  ->  do ensureSymbolsMatch balance.Commodity.Value entry.Commodity.Value
//                                { balance with Balance = { balance.Balance with Amount = balance.Balance.Amount + entry.Amount.Amount };
//                                                Commodity = Some { balance.Commodity.Value with Amount = balance.Commodity.Value.Amount + entry.Commodity.Value.Amount } }
//            | otherwise     ->  { balance with Balance = { balance.Balance with Amount = balance.Balance.Amount + entry.Amount.Amount } }
            { balance with Balance = { balance.Balance with Amount = balance.Balance.Amount + entry.Amount.Amount } }
        let addAmountForAccounts entry = 
            fun account -> 
                match accountBalanceMap.ContainsKey(account) with
                | true  -> accountBalanceMap.[account] <- updateAccountBalanceForAccount account accountBalanceMap.[account] entry
                | false -> accountBalanceMap.[account] <- createAccountBalanceForAccount account entry
        let forEachAccountInLineageAddAmount entry =
            List.iter (addAmountForAccounts entry) entry.AccountLineage
        do List.iter forEachAccountInLineageAddAmount entries
        accountBalanceMap.Keys
        |> Seq.map (fun key -> accountBalanceMap.[key])
        |> Seq.toList
    *)


    let lookupPricePoint (symbol : SymbolValue) (periodEnd : option<DateTime>) priceDB journalPriceDB =
        let selectPricePointByDate symbolData =
            symbolData.Prices
            |> List.filter (fun symbolPrice -> symbolPrice.Date <= if periodEnd.IsSome then periodEnd.Value else symbolPrice.Date)
            |> List.maxBy (fun symbolPrice -> symbolPrice.Date)
            |> Some
        match Map.tryFind symbol priceDB with
        | Some symbolData -> 
            selectPricePointByDate symbolData
        | None ->
            match Map.tryFind symbol journalPriceDB with
            | Some symbolData ->
                selectPricePointByDate symbolData
            | None -> None


    let computeBasis (symbol : Symbol) (filters : QueryFilters) (journal : Journal) =
        let basisAccount = "Basis:" + symbol.Value
        let basisFilter = {filters with AccountsWith = Some [basisAccount]; ExcludeAccountsWith = None;}
        let basisAmount = 
            journal
            |> filterPostings basisFilter
            |> List.filter (fun (p:Posting) -> p.Amount.Symbol.Value = "$")
            |> List.sumBy (fun (p:Posting) -> p.Amount.Value)
        Amount.create basisAmount ({Value = "$"; Quoted = false}) SymbolLeftWithSpace


    // TODO: Review this for handling multiple commodities in an account.. and hard coded 1 to find these accounts is horrible
    let computeCommodityValues (accountBalances : list<AccountBalance>) (priceDB : SymbolPriceDB) (filters : QueryFilters) (journal : Journal) =
        let computeRealBalance (accountBalance : AccountBalance) =
            match List.length accountBalance.Balance with
            | 1 -> 
                let first = List.head accountBalance.Balance
                match first.Symbol with
                | s when s.Value <> "$" -> 
                    match lookupPricePoint s.Value filters.PeriodEnd priceDB journal.JournalPriceDB with
                    | Some pricePoint ->
                        let realBalance = Amount.create (pricePoint.Price.Value * first.Value) pricePoint.Price.Symbol SymbolLeftWithSpace
                        let basisBalance = computeBasis s filters journal
                        { accountBalance with Balance = [realBalance]; Basis = [basisBalance]; Commodity = Some first; Price = Some pricePoint.Price; PriceDate = Some pricePoint.Date;}
                    | None -> accountBalance
                | _ -> accountBalance
            | _ -> accountBalance
        accountBalances
        |> List.map computeRealBalance


    /// Groups postings by header and returns (date, payee, postings) tuples
    let calculateRegisterLines postings =
        let runningTotal = ref 0M
        // return (account, amount, total) for a posting
        // using localized side-effects here to simplify computation of running total
        let calculatePostingLine (posting : Posting) =
            runningTotal := !runningTotal + posting.Amount.Value
            (posting.Account, posting.Amount.Value, !runningTotal)
        postings
        |> Seq.groupBy (fun posting -> posting.Header)
        |> Seq.map (fun (header, postings) -> header.Date, header.Payee, Seq.map calculatePostingLine postings |> Seq.toList |> List.rev)

            

open Support


/// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters,
/// where accountBalances has type AccountBalance and totalBalance is a pair: (total balance, option<real balance>)
let balance (filters : QueryFilters) (journal : Journal) (priceDB : SymbolPriceDB) =
    let filteredPostings = filterPostings filters journal

    // sum to get account balances, discard accounts with 0 balance
    let accountBalances =
        filteredPostings
        |> sumPostingsByAccount
        |> List.map (fun accountBalance -> {accountBalance with Balance = List.filter (fun balance -> balance.Value <> 0M) accountBalance.Balance})
        |> List.filter (fun accountBalance -> (List.length accountBalance.Balance) > 0)
        //|> List.filter (fun accountBalance -> accountBalance.Balance.Amount <> 0M)
        
    // Calculate real value, price, and price date for commodities
    let accountBalances = computeCommodityValues accountBalances priceDB filters journal

    // calculate (total balance, real balance) pair
    let totalBalance, totalBasisBalance =
        let sumBalances balances =
            let map = new SymbolAmountMap()
            balances
            |> List.collect id
            |> List.iter (addAmountForCommodity map)
            map.Values
            |> Seq.toList
            |> List.sort
        let balances, basisBalances = 
            accountBalances
            |> List.map (fun accountBalance -> (accountBalance.Balance, accountBalance.Basis))
            |> List.unzip
        (sumBalances balances, sumBalances basisBalances)


    //let totalBalances = (totalBalance, totalBasisBalance)
    let totalBalances = (totalBalance, totalBasisBalance)
    (*
    let totalBalances = 
        accountBalances
        |> List.filter (fun accountBalance -> Set.contains accountBalance.Account journal.MainAccounts)
        |> List.map (fun accountBalance ->
                            let realBalance = match accountBalance.RealBalance with
                                                | Some balance -> balance
                                                | None -> accountBalance.Balance
                            (accountBalance.Balance, realBalance))
        |> List.fold (fun (totalBalance : Amount, totalRealBalance : Amount) (balance, realBalance) -> 
                            let newTotalBalance = {totalBalance with Amount = totalBalance.Amount + balance.Amount}
                            let newTotalRealBalance = {totalRealBalance with Amount = totalRealBalance.Amount + realBalance.Amount}
                            (newTotalBalance, newTotalRealBalance))
                        ({Amount = 0.0M; Symbol = Some "$"}, {Amount = 0.0M; Symbol = Some "$"})

    // if total balance = real balance, omit real balance
    let totalBalances =
        match totalBalances with
        | totalBalance, totalRealBalance when totalBalance = totalRealBalance -> totalBalance, None
        | totalBalance, totalRealBalance -> totalBalance, Some totalRealBalance
    *)

    // filter parent accounts where amount is the same as the (assumed) single child
    let accountBalances =
        let amountsEqual a b =
            (List.sort a) = (List.sort b)
        let existsChildAccountWithSameAmount allBalances accountBalance =
            allBalances
            |> List.exists (fun otherAccountBalance -> otherAccountBalance.Account.StartsWith(accountBalance.Account) 
                                                        && otherAccountBalance.Account.Length > accountBalance.Account.Length
                                                        && (amountsEqual otherAccountBalance.Balance accountBalance.Balance))
                                                        //&& otherAccountBalance.Balance.Amount = accountBalance.Balance.Amount)
            |> not
        accountBalances
        |> List.filter (existsChildAccountWithSameAmount accountBalances)

    (accountBalances, totalBalances)


/// Returns a list of (date, payee, postings) tuples that match the filters,
/// where lines is a list of (account, amount, total) tuples.
let register (filters : QueryFilters) (journal : Journal) =
    let filteredPostings = filterPostings filters journal
    filteredPostings
    |> calculateRegisterLines
    |> Seq.toList
    |> List.rev


/// Returns a sorted list of (payee, amount) tuples
let outstandingPayees (journal : Journal) =
    let calculatePayeeAmounts (payees : Map<string,decimal>) (posting : Posting) =
        if posting.Account.StartsWith("Assets:Receivables:") || posting.Account.StartsWith("Liabilities:Payables:") then 
            let payee = posting.Account.Replace("Assets:Receivables:", "").Replace("Liabilities:Payables:", "")
            let currentAmount = if payees.ContainsKey(payee) then payees.[payee] else 0M
            Map.add payee (currentAmount + posting.Amount.Value) payees
        else payees
    journal.Postings
    |> List.fold calculatePayeeAmounts Map.empty
    |> Map.filter (fun _ amount -> amount <> 0M)
    |> Map.toList


/// Returns a list of symbols used in the journal
let identifySymbolUsage (journal : Journal) =
    let buildSymbolMap map (posting : Posting) =
        let symbol = posting.Amount.Symbol.Value
        match Map.tryFind symbol map with
        | Some symbolUsage when symbolUsage.FirstAppeared > posting.Header.Date ->
            Map.add symbol {symbolUsage with FirstAppeared = posting.Header.Date} map
        | Some symbolUsage ->
            map
        | otherwise ->
            Map.add symbol {Symbol = posting.Amount.Symbol; FirstAppeared = posting.Header.Date; ZeroBalanceDate = None;} map

    let determineZeroBalanceDate (postings : Posting list) (symbol : SymbolValue) (symbolUsage : SymbolUsage) =
        let assetPostingsWithSymbol =
            postings 
            |> List.filter (fun (p : Posting) -> 
                p.Amount.Symbol.Value = symbol 
                && p.AccountLineage.Head.ToLower() = "assets")
        let balance =
            assetPostingsWithSymbol
            |> List.fold (fun balance posting -> balance + posting.Amount.Value) 0M
        let lastDate =
            assetPostingsWithSymbol
            |> List.fold
                (fun date posting ->
                    if posting.Header.Date > date then posting.Header.Date 
                    else date)
                (List.head assetPostingsWithSymbol).Header.Date
        match balance with
        | 0M        -> {symbolUsage with ZeroBalanceDate = Some lastDate}
        | otherwise -> symbolUsage

    journal.Postings
    |> List.fold buildSymbolMap Map.empty
    |> Map.map (determineZeroBalanceDate journal.Postings)
    |> Map.toList
    |> List.map snd

