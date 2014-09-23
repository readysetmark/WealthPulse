namespace WealthPulse

open System
open System.Net
open System.IO
open System.Text.RegularExpressions
open WealthPulse.Types

module SymbolPrices =

    //
    // Types
    //

    type Price = decimal

    type SymbolConfig = {
        Symbol: Symbol;
        GoogleFinanceSearchSymbol: string;
    }

    type SymbolConfigs = Map<Symbol, SymbolConfig>

    type SymbolPrice = {
        Date: System.DateTime;
        Symbol: Symbol;
        Price: Price
    }

    type SymbolPriceData = {
        Symbol: Symbol;
        FirstDate: System.DateTime;
        LastDate:  System.DateTime;
        Prices:    list<SymbolPrice>;
    }

    type SymbolPriceDB = Map<Symbol, SymbolPriceData>

    type Pagination = {
        Start: int;
        RecordsPerPage: int;
        TotalRecords: int;
    }


    //
    // SymbolConfig functions
    //

    let deserializeSymbolConfigs (path : string) =
        let toSymbolConfig (regexMatch : Match) =
            let symbol = regexMatch.Groups.[1].Value
            let searchSymbol = regexMatch.Groups.[2].Value
            {Symbol = symbol; GoogleFinanceSearchSymbol = searchSymbol}
        match File.Exists(path) with
        | true ->
            use sr = new StreamReader(path)
            let contents = sr.ReadToEnd()
            sr.Close()
            // parsing: SC Commodity GoogleFinanceSearchSymbol
            let regex = new Regex("SC (\w+) ([\w:]+)")
            regex.Matches(contents)
            |> Seq.cast<Match>
            |> Seq.map toSymbolConfig
            |> Seq.toList
        | false -> 
            List.Empty

    let loadSymbolConfig (path : string) : SymbolConfigs =
        deserializeSymbolConfigs path
        |> List.map (fun s -> s.Symbol, s)
        |> Map.ofList

    let printSymbolConfigs (configs : SymbolConfigs) =
        let printSymbolConfig (_symbol : string) (config : SymbolConfig) =
            printfn "%s %s" config.Symbol config.GoogleFinanceSearchSymbol
        printfn "Symbol Config:"
        Map.iter printSymbolConfig configs


