namespace Webledger

// Contains all the type definitions in the Journal

module Journal =

    /// A commodity is just a string i.e. $, CAD$, US$, AAPL
    type Commodity = string

    /// An amount plus the optional commodity
    type Amount = decimal * Commodity option

    /// Transaction status
    type Status =
        | Cleared
        | Uncleared

    /// Entry types
    type EntryType =
        | Balanced
        | VirtualBalanced
        | VirtualUnbalanced

    /// Transaction header
    type Header = {
        Date: System.DateTime;
        Status: Status;
        Code: string option;
        Description: string;
        Comment: string option
    }

    /// Entry line
    type Entry = {
        Header: Header;
        Account: string;
        AccountLineage: string list;
        EntryType: EntryType;
        Amount: Amount;
        Value: Amount option;
        Comment: string option;
    }

    /// Journal of all transactions and accounts
    type JournalData = {
        Transactions: Entry list;
        MainAccounts: Set<string>;
        AllAccounts: Set<string>;
    }
