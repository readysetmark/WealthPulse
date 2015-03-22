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
let extractPricesTests =
    testList "extractPrices" [
        testCase "all prices extracted" <| fun _ ->
            let symbolPriceDB =
                parseFile journal testFile
                |> extractPrices

            Assert.Equal("2 symbols in map", 2, symbolPriceDB.Count)
            Assert.Equal("1 price for SII", 1, List.length symbolPriceDB.["SII"].Prices)
            Assert.Equal("2 prices for WE", 2, List.length symbolPriceDB.["WE"].Prices)
    ]
