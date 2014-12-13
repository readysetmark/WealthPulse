module Journal.Types

open System

// Journal Types

/// A commodity symbol. e.g. "$", "AAPL", "MSFT"
type Symbol = string

/// A ledger account. e.g. "Assets:Accounts:Savings"
type Account = string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Account =

    /// Calculate full account lineage for a particular account. This will return
    /// a list of all parent accounts and the account itself.
    /// e.g. given "a:b:c", returns ["a:b:c"; "a:b"; "a"]
    let getAccountLineage (account: string) =
        /// Use with fold to get all combinations.
        let combinator (s: string list) (t: string) =
            if not s.IsEmpty then (s.Head + ":" + t) :: s else t :: s
        account.Split ':'
        |> Array.fold combinator []
        |> List.rev
    

/// An amount is a quantity and an optional symbol.
type Amount = {
    Amount: decimal;
    Symbol: Symbol option;
}

/// Transaction status.
type Status =
    | Cleared
    | Uncleared

/// Entry type.
type EntryType =
    | Balanced
    | VirtualBalanced
    | VirtualUnbalanced

/// Transaction header.
type Header = {
    Date: System.DateTime;
    Status: Status;
    Code: string option;
    Description: string;
    Comment: string option
}

/// Transaction entry line.
type Entry = {
    Header: Header;
    Account: string;
    AccountLineage: string list;
    EntryType: EntryType;
    Amount: Amount;
    Comment: string option;
}

/// Journal with all entries and accounts.
type Journal = {
    Entries: Entry list;
    MainAccounts: Set<string>;
    AllAccounts: Set<string>;
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Journal = 
    
    /// Given a list of journal entries, returns a Journal record
    let create entries =
        let mainAccounts = Set.ofList <| List.map (fun (entry : Entry) -> entry.Account) entries
        let allAccounts = Set.ofList <| List.collect (fun entry -> entry.AccountLineage) entries
        { Entries=entries; MainAccounts=mainAccounts; AllAccounts=allAccounts; }


// Symbol Usage Types
    
/// Symbol Usage record.
type SymbolUsage = {
    Symbol: Symbol;
    FirstAppeared: DateTime;
    ZeroBalanceDate: DateTime option;
}