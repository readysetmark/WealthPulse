namespace FetchPrices

open System.Net
open System.IO
open System.Text.RegularExpressions

(*
    There's actually quite a bit I need to handle
        [ ] Need commodity, first appeared, zero balance date, query string
            - Query string will have to come from a config file
        [x] Need existing known prices loaded
        [ ] Determine if we need to attempt to retrieve new prices for a particular commodity
        [ ] Retrieve prices
            [ ] have price for first appeared?
            [ ] generate url
            [ ] fetch page and extract prices
            [ ] generate next page url
            [ ] Use prices from ledger file to fill in gaps that cannot be retrieved
        [x] Store retrieved prices

    Inputs:
        [x] From pricedb file, startup: Commodity, first price, last price, list of all prices
        - From Journal, ongoing (when changed): Commodity, first appeared, zero balance date
        - From config file, ongoing (when changed): Commodity, google finance key from config file
    
    Outputs:
        - In-memory price db
        [x] pricedb file

    Boot process:
        [x] Load pricedb file, group by commodity and sort by date. determine first and last prices per commodity.
        - Get journal info & add watch
        - Get config info & add watch
        - Get new prices & add timer (24 hours)

    Get new prices process:
        [ ] Compare usage dates to db dates. are end points convered?
        [ ] If not covered:
            [ ] If have no dates, query google for whole date range
            [ ] If have dates, query google for dates missing at start, then end (two separate queries)
        [ ] Get prices
            [ ] Generate URL
            [ ] Fetch page and extract prices
            [ ] Generate next URL

        


    
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
        GoogleFinanceKey: string;
    }

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

    type PriceDB = Map<Commodity,CommodityPriceDB>

    type Pagination = {
        Start: int;
        RecordsPerPage: int;
        TotalRecords: int;
    }

    let fetch (url : string) =
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

    let printPrices (prices : list<CommodityPrice>) =
        let printMatch (price : CommodityPrice) =
            do printfn "%s - %s - %s" price.Commodity (price.Date.ToString("yyyy-MM-dd")) (price.Price.ToString())
        do printfn "Found %d prices:" <| List.length prices
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
        use sr = new StreamReader(path)
        let contents = sr.ReadToEnd()
        sr.Close()
        let regex = new Regex("P (\d{4}-\d{2}-\d{2}) (\w+) (\d+.\d+)")
        regex.Matches(contents)
        |> Seq.cast<Match>
        |> Seq.map toCommodityPrice
        |> Seq.toList

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
        sprintf "https://www.google.com/finance/historical?q=%s&startdate=%s&enddate=%s&num=100" query startDate endDate

    
    let rec getPrices (baseURL : string) (startAtRecord : int) (commodity : Commodity) =
        let url = sprintf "%s&start=%s" baseURL (startAtRecord.ToString())
        printfn "Fetch URL: %s" url
        let html = fetch url
        let prices = scrapePrices commodity html
        let pagination = scrapePagination html
        match pagination.Start, pagination.TotalRecords with
        | start, total when start <= total ->
            let startAt = pagination.Start + pagination.RecordsPerPage
            prices @ getPrices baseURL startAt commodity
        | otherwise -> prices
            

    let getPricesForNewCommodity (usage: CommodityUsage) (config : CommodityConfig) =
        let baseURL = generateBaseURL config.GoogleFinanceKey usage.FirstAppeared usage.ZeroBalanceDate
        printfn "baseURL = %s" baseURL
        let prices = getPrices baseURL 0 usage.Commodity
        createCommodityPriceDB (usage.Commodity, prices)
        
//    let updatePricesForCommodity (usage: CommodityUsage) (config : CommodityConfig) (cpDB : CommodityPriceDB) = 
//        cpDB
//
//    let fetchPricesForCommodity (usage: CommodityUsage) (config : CommodityConfig) (cpDB : option<CommodityPriceDB>) =
//        match usage, cpDB with
//        | usage, Some cpDB -> updatePricesForCommodity usage config cpDB
//        | usage, None      -> getPricesForNewCommodity usage config
        
    
    //let fetchPrices (usages : list<CommodityUsage>) (config : list<CommodityConfig>) (priceDB : PriceDB) =
        




    (*********************************************************************
        SCRATCH
    *********************************************************************)

    let usage = {Commodity = "TDB900"; FirstAppeared = new System.DateTime(2008, 3, 28); ZeroBalanceDate = None}
    let config   = {Commodity = "TDB900"; GoogleFinanceKey = "MUTF_CA:TDB900"}

    let path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\tdb900.html"
    let prices_path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\.pricedb"
    let url = "https://www.google.com/finance/historical?q=MUTF_CA%3ATDB900&startdate=Mar+28%2C+2008&enddate=Jun+28%2C+2014&num=60"
    //let url_full = "https://www.google.com/finance/historical?q=NASDAQ%3AGOOGL&startdate=Apr+24%2C+2007&enddate=Jun+17%2C+2014&num=30&start=30"
    
    let cpDB = getPricesForNewCommodity usage config
    let priceDB : PriceDB = Map.ofSeq <| seq { yield cpDB }
    printPriceDB priceDB

//    let priceDB = loadPriceDB prices_path
//    printPriceDB priceDB
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
