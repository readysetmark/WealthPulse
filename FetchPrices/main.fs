namespace FetchPrices

open System.Net
open System.IO
open System.Text.RegularExpressions

(*
    There's actually quite a bit I need to handle
        - Need commodity, first appeared, zero balance date, query string
            - Query string will have to come from a config file
        - Need existing known prices loaded
        - Determine if we need to attempt to retrieve new prices for a particular commodity
        - Retrieve prices
            - have price for first appeared?
            - generate url
            - fetch page and extract prices
            - generate next page url
        - Store retrieved prices
        - Use prices from ledger file to fill in gaps that cannot be retrieved
*)

module Main =
    
    type Commodity = string
    type Price = decimal

    type CommodityPrice = {
        Date: System.DateTime;
        Commodity: Commodity;
        Price: Price
    }

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



    let path = @"C:\Users\Mark\Nexus\Documents\finances\ledger\tdb900.html"
    let url = "https://www.google.com/finance/historical?q=MUTF_CA%3ATDB900&startdate=Mar+28%2C+2008&enddate=Jun+28%2C+2014&num=60"
    //let url_full = "https://www.google.com/finance/historical?q=NASDAQ%3AGOOGL&startdate=Apr+24%2C+2007&enddate=Jun+17%2C+2014&num=30&start=30"
    
    let html = readFile path

    html
    |> scrapePrices "TDB900"
    |> printPrices
    
    html
    |> scrapePagination
    |> printPagination
    
    do printfn "Press any key to quit..."
    ignore <| System.Console.ReadLine()
