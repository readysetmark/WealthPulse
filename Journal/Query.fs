module Journal.Query

open System
open Journal.Types
open Journal.SymbolPrices

type QueryFilters = {
    AccountsWith: string list option;
    ExcludeAccountsWith: string list option;
    PeriodStart: DateTime option;
    PeriodEnd: DateTime option;
}

type AccountBalance = {
    Account: string;
    Balance: Amount list;
    Basis: Amount list option;
    Commodity: Amount option;
    Price: Amount option;
    PriceDate: DateTime option;
}


module private Support =

    type SymbolAmountMap = Map<Symbol, Amount>
    type AccountAmountsMap = Map<Account, SymbolAmountMap>

    /// Account contains one of "termsOption" if "termsOption" provided, otherwise defaultValue
    let containsOneOf (defaultValue : bool) (termsOption : string list option) (account : string) : bool =
        match termsOption with
        | Some terms ->
            terms
            |> List.exists (fun (token : string) -> account.ToLower().Contains(token.ToLower()))
        | None       -> defaultValue

    /// Is date within periodStartOption and periodEndOption?
    let withinPeriod (date : DateTime) (periodStartOption : DateTime option) (periodEndOption : DateTime option) : bool =
        match periodStartOption, periodEndOption with
        | Some periodStart, Some periodEnd -> periodStart <= date && date <= periodEnd
        | Some periodStart, None           -> periodStart <= date
        | None, Some periodEnd             -> date <= periodEnd
        | _, _                             -> true

    /// Apply filters to retrieve journal postings
    let filterPostings (filters : QueryFilters) (journal : Journal) : Posting list =
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

    /// Add an amount to a SymbolAmountMap
    let addAmountForSymbol (symbolAmounts : SymbolAmountMap) (amount : Amount) : SymbolAmountMap =
        let updatedAmount =
            match Map.tryFind amount.Symbol symbolAmounts with
            | Some mapAmount -> { mapAmount with Value = mapAmount.Value + amount.Value }
            | None           -> amount
        Map.add amount.Symbol updatedAmount symbolAmounts

    /// Convert a SymbolAmountMap to a list of Amounts sorted by Symbol
    let symbolAmountMapToSortedAmounts (symbolAmounts: SymbolAmountMap) : Amount list =
        symbolAmounts
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.sortBy (fun amount -> amount.Symbol.Value)
        |> Seq.toList

    /// Sums a list of postings by account and returns a list of AccountBalance records
    let sumPostingsByAccount (postings : Posting list) : AccountBalance list =
        let buildAccountAmountsMap (accountAmounts : AccountAmountsMap) (posting : Posting) =
            let updatedSymbolAmounts =
                match Map.tryFind posting.Account accountAmounts with
                | Some symbolAmounts -> addAmountForSymbol symbolAmounts posting.Amount
                | None               -> addAmountForSymbol Map.empty posting.Amount
            Map.add posting.Account updatedSymbolAmounts accountAmounts

        let accountAmounts = List.fold buildAccountAmountsMap Map.empty postings

        accountAmounts
        |> Map.toSeq
        |> Seq.map (fun (account, symbolAmounts) ->
            let amounts = symbolAmountMapToSortedAmounts symbolAmounts
            {
                Account = account; 
                Balance = amounts;
                Basis = None;
                Commodity = None;
                Price = None;
                PriceDate = None;
            })
        |> Seq.toList

    /// Filters out accounts with 0 balances
    let discardAccountsWithZeroBalance (accountBalances : AccountBalance list) : AccountBalance list =
        accountBalances
        |> List.map (fun accountBalance ->
            let nonZeroBalances = List.filter (fun balance -> balance.Value <> 0M) accountBalance.Balance
            { accountBalance with Balance = nonZeroBalances })
        |> List.filter (fun accountBalance -> (List.length accountBalance.Balance) > 0)

    /// Calculate total balance (sum of all account balances)
    let calculateTotalBalance (accountBalances: AccountBalance List) : AccountBalance =
        let sumBalances (balances : SymbolAmountMap) (accountBalance : AccountBalance) =
            List.fold addAmountForSymbol balances accountBalance.Balance

        let totalBalance =
            accountBalances
            |> List.fold sumBalances Map.empty
            |> symbolAmountMapToSortedAmounts

        {
            Account = "";
            Balance = totalBalance;
            Basis = None;
            Commodity = None;
            Price = None;
            PriceDate = None;
        }

(*
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

*)

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


/// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters
let balance (filters : QueryFilters) (journal : Journal) : (AccountBalance list * AccountBalance) =
    let filteredPostings = filterPostings filters journal

    // sum to get account balances, discard accounts with 0 balance
    let accountBalances =
        filteredPostings
        |> sumPostingsByAccount
        |> discardAccountsWithZeroBalance

    // Calculate real value, price, and price date for commodities
    //let accountBalances = computeCommodityValues accountBalances priceDB filters journal

    // calculate (total balance, real balance) pair
    let totalBalance = calculateTotalBalance accountBalances

    (*
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
    *)

    (accountBalances, totalBalance)



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

