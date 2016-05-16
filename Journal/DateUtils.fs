module Journal.DateUtils

[<Literal>]
let RenderDateFormat = "yyyy-MM-dd"

let getFirstOfMonth (fromDate: System.DateTime) =
    new System.DateTime(year=fromDate.Year, month=fromDate.Month, day=1)

let getLastOfMonth (fromDate: System.DateTime) =
    (getFirstOfMonth fromDate).AddMonths(1).AddDays(-1.0)
