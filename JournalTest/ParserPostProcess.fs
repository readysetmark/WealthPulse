module Journal.Tests.Parser.PostProcess

open Fuchu
open FParsec
open Journal.Parser.Combinators
open Journal.Parser.PostProcess
open Journal.Parser.Types
open Journal.Types

let sampleFile =
    [
        "; first comment";
        "";
        "2015-03-20 * Basic transaction ;comment";
        "  Expenses:Groceries    $45.00";
        "  Liabilities:Credit";
        "";
        "2015-03-20 * Buy stocks";
        "  Assets:Investments:Stocks    33.245 \"MUTF514\"";
        "  Assets:Savings               $-250.00";
        "  Basis:MUTF514:2015-03-20     -33.245 \"MUTF514\"";
        "  Basis:MUTF514:2015-03-20     $250.00";
        "";
        "P 2015-03-20 \"MUTF514\" $7.52";
        "";
    ] |> String.concat "\r\n"


let parse parser text =
    match run parser text with
    | Success(result, _, _) -> Some(result)
    | Failure(error, _, _)  -> failwith error

[<Tests>]
let extractPricesTests =
    testList "extractPrices" [
        testCase "all prices extract" <| fun _ ->
            let symbolPriceDB =
                parse journal sampleFile
                |> Option.get
                |> extractPrices

            Assert.Equal("1 symbol in map", 1, symbolPriceDB.Count)
            Assert.Equal("1 price for MUTF514", 1, List.length symbolPriceDB.["MUTF514"].Prices)
    ]
