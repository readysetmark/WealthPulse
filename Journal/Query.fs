module Journal.Query

open System
open Journal.Types
open Journal.SymbolPrices

type QueryOptions = {
    AccountsWith: string list option;
    ExcludeAccountsWith: string list option;
    PeriodStart: DateTime option;
    PeriodEnd: DateTime option;
    ConvertCommodities: bool;
}

type AccountBalance = {
    Account: Account;
    Balance: Amount list;
    Basis: Amount list option;
    Commodity: Amount option;
    Price: Amount option;
    PriceDate: DateTime option;
}

type RegisterPosting = {
    Account: Account;
    Amount: Amount;
    Balance: Amount list;
}

type Register = {
    Date: DateTime;
    Payee: Payee;
    Postings: RegisterPosting list;
}

type OutstandingPayee = {
    Payee: Account;
    Balance: Amount list;
}


module private Support =

    type SymbolAmountMap = Map<Symbol, Amount>

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SymbolAmountMap =

        /// Add an amount to a SymbolAmountMap by adding the value to the existing amount
        /// for a particular symbol, or create a new symbol entry for the amount if there is none
        let add (symbolAmounts : SymbolAmountMap) (amount : Amount) : SymbolAmountMap =
            let updatedAmount =
                match Map.tryFind amount.Symbol symbolAmounts with
                | Some mapAmount -> { mapAmount with Value = mapAmount.Value + amount.Value }
                | None           -> amount
            Map.add amount.Symbol updatedAmount symbolAmounts

        /// Filter out any symbols that have a 0 amount balance
        let filterZeroAmounts (symbolAmounts: SymbolAmountMap) : SymbolAmountMap =
            symbolAmounts
            |> Map.filter (fun symbol amount -> amount.Value <> 0M)

        /// Convert a SymbolAmountMap to a list of Amounts sorted by Symbol
        let toSortedAmountList (symbolAmounts: SymbolAmountMap) : Amount list =
            symbolAmounts
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.sortBy (fun amount -> amount.Symbol.Value)
            |> Seq.toList

    type AccountAmountsMap = Map<Account, SymbolAmountMap>

    type RealAndBasisAmounts = {
        Real: SymbolAmountMap;
        Basis: SymbolAmountMap;
    }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RealAndBasisAmounts =

        let addAccountBalanceBalances (amounts : RealAndBasisAmounts) (accountBalance : AccountBalance) : RealAndBasisAmounts =
            let realBalances = List.fold SymbolAmountMap.add amounts.Real accountBalance.Balance
            let basisBalances =
                match accountBalance.Basis with
                | Some basis -> List.fold SymbolAmountMap.add amounts.Basis basis
                | None       -> amounts.Basis
            { amounts with Real = realBalances; Basis = basisBalances }

        let toSortedAmountLists (amounts : RealAndBasisAmounts) : Amount List * (Amount List Option) =
            let real = SymbolAmountMap.toSortedAmountList amounts.Real
            let basis = 
                match Map.isEmpty amounts.Basis with
                | true  -> None
                | false -> Some <| SymbolAmountMap.toSortedAmountList amounts.Basis
            real, basis

    type AccountRealAndBasisAmountsMap = Map<Account, RealAndBasisAmounts>

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
    let filterPostings (filters : QueryOptions) (journal : Journal) : Posting list =
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
    let sumPostingsByAccount (postings : Posting list) : AccountBalance list =
        let buildAccountAmountsMap (accountAmounts : AccountAmountsMap) (posting : Posting) =
            let updatedSymbolAmounts =
                match Map.tryFind posting.Account accountAmounts with
                | Some symbolAmounts -> SymbolAmountMap.add symbolAmounts posting.Amount
                | None               -> SymbolAmountMap.add Map.empty posting.Amount
            Map.add posting.Account updatedSymbolAmounts accountAmounts

        postings
        |> List.fold buildAccountAmountsMap Map.empty 
        |> Map.toSeq
        |> Seq.map (fun (account, symbolAmounts) ->
            let amounts = SymbolAmountMap.toSortedAmountList symbolAmounts
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
    let calculateTotalBalance (accountBalances : AccountBalance List) : AccountBalance =
        let real, basis =
            accountBalances
            |> List.fold RealAndBasisAmounts.addAccountBalanceBalances { Real = Map.empty; Basis = Map.empty }
            |> RealAndBasisAmounts.toSortedAmountLists

        {
            Account = "";
            Balance = real;
            Basis = basis;
            Commodity = None;
            Price = None;
            PriceDate = None;
        }

    /// Calculate parent account balances
    let calculateParentAccountBalances (accountBalances : AccountBalance List) : AccountBalance list =
        let addBalancesForParentAccount (accountBalance : AccountBalance) (parentAccountBalances : AccountRealAndBasisAmountsMap) (account : Account) =
            let updatedParentAmounts =
                let parentAccountAmounts =
                    match Map.tryFind account parentAccountBalances with
                    | Some parentAccountAmounts -> parentAccountAmounts
                    | None -> { Real = Map.empty; Basis = Map.empty }
                RealAndBasisAmounts.addAccountBalanceBalances parentAccountAmounts accountBalance
            Map.add account updatedParentAmounts parentAccountBalances

        let buildParentAccountAmounts (parentAccountBalances : AccountRealAndBasisAmountsMap) (accountBalance: AccountBalance) =
            accountBalance.Account
            |> Account.getAccountLineage 
            |> List.rev
            |> Seq.skip 1
            |> Seq.fold (addBalancesForParentAccount accountBalance) parentAccountBalances

        accountBalances
        |> List.fold buildParentAccountAmounts Map.empty
        |> Map.toSeq
        |> Seq.map (fun (account, amounts) ->
            let real, basis = RealAndBasisAmounts.toSortedAmountLists amounts
            {
                Account = account;
                Balance = real;
                Basis = basis;
                Commodity = None;
                Price = None;
                PriceDate = None;
            })
        |> Seq.toList

    /// Filter parent accounts where amount is the same as the (assumed) single child
    let filterParentAccounts (accountBalances : AccountBalance list) : AccountBalance list =
        let amountsEqual a b =
            (List.sort a) = (List.sort b)

        let existsChildAccountWithSameAmount (allBalances : AccountBalance list) (accountBalance : AccountBalance) : bool =
            allBalances
            |> List.exists (fun otherAccountBalance -> otherAccountBalance.Account.StartsWith(accountBalance.Account) 
                                                        && otherAccountBalance.Account.Length > accountBalance.Account.Length
                                                        && (amountsEqual otherAccountBalance.Balance accountBalance.Balance))
            |> not

        accountBalances
        |> List.filter (existsChildAccountWithSameAmount accountBalances)

    /// Try to find a symbol price as of periodEnd or today. Return Some SymbolPrice if found or None otherwise.
    let tryFindSymbolPrice (symbol : SymbolValue) (periodEnd : DateTime option) (journal : Journal) : SymbolPrice option =
        let selectPricePointByDate (symbolPriceCollection : SymbolPriceCollection) =
            symbolPriceCollection.Prices
            |> List.filter (fun symbolPrice -> symbolPrice.Date <= if periodEnd.IsSome then periodEnd.Value else symbolPrice.Date)
            |> List.maxBy (fun symbolPrice -> symbolPrice.Date)
            |> Some

        // try the downloaded price db first, then the prices entered in the journal file
        match Map.tryFind symbol journal.DownloadedPriceDB with
        | Some symbolPriceCollection -> 
            selectPricePointByDate symbolPriceCollection
        | None ->
            match Map.tryFind symbol journal.PriceDB with
            | Some symbolPriceCollection ->
                selectPricePointByDate symbolPriceCollection
            | None -> None

    /// Compute the basis amount for a symbol over a period specified by the filters.
    let computeBasis (account : Account) (filters : QueryOptions) (journal : Journal) : Amount =
        let basisAccountParts =
            "Basis" :: (
                account.Split ':'
                |> List.ofArray
                |> List.tail)
        let basisAccount = String.Join(":", basisAccountParts)
        let basisFilter = {filters with AccountsWith = Some [basisAccount]; ExcludeAccountsWith = None;}
        let basisAmount = 
            journal
            |> filterPostings basisFilter
            |> List.filter (fun (p:Posting) -> p.Amount.Symbol.Value = "$")
            |> List.sumBy (fun (p:Posting) -> p.Amount.Value)
        Amount.create basisAmount ({Value = "$"; Quoted = false}) SymbolLeftNoSpace

    // TODO: How do I handle if we're computing commodity values and an account has more than one commodity?
    /// Compute real and basis values for commodities held in an account. 
    /// Making an assumption right now that an account should only hold one type of commodity.
    let computeCommodityValues (options : QueryOptions) (journal : Journal) (accountBalances : AccountBalance list) : AccountBalance list =
        let computeRealBalance (accountBalance : AccountBalance) =
            match List.length accountBalance.Balance with
            | 1 ->
                let nonDollarAmount (amount : Amount) = amount.Symbol.Value <> "$"
                match List.tryFind nonDollarAmount accountBalance.Balance with
                | Some amount -> 
                    match tryFindSymbolPrice amount.Symbol.Value options.PeriodEnd journal with
                    | Some pricePoint ->
                        let balance = { pricePoint.Price with Value = pricePoint.Price.Value * amount.Value }
                        let basis = computeBasis accountBalance.Account options journal
                        {
                            accountBalance with
                                Balance = [balance];
                                Basis = Some [basis];
                                Commodity = Some amount;
                                Price = Some pricePoint.Price;
                                PriceDate = Some pricePoint.Date;
                        }
                    | None -> { accountBalance with Basis = Some accountBalance.Balance; }
                | _ -> { accountBalance with Basis = Some accountBalance.Balance; }
            | _ -> { accountBalance with Basis = Some accountBalance.Balance; }

        match options.ConvertCommodities with
        | true ->
            accountBalances
            |> List.map computeRealBalance
        | false ->
            accountBalances

    /// Groups postings by header and returns (date, payee, postings) tuples
    let calculateRegisterLines (postings : Posting list) : Register list =
        let postingTotalMap = 
            postings
            |> Seq.scan (fun symbolAmounts posting -> SymbolAmountMap.add symbolAmounts posting.Amount) Map.empty
            |> Seq.skip 1  // head of sequence is Map.empty, so skip
            |> Seq.map SymbolAmountMap.toSortedAmountList
            |> Seq.zip postings
            |> Map.ofSeq

        postings
        |> Seq.groupBy (fun posting -> posting.Header)
        |> Seq.map (fun (header, postings) ->
                        let registerPostings =
                            postings
                            |> Seq.map (fun posting ->
                                            {
                                                Account = posting.Account;
                                                Amount = posting.Amount;
                                                Balance = Map.find posting postingTotalMap;
                                            })
                            |> Seq.toList
                            |> List.rev
                        {
                            Date = header.Date;
                            Payee = header.Payee;
                            Postings = registerPostings;
                        })
        |> Seq.toList
        |> List.rev



open Support


/// Returns a tuple of (accountBalances, totalBalance) that match the filters in parameters
let balance (options : QueryOptions) (journal : Journal) : (AccountBalance list * AccountBalance) =
    let accountBalances =
        journal
        |> filterPostings options
        |> sumPostingsByAccount
        |> discardAccountsWithZeroBalance
        |> computeCommodityValues options journal

    // calculate (total balance, real balance) pair
    let totalBalance = calculateTotalBalance accountBalances

    // Calculate all parent accounts and then remove the unnecessary ones and sort
    let parentAccountBalances = calculateParentAccountBalances accountBalances
    let accountBalances =
        accountBalances @ parentAccountBalances
        |> filterParentAccounts
        |> List.sortBy (fun accountBalance -> accountBalance.Account)

    (accountBalances, totalBalance)



/// Returns a list of register entries that match the filters.
let register (options : QueryOptions) (journal : Journal) : Register list =
    journal
    |> filterPostings options
    |> calculateRegisterLines


/// Returns a list of Outstanding Payees, which is any account with an outstanding
/// receivable or payable amount.
let outstandingPayees (journal : Journal) : OutstandingPayee list =
    let calculatePayeeAmounts (payees : Map<Account,SymbolAmountMap>) (posting : Posting) =
        if posting.Account.StartsWith("Assets:Receivables:") || posting.Account.StartsWith("Liabilities:Payables:") then 
            let payee = posting.Account.Replace("Assets:Receivables:", "").Replace("Liabilities:Payables:", "")
            let payeeBalance =
                match Map.tryFind payee payees with
                | Some balance -> SymbolAmountMap.add balance posting.Amount
                | None         -> SymbolAmountMap.add Map.empty posting.Amount
            Map.add payee payeeBalance payees
        else payees
    journal.Postings
    |> List.fold calculatePayeeAmounts Map.empty
    |> Map.map (fun payee balance ->
                    balance
                    |> SymbolAmountMap.filterZeroAmounts
                    |> SymbolAmountMap.toSortedAmountList)
    |> Map.filter (fun payee balance -> not <| List.isEmpty balance)
    |> Map.toList
    |> List.map (fun (payee, balance) -> { Payee = payee; Balance = balance })


/// Returns a list of symbols used in the journal
let identifySymbolUsage (journal : Journal) : SymbolUsage list =
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

