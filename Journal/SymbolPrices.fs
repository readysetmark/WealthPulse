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


    //
    // Load Symbol Prices
    //

    let deserializePrices (path : string) =
        let toSymbolPrice (regexMatch : Match) =
            let date = System.DateTime.Parse(regexMatch.Groups.[1].Value)
            let symbol = regexMatch.Groups.[2].Value
            let price = System.Decimal.Parse(regexMatch.Groups.[3].Value)
            {Date = date; Symbol = symbol; Price = price}
        match File.Exists(path) with
        | true ->
            use sr = new StreamReader(path)
            let contents = sr.ReadToEnd()
            sr.Close()
            let regex = new Regex("P (\d{4}-\d{2}-\d{2}) (\w+) (\d+.\d+)")
            regex.Matches(contents)
            |> Seq.cast<Match>
            |> Seq.map toSymbolPrice
            |> Seq.toList
        | false ->
            List.Empty

    let createSymbolPriceDB (s : Symbol, prices : seq<SymbolPrice>) =
        let sortedPrices = 
            prices
            |> Seq.toList
            |> List.sortBy (fun sp -> sp.Date)
        let firstDate = (List.head sortedPrices).Date
        let lastDate = (List.nth sortedPrices <| ((List.length sortedPrices) - 1)).Date
        (s, {Symbol = s; FirstDate = firstDate; LastDate = lastDate; Prices = sortedPrices;})

    let loadSymbolPriceDB (path : string) : SymbolPriceDB =
        deserializePrices path
        |> Seq.ofList
        |> Seq.groupBy (fun sp -> sp.Symbol)
        |> Seq.map createSymbolPriceDB
        |> Map.ofSeq

    let printSymbolPriceDB (priceDB : SymbolPriceDB) =
        let dateFormat = "yyyy-MM-dd"
        let printSymbolPrices _ (sd : SymbolPriceData) =
            let printPrice (price : SymbolPrice) =
                do printfn "%s - %s" (price.Date.ToString(dateFormat)) (price.Price.ToString())
            do printfn "----"
            do printfn "Symbol:  %s" sd.Symbol
            do printfn "First Date: %s" (sd.FirstDate.ToString(dateFormat))
            do printfn "Last Date:  %s" (sd.LastDate.ToString(dateFormat))
            do printfn "Price History:"
            List.iter printPrice sd.Prices
        priceDB
        |> Map.iter printSymbolPrices
    

