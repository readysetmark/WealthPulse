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
        let trim (s : string) = 
            s.Trim()

        /// Convert a char array of digits to an Int32
        let charArrayToInt32 (a : char[]) =
            a |> System.String.Concat |> System.Int32.Parse
    

    module Terminals =
        let isWhitespace c = c = ' ' || c = '\t'


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

        /// Parse a subaccount, which can be any alphanumeric character sequence
        let subaccount : Parser<Account> =
            let isAlphanumeric c = isDigit(c) || isLetter(c)
            many1Satisfy isAlphanumeric

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
            (attempt (between quote quote (many1Chars quotedSymbolChar)) |>> Symbol.create true)
            <|> (many1Chars unquotedSymbolChar |>> Symbol.create false)


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


        



        /// Parse a complete transaction
        let parseTransaction =
            let parsePosting = attempt (skipWS >>. (posting <|> commentLine) .>> newline)
            header .>> newline .>>. many parsePosting |>> Transaction

        /// Parse a price entry. e.g. "P 2014/12/14 AAPL $23.44"
        let parsePrice =
            let parseP = pchar 'P' .>> skipWS
            parseP >>. pipe3 date symbol amount SymbolPrice.create |>> PriceLine

        /// Parse a complete ledger journal
        let parseJournal =
            sepEndBy (commentLine <|> parseTransaction <|> parsePrice) (many (skipWS >>. newline))


        /// Price file parsing combinators

        /// Parse a price entry in a price file. e.g. "P 2014/12/14 AAPL $23.44"
        let parsePriceFilePrice =
            let parseP = pchar 'P' .>> skipWS
            parseP >>. pipe3 date symbol amount SymbolPrice.create

        /// Parse a prices file
        let parsePriceFilePrices =
            sepEndBy parsePriceFilePrice (many (skipWS >>. newline))


        
    // Module PostProcess contains post-parsing transformations

    module private PostProcess =
        open Types

        /// Transforms the ParseTree tree data structure into a list of (Header, ParsedPosting list) tuples
        /// Basically, we're dropping all the comment nodes
        let transformParseTreeToTransactions lines =
            let transactionFilter (line: ParseTree) =
                match line with
                | Transaction(_,_) -> true
                | _ -> false

            let getTransactionHeader (line: ParseTree) =
                match line with
                | Transaction(header, lines) -> (header, lines)
                | _ -> failwith "Unexpected AST value in getTransactionHeader"

            let transactionPostingFilter (line: ParseTree) =
                match line with
                | PostingLine(_) -> true
                | _ -> false

            let getTransactionPosting (line: ParseTree) =
                match line with
                | PostingLine(p) -> p
                | _ -> failwith "Unexpected AST value in getTransactionPosting"

            let getTransactionPostings (header, lines) =
                let postings = 
                    lines
                    |> List.filter transactionPostingFilter
                    |> List.map getTransactionPosting
                (header, postings)

            lines
            |> List.filter transactionFilter
            |> List.map getTransactionHeader
            |> List.map getTransactionPostings


        /// Verifies that transactions balance and autobalances transactions if
        /// one amount is missing.
        /// TODO: The possibility of different symbols for amounts is completely ignored right now.
        (* This will have to be totally re-worked. No longer going to have virtual or virtual unbalanced.
            Also need to account for different commodities.

        let balanceTransactions transactions =
            // Virtual Unbalanced transactions must have an amount (since they are unbalanced)
            let verifyVirtualUnbalanced entries =
                let virtualUnbalancedMissingAmount =
                    List.filter (fun entry -> entry.EntryType = VirtualUnbalanced && entry.Amount.IsNone) entries
                match List.length virtualUnbalancedMissingAmount with
                | 0 -> entries
                | otherwise -> failwith "Encountered virtual unbalanced entry missing an amount."

            // Generic balance checker for balanced entry types. Balanced entries should sum 0 or have only 1
            // amount missing (which can be auto-balanced).
            let verifyBalanced entryType entries =
                let balancedEntries = List.filter (fun entry -> entry.EntryType = entryType) entries
                let symbol =
                    balancedEntries
                    |> List.tryPick (fun entry -> if entry.Amount.IsSome
                                                  then Some <| entry.Amount.Value.Symbol
                                                  else None)
                let sum =
                    balancedEntries
                    |> List.fold (fun sum entry -> if entry.Amount.IsSome
                                                   then sum + entry.Amount.Value.Value
                                                   else sum)
                                 0M
                let numMissing =
                    balancedEntries
                    |> List.filter (fun entry -> entry.Amount.IsNone)
                    |> List.length
                (sum, symbol, numMissing)

            // Balanced entry types can be autobalanced as long as there is only 1 amount missing.
            let autobalance entryType entries =
                match verifyBalanced entryType entries with
                | _, _, numMissing when numMissing > 1 -> failwith "Encountered balanced transaction with more than one amount missing."
                | sum, symbol, numMissing when numMissing = 1 ->
                    entries
                    |> List.map (fun entry -> if entry.Amount.IsNone
                                              then { entry with Amount = Some {Value = -sum; Symbol = symbol; Format = SymbolLeftNoSpace} }
                                              else entry)
                | sum, _, _ when sum <> 0M -> failwith "Encountered balanced transaction that is not balanced."
                | otherwise -> entries

            // Transform pipeline to balance transaction entries
            let balanceEntries =
               verifyVirtualUnbalanced
               >> autobalance VirtualBalanced
               >> autobalance Balanced

            transactions
            |> List.map (fun (header, entries) -> (header, balanceEntries entries))
        *)
            

        /// Convert to a list of journal postings (transaction postings)
        let toPostingList ts =
            let transactionToJournal (header, ps) =
                //let header = ({ LineNumber=h.LineNumber; Date=h.Date; Status=h.Status; Code=h.Code; Payee=h.Payee; Comment=h.Comment; } : Header)
                let toPosting (p : ParsedPosting) =
                    ({
                        LineNumber = p.LineNumber;
                        Header = header; 
                        Account = p.Account;
                        AccountLineage = Account.getAccountLineage p.Account;
                        Amount = p.Amount.Value;
                        AmountSource = p.AmountSource;
                        Comment = p.Comment 
                    } : Posting)
                List.map toPosting ps
            List.collect transactionToJournal ts


        /// Pipelined functions applied to the AST to produce the final journal data structure
        let extractPostings = 
            transformParseTreeToTransactions
            // >> balanceTransactions
            >> toPostingList


        /// Extract the price entries from the AST
        let extractPrices lines =
            let priceOnly (line : ParseTree) =
                match line with
                | PriceLine p -> Some p
                | _ -> None
            lines
            |> List.choose priceOnly
            |> SymbolPriceDB.createFromSymbolPriceList
            
            

    
    let private extractResult result =
        match result with
            | Success(ast, _, _) -> ast
            | Failure(errorMsg, _, _) -> failwith errorMsg

    /// Run the parser against a ledger journal file
    let parseJournalFile fileName encoding =
        let ast = 
            runParserOnFile Combinators.parseJournal () fileName encoding
            |> extractResult
        let postings = PostProcess.extractPostings ast
        let pricedb = PostProcess.extractPrices ast
        (postings, pricedb)

    /// Run the parser against a prices file
    let parsePricesFile fileName encoding =
        runParserOnFile Combinators.parsePriceFilePrices () fileName encoding
        |> extractResult
