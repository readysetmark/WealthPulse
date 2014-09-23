namespace FetchPrices

open System.Net
open System.IO
open System.Text.RegularExpressions

(*
    There's actually quite a bit I need to handle
        [x] Need commodity, first appeared, zero balance date, query string
            - Query string will have to come from a config file
        [x] Need existing known prices loaded
        [x] Determine if we need to attempt to retrieve new prices for a particular commodity
        [ ] Retrieve prices
            [x] have price for first appeared?
            [x] generate url
            [x] fetch page and extract prices
            [x] generate next page url
            [ ] Use prices from ledger file to fill in gaps that cannot be retrieved
                [ ] Generate list from ledger entries
                [ ] Combine two lists
        [x] Store retrieved prices

    Inputs:
        [x] From pricedb file, startup: Commodity, first price, last price, list of all prices
        - From Journal, ongoing (when changed): Commodity, first appeared, zero balance date
        - From config file, ongoing (when changed): Commodity, google finance key from config file
    
    Outputs:
        [x] In-memory price db
        [x] pricedb file

    Boot process:
        [x] Load pricedb file, group by commodity and sort by date. determine first and last prices per commodity.
        - Get journal info & add watch
        - Get config info & add watch
        - Get new prices & add timer (24 hours)

    Get new prices process:
        [x] Compare usage dates to db dates. are end points convered?
        [x] If not covered:
            [x] If have no dates, query google for whole date range
            [x] If have dates, query google for dates missing at start, then end (two separate queries)
        [x] Get prices
            [x] Generate URL
            [x] Fetch page and extract prices
            [x] Generate next URL

*)

