module Journal.DateUtils

let getFirstOfMonth (fromDate: System.DateTime) = new System.DateTime(year=fromDate.Year, month=fromDate.Month, day=1)
    
let getLastOfMonth (fromDate: System.DateTime) = (new System.DateTime(year=fromDate.Year, month=fromDate.Month, day=1)).AddMonths(1).AddDays(-1.0)
