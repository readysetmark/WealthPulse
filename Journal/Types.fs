module Journal.Types

open Journal.DateUtils
open System
open System.Text.RegularExpressions

// Journal Types

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Account =

    /// A ledger account. e.g. "Assets:Accounts:Savings"
    type T = string

    /// Calculate full account lineage for a particular account. This will return
    /// a list of all parent accounts and the account itself.
    /// e.g. given "a:b:c", returns ["a"; "a:b"; "a:b:c"]
    let getAccountLineage (account: T) =
        /// Use with fold to get all combinations.
        let combineIntoLineage (lineage: string list) (accountLevel: string) =
            match lineage.IsEmpty with
            | true  -> accountLevel :: lineage
            | false -> (lineage.Head + ":" + accountLevel) :: lineage
        account.Split ':'
        |> Array.fold combineIntoLineage []
        |> List.rev


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Symbol =

    /// A commodity symbol. e.g. "$", "AAPL", "MSFT"
    type T = {
        Value: Value;
        RenderOption: RenderOption;
    }

    and Value = string

    and RenderOption =
        | Quoted
        | Unquoted

    let makeRO symbol renderOption =
        {Value = symbol; RenderOption = renderOption}

    /// Render option will be automatically detected/assigned based on
    /// whether the symbol contains characters that *must* be quoted
    /// (which are: -.,0123456789 @;)
    let make symbol =
        let renderOption =
            match Regex.IsMatch(symbol, "[\-\.\,\d\ @;]") with
            | true -> Quoted
            | false -> Unquoted
        makeRO symbol renderOption

    let render symbol =
        match symbol.RenderOption with
        | Quoted   -> "\"" + symbol.Value + "\""
        | Unquoted -> symbol.Value


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Amount =

    /// An amount is a quantity and a symbol.
    type T = {
        Quantity: Quantity;
        Symbol: Symbol.T;
        RenderOption: RenderOption;
    }

    and Quantity = decimal

    and RenderOption = 
        | SymbolLeftWithSpace
        | SymbolLeftNoSpace
        | SymbolRightWithSpace
        | SymbolRightNoSpace

    let make quantity symbol renderOption =
        {Quantity = quantity; Symbol = symbol; RenderOption = renderOption;}

    let render (amount : T) =
        let renderedSymbol = Symbol.render amount.Symbol
        let quantityString = amount.Quantity.ToString()
        match amount.RenderOption with
        | SymbolLeftWithSpace  -> renderedSymbol + " " + quantityString
        | SymbolLeftNoSpace    -> renderedSymbol + quantityString
        | SymbolRightWithSpace -> quantityString + " " + renderedSymbol
        | SymbolRightNoSpace   -> quantityString + renderedSymbol


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPrice =

    /// Symbol price as of a certain date.
    type T = {
        Date: System.DateTime;
        Symbol: Symbol.T;
        Price: Amount.T;
        LineNumber: int64 option;
    }

    let makeLN date symbol price lineNum =
        {Date = date; Symbol = symbol; Price = price; LineNumber = lineNum;}

    let make date symbol price =
        makeLN date symbol price None

    let render symbolPrice =
        let date = symbolPrice.Date.ToString(RenderDateFormat)
        let symbol = Symbol.render symbolPrice.Symbol
        let price = Amount.render symbolPrice.Price
        sprintf "P %s %s %s" date symbol price


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPriceCollection =

    /// A symbol price collection keeps all historical prices for a symbol, plus some metadata.
    type T = {
        Symbol:    Symbol.T;
        FirstDate: System.DateTime;
        LastDate:  System.DateTime;
        Prices:    SymbolPrice.T list;
    }

    let fromList (prices : seq<SymbolPrice.T>) =
        let sortedPrices = 
            prices
            |> Seq.toList
            |> List.sortBy (fun sp -> sp.Date)
        let symbol = (List.head sortedPrices).Symbol
        let firstDate = (List.head sortedPrices).Date
        let lastDate = (List.item ((List.length sortedPrices) - 1) sortedPrices).Date
        {Symbol = symbol; FirstDate = firstDate; LastDate = lastDate; Prices = sortedPrices;}

    let prettyPrint spc =
        let printPrice (price : SymbolPrice.T) =
            do printfn "%s - %s" (price.Date.ToString(RenderDateFormat)) (Amount.render price.Price)
        do printfn "Symbol:  %s" spc.Symbol.Value
        do printfn "First Date: %s" (spc.FirstDate.ToString(RenderDateFormat))
        do printfn "Last Date:  %s" (spc.LastDate.ToString(RenderDateFormat))
        do printfn "Price History:"
        List.iter printPrice spc.Prices


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPriceDB =

    /// Symbol Price DB is a map of symbols to symbol price collections
    type T = Map<Symbol.Value, SymbolPriceCollection.T>

    let fromList (prices : list<SymbolPrice.T>) : T =
        prices
        |> Seq.groupBy (fun sp -> sp.Symbol.Value)
        |> Seq.map (fun (symbolValue, symbolPrices) -> symbolValue, SymbolPriceCollection.fromList symbolPrices)
        |> Map.ofSeq

    let prettyPrint (priceDB : T) =
        let printSymbolPrices _ (spc : SymbolPriceCollection.T) =
            do printfn "----"
            do SymbolPriceCollection.prettyPrint spc
        priceDB
        |> Map.iter printSymbolPrices


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolConfig =

    type T = {
        Symbol: Symbol.T;
        GoogleFinanceSearchSymbol: GoogleFinanceSearchSymbol;
    }

    and GoogleFinanceSearchSymbol = string

    let make symbol googleFinanceSymbol =
        {Symbol = symbol; GoogleFinanceSearchSymbol = googleFinanceSymbol}

    let render config =
        sprintf "SC %s %s" (Symbol.render config.Symbol) config.GoogleFinanceSearchSymbol


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolConfigCollection =

    type T = Map<Symbol.Value, SymbolConfig.T>

    let fromList (symbolConfigs : SymbolConfig.T list) =
        symbolConfigs
        |> List.map (fun sc -> sc.Symbol.Value, sc)
        |> Map.ofList

    let prettyPrint (configs : T) : unit =
        printfn "Symbol Configs:"
        configs
        |> Map.iter (fun sym config -> printfn "%s" <| SymbolConfig.render config)


