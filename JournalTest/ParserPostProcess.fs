module Journal.Tests.Parser.PostProcess

open Fuchu
open FParsec
open Journal.Parser.Combinators
open Journal.Parser.PostProcess
open Journal.Parser.Types
open Journal.Types

let testFile = "JournalTest/testfiles/simple.dat"

let parseFile parser filepath =
    match runParserOnFile parser () filepath System.Text.Encoding.UTF8 with
    | Success(result, _, _) -> result
    | Failure(error, _, _) -> failwith error

[<Tests>]
let mapToHeaderParsedPostingTuplesTests =
    testList "mapToHeaderParsedPostingTuples" [
        testCase "all transactions" <| fun _ ->
            let txs =
                parseFile journal testFile
                |> mapToHeaderParsedPostingTuples

            Assert.Equal("8 transactions", 8, List.length txs)
            Assert.Equal("3 postings in 1st transaction", 3, List.nth txs 0 |> snd |> List.length)
            Assert.Equal("2 postings in 2nd transaction", 2, List.nth txs 1 |> snd |> List.length)
            Assert.Equal("4 postings in 3rd transaction", 4, List.nth txs 2 |> snd |> List.length)
            Assert.Equal("4 postings in 4th transaction", 4, List.nth txs 3 |> snd |> List.length)
            Assert.Equal("2 postings in 5th transaction", 2, List.nth txs 4 |> snd |> List.length)
            Assert.Equal("2 postings in 6th transaction", 2, List.nth txs 5 |> snd |> List.length)
            Assert.Equal("2 postings in 7th transaction", 2, List.nth txs 6 |> snd |> List.length)
            Assert.Equal("5 postings in 8th transaction", 5, List.nth txs 7 |> snd |> List.length)
    ]

[<Tests>]
let extractPricesTests =
    testList "extractPrices" [
        testCase "all prices extracted" <| fun _ ->
            let symbolPriceDB =
                parseFile journal testFile
                |> extractPrices

            Assert.Equal("2 symbols in price DB", 2, symbolPriceDB.Count)
            Assert.Equal("1 price for SII", 1, List.length symbolPriceDB.["SII"].Prices)
            Assert.Equal("2 prices for WE", 2, List.length symbolPriceDB.["WE"].Prices)
    ]
