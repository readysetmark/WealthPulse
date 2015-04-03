module Journal.SymbolPrices

open System
open System.Net
open System.IO
open System.Text.RegularExpressions
open Journal.Types

//
// Types
//

type Pagination = {
    Start: int;
    RecordsPerPage: int;
    TotalRecords: int;
}


//
// SymbolConfig functions
//

let loadSymbolConfig (path : string) : SymbolConfigCollection =
    match File.Exists(path) with
    | true ->
        Parser.parseConfigFile path System.Text.Encoding.UTF8
    | false ->
        List.empty
    |> SymbolConfigCollection.fromList


//
// Load Symbol Prices
//

let loadSymbolPriceDB (path : string) : SymbolPriceDB =
    match File.Exists(path) with
    | true ->
        Parser.parsePricesFile path System.Text.Encoding.ASCII
    | false ->
        List.Empty
    |> SymbolPriceDB.fromList


(*

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
        let price = Amount.create <| System.Decimal.Parse(regexMatch.Groups.[2].Value) <| Some "$"
        SymbolPrice.create date symbol price

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
    | otherwise -> Some <| SymbolPriceCollection.create (usage.Symbol, prices)
        

let updatePricesForSymbol (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceCollection) = 
    let getEarlierMissingPrices (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceCollection) =
        match usage.FirstAppeared, symbolData.FirstDate with
        | firstAppeared, firstDate when firstAppeared < firstDate ->
            let endDate = firstDate.AddDays(-1.0)
            let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol usage.FirstAppeared (Some endDate)
            getPrices baseURL 0 usage.Symbol
        | otherwise -> List.Empty
        
    let getLaterMissingPrices (usage: SymbolUsage) (config : SymbolConfig) (symbolData : SymbolPriceCollection) =
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
    SymbolPriceCollection.create (symbolData.Symbol, allPrices)


let fetchPricesForSymbol (usage: SymbolUsage) (config : SymbolConfig) (symbolData : option<SymbolPriceCollection>) =
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
    let updateDB (priceDB : SymbolPriceDB) ((commodity : string), (cpDB : SymbolPriceCollection)) =
        Map.add commodity cpDB priceDB

    usages
    |> List.filter symbolsWithConfig
    |> List.map getUpdatedSymbolPriceDB
    |> List.choose id
    |> List.fold updateDB priceDB
    

//
// Save Symbol Prices
//

let serializeSymbolPriceList (sw : StreamWriter) (prices : list<SymbolPrice>) =
    let toPriceString (price : SymbolPrice) =
        // format is "P DATE SYMBOL PRICE"
        sprintf "P %s %s %s" (price.Date.ToString("yyyy-MM-dd")) price.Symbol (price.Price.Amount.ToString())
    prices
    |> List.iter (fun price -> sw.WriteLine(SymbolPrice.serialize price))


let saveSymbolPriceDB (path : string) (priceDB : SymbolPriceDB) =
    use sw = new StreamWriter(path, false)
    priceDB
    |> Map.iter (fun _ commodityPriceDB -> serializeSymbolPriceList sw commodityPriceDB.Prices)
    sw.Close()

*)