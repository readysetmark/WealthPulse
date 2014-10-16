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
    

    //
    // Update Symbol Prices
    //

    let fetch (url : string) =
        //printfn "Fetching URL: %s" url
        let req = WebRequest.Create(url) :?> HttpWebRequest
        req.Method <- "GET"
        req.UserAgent <- "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.114 Safari/537.36"
        use resp = req.GetResponse()
        use stream = resp.GetResponseStream()
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    

    let scrapePrices (symbol : Symbol) (html : string) =
        let toSymbolPrice (regexMatch : Match) =
            let date = System.DateTime.Parse(regexMatch.Groups.[1].Value)
            let price = System.Decimal.Parse(regexMatch.Groups.[2].Value)
            {Date = date; Symbol = symbol; Price = price}

        let regex = new Regex("<td class=\"lm\">(\w+ \d{1,2}, \d{4})\s<td class=\"rgt rm\">(\d+\.\d+)")
        regex.Matches(html)
        |> Seq.cast<Match>
        |> Seq.map toSymbolPrice
        |> Seq.toList


    let scrapePagination (html : string) =
        let regex = new Regex("google.finance.applyPagination\(\s(\d+),\s(\d+),\s(\d+),")
        let matches = regex.Matches(html)
        {
            Start = System.Int32.Parse(matches.[0].Groups.[1].Value);
            RecordsPerPage = System.Int32.Parse(matches.[0].Groups.[2].Value);
            TotalRecords = System.Int32.Parse(matches.[0].Groups.[3].Value);
        }

    
    let rec getPrices (baseURL : string) (startAtRecord : int) (symbol : Symbol) =
        let url = sprintf "%s&start=%s" baseURL (startAtRecord.ToString())
        let html = fetch url
        let prices = scrapePrices symbol html
        let pagination = scrapePagination html
        match pagination.Start, pagination.TotalRecords with
        | start, total when start < total ->
            let startAt = pagination.Start + pagination.RecordsPerPage
            prices @ getPrices baseURL startAt symbol
        | otherwise -> prices
    

    let generateBaseURL (searchKey : string) (startDate : System.DateTime) (endDate : option<System.DateTime>) =
        let dateFormat = "MMM d, yyyy"
        let query = System.Net.WebUtility.UrlEncode(searchKey)
        let startDate = System.Net.WebUtility.UrlEncode(startDate.ToString(dateFormat))
        let endDate =
            match endDate with
            | Some d -> System.Net.WebUtility.UrlEncode(d.ToString(dateFormat))
            | None   -> System.Net.WebUtility.UrlEncode(System.DateTime.Today.ToString(dateFormat))
        let baseURL = sprintf "https://www.google.com/finance/historical?q=%s&startdate=%s&enddate=%s&num=100" query startDate endDate
        baseURL


    let printNewPrices (prices : list<SymbolPrice>) =
        let printMatch (price : SymbolPrice) =
            do printfn "%s - %s - %s" price.Symbol (price.Date.ToString("yyyy-MM-dd")) (price.Price.ToString())
        match List.length prices with
        | length when length = 0 -> do printfn "No new prices to add."
        | length when length = 1 -> do printfn "Adding %d price:" length
        | otherwise              -> do printfn "Adding %d prices:" <| List.length prices
        prices
        |> List.iter printMatch


    let getPricesForNewSymbol (usage: SymbolUsage) (config : SymbolConfig) =
        let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol usage.FirstAppeared usage.ZeroBalanceDate
        let prices = getPrices baseURL 0 usage.Symbol
        match List.length prices with
        | 0         -> None
        | otherwise -> Some <| createSymbolPriceDB (usage.Symbol, prices)
        

    let updatePricesForSymbol (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceData) = 
        let getEarlierMissingPrices (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceData) =
            match usage.FirstAppeared, symbolData.FirstDate with
            | firstAppeared, firstDate when firstAppeared < firstDate ->
                let endDate = firstDate.AddDays(-1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol usage.FirstAppeared (Some endDate)
                getPrices baseURL 0 usage.Symbol
            | otherwise -> List.Empty
        
        let getLaterMissingPrices (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceData) =
            match usage.ZeroBalanceDate, symbolData.LastDate with
            | None, lastDate when lastDate < System.DateTime.Today ->
                let startDate = lastDate.AddDays(1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol startDate (Some System.DateTime.Today)
                getPrices baseURL 0 usage.Symbol
            | Some zeroBalanceDate, lastDate when lastDate < zeroBalanceDate ->
                let startDate = lastDate.AddDays(1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol startDate (Some zeroBalanceDate)
                getPrices baseURL 0 usage.Symbol
            | otherwise -> List.Empty

        let earlierPrices = getEarlierMissingPrices usage config symbolData
        let laterPrices = getLaterMissingPrices usage config symbolData
        printNewPrices (earlierPrices @ laterPrices)
        let allPrices = earlierPrices @ symbolData.Prices @ laterPrices
        createSymbolPriceDB (symbolData.Symbol, allPrices)


    let fetchPricesForSymbol (usage: SymbolUsage) (config : SymbolConfig) (symbolData : option<SymbolPriceData>) =
        printfn "Fetching prices for: %s" usage.Symbol
        match usage, symbolData with
        | usage, Some symbolData -> Some <| updatePricesForSymbol usage config symbolData
        | usage, None            -> getPricesForNewSymbol usage config
    

    let updateSymbolPriceDB (usages : list<SymbolUsage>) (configs : SymbolConfigs) (priceDB : SymbolPriceDB) =
        let symbolsWithConfig (usage : SymbolUsage) =
            Map.containsKey usage.Symbol configs
        let getUpdatedSymbolPriceDB (usage : SymbolUsage) =
            let config = Map.find usage.Symbol configs
            let symbolData = Map.tryFind config.Symbol priceDB
            fetchPricesForSymbol usage config symbolData
        let updateDB (priceDB : SymbolPriceDB) ((commodity : string), (cpDB : SymbolPriceData)) =
            Map.add commodity cpDB priceDB

        usages
        |> List.filter symbolsWithConfig
        |> List.map getUpdatedSymbolPriceDB
        |> List.choose id
        |> List.fold updateDB priceDB
    