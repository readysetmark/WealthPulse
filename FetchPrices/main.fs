namespace FetchPrices

open System.Net
open System.IO

module Main =
    let fetch (url : string) =
        let req = WebRequest.Create(url) :?> HttpWebRequest
        req.Method <- "GET"
        req.UserAgent <- "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.114 Safari/537.36"
        use resp = req.GetResponse()
        use stream = resp.GetResponseStream()
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    
    let url = "https://www.google.com/finance/historical?q=NASDAQ%3AGOOGL&startdate=Apr+24%2C+2007&enddate=Jun+17%2C+2014&num=30&start=30"
    do printfn "Fetching: %s" url
    do printfn "%s" <| fetch url
    ignore <| System.Console.ReadLine()
