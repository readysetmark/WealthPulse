module Journal.Tests.Parser.PostProcess

open Fuchu
open FParsec
open Journal.Parser.Combinators
open Journal.Parser.PostProcess
open Journal.Parser.Types
open Journal.Types
open Journal.Types.Posting

let testfilesPath = "../../testfiles"
let simpleFile = testfilesPath + "/simple.dat"
let balancedFile = testfilesPath + "/balanced.dat"
let unbalancedFile = testfilesPath + "/unbalanced.dat"
let inferredFile = testfilesPath + "/inferred.dat"
let missingAmountsFile = testfilesPath + "/missingamounts.dat"

let parseFile parser filepath =
    match runParserOnFile parser () filepath System.Text.Encoding.UTF8 with
    | Success(result, _, _) -> result
    | Failure(error, _, _) -> failwith error

[<Tests>]
let mapToHeaderParsedPostingTuplesTests =
    testList "mapToHeaderParsedPostingTuples" [
        testCase "all transactions" <| fun _ ->
            let txs =
                parseFile journal simpleFile
                |> mapToHeaderParsedPostingTuples

            Assert.Equal("8 transactions", 8, List.length txs)
            Assert.Equal("3 postings in 1st transaction", 3, txs.[0] |> snd |> List.length)
            Assert.Equal("2 postings in 2nd transaction", 2, txs.[1] |> snd |> List.length)
            Assert.Equal("4 postings in 3rd transaction", 4, txs.[2] |> snd |> List.length)
            Assert.Equal("4 postings in 4th transaction", 4, txs.[3] |> snd |> List.length)
            Assert.Equal("2 postings in 5th transaction", 2, txs.[4] |> snd |> List.length)
            Assert.Equal("2 postings in 6th transaction", 2, txs.[5] |> snd |> List.length)
            Assert.Equal("2 postings in 7th transaction", 2, txs.[6] |> snd |> List.length)
            Assert.Equal("5 postings in 8th transaction", 5, txs.[7] |> snd |> List.length)
    ]

[<Tests>]
let balanceTransactionsTests =
    testList "balanceTransactions" [
        testCase "balanced" <| fun _ ->
            let txs =
                parseFile journal balancedFile
                |> mapToHeaderParsedPostingTuples
                |> balanceTransactions

            Assert.Equal("1 transaction", 1, List.length txs)
            let tx = List.head txs
            Assert.Equal("4 postings in transaction", 4, tx |> snd |> List.length)
            List.iter (fun (posting : ParsedPosting) ->
                            Assert.Equal("Posting has amount", true, posting.Amount.IsSome)
                            Assert.Equal("Posting amount was provided", true, posting.AmountSource = Provided))
                      (snd tx)

        testCase "successfully inferred" <| fun _ ->
            let txs =
                parseFile journal inferredFile
                |> mapToHeaderParsedPostingTuples
                |> balanceTransactions

            Assert.Equal("1 transaction", 1, List.length txs)
            let tx = List.head txs
            let postings = snd tx
            Assert.Equal("4 postings in transaction", 4, List.length postings)
            let posting1 = postings.[0]
            Assert.Equal("Posting has amount", true, posting1.Amount.IsSome)
            Assert.Equal("Posting amount was provided", true, posting1.AmountSource = Provided)
            let posting2 = postings.[1]
            Assert.Equal("Posting has amount", true, posting2.Amount.IsSome)
            Assert.Equal("Posting amount was provided", true, posting2.AmountSource = Provided)
            let posting3 = postings.[2]
            Assert.Equal("Posting has amount", true, posting3.Amount.IsSome)
            Assert.Equal("Posting amount was provided", true, posting3.AmountSource = Provided)
            let posting4 = postings.[3]
            Assert.Equal("Posting has amount", true, posting4.Amount.IsSome)
            Assert.Equal("Posting amount was provided", true, posting4.AmountSource = Inferred)

        testCase "unbalanced" <| fun _ ->
            Assert.Raise("unbalanced", typeof<System.Exception>, fun () ->
                parseFile journal unbalancedFile
                |> mapToHeaderParsedPostingTuples
                |> balanceTransactions
                |> ignore)

        testCase "missing amounts" <| fun _ ->
            Assert.Raise("missing amounts", typeof<System.Exception>, fun () ->
                parseFile journal missingAmountsFile
                |> mapToHeaderParsedPostingTuples
                |> balanceTransactions
                |> ignore)
    ]

[<Tests>]
let extractPricesTests =
    testList "extractPrices" [
        testCase "all prices extracted" <| fun _ ->
            let symbolPriceDB =
                parseFile journal simpleFile
                |> extractPrices

            Assert.Equal("2 symbols in price DB", 2, symbolPriceDB.Count)
            Assert.Equal("1 price for SII", 1, List.length symbolPriceDB.["SII"].Prices)
            Assert.Equal("2 prices for WE", 2, List.length symbolPriceDB.["WE"].Prices)
    ]
