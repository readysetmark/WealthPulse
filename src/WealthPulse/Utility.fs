namespace WealthPulse

module Utility =

    let time f =
        let start = System.DateTime.Now
        let res = f()
        let finish = System.DateTime.Now
        (res, finish - start)