type Comment = string

type Payee = string


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Header =

    /// Transaction header.
    type T = {
        LineNumber: int64;
        Date: System.DateTime;
        Status: Status;
        Code: Code option;
        Payee: Payee;
        Comment: Comment option
    }

    and Code = string

    and Status =
        | Cleared
        | Uncleared

    let make lineNum date status code payee comment =
        {LineNumber=lineNum; Date=date; Status=status; Code=code; Payee=payee; Comment=comment}


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Posting =

    /// Transaction posting.
    type T = {
        LineNumber: int64;
        Header: Header.T;
        Account: Account.T;
        AccountLineage: Account.T list;
        Amount: Amount.T;
        AmountSource: AmountSource;
        Comment: string option;
    }

    /// An amount may be provided or inferred in a transaction
    and AmountSource =
        | Provided
        | Inferred

    let make lineNum header account accountLineage amount amountSource comment =
        {
            LineNumber = lineNum;
            Header = header;
            Account = account;
            AccountLineage = accountLineage;
            Amount = amount;
            AmountSource = amountSource;
            Comment = comment;
        }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Journal = 

    /// Journal with all postings and accounts.
    type T = {
        Postings: Posting.T list;
        MainAccounts: Set<string>;
        AllAccounts: Set<string>;
        PriceDB: SymbolPriceDB.T;
        DownloadedPriceDB : SymbolPriceDB.T;
    }

    /// Given a list of journal postings, returns a Journal record
    let make postings priceDB downloadedPriceDB =
        let mainAccounts =
            postings
            |> List.map (fun (posting : Posting.T) -> posting.Account)
            |> Set.ofList
        let allAccounts =
            postings
            |> List.collect (fun posting -> posting.AccountLineage)
            |> Set.ofList
        {
            Postings = postings;
            MainAccounts = mainAccounts;
            AllAccounts = allAccounts;
            PriceDB = priceDB;
            DownloadedPriceDB = downloadedPriceDB;
        }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolUsage =

    /// Symbol Usage record.
    type T = {
        Symbol: Symbol.T;
        FirstAppeared: DateTime;
        ZeroBalanceDate: DateTime option;
    }

    let make symbol firstAppeared zeroBalanceDate =
        { Symbol = symbol; FirstAppeared = firstAppeared; ZeroBalanceDate = zeroBalanceDate; }