module Main =
    
    type Commodity = string
    type Price = decimal

    type CommodityUsage = {
        Commodity: Commodity;
        FirstAppeared: System.DateTime;
        ZeroBalanceDate: System.DateTime option;
    }

    type CommodityConfig = {
        Commodity: Commodity;
        GoogleFinanceSearchSymbol: string;
    }

    type CommodityConfigs = Map<Commodity, CommodityConfig>

    type CommodityPrice = {
        Date: System.DateTime;
        Commodity: Commodity;
        Price: Price
    }

    type CommodityPriceDB = {
        Commodity: Commodity;
        FirstDate: System.DateTime;
        LastDate:  System.DateTime;
        Prices:    list<CommodityPrice>;
    }

    type PriceDB = Map<Commodity, CommodityPriceDB>

    type Pagination = {
        Start: int;
        RecordsPerPage: int;
        TotalRecords: int;
    }

    let fetch (url : string) =
        //printfn "Fetching URL: %s" url
        let req = WebRequest.Create(url) :?> HttpWebRequest
        req.Method <- "GET"
        req.UserAgent <- "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.114 Safari/537.36"
        use resp = req.GetResponse()
        use stream = resp.GetResponseStream()
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    
    let fetchToFile (url : string) (path : string) =
        do printfn "Fetching: %s" url
        let html = fetch url
        do printfn "Writing to file."
        use sw = new StreamWriter(path, false)
        do fprintf sw "%s" <| html
        sw.Close()

    let readFile (path : string) =
        use sr = new StreamReader(path)
        let html = sr.ReadToEnd()
        sr.Close()
        html

    let scrapePrices (c : Commodity) (html : string) =
        let toCommodityPrice (regexMatch : Match) =
            let date = System.DateTime.Parse(regexMatch.Groups.[1].Value)
            let price = System.Decimal.Parse(regexMatch.Groups.[2].Value)
            {Date = date; Commodity = c; Price = price}

        let regex = new Regex("<td class=\"lm\">(\w+ \d{1,2}, \d{4})\s<td class=\"rgt rm\">(\d+\.\d+)")
        regex.Matches(html)
        |> Seq.cast<Match>
        |> Seq.map toCommodityPrice
        |> Seq.toList

    let printNewPrices (prices : list<CommodityPrice>) =
        let printMatch (price : CommodityPrice) =
            do printfn "%s - %s - %s" price.Commodity (price.Date.ToString("yyyy-MM-dd")) (price.Price.ToString())
        match List.length prices with
        | length when length = 0 -> do printfn "No new prices to add."
        | length when length = 1 -> do printfn "Adding %d price:" length
        | otherwise              -> do printfn "Adding %d prices:" <| List.length prices
        prices
        |> List.iter printMatch

    
    let serializePriceList (sw : StreamWriter) (prices : list<CommodityPrice>) =
        let toPriceString (price : CommodityPrice) =
            // format is "P DATE SYMBOL PRICE"
            sprintf "P %s %s %s" (price.Date.ToString("yyyy-MM-dd")) price.Commodity (price.Price.ToString())
        prices
        |> List.iter (fun price -> sw.WriteLine(toPriceString price))

    let savePriceDB (path : string) (priceDB : PriceDB) =
        use sw = new StreamWriter(path, false)
        priceDB
        |> Map.iter (fun _ commodityPriceDB -> serializePriceList sw commodityPriceDB.Prices)
        sw.Close()
        
    let deserializePrices (path : string) =
        let toCommodityPrice (regexMatch : Match) =
            let date = System.DateTime.Parse(regexMatch.Groups.[1].Value)
            let commodity = regexMatch.Groups.[2].Value
            let price = System.Decimal.Parse(regexMatch.Groups.[3].Value)
            {Date = date; Commodity = commodity; Price = price}
        match File.Exists(path) with
        | true ->
            use sr = new StreamReader(path)
            let contents = sr.ReadToEnd()
            sr.Close()
            let regex = new Regex("P (\d{4}-\d{2}-\d{2}) (\w+) (\d+.\d+)")
            regex.Matches(contents)
            |> Seq.cast<Match>
            |> Seq.map toCommodityPrice
            |> Seq.toList
        | false ->
            List.Empty

    let createCommodityPriceDB (c : Commodity, prices : seq<CommodityPrice>) =
        let sortedPrices = 
            prices
            |> Seq.toList
            |> List.sortBy (fun cp -> cp.Date)
        let firstDate = (List.head sortedPrices).Date
        let lastDate = (List.nth sortedPrices <| ((List.length sortedPrices) - 1)).Date
        (c, {Commodity = c; FirstDate = firstDate; LastDate = lastDate; Prices = sortedPrices;})

    let loadPriceDB (path : string) : PriceDB =
        deserializePrices path
        |> Seq.ofList
        |> Seq.groupBy (fun cp -> cp.Commodity)
        |> Seq.map createCommodityPriceDB
        |> Map.ofSeq

    let printPriceDB (priceDB : PriceDB) =
        let dateFormat = "yyyy-MM-dd"
        let printCommodityPrices _ (db : CommodityPriceDB) =
            let printPrice (price : CommodityPrice) =
                do printfn "%s - %s" (price.Date.ToString(dateFormat)) (price.Price.ToString())
            do printfn "----"
            do printfn "Commodity:  %s" db.Commodity
            do printfn "First Date: %s" (db.FirstDate.ToString(dateFormat))
            do printfn "Last Date:  %s" (db.LastDate.ToString(dateFormat))
            do printfn "Price History:"
            List.iter printPrice db.Prices
        priceDB
        |> Map.iter printCommodityPrices
    
        

    let scrapePagination (html : string) =
        let regex = new Regex("google.finance.applyPagination\(\s(\d+),\s(\d+),\s(\d+),") //
        let matches = regex.Matches(html)
        {
            Start = System.Int32.Parse(matches.[0].Groups.[1].Value);
            RecordsPerPage = System.Int32.Parse(matches.[0].Groups.[2].Value);
            TotalRecords = System.Int32.Parse(matches.[0].Groups.[3].Value);
        }

    let printPagination (page : Pagination) =
        do printfn "%d start, %d records per page, %d total records" page.Start page.RecordsPerPage page.TotalRecords


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

    
    let rec getPrices (baseURL : string) (startAtRecord : int) (commodity : Commodity) =
        let url = sprintf "%s&start=%s" baseURL (startAtRecord.ToString())
        let html = fetch url
        let prices = scrapePrices commodity html
        let pagination = scrapePagination html
        match pagination.Start, pagination.TotalRecords with
        | start, total when start < total ->
            let startAt = pagination.Start + pagination.RecordsPerPage
            prices @ getPrices baseURL startAt commodity
        | otherwise -> prices
            

    let getPricesForNewCommodity (usage: CommodityUsage) (config : CommodityConfig) =
        let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol usage.FirstAppeared usage.ZeroBalanceDate
        let prices = getPrices baseURL 0 usage.Commodity
        match List.length prices with
        | 0         -> None
        | otherwise -> Some <| createCommodityPriceDB (usage.Commodity, prices)
        
    let updatePricesForCommodity (usage: CommodityUsage) (config : CommodityConfig) (cpDB : CommodityPriceDB) = 
        let getEarlierMissingPrices (usage: CommodityUsage) (config : CommodityConfig) (cpDB : CommodityPriceDB) =
            match usage.FirstAppeared, cpDB.FirstDate with
            | firstAppeared, firstDate when firstAppeared < firstDate ->
                let endDate = firstDate.AddDays(-1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol usage.FirstAppeared (Some endDate)
                getPrices baseURL 0 usage.Commodity
            | otherwise -> List.Empty
        
        let getLaterMissingPrices (usage: CommodityUsage) (config : CommodityConfig) (cpDB : CommodityPriceDB) =
            match usage.ZeroBalanceDate, cpDB.LastDate with
            | None, lastDate when lastDate < System.DateTime.Today ->
                let startDate = lastDate.AddDays(1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol startDate (Some System.DateTime.Today)
                getPrices baseURL 0 usage.Commodity
            | Some zeroBalanceDate, lastDate when lastDate < zeroBalanceDate ->
                let startDate = lastDate.AddDays(1.0)
                let baseURL = generateBaseURL config.GoogleFinanceSearchSymbol startDate (Some zeroBalanceDate)
                getPrices baseURL 0 usage.Commodity
            | otherwise -> List.Empty

        let earlierPrices = getEarlierMissingPrices usage config cpDB
        let laterPrices = getLaterMissingPrices usage config cpDB
        printNewPrices (earlierPrices @ laterPrices)
        let allPrices = 
            earlierPrices @ cpDB.Prices @ laterPrices
            |> List.sortBy (fun p -> p.Date)
        cpDB.Commodity, {cpDB with Prices = allPrices}

    let fetchPricesForCommodity (usage: CommodityUsage) (config : CommodityConfig) (cpDB : option<CommodityPriceDB>) =
        printfn "Fetching prices for: %s" usage.Commodity
        match usage, cpDB with
        | usage, Some cpDB -> Some <| updatePricesForCommodity usage config cpDB
        | usage, None      -> getPricesForNewCommodity usage config
        
    
    let updatePriceDB (usages : list<CommodityUsage>) (configs : CommodityConfigs) (priceDB : PriceDB) =
        let commoditiesWithConfig (usage : CommodityUsage) =
            Map.containsKey usage.Commodity configs
        let getUpdatedCommodityPriceDB (usage : CommodityUsage) =
            let config = Map.find usage.Commodity configs
            let cpDB = Map.tryFind config.Commodity priceDB
            fetchPricesForCommodity usage config cpDB
        let updateDB (priceDB : PriceDB) ((commodity : string), (cpDB : CommodityPriceDB)) =
            Map.add commodity cpDB priceDB

        usages
        |> List.filter commoditiesWithConfig
        |> List.map getUpdatedCommodityPriceDB
        |> List.choose id
        |> List.fold updateDB priceDB
        

    let deserializeCommodityConfigs (path : string) =
        let toCommodityConfig (regexMatch : Match) =
            let commodity = regexMatch.Groups.[1].Value
            let searchSymbol = regexMatch.Groups.[2].Value
            {Commodity = commodity; GoogleFinanceSearchSymbol = searchSymbol}
        match File.Exists(path) with
        | true ->
            use sr = new StreamReader(path)
            let contents = sr.ReadToEnd()
            sr.Close()
            // CC Commodity GoogleFinanceSearchSymbol
            let regex = new Regex("CC (\w+) ([\w:]+)")
            regex.Matches(contents)
            |> Seq.cast<Match>
            |> Seq.map toCommodityConfig
            |> Seq.toList
        | false -> 
            List.Empty

    let loadCommodityConfig (path : string) : CommodityConfigs =
        deserializeCommodityConfigs path
        |> List.map (fun c -> c.Commodity, c)
        |> Map.ofList

    let printCommodityConfigs (configs : CommodityConfigs) =
        let printCommodityConfig (_commodity : string) (config : CommodityConfig) =
            printfn "%s %s" config.Commodity config.GoogleFinanceSearchSymbol
        Map.iter printCommodityConfig configs


    let deserializeUsages (path : string) =
        let toCommodityUsage (regexMatch : Match) =
            let commodity = regexMatch.Groups.[1].Value
            let firstAppeared = System.DateTime.Parse(regexMatch.Groups.[2].Value)
            let zeroBalanceDate =
                match regexMatch.Groups.[3].Success with
                | true  -> Some <| System.DateTime.Parse(regexMatch.Groups.[3].Value)
                | false -> None
            { Commodity = commodity; FirstAppeared = firstAppeared; ZeroBalanceDate = zeroBalanceDate; }
        match File.Exists(path) with
        | true ->
            use sr = new StreamReader(path)
            let contents = sr.ReadToEnd()
            sr.Close()
            // U Commodity FirstAppeared [ZeroBalanceDate]
            let regex = new Regex("U ([\w\$]+) (\d{4}-\d{2}-\d{2})[ ]?(\d{4}-\d{2}-\d{2})?")
            regex.Matches(contents)
            |> Seq.cast<Match>
            |> Seq.map toCommodityUsage
            |> Seq.toList
        | false -> 
            List.Empty

    let printCommodityUsages (usages : list<CommodityUsage>) =
        let dateFormat = "yyyy-MM-dd"
        let printCommodityUsage (usage : CommodityUsage) =
            let zeroBalanceDate =
                match usage.ZeroBalanceDate with
                | Some date -> date.ToString(dateFormat)
                | None      -> ""
            printfn "%s %s %s" usage.Commodity (usage.FirstAppeared.ToString(dateFormat)) zeroBalanceDate
        List.iter printCommodityUsage usages


    (*********************************************************************
        SCRATCH
    *********************************************************************)
    
    //let path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\tdb900.html"
    let prices_path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.pricedb"
    let config_path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.config_standalone"
    let usage_path  = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.usage"
    //let url = "https://www.google.com/finance/historical?q=MUTF_CA%3ATDB900&startdate=Mar+28%2C+2008&enddate=Jun+28%2C+2014&num=60"
    //let url_full = "https://www.google.com/finance/historical?q=NASDAQ%3AGOOGL&startdate=Apr+24%2C+2007&enddate=Jun+17%2C+2014&num=30&start=30"
    
    let usages = deserializeUsages usage_path
    //printCommodityUsages usages
    let configs = loadCommodityConfig config_path

    //printCommodityConfigs configs
//    let cpDB = fetchPricesForCommodity usage config None
//    let priceDB : PriceDB = Map.ofSeq <| seq { yield cpDB }
//    printPriceDB priceDB
//    savePriceDB prices_path priceDB

    let priceDB = loadPriceDB prices_path
    //printPriceDB priceDB
    
    let newPriceDB = updatePriceDB usages configs priceDB
    savePriceDB prices_path newPriceDB
//    savePriceDB prices_path priceDB
    
//    let html = readFile path

//    html
//    |> scrapePrices "TDB900"
//    |> serializePrices prices_path
    
//    html
//    |> scrapePagination
//    |> printPagination
    
//    do printfn "Press any key to quit..."
//    ignore <| System.Console.ReadLine()
