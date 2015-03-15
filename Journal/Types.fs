module Journal.Types

open System

// Journal Types

/// A ledger account. e.g. "Assets:Accounts:Savings"
type Account = string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Account =

    /// Calculate full account lineage for a particular account. This will return
    /// a list of all parent accounts and the account itself.
    /// e.g. given "a:b:c", returns ["a:b:c"; "a:b"; "a"]
    let getAccountLineage (account: string) =
        /// Use with fold to get all combinations.
        let combinator (s: string list) (t: string) =
            if not s.IsEmpty then (s.Head + ":" + t) :: s else t :: s
        account.Split ':'
        |> Array.fold combinator []
        |> List.rev


/// A commodity symbol. e.g. "$", "AAPL", "MSFT"
type Symbol = string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Symbol =

    let serialize (symbol : Symbol) =
        let quoteSymbol = String.exists (fun c -> "-0123456789., @;".IndexOf(c) >= 0) symbol
        if quoteSymbol then "\"" + symbol + "\"" else symbol


/// An amount is a quantity and an optional symbol.
type Amount = {
    Amount: decimal;
    Symbol: Symbol option;
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Amount =

    let create amount symbol =
        {Amount = amount; Symbol = symbol;}

    let serialize (amount : Amount) =
        match amount.Symbol with
        | Some symbol ->
            let symbol = Symbol.serialize symbol
            if symbol.StartsWith("\"") then amount.Amount.ToString() + " " + symbol else symbol + amount.Amount.ToString()
        | None -> amount.Amount.ToString()


/// Symbol price as of a certain date.
type SymbolPrice = {
    Date: System.DateTime;
    Symbol: Symbol;
    Price: Amount;
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPrice =

    let create date symbol price =
        {Date = date; Symbol = symbol; Price = price;}

    let serialize (sp : SymbolPrice) =
        let dateFormat = "yyyy-MM-dd"
        sprintf "P %s %s %s" (sp.Date.ToString(dateFormat)) (Symbol.serialize sp.Symbol) (Amount.serialize sp.Price)


/// A symbol price collection keeps all historical prices for a symbol, plus some metadata.
type SymbolPriceCollection = {
    Symbol: Symbol;
    FirstDate: System.DateTime;
    LastDate:  System.DateTime;
    Prices:    list<SymbolPrice>;
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPriceCollection =

    let create (s : Symbol, prices : seq<SymbolPrice>) =
        let sortedPrices = 
            prices
            |> Seq.toList
            |> List.sortBy (fun sp -> sp.Date)
        let firstDate = (List.head sortedPrices).Date
        let lastDate = (List.nth sortedPrices <| ((List.length sortedPrices) - 1)).Date
        (s, {Symbol = s; FirstDate = firstDate; LastDate = lastDate; Prices = sortedPrices;})

    let prettyPrint spc =
        let dateFormat = "yyyy-MM-dd"
        let printPrice (price : SymbolPrice) =
            do printfn "%s - %s" (price.Date.ToString(dateFormat)) (Amount.serialize price.Price)
        do printfn "Symbol:  %s" spc.Symbol
        do printfn "First Date: %s" (spc.FirstDate.ToString(dateFormat))
        do printfn "Last Date:  %s" (spc.LastDate.ToString(dateFormat))
        do printfn "Price History:"
        List.iter printPrice spc.Prices


/// Symbol Price DB is a map of symbols to symbol price collections
type SymbolPriceDB = Map<Symbol, SymbolPriceCollection>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SymbolPriceDB =

    let createFromSymbolPriceList (prices : list<SymbolPrice>) : SymbolPriceDB =
        prices
        |> Seq.groupBy (fun sp -> sp.Symbol)
        |> Seq.map SymbolPriceCollection.create
        |> Map.ofSeq

    let prettyPrint (priceDB : SymbolPriceDB) =
        let dateFormat = "yyyy-MM-dd"
        let printSymbolPrices _ (spc : SymbolPriceCollection) =
            do printfn "----"
            do SymbolPriceCollection.prettyPrint spc
        priceDB
        |> Map.iter printSymbolPrices


type Code = string
type Payee = string
type Comment = string

/// Transaction status.
type Status =
    | Cleared
    | Uncleared

/// Entry type.
type EntryType =
    | Balanced
    | VirtualBalanced
    | VirtualUnbalanced

/// Transaction header.
type Header = {
    Date: System.DateTime;
    Status: Status;
    Code: Code option;
    Payee: Payee;
    Comment: Comment option
}

/// Transaction entry line.
type Entry = {
    Header: Header;
    Account: string;
    AccountLineage: string list;
    EntryType: EntryType;
    Amount: Amount;
    Comment: string option;
}

/// Journal with all entries and accounts.
type Journal = {
    Entries: Entry list;
    MainAccounts: Set<string>;
    AllAccounts: Set<string>;
    JournalPriceDB: SymbolPriceDB;
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Journal = 
    
    /// Given a list of journal entries, returns a Journal record
    let create entries pricedb =
        let mainAccounts = Set.ofList <| List.map (fun (entry : Entry) -> entry.Account) entries
        let allAccounts = Set.ofList <| List.collect (fun entry -> entry.AccountLineage) entries
        { Entries=entries; MainAccounts=mainAccounts; AllAccounts=allAccounts; JournalPriceDB=pricedb}


// Symbol Usage Types
    
/// Symbol Usage record.
type SymbolUsage = {
    Symbol: Symbol;
    FirstAppeared: DateTime;
    ZeroBalanceDate: DateTime option;
}