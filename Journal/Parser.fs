namespace Journal

open FParsec
open Journal.Types

/// Parser module contains functions for parsing the Ledger journal file
module Parser =

    /// Contains transient types used during parsing. Eventually all the data
    /// will be converted to the appropriate Journal types.
    module Types =

        // Types for resolving value restriction errors with FParsec
        type UserState = unit
        type Parser<'t> = Parser<'t, UserState>

        /// Parsed Posting. The only difference from a Journal.Type.Posting is the Amount field is an option,
        /// to allow for postings with blank amounts during parsing. A blank will get calculated when we
        /// create the Journal.    
        type ParsedPosting = {
            LineNumber: int64;
            Account: string;
            Amount: Amount option;
            AmountSource: AmountSource;
            Comment: string option
        }

        /// A parsed line will be one of these types
        type ParseTree =
            | CommentLine of Journal.Types.Comment
            | PriceLine of SymbolPrice
            | Transaction of Header * ParseTree list
            | PostingLine of ParsedPosting


    /// Parser utility functions
    module Utilities =

        /// Call Trim() on a string
        let trim (s : string) : string = 
            s.Trim()

        /// Convert a char array of digits to an Int32
        let charArrayToInt32 (a : char[]) : int32 =
            a |> System.String.Concat |> System.Int32.Parse
    

    module Terminals =
        let isWhitespace (c : char) : bool = 
            c = ' ' || c = '\t'


    /// Parsing combinator functions
    module Combinators =
        open Terminals
        open Types
        open Utilities
        
        // Whitespace Parsers

        /// Skip whitespace as spaces and tabs
        let skipWS : Parser<unit> = 
            skipManySatisfy isWhitespace

        let whitespace : Parser<string> =
            many1Satisfy isWhitespace


        // Line number

        /// Get current line number
        let lineNumber : Parser<int64> =
            let posLineNumber (pos : Position) = pos.Line
            getPosition |>> posLineNumber


        // Comment Parsers

        /// Parse a comment that begins with a semi-colon (;)
        let comment : Parser<Comment> =
            let commentChar = noneOf "\r\n"
            pchar ';' >>. manyChars commentChar |>> trim

        /// Parse a journal comment line
        let commentLine : Parser<ParseTree> =
            comment |>> CommentLine


        // Date Parsers

        /// Parse a 4 digit year
        let year : Parser<int32> =
            parray 4 digit |>> charArrayToInt32

        /// Parse a 2 digit month
        let month : Parser<int32> =
            parray 2 digit |>> charArrayToInt32

        /// Parse a 2 digit day
        let day : Parser<int32> =
            parray 2 digit |>> charArrayToInt32

        /// Parse a date
        let date : Parser<System.DateTime> =
            let isDateSeparator c = c = '/' || c = '-'
            let dateSeparator = satisfy isDateSeparator
            let createDate ((year, month), day) = new System.DateTime(year, month, day)
            year .>> dateSeparator .>>. month .>> dateSeparator .>>. day .>> skipWS
            |>> createDate


        // Simple Transaction Header Field Parsers

        /// Parse transaction status as Cleared or Uncleared
        let status : Parser<Status> = 
            let parseCleared = charReturn '*' Cleared
            let parseUncleared = charReturn '!' Uncleared
            (parseCleared <|> parseUncleared) .>> skipWS

        /// Parse a transaction code between parentheses
        let code : Parser<Code> = 
            let codeChar = noneOf ");\r\n"
            between (pchar '(') (pchar ')') (manyChars codeChar) .>> skipWS

        /// Parse a payee
        let payee : Parser<Payee> = 
            let payeeChar = noneOf ";\r\n"
            many1Chars payeeChar |>> trim


        // Transaction Header

        /// Parse a complete transaction header
        let header : Parser<Header> =
            let createHeader (((((lineNum, date), status), code), payee), comment) =
                Header.create lineNum date status code payee comment
            lineNumber .>>. date .>>. status .>>. (opt code) .>>. payee .>>. (opt comment)
            |>> createHeader


        // Account Parsers

        /// Parse a subaccount
        let subaccount : Parser<Account> =
            let subaccountChar = noneOf ";: \t\r\n\""
            many1Chars subaccountChar

        /// Parse an account
        let account : Parser<Account list> =
            sepBy1 subaccount (pchar ':') .>> skipWS


        // Quantity Parser

        /// Parse a quantity
        let quantity : Parser<decimal> =
            let negativeSign = pstring "-"
            let isAmountChar c = isDigit c || c = ','
            let integerPart =
                digit .>>. manySatisfy isAmountChar
                |>> System.String.Concat
            let fractionPart =
                (pstring ".") .>>. many1Satisfy isDigit
                |>> System.String.Concat
            let createDecimal negSign intPart fracPart =
                let qty = 
                    match negSign, fracPart with
                    | Some(n), Some(f) -> n + intPart + f
                    | Some(n), None    -> n + intPart
                    | None, Some(f)    -> intPart + f
                    | None, None       -> intPart
                System.Decimal.Parse(qty)
            pipe3 (opt negativeSign) integerPart (opt fractionPart) createDecimal


        // Symbol Parsers

        /// Parse a quoted symbol
        let symbol : Parser<Symbol> =
            let quote = pchar '\"'
            let quotedSymbolChar = noneOf "\r\n\""
            let unquotedSymbolChar = noneOf "-0123456789., @;\r\n\""
            let makeSymbol renderOption symbol = Symbol.makeSR symbol renderOption
            (attempt (between quote quote (many1Chars quotedSymbolChar)) |>> makeSymbol Quoted)
            <|> (many1Chars unquotedSymbolChar |>> makeSymbol Unquoted)


        // Amount Parsers

        let amount : Parser<Amount> =
            let amountSymbolThenQuantity =
                let createAmount symbol ws qty =
                    match ws with
                    | Some(_) -> Amount.create qty symbol SymbolLeftWithSpace
                    | None    -> Amount.create qty symbol SymbolLeftNoSpace
                pipe3 symbol (opt whitespace) quantity createAmount
            let amountQuantityThenSymbol =
                let createAmount qty ws symbol =
                    match ws with
                    | Some(_) -> Amount.create qty symbol SymbolRightWithSpace
                    | None    -> Amount.create qty symbol SymbolRightNoSpace
                pipe3 quantity (opt whitespace) symbol createAmount
            (amountSymbolThenQuantity <|> amountQuantityThenSymbol) .>> skipWS

        let amountOrInferred : Parser<AmountSource * Amount option> =
            let computeSource amount =
                match amount with
                | Some(a) -> (Provided, amount)
                | None    -> (Inferred, None)
            opt amount
            |>> computeSource


        // Posting Parser

        /// Parse a transaction posting
        let posting : Parser<ParseTree> =
            let createParsedPosting lineNum account (amountSource, amount) comment =
                {
                    LineNumber = lineNum;
                    Account = String.concat ":" account;
                    AmountSource = amountSource
                    Amount = amount;
                    Comment = comment
                }
            pipe4 lineNumber account amountOrInferred (opt comment) createParsedPosting
            |>> PostingLine


        // Transaction Parser

        /// Parse a complete transaction
        let transaction : Parser<ParseTree> =
            let parsePosting = attempt (skipWS >>. (posting <|> commentLine) .>> newline)
            header .>> newline .>>. many parsePosting |>> Transaction


        // Price Parsers

        /// Parse a price entry. e.g. "P 2014/12/14 AAPL $23.44"
        let price : Parser<SymbolPrice> =
            let priceLeader = pchar 'P' .>> skipWS
            priceLeader >>. pipe4 lineNumber date (symbol .>> skipWS) amount SymbolPrice.create

        /// Parse a price line within a journal file
        let priceLine : Parser<ParseTree> =
            price |>> PriceLine


        // Symbol Configuration Parser

        /// Parse a symbol configuration entry.
        /// e.g. "SC <symbol> <google finance search symbol>"
        let symbolConfig : Parser<SymbolConfig> =
            let symbolConfigLeader = pstring "SC" .>> skipWS
            let googleSymbolChar = noneOf "; \t\r\n\""
            symbolConfigLeader >>. pipe2 (symbol .>> skipWS) (many1Chars googleSymbolChar) SymbolConfig.create


        // Journal Parser

        /// Parse a complete ledger journal
        let journal : Parser<ParseTree list> =
            sepEndBy (commentLine <|> transaction <|> priceLine) (many1 (skipWS >>. newline))


        // Price DB Parser

        /// Parse a prices file
        let priceDB : Parser<SymbolPrice list> =
            sepEndBy price (many1 (skipWS >>. newline))


        // Config File Parser

        /// Parse a config file
        let config : Parser<SymbolConfig list> =
            sepEndBy symbolConfig (many1 (skipWS >>. newline))


        
    // Module PostProcess contains post-parsing transformations

    module PostProcess =
        open Types

        /// Transforms the ParseTree tree data structure into a list of (Header, ParsedPosting list) tuples
        /// Basically, we're dropping all the comment nodes
        let mapToHeaderParsedPostingTuples (lines : ParseTree list) : (Header * ParsedPosting list) list =
            let isTransaction (line : ParseTree) =
                match line with
                | Transaction(_,_) -> true
                | _ -> false

            let isParsedPosting (line : ParseTree) =
                match line with
                | PostingLine(_) -> true
                | _ -> false

            let toParsedPosting (line : ParseTree) =
                match line with
                | PostingLine(p) -> p
                | _ -> failwith "Unexpected ParseTree value in toParsedPosting"

            let toHeaderPostingsTuple (line : ParseTree) =
                match line with
                | Transaction(header, lines) ->
                    let postings = 
                        lines
                        |> List.filter isParsedPosting
                        |> List.map toParsedPosting
                    (header, postings)    
                | _ -> failwith "Unexpected ParseTree value in toHeaderPostingsTuple"
            
            lines
            |> List.filter isTransaction
            |> List.map toHeaderPostingsTuple


        /// Verifies that transactions balance and autobalances transactions if 
        /// one amount is missing.
        let balanceTransactions (transactions : (Header * ParsedPosting list) list) : (Header * ParsedPosting list) list =
            // Calculates balances by symbol for a transaction. Returns any non-zero balances by symbol
            // and the number of postings with Inferred amounts.
            let postingsBalance (postings : ParsedPosting list) =
                let sumPostingsBySymbol (balance : Map<SymbolValue, Amount>) (posting : ParsedPosting) =
                    let symbol = posting.Amount.Value.Symbol.Value
                    match balance.ContainsKey symbol with
                    | true  ->
                        let amount = balance.[symbol]
                        balance.Add(symbol, {amount with Value = amount.Value + posting.Amount.Value.Value})
                    | false ->
                        balance.Add(symbol, posting.Amount.Value)

                let symbolBalances =
                    postings
                    |> List.filter (fun posting -> posting.AmountSource = Provided)
                    |> List.fold sumPostingsBySymbol Map.empty
                    |> Map.filter (fun symbol amount -> amount.Value <> 0M)

                let numInferredPostings =
                    postings
                    |> List.filter (fun posting -> posting.AmountSource = Inferred)
                    |> List.length

                (symbolBalances, numInferredPostings)

            // Postings can be autobalanced as long as there is only 1 amount missing
            // and only one symbol out of balance.
            let autobalance postings =
                let symbolBalances, numInferredPostings = postingsBalance postings

                match numInferredPostings, (Seq.length symbolBalances) with
                | numMissing, _ when numMissing > 1 ->
                    failwith "Encountered transaction with more than one amount missing."
                | numMissing, numUnbalancedSymbols when numUnbalancedSymbols > numMissing ->
                    failwith "Encountered transaction with one or more symbols out of balance."
                | numMissing, numUnbalancedSymbols when numMissing = 1 && numUnbalancedSymbols = 1 ->
                    let balance = (Seq.nth 0 symbolBalances).Value
                    postings
                    |> List.map (fun posting ->
                        if posting.AmountSource = Inferred
                        then {posting with Amount = Some {balance with Value = -balance.Value}}
                        else posting)
                | otherwise ->
                    postings

            transactions
            |> List.map (fun (header, postings) -> (header, autobalance postings))
            

        /// Convert to a list of journal postings (transaction postings)
        let toPostingList (txs : (Header * ParsedPosting list) list) : Posting list =
            let transactionToPostings (header, ps) =
                let toPosting (p : ParsedPosting) =
                    {
                        LineNumber = p.LineNumber;
                        Header = header; 
                        Account = p.Account;
                        AccountLineage = Account.getAccountLineage p.Account;
                        Amount = p.Amount.Value;
                        AmountSource = p.AmountSource;
                        Comment = p.Comment 
                    }
                List.map toPosting ps
            List.collect transactionToPostings txs


        /// Pipelined functions applied to the ParseTree to produce the final
        /// journal data structure
        let extractPostings = 
            mapToHeaderParsedPostingTuples
            >> balanceTransactions
            >> toPostingList


        /// Extract the price entries from the AST
        let extractPrices (lines : ParseTree list) : SymbolPriceDB =
            let priceOnly (line : ParseTree) =
                match line with
                | PriceLine p -> Some p
                | _ -> None
            lines
            |> List.choose priceOnly
            |> SymbolPriceDB.fromList
            
            

    
    let private extractResult result =
        match result with
        | Success(r, _, _)        -> r
        | Failure(errorMsg, _, _) -> failwith errorMsg

    /// Run the parser against a ledger journal file
    let parseJournalFile fileName encoding =
        let parseTree = 
            runParserOnFile Combinators.journal () fileName encoding
            |> extractResult
        let postings = PostProcess.extractPostings parseTree
        let pricedb = PostProcess.extractPrices parseTree
        (postings, pricedb)

    /// Run the parser against a prices file
    let parsePricesFile fileName encoding =
        runParserOnFile Combinators.priceDB () fileName encoding
        |> extractResult

    /// Run the parser against a config file
    let parseConfigFile fileName encoding =
        runParserOnFile Combinators.config () fileName encoding
        |> extractResult
