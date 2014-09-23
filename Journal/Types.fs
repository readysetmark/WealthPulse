namespace WealthPulse

open System

module Types =

    type Symbol = string

    type SymbolUsage = {
        Symbol: Symbol;
        FirstAppeared: DateTime;
        ZeroBalanceDate: DateTime option;
    } 